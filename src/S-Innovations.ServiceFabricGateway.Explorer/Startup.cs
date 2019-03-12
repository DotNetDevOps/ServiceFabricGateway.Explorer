using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Autofac;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;


namespace ServiceFabricGateway.Explorer
{
    public class Startup
    {
        private readonly ILifetimeScope container;
        private readonly IHostingEnvironment env;
        public Startup(ILifetimeScope container, IHostingEnvironment hostingEnvironment)
        {
            this.container = container ?? throw new ArgumentNullException(nameof(container));
            this.env = hostingEnvironment ?? throw new ArgumentNullException(nameof(hostingEnvironment));
        }

      
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            var reverseProxyOptions = container.Resolve<IOptions<ReverseProxyOptions>>().Value;
            var oidc = container.Resolve<IOptions<OidcClientConfiguration>>().Value;

            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Latest); ;

          

            services.AddAuthentication()
                .AddIdentityServerAuthentication((options =>
            {
                options.Authority = oidc.Authority; // $"https://{reverseProxyOptions.ServerName}/identity";
                options.RequireHttpsMetadata = true;
                options.EnableCaching = false;
                options.ApiName = $"https://{reverseProxyOptions.ServerName}/gateway";
            }));
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.Use(async (ctx, next) =>
                {
                    if (Path.GetExtension(ctx.Request.Path) == ".ts" || Path.GetExtension(ctx.Request.Path) == ".tsx")
                    {

                        await ctx.Response.WriteAsync(File.ReadAllText(ctx.Request.Path.Value.Substring(1)));
                    }
                    else
                    {
                        await next();
                    }

                });
            }

            app.UseStaticFiles();
            app.UseMvc();
        }
    }
}
