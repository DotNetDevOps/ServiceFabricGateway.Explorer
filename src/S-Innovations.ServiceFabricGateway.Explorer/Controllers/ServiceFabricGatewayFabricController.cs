using IdentityModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Fabric;
using System.Fabric.Description;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ServiceFabricGateway.Explorer.Controllers
{
    public class ServiceDeploymentModel
    {
        public string ServiceTypeName { get; set; }
        public string ServiceName { get;  set; }
        public byte[] InitializationData { get; set; }
    }
    public class DeploymentModel
    {
        public bool DeleteIfExists { get; set; }
        public string RemoteUrl { get; set; }
        public string ApplicationTypeName { get; set; }
        public string ApplicationTypeVersion { get; set; }
        public string ApplicationName { get; set; }
        public Dictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();

        public ServiceDeploymentModel[] ServiceDeployments { get; set; } = Array.Empty<ServiceDeploymentModel>();
    }
    public static class ex
    {
        public static NameValueCollection ToNameValueCollection<TKey, TValue>(
    this IDictionary<TKey, TValue> dict)
        {
            var nameValueCollection = new NameValueCollection();

            foreach (var kvp in dict)
            {
                string value = null;
                if (kvp.Value != null)
                    value = kvp.Value.ToString();

                nameValueCollection.Add(kvp.Key.ToString(), value);
            }

            return nameValueCollection;
        }
    }


    [Authorize(AuthenticationSchemes = IdentityServer4.AccessTokenValidation.IdentityServerAuthenticationDefaults.AuthenticationScheme)]
    public class FabricController : Controller
    {
        private readonly ILogger<FabricController> logger;

        public FabricController(ILogger<FabricController> logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

       

        public IActionResult JSON(object ob)
        {
            var jsonSerializer = new Newtonsoft.Json.JsonSerializer()
            {

                ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver()
            };


            jsonSerializer.Converters.Add(new StringEnumConverter());
            return Content(JToken.FromObject(ob, jsonSerializer).ToString(Newtonsoft.Json.Formatting.Indented), "application/json");
        }


        [HttpGet("providers/ServiceFabricGateway.Fabric/applications")]
        public async Task<IActionResult> ListApplication([FromServices] FabricClient fabric)
        {
            

            var applications = await fabric.QueryManager.GetApplicationListAsync();

            return JSON(applications);

        }
        public static string RemoveTypeFromName(string str)
        {
            if (str.EndsWith("Type")){
                return str.Substring(0, str.Length - 4);
            }
            return str;
        }
        [HttpPost("providers/ServiceFabricGateway.Fabric/metadata")]
        public async Task<IActionResult> GetMetadata([FromServices] FabricClient fabric, string remoteUrl)
        {
            using (var stream = await new HttpClient().GetStreamAsync(remoteUrl))
            {
                var zip = new ZipArchive(stream, ZipArchiveMode.Read);
                var entry = zip.GetEntry("ApplicationManifest.xml");

                XDocument xDocument = await XDocument.LoadAsync(entry.Open(), LoadOptions.None, HttpContext.RequestAborted);
                XNamespace ns = xDocument.Root.GetDefaultNamespace();
                var Parameters = xDocument.Root.Element(ns+ "Parameters").Elements(ns+"Parameter").ToDictionary(e=>e.Attribute("Name").Value,e=>e.Attribute("DefaultValue").Value);


                var applicationName = RemoveTypeFromName(xDocument.Root.Attribute("ApplicationTypeName").Value);
                var applications = await fabric.QueryManager.GetApplicationListAsync(new Uri($"fabric:/{applicationName}"));
                if (applications.Any(a=>a.ApplicationName == new Uri($"fabric:/{applicationName}")))
                {

                    foreach(var param in applications.FirstOrDefault(a => a.ApplicationName == new Uri($"fabric:/{applicationName}")).ApplicationParameters)
                    {
                        if (Parameters.ContainsKey(param.Name))
                        {
                            Parameters[param.Name] = param.Value;
                        }
                    }
                   
                }

                var services = new List<object>();
                foreach(var import in xDocument.Root.Descendants(ns+ "ServiceManifestImport"))
                {
                    var ServiceManifestRef = import.Element(ns + "ServiceManifestRef");
                    var path = ServiceManifestRef.Attribute("ServiceManifestName").Value;
                    var sentry = zip.GetEntry(path + "/ServiceManifest.xml");

                    XDocument sxDocument = await XDocument.LoadAsync(sentry.Open(), LoadOptions.None, HttpContext.RequestAborted);
                    XNamespace sns = sxDocument.Root.GetDefaultNamespace();
                    services.AddRange(sxDocument.Root.Element(sns + "ServiceTypes").Elements(sns + "StatelessServiceType")
                         .Select(e => new
                         {
                             ServiceTypeName = e.Attribute("ServiceTypeName").Value,
                             ServiceName = RemoveTypeFromName( e.Attribute("ServiceTypeName").Value)
                         }));

                }

                var obj = new {
                    applicationTypeName = xDocument.Root.Attribute("ApplicationTypeName").Value,
                    applicationName = applicationName,
                    applicationTypeVersion = xDocument.Root.Attribute("ApplicationTypeVersion").Value,
                    parameters= Parameters,
                    services = services
                };

                logger.LogInformation("metadata model {@deploymentModel}", obj);

                return Ok(obj);
            }

        }

        [HttpGet("providers/ServiceFabricGateway.Gateway/gateways")]
        public async Task<IActionResult> ListGateways([FromServices] IOptions<ReverseProxyOptions> options, [FromServices] IHostingEnvironment hostingEnvironment)
        {
            var http = new HttpClient();
            if (hostingEnvironment.IsDevelopment())
            {
                var str = await http.GetStringAsync("https://sf-gateway-test.westeurope.cloudapp.azure.com/gateway/services");

                return Content(str, "application/json");
            }
            {
                var str = await http.GetStringAsync($"https://{options.Value.ServerName}/gateway/services");

                return Content(str, "application/json");
            }

        }

        [HttpGet("providers/ServiceFabricGateway.Fabric/applicationsTypes")]
        public async Task<IActionResult> ListApplicationTypes([FromServices] FabricClient fabric)
        {
           

            var applications = await fabric.QueryManager.GetApplicationTypeListAsync();



            return JSON(applications);
        }
        [HttpGet("providers/ServiceFabricGateway.Fabric/applications/{applicationName}/services")]
        public async Task<IActionResult> ListServices([FromServices] FabricClient fabric,string applicationName)
        {
             

            var applications = await fabric.QueryManager.GetServiceListAsync(new Uri($"fabric:/{applicationName}"));



            return JSON(applications);
        }

      
        [HttpDelete("providers/ServiceFabricGateway.Fabric/applications/{applicationName}")]
        public async Task<IActionResult> DeleteApplication([FromServices] FabricClient fabric, string applicationName)
        {
           
            // var applications = await fabric.QueryManager.GetServiceListAsync(new Uri($"fabric:/{applicationName}"));

            await fabric.ApplicationManager.DeleteApplicationAsync(new DeleteApplicationDescription(new Uri($"fabric:/fabric:/{applicationName}")));

            return NoContent();
        }

        [HttpPost("providers/ServiceFabricGateway.Fabric/applications/{applicationName}/deployments")]
        public Task<IActionResult> UpgradeApplication([FromServices] FabricClient fabric, string applicationName, string remoteUrl, bool deleteIfExists)
        {
           
            return CreateDeployment(fabric, new DeploymentModel
            {
                RemoteUrl = remoteUrl,
                DeleteIfExists = deleteIfExists,
                ApplicationName = applicationName,              
            });
        }

       



        [HttpPost("providers/ServiceFabricGateway.Fabric/deployments", Name = nameof(CreateDeployment))]
        public async Task<IActionResult> CreateDeployment([FromServices] FabricClient fabric,[FromBody]DeploymentModel deploymentModel)
        {

            logger.LogInformation("Using {fabricClient}", fabric.Settings.ClientFriendlyName);



            using (var stream = await new HttpClient().GetStreamAsync(deploymentModel.RemoteUrl))
            {
                var zip = new ZipArchive(stream, ZipArchiveMode.Read);
                var entry = zip.GetEntry("ApplicationManifest.xml");

                XDocument xDocument = await XDocument.LoadAsync(entry.Open(), LoadOptions.None, HttpContext.RequestAborted);
                deploymentModel.ApplicationTypeName = xDocument.Root.Attribute("ApplicationTypeName").Value;
                deploymentModel.ApplicationTypeVersion = xDocument.Root.Attribute("ApplicationTypeVersion").Value;

                logger.LogInformation("Updated deployment model {@deploymentModel}", deploymentModel);
            }

            var types = await fabric.QueryManager.GetApplicationTypeListAsync(deploymentModel.ApplicationTypeName);

            if (!types.Any(a => a.ApplicationTypeName == deploymentModel.ApplicationTypeName && a.ApplicationTypeVersion == deploymentModel.ApplicationTypeVersion))
            {
                logger.LogInformation("Starting to provision {@deploymentModel}", deploymentModel);

                //            fabric.ApplicationManager.CreateApplicationAsync(new System.Fabric.Description.ApplicationDescription{ )
                await fabric.ApplicationManager.ProvisionApplicationAsync(
                    new ExternalStoreProvisionApplicationTypeDescription(
                        applicationPackageDownloadUri: new Uri(deploymentModel.RemoteUrl),
                        applicationTypeName: deploymentModel.ApplicationTypeName,
                        applicationTypeVersion: deploymentModel.ApplicationTypeVersion
                    ),TimeSpan.FromMinutes(5),HttpContext.RequestAborted
                );
                logger.LogInformation("Completed to provision {@deploymentModel}", deploymentModel);


            }

            var applicationName = new Uri($"fabric:/{deploymentModel.ApplicationName}");
            var applications = await fabric.QueryManager.GetApplicationListAsync(applicationName);
            if (!applications.Any(application => application.ApplicationName == applicationName))
            {
                await CreateApplication(deploymentModel, fabric, applicationName);
            }
            else
            {
                var existing = applications.FirstOrDefault(a => a.ApplicationName == applicationName);

                if (deploymentModel.DeleteIfExists)
                {
                    foreach (var param in existing.ApplicationParameters)
                    {
                        deploymentModel.Parameters.Add(param.Name, param.Value);
                    }
                  
                    await fabric.ApplicationManager.DeleteApplicationAsync(new DeleteApplicationDescription(applicationName));
                    await Task.Delay(1000);
                    await CreateApplication(deploymentModel, fabric, applicationName);

                    
                }
                else
                {

                    var upgrade = new ApplicationUpgradeDescription
                    {

                        ApplicationName = applicationName,
                        TargetApplicationTypeVersion = deploymentModel.ApplicationTypeVersion,
                        UpgradePolicyDescription = new MonitoredRollingApplicationUpgradePolicyDescription()
                        {
                            UpgradeMode = RollingUpgradeMode.Monitored,
                            MonitoringPolicy = new RollingUpgradeMonitoringPolicy()
                            {
                                FailureAction = UpgradeFailureAction.Rollback
                            }
                        }

                    };
                    foreach (var param in  existing.ApplicationParameters)
                    {
                        upgrade.ApplicationParameters.Add(param.Name, deploymentModel.Parameters.ContainsKey(param.Name) ? deploymentModel.Parameters[param.Name]: param.Value);
                    }
                    foreach(var param in deploymentModel.Parameters.Where(k=>!existing.ApplicationParameters.Any(a=>a.Name==k.Key)))
                    {
                        upgrade.ApplicationParameters.Add(param.Key, param.Value);
                    }
                    

                    await fabric.ApplicationManager.UpgradeApplicationAsync(upgrade);
                }
            }

           


            return CreatedAtRoute(nameof(CreateDeployment), new { });
        }

        private void ServiceManager_ServiceNotificationFilterMatched(object sender, EventArgs e)
        {
           
        }

        private async Task CreateApplication(DeploymentModel deploymentModel, FabricClient fabric, Uri applicationName)
        {
            logger.LogInformation("Starting to create application {@deploymentModel}", deploymentModel);
            await fabric.ApplicationManager.CreateApplicationAsync(
                new ApplicationDescription(
                    applicationName: applicationName,
                    applicationTypeName: deploymentModel.ApplicationTypeName,
                    applicationTypeVersion: deploymentModel.ApplicationTypeVersion,
                    applicationParameters: deploymentModel.Parameters.ToNameValueCollection()
                )
            );
            logger.LogInformation("Completed to create application {@deploymentModel}", deploymentModel);

            foreach (var serviceDeployment in deploymentModel.ServiceDeployments)
            {
                var serviceName = new Uri($"{applicationName}/{serviceDeployment.ServiceName}");
                logger.LogInformation("creating service for {applicationName} {ServiceName}", applicationName,serviceName);
                var services = await fabric.QueryManager.GetServiceListAsync(applicationName, serviceName);

                if (!services.Any(s => s.ServiceName == serviceName))
                {
                    await fabric.ServiceManager.CreateServiceAsync(description: new StatelessServiceDescription
                    {
                        ServiceTypeName = serviceDeployment.ServiceTypeName,
                        ApplicationName = applicationName,
                        ServiceName = serviceName,
                        InitializationData = serviceDeployment.InitializationData,
                        PartitionSchemeDescription = new SingletonPartitionSchemeDescription() { }
                    });
                    logger.LogInformation("Service created for {ServiceName}", serviceName);
                }
            }
            logger.LogInformation("Completed to create services {@deploymentModel}", deploymentModel);
        }


        [HttpPost("providers/ServiceFabricGateway.Fabric/security/encryptParameter")]
        public async Task<IActionResult> EncryptParameter([FromBody] EncryptParameterModel model, [FromServices] ICodePackageActivationContext codePackageActivationContext )
        {
            var encoded = Encoding.Unicode.GetBytes(model.Value);

            var thumbprint = codePackageActivationContext.GetConfigurationPackageObject("Config").Settings.Sections["Infrastructure"].Parameters["SecretsCertificate_Thumbprint"].Value;


            var cert = X509.LocalMachine.My.Thumbprint.Find(thumbprint, validOnly: false).FirstOrDefault();

            var content = new ContentInfo(encoded);
            var env = new EnvelopedCms(content);
            env.Encrypt(new CmsRecipient(cert));

            return Ok(new { value = Convert.ToBase64String(env.Encode())});
        }
    }

   
    public class EncryptParameterModel
    {
        public string Value { get; set; }
    }
}
