using System;
using System.Collections.Generic;
using System.Fabric;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using DotNetDevOps.ServiceFabric.Hosting;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ServiceFabric.Services.Remoting.Client;
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
            //var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

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


                 if (context.HostingEnvironment.IsDevelopment())
                 {
                     configurationBuilder.AddUserSecrets<Startup>();
                 }
             })
             //Setup services that exists on root, shared in all services
             .ConfigureServices((context, services) =>
             {



                 services.AddSingleton<CloudStorageAccount>((c) =>
                 {
                        var storage = c.GetService<IApplicationStorageService>();
                         if (storage != null)
                         {
                         var token = storage.GetApplicationStorageSharedAccessSignature().GetAwaiter().GetResult();
                         var name = storage.GetApplicationStorageAccountNameAsync().GetAwaiter().GetResult();
                         return new CloudStorageAccount(new StorageCredentials(token), name, null, true);
                     }

                    
                     return CloudStorageAccount.Parse(context.Configuration.GetSection("IdentityService")["StorageAccount"]);



                 });
             })
             .Configure<IdentityServiceOptions>("IdentityService")
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
             
            container.ConfigureServices((context, services) =>
            {
                services.AddSingleton< IApplicationManager>(new Dummy());
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
           

            host.ConfigureServices((context, services) =>
            {
                services.AddScoped(sp => ServiceProxy.Create<IApplicationStorageService>(new Uri("fabric:/S-Innovations.ServiceFabric.GatewayApplication/ApplicationStorageService"), listenerName: "V2_1Listener"));
                 
            });


            host.WithKestrelHosting<Startup>("ServiceFabricGateway.IdentityServiceType", (c) =>
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
                              UseHttp01Challenge = c.Resolve<Microsoft.Extensions.Hosting.IHostingEnvironment>().IsProduction(),
                          }
                      }
                  });

            await host.Build().RunAsync();
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
