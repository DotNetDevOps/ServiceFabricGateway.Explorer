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



            var host = new FabricHostBuilder(args)
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

              }).ConfigureServices((context, services) =>
              {
                  services.WithKestrelHosting<Startup>("S-Innovations.ServiceFabricGateway.ExplorerType", ConfigureGateways);

                  if (!args.Contains("--serviceFabric"))
                  {
                      ConfigureIIS(services);
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

            await host.RunConsoleAsync();




        }

        private static IServiceCollection ConfigureIIS(IServiceCollection services)
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


            services.AddSingleton(fabricClient);

            return services;
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
