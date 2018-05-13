using IdentityServer4;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServiceFabricGateway.IdentityService.Configuration
{
    public class ConfigureCookieDomainFromEnvironment : ConfigureNamedOptions<CookieAuthenticationOptions>
    {
        private IHostingEnvironment _environment;
        private readonly IOptions<IdentityServiceOptions> options;

        public ConfigureCookieDomainFromEnvironment(IHostingEnvironment env, IOptions<IdentityServiceOptions> options) : base(IdentityServerConstants.DefaultCookieAuthenticationScheme, null)
        {
            this._environment = env;
            this.options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public override void Configure(string name, CookieAuthenticationOptions options)
        {
            if (name == IdentityServerConstants.DefaultCookieAuthenticationScheme)
            {
               

                //if (!_environment.IsDevelopment())
                //    options.Cookie.Domain = this.options.Value.ServerName;
            }
             
        }
    }
}
