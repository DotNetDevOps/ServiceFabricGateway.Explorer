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
using Autofac;
using DotNetDevOps.ServiceFabric.Hosting;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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

        /// <summary>
        /// Event Handler delegate to log if an unhandled AppDomain exception occurs.
        /// </summary>
        /// <param name="sender">the sender</param>
        /// <param name="e">the exception details</param>
        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = e.ExceptionObject as Exception;
            //  ServiceEventSource.Current.UnhandledException(ex.GetType().Name, ex.Message, ex.StackTrace);
        }

        /// <summary>
        /// Event Handler delegate to log if an unobserved task exception occurs.
        /// </summary>
        /// <param name="sender">the sender</param>
        /// <param name="e">the exception details</param>
        /// <remarks>
        /// We intentionally do not mark the exception as Observed, which would prevent the process from being terminated.
        /// We want the unobserved exception to take out the process. Note, as of .NET 4.5 this relies on the ThrowUnobservedTaskExceptions
        /// runtime configuration in the host App.Config settings.
        /// </remarks>
        private static void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            //  ServiceEventSource.Current.UnobservedTaskException(e.Exception?.GetType().Name, e.Exception?.Message, e.Exception?.StackTrace);

            AggregateException flattened = e.Exception?.Flatten();
            foreach (Exception ex in flattened?.InnerExceptions)
            {
                //   ServiceEventSource.Current.UnobservedTaskException(ex.GetType().Name, ex.Message, ex.StackTrace);
            }

            // Marking as observed to prevent process exit.
            // e.SetObserved();
        }

        public static async Task Main(string[] args)
        {
            // var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

            Environment.SetEnvironmentVariable("LD_LIBRARY_PATH", null);
            // var cp = CertificateProvider.GetProvider("BouncyCastle");

            // Setup unhandled exception handlers.
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;



            var host = new FabricHostBuilder()
              //Add fabric configuration provider
              .ConfigureAppConfiguration((context, configurationBuilder) =>
              {
                  //  context.HostingEnvironment.EnvironmentName = System.Environment.GetEnvironmentVariable("NETCORE_ENVIRONMENT");

                  configurationBuilder
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true)
                    .AddEnvironmentVariables();

                  if (args.Contains("--serviceFabric"))
                  {
                      configurationBuilder.AddServiceFabricConfig("Config");
                  }

              })
                .Configure<EndpointsOptions>("Endpoints")
                .Configure<OidcClientConfiguration>("OidcClientConfiguration")
                .Configure<ReverseProxyOptions>("ReverseProxySettings")
                //Configure Logging
                .ConfigureSerilogging(
                    (context, logConfiguration) =>
                             logConfiguration.MinimumLevel.Debug()
                             .Enrich.FromLogContext()
                              .WriteTo.LiterateConsole(outputTemplate: LiterateLogTemplate))
                              
                .ConfigureApplicationInsights();


            if (args.Contains("--serviceFabric"))
            {

                await RunFabric(host);
            }
            else
            {
                await RunIis(host);
            }




        }

        private static async Task RunIis(IHostBuilder container)
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

            container.ConfigureServices((context, services) =>
            {
                services.AddSingleton(fabricClient);
            });

            var app = container.Build();

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
                   .ConfigureServices((ctx, collection) => { collection.AddSingleton(container); })
                 .Build();

            await app.StartAsync();

            await host.RunAsync();

            await app.StopAsync();


        }

        private static async Task RunFabric(IHostBuilder host)
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






            host.WithKestrelHosting<Startup>("S-Innovations.ServiceFabricGateway.ExplorerType", ConfigureGateways);

            await host.Build().RunAsync();
        }

        private static KestrelHostingServiceOptions ConfigureGateways(IComponentContext container)
        {

            var options = container.Resolve<IOptions<ReverseProxyOptions>>().Value;
            var isProd = container.Resolve<Microsoft.Extensions.Hosting.IHostingEnvironment>().IsProduction();

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
