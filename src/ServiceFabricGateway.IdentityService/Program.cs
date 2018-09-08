using System;
using System.Collections.Generic;
using System.Fabric;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Serilog;
using Serilog.Events;
using SInnovations.ServiceFabric.Gateway.Model;
using SInnovations.ServiceFabric.RegistrationMiddleware.AspNetCore;
using SInnovations.ServiceFabric.RegistrationMiddleware.AspNetCore.Configuration;
using SInnovations.ServiceFabric.RegistrationMiddleware.AspNetCore.Extensions;
using SInnovations.ServiceFabric.RegistrationMiddleware.AspNetCore.Model;
using SInnovations.ServiceFabric.RegistrationMiddleware.AspNetCore.Services;
using SInnovations.ServiceFabric.Storage.Services;
using SInnovations.Unity.AspNetCore;
using Unity;
using Unity.Injection;
using Unity.Lifetime;
using Unity.Microsoft.DependencyInjection;

namespace ServiceFabricGateway.IdentityService
{

    public class IdentityServiceOptions
    {
        public string ServerName { get; set; }
        public string Thumbprint { get; set; }
    }
    public class Program
    {
        private const string LiterateLogTemplate = "[{Timestamp:HH:mm:ss} {Level}] {SourceContext}{NewLine}{Message}{NewLine}{Exception}{NewLine}";

        public static void Main(string[] args)
        {
            //var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");





            using (var container = new FabricContainer())

            {
                var environment = container.Resolve<IHostingEnvironment>();

                var config = new ConfigurationBuilder()
             .SetBasePath(Directory.GetCurrentDirectory())
             .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
             .AddJsonFile($"appsettings.{environment.EnvironmentName}.json", optional: true)
             .AddEnvironmentVariables();

                if (environment.IsDevelopment())
                {
                    config.AddUserSecrets<Startup>();
                }

                container.AddOptions()
                      .UseConfiguration(config) //willl also be set on hostbuilder                      
                      .ConfigureSerilogging(logConfiguration =>
                         logConfiguration//.MinimumLevel.Information()
                          .Enrich.FromLogContext()
                          .WriteTo.LiterateConsole(outputTemplate: LiterateLogTemplate))
                      .ConfigureApplicationInsights();


                container.Configure<IdentityServiceOptions>("IdentityService");

                container.RegisterType<CloudStorageAccount>(new ContainerControlledLifetimeManager(), new InjectionFactory((c) =>
                {


                    if (c.IsRegistered<IApplicationStorageService>())
                    {
                        var storage = c.Resolve<IApplicationStorageService>();
                        var token = storage.GetApplicationStorageSharedAccessSignature().GetAwaiter().GetResult();
                        var name = storage.GetApplicationStorageAccountNameAsync().GetAwaiter().GetResult();
                        return new CloudStorageAccount(new StorageCredentials(token), name, null, true);

                    }
                    return CloudStorageAccount.Parse(c.Resolve<IConfigurationRoot>().GetSection("IdentityService")["StorageAccount"]);



                }));

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
            container.RegisterInstance<IApplicationManager>(new Dummy());

            Log.Logger = new LoggerConfiguration()
                 .MinimumLevel.Debug()
                 .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                 .Enrich.FromLogContext()
                 .WriteTo.Console()
                 .CreateLogger();

            var host = new WebHostBuilder()
                 .UseKestrel()
                 .UseContentRoot(Directory.GetCurrentDirectory())
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

        private static void RunInServiceFabric(IUnityContainer container)
        {
            container.WithServiceProxy<IApplicationStorageService>("fabric:/S-Innovations.ServiceFabric.GatewayApplication/ApplicationStorageService", listenerName: "V2_1Listener");



            container.WithKestrelHosting<Startup>("ServiceFabricGateway.IdentityServiceType", (c) =>
                  new KestrelHostingServiceOptions
                  {
                      GatewayOptions = new GatewayOptions
                      {
                          Key = "ServiceFabricGateway.IdentityServiceType",
                          ServerName = c.Resolve<IOptions<IdentityServiceOptions>>().Value.ServerName,
                          ReverseProxyLocation = "/identity/",
                          Ssl = new SslOptions
                          {
                              Enabled = true,
                              SignerEmail = "info@earthml.com",
                              UseHttp01Challenge = c.Resolve<IHostingEnvironment>().IsProduction(),
                          }
                      }
                  });

            Thread.Sleep(Timeout.Infinite);
        }


    }

    public class Dummy : IApplicationManager
    {
        public Task RestartRequestAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
