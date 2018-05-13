using System;
using System.Collections.Generic;
using System.Fabric;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Serilog;
using Serilog.Events;
using SInnovations.ServiceFabric.Gateway.Model;
using SInnovations.ServiceFabric.RegistrationMiddleware.AspNetCore;
using SInnovations.ServiceFabric.RegistrationMiddleware.AspNetCore.Configuration;
using SInnovations.ServiceFabric.RegistrationMiddleware.AspNetCore.Extensions;
using SInnovations.ServiceFabric.RegistrationMiddleware.AspNetCore.Model;
using SInnovations.Unity.AspNetCore;
using Unity;
using Unity.Microsoft.DependencyInjection;

namespace ServiceFabricGateway.Explorer
{

    public class ReverseProxyOptions
    {
        public string ServerName { get; set; }

    }
    public class EndpointsOptions
    {
        public string StorageServiceEndpoint { get; set; }
        public string ResourceApiEndpoint { get; set; }

    }
    public class OidcClientConfiguration
    {
        public string Authority { get; set; }

        [JsonProperty("client_id")]
        public string ClientId { get; set; }

        public int accessTokenExpiringNotificationTime { get; set; }
        public string scope { get; set; }
    }
    public class Program
    {
        private const string LiterateLogTemplate = "[{Timestamp:HH:mm:ss} {Level}] {SourceContext}{NewLine}{Message}{NewLine}{Exception}{NewLine}";

      

        public static void Main(string[] args)
        {
           // var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
 
           



            using (var container = new FabricContainer())
            {
                var config = new ConfigurationBuilder()
               .SetBasePath(Directory.GetCurrentDirectory())
               .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
               .AddJsonFile($"appsettings.{container.Resolve<IHostingEnvironment>().EnvironmentName}.json", optional: true)
               .AddEnvironmentVariables();

                container.AddOptions()
                      .UseConfiguration(config) //willl also be set on hostbuilder                      
                      .ConfigureSerilogging(logConfiguration =>
                          logConfiguration.MinimumLevel.Information()
                          .Enrich.FromLogContext()
                          .WriteTo.LiterateConsole(outputTemplate: LiterateLogTemplate))
                      .ConfigureApplicationInsights();

                container.Configure<EndpointsOptions>("Endpoints");
                container.Configure<OidcClientConfiguration>("OidcClientConfiguration");
                container.Configure<ReverseProxyOptions>("ReverseProxySettings");

                if (args.Contains("--serviceFabric"))
                {
                    config.AddServiceFabricConfig("Config"); // Add Service Fabric configuration settings.
                    RunInServiceFabric(container);
                }
                else
                {
                    RunOnIIS(container);
                }

            }

        }

        private static void RunOnIIS(IUnityContainer container)
        {

            X509Credentials cert = new X509Credentials
            {
                FindType = X509FindType.FindByThumbprint,
                FindValue = "7B58E936E077ADCFD8ABD278C4BD895D652411CD",
                ProtectionLevel = ProtectionLevel.EncryptAndSign,
                StoreLocation = StoreLocation.CurrentUser,
                StoreName = "My",
            };
            cert.RemoteCertThumbprints.Add("7B58E936E077ADCFD8ABD278C4BD895D652411CD");


            var fabricClient = new FabricClient(cert,
                new FabricClientSettings
                { 
                    ClientFriendlyName = "S-Innovations VSTS Deployment Client"
                }, "sf-gateway-test.westeurope.cloudapp.azure.com:19000");
            
            container.RegisterInstance(fabricClient);

            Log.Logger = new LoggerConfiguration()
                 .MinimumLevel.Debug()
                 .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                 .Enrich.FromLogContext()
                 .WriteTo.Console()
                 .CreateLogger();

            var host = new WebHostBuilder()
                 .UseKestrel()
                 .UseContentRoot(Directory.GetCurrentDirectory())
                .UseWebRoot("artifacts")
                 .ConfigureLogging(logbuilder =>
                 {

                     logbuilder.AddSerilog();
                 })
                 .UseIISIntegration()
                 .UseStartup<Startup>()
                 .UseApplicationInsights()
                 .UseUnityServiceProvider(container)
                   .ConfigureServices((ctx, collection) => { collection.AddSingleton(container); })
                 .Build();

            host.Run();
        }

        private static void RunInServiceFabric(FabricContainer container)
        {
 
                //string clientCertThumb = "71DE04467C9ED0544D021098BCD44C71E183414E";
                //string serverCertThumb = "A8136758F4AB8962AF2BF3F27921BE1DF67F4326";
                //string CommonName = "www.clustername.westus.azure.com";
                //string connection = "sf-gateway-test.westeurope.cloudapp.azure.com:19000";

                //var xc = GetCredentials(clientCertThumb, serverCertThumb, CommonName);
                //var fc = new FabricClient(xc, connection);

                //try
                //{
                //    var ret = fc.ClusterManager.GetClusterManifestAsync().Result;
                //    Console.WriteLine(ret.ToString());
                //}
                //catch (Exception e)
                //{
                //    Console.WriteLine("Connect failed: {0}", e.Message);
                //}


              
                
            

            container.WithKestrelHosting<Startup>("S-Innovations.ServiceFabricGateway.ExplorerType", ConfigureGateways);

            Thread.Sleep(Timeout.Infinite);
        }

        private static KestrelHostingServiceOptions ConfigureGateways(IUnityContainer container)
        {

            var options = container.Resolve<IOptions<ReverseProxyOptions>>().Value;
            var isProd = container.Resolve<IHostingEnvironment>().IsProduction();

            return new KestrelHostingServiceOptions
            {
                GatewayOptions = new GatewayOptions
                {
                    Key = "S-Innovations.ServiceFabricGateway.ExplorerType",
                    ServerName = options.ServerName,
                    ReverseProxyLocation = "/explorer/",
                    Ssl = new SslOptions
                    {
                        Enabled = true,
                        SignerEmail = "info@earthml.com",
                        UseHttp01Challenge = isProd
                    }
                },
                AdditionalGateways = new[]
                {
                    new  GatewayOptions{
                        Key = "S-Innovations.ServiceFabricGateway.ServiceProvider",
                        ServerName = options.ServerName,
                        ReverseProxyLocation =
                            new string[]{ "ServiceFabricGateway.Fabric","ServiceFabricGateway.Gateway"}.BuildResourceProviderLocation(),
                        Ssl = new SslOptions
                            {
                            Enabled = true,
                            SignerEmail = "info@earthml.com",
                            UseHttp01Challenge= isProd
                            }
                    },
                }
            };
        }

    }
}
