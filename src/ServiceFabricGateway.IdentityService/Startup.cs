using Autofac;
using IdentityModel;
using IdentityServer4;
using IdentityServer4.Models;
using IdentityServer4.Services;
using IdentityServer4.Stores.Serialization;
using IdentityServer4.Validation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json.Linq;
using ServiceFabricGateway.IdentityService.Configuration;
using ServiceFabricGateway.IdentityService.Controllers;
using SInnovations.ServiceFabric.Gateway.Common.Services;
using SInnovations.ServiceFabric.ResourceProvider;
using SInnovations.ServiceFabric.Storage.Extensions;
using SInnovations.ServiceFabric.Storage.Services;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace ServiceFabricGateway.IdentityService
{
    public class LocalResourceOwnerPasswordValidator : IResourceOwnerPasswordValidator
    {
        private readonly CloudStorageAccount storage;
        private readonly ISystemClock systemClock;
        private readonly ILogger logger;

        public LocalResourceOwnerPasswordValidator(CloudStorageAccount cloudStorageAccount, ISystemClock systemClock, ILogger<LocalResourceOwnerPasswordValidator> logger)
        {
            this.storage = cloudStorageAccount ?? throw new ArgumentNullException(nameof(cloudStorageAccount));
            this.systemClock = systemClock ?? throw new ArgumentNullException(nameof(systemClock));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        public async Task ValidateAsync(ResourceOwnerPasswordValidationContext context)
        {
            var container = storage.CreateCloudBlobClient().GetContainerReference("identity");

            var local = container.GetBlockBlobReference("local.json");

            if (await container.ExistsAsync() && await local.ExistsAsync())
            {

                var localuser = JToken.Parse(local.DownloadTextAsync().GetAwaiter().GetResult()).ToObject<LocalUsersProvisionModel>();
                logger.LogInformation("Testing {@localuser} agaisnt {@context} ", localuser, context);
                if (context.Password.Sha256() == localuser.Password && context.UserName == localuser.Email)
                {
                    var svr = new IdentityServerUser(localuser.Email)
                    {
                        AuthenticationTime = systemClock.UtcNow.DateTime,
                        AdditionalClaims = new[]
                        {
                        new Claim(JwtClaimTypes.GivenName ,localuser.FirstName),
                              new Claim(JwtClaimTypes.FamilyName ,localuser.LastName),
                               new Claim(JwtClaimTypes.Name ,$"{localuser.FirstName} {localuser.LastName}")
                    },
                        IdentityProvider = "local"
                    };
                    var claimsPrincipal = svr.CreatePrincipal();

                    context.Result = new GrantValidationResult
                    {
                        Subject = claimsPrincipal
                    };
                    return;
                }

                //idsrvBuilder.AddTestUsers(new List<IdentityServer4.Test.TestUser>{ new IdentityServer4.Test.TestUser{
                //         IsActive = true, Password = localuser.Password, SubjectId = localuser.Email, Username =localuser.Email, Claims=new []{
                //             new Claim(IdentityModel.JwtClaimTypes.GivenName ,localuser.FirstName),
                //              new Claim(IdentityModel.JwtClaimTypes.FamilyName ,localuser.LastName),
                //               new Claim(IdentityModel.JwtClaimTypes.Name ,$"{localuser.FirstName} {localuser.LastName}")
                //         }
                //    } });
            }

            context.Result = new GrantValidationResult(TokenRequestErrors.InvalidGrant, "local user not found");
        }
    }
    public class ADProfileService : IProfileService
    {
        private readonly ILogger logger;
        private readonly CloudStorageAccount storage;

        public ADProfileService(ILogger<ADProfileService> logger, CloudStorageAccount cloudStorageAccount)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.storage = cloudStorageAccount ?? throw new ArgumentNullException(nameof(cloudStorageAccount));
        }
        public async Task GetProfileDataAsync(ProfileDataRequestContext context)
        {
            context.LogProfileRequest(logger);

            var container = storage.CreateCloudBlobClient().GetContainerReference("identity");

            var local = container.GetBlockBlobReference("local.json");

            if (await container.ExistsAsync() && await local.ExistsAsync())
            {

                var localuser = JToken.Parse(local.DownloadTextAsync().GetAwaiter().GetResult()).ToObject<LocalUsersProvisionModel>();
                context.AddRequestedClaims(new[] {
                 new Claim(JwtClaimTypes.GivenName ,localuser.FirstName),
                              new Claim(JwtClaimTypes.FamilyName ,localuser.LastName),
                               new Claim(JwtClaimTypes.Name ,$"{localuser.FirstName} {localuser.LastName}")
                });
            }
              
            context.LogIssuedClaims(logger);

          
        }

        public Task IsActiveAsync(IsActiveContext context)
        {
            context.IsActive = true;

            return Task.CompletedTask;
        }
    }

    //public class MyAuthorizeInteractionResponseGenerator : AuthorizeInteractionResponseGenerator
    //{
    //    private readonly IHttpContextAccessor httpContextAccessor;
    //    private readonly IResourceOwnerPasswordValidator resourceOwnerPasswordValidator;

    //    public MyAuthorizeInteractionResponseGenerator(IHttpContextAccessor httpContextAccessor, ISystemClock clock, ILogger<AuthorizeInteractionResponseGenerator> logger, IConsentService consent, IProfileService profile, IResourceOwnerPasswordValidator resourceOwnerPasswordValidator) : base(clock, logger, consent, profile)
    //    {
    //        this.httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    //        this.resourceOwnerPasswordValidator = resourceOwnerPasswordValidator ?? throw new ArgumentNullException(nameof(resourceOwnerPasswordValidator));
    //    }
    //    public override async Task<InteractionResponse> ProcessInteractionAsync(ValidatedAuthorizeRequest request, ConsentResponse consent = null)
    //    {
    //        var arc = request.GetAcrValues();

    //        // Logger.LogInformation("{@request}", request);

    //        if (request.ClientId == "ServiceFabricGateway.Explorer" && request.PromptMode == "none")
    //        {
    //            var username = arc.FirstOrDefault(c => c.StartsWith("usr:")).Substring("usr:".Length);
    //            var password = arc.FirstOrDefault(c => c.StartsWith("pwd:")).Substring("pwd:".Length);

    //            var contx = new ResourceOwnerPasswordValidationContext { UserName = username, Password = password };

    //            await resourceOwnerPasswordValidator.ValidateAsync(contx);

    //            if (!contx.Result.IsError)
    //            {

    //                var svr = new IdentityServerUser(contx.Result.Subject.GetSubjectId()) {
    //                    AuthenticationTime = Clock.UtcNow.DateTime,
    //                    AdditionalClaims = contx.Result.Subject.Claims.ToList(),
    //                    IdentityProvider = "local"
    //                };
    //                var claimsPrincipal = svr.CreatePrincipal();
    //                request.Subject = contx.Result.Subject;

    //                await httpContextAccessor.HttpContext.SignInAsync(svr);

    //                return new InteractionResponse() { };
    //            }
    //        }


    //        return await base.ProcessInteractionAsync(request, consent);


    //    }

    //}

    public class Startup
    {
        private readonly ILifetimeScope _container;
        private readonly IHostingEnvironment env;
        private readonly IdentityServiceOptions options;
        private readonly ILogger _logger;
        public Startup(ILifetimeScope container, IHostingEnvironment hostingEnvironment)
        {
            _container = container;
            this.env = hostingEnvironment ?? throw new ArgumentNullException(nameof(hostingEnvironment));
            this.options = _container.Resolve<IOptions<IdentityServiceOptions>>().Value??throw new ArgumentNullException(nameof(IOptions<IdentityServiceOptions>));
            this._logger = _container.Resolve<ILoggerFactory>().CreateLogger<Startup>();
            _logger.LogInformation("{@IdentityServiceOptions}", options);

        }
        
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddCors(o =>
            {
                o.AddPolicy("IdentityServicePolicy", builder => builder.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin().AllowCredentials().SetPreflightMaxAge(TimeSpan.FromHours(1)));
            });



            var secrets = _container.Resolve<IKeyVaultService>().GetSecretsAsync("test").GetAwaiter().GetResult();
            var certs = secrets.Select(s => new X509Certificate2(Convert.FromBase64String(s), (string)null, X509KeyStorageFlags.MachineKeySet)).ToArray();
            _logger.LogInformation("Found {count} certificates",certs.Length);
            //  X509Certificate2 cert = X509.LocalMachine.My.Thumbprint.Find(options.Thumbprint, validOnly: false).FirstOrDefault();

            if (!env.IsDevelopment())
            {
                
                services.AddApplicationStorageDataProtection(_container.Resolve<IApplicationStorageService>(), certs.First(), $"{options.Thumbprint.ToLower()}-identity", certs.Skip(1).ToArray());

            }
            services.AddSingleton<IConfigureOptions<CookieAuthenticationOptions>, ConfigureCookieDomainFromEnvironment>();
            services.AddTransient<IEventSink, DefaultEventSink>();
       //     services.AddScoped<IAuthorizeInteractionResponseGenerator, MyAuthorizeInteractionResponseGenerator>();

            services.AddMvc()
                .SetCompatibilityVersion(CompatibilityVersion.Latest)
            .AddJsonOptions((options) =>
            {
                options.SerializerSettings.Converters.Add(new IdentityServer4.Stores.Serialization.ClaimConverter());
            });

            var idsrvBuilder = services.AddIdentityServer(options =>
            {

                options.Events.RaiseFailureEvents = true;
                options.Events.RaiseErrorEvents = true;
                options.Events.RaiseSuccessEvents = true;
                options.UserInteraction.LoginUrl = "/login";
                options.UserInteraction.LogoutUrl = "/logout";


            })
            .AddSigningCredential(certs.First())
            .AddInMemoryClients(new[] {  new Client {
                    ClientId = "ServiceFabricGateway.Explorer",
                    ClientName = "Service Fabric Gateway Explorer",
                    ClientUri = $"https://{options.ServerName}/explorer/",
                    AllowedGrantTypes = GrantTypes.Implicit,
                    AllowAccessTokensViaBrowser = true,
                    RequireClientSecret = false,
                    RequireConsent = false,
                    AccessTokenType = AccessTokenType.Jwt,
                     PostLogoutRedirectUris=
                {
                        $"https://{options.ServerName}/explorer/",
                           "https://localhost:44352/",
                },
                    RedirectUris =
                        {
                            $"https://{options.ServerName}/explorer/",
                            $"https://{options.ServerName}/explorer/silent",
                            "https://localhost:44352/",
                            "https://localhost:44352/silent"
                        },
                    AllowedCorsOrigins = {  $"https://{options.ServerName}" ,"https://localhost:44352"},
                    AllowedScopes =
                        {
                            IdentityServerConstants.StandardScopes.OpenId,
                            IdentityServerConstants.StandardScopes.Profile,
                            IdentityServerConstants.StandardScopes.Email,
                            $"https://{options.ServerName}/identity",
                            $"https://{options.ServerName}/gateway"
                        },
                    }
            }).AddInMemoryApiResources(new[]
           {
                new ApiResource($"https://{options.ServerName}/identity", "Service Fabric Gateway Identity API"),
                new ApiResource($"https://{options.ServerName}/gateway", "Service Fabric Gateway Management API")


           }).AddInMemoryIdentityResources(new[]
            {
                // some standard scopes from the OIDC spec
                new IdentityResources.OpenId(),
                new IdentityResources.Profile(),
                new IdentityResources.Email(),

                // custom identity resource with some consolidated claims
                new IdentityResource("custom.profile", new[] { JwtClaimTypes.Name, JwtClaimTypes.Email, "location" })
            });
            
            
            

            var authentication = services.AddAuthentication();



            authentication.AddIdentityServerAuthentication((options =>
                {
                    options.Authority = this.options.ServerName.Contains("localhost") ? $"https://{this.options.ServerName}" : $"https://{this.options.ServerName}/identity";
                    options.RequireHttpsMetadata = true;
                    options.EnableCaching = false;
                    options.ApiName = $"https://{this.options.ServerName}/identity";
                }));

            idsrvBuilder.AddResourceOwnerValidator<LocalResourceOwnerPasswordValidator>();
            idsrvBuilder.AddProfileService<ADProfileService>();

            try
            {
                var storage = _container.Resolve<CloudStorageAccount>();
                var container = storage.CreateCloudBlobClient().GetContainerReference("identity");

                var blob = container.GetBlockBlobReference("azuread.json");
                if (container.ExistsAsync().GetAwaiter().GetResult() && blob.ExistsAsync().GetAwaiter().GetResult())
                {
                    var appRegistration = JToken.Parse(blob.DownloadTextAsync().GetAwaiter().GetResult());

                   


                    authentication.AddOpenIdConnect("AAD", "Azure Active Directory", options =>
                        {
                            options.CorrelationCookie.Path = "/identity/aad-signin-oidc";
                            options.NonceCookie.Path = "/identity/aad-signin-oidc";
                            options.SignInScheme = IdentityServerConstants.ExternalCookieAuthenticationScheme;
                            options.SignOutScheme = IdentityServerConstants.SignoutScheme;
                            options.CallbackPath = "/aad-signin-oidc";
                            options.Authority = "https://login.microsoftonline.com/common";
                            options.ClientId = appRegistration.SelectToken("$.appId").ToString();// "f98fa34a-2aa2-4ff4-b7bf-a7ef5ab1890b";
                        options.Scope.Add("openid");
                            options.Scope.Add("profile");
                            options.TokenValidationParameters = new TokenValidationParameters
                            {
                                ValidateIssuer = false
                            };
                            options.GetClaimsFromUserInfoEndpoint = true;
                            options.Events.OnRedirectToIdentityProvider = (r) =>
                            {
                                r.ProtocolMessage.RedirectUri = r.ProtocolMessage.RedirectUri.Replace("identity//", "identity/");

                                return Task.CompletedTask;
                            };
                        });
                }
            }catch(Exception ex)
            {

            }

            JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
               
            }
            if (env.IsStaging())
            {
                app.Use((context, next) =>
                {
                    context.Request.Host = new HostString(context.Request.Host.Host, 8500);
                    return next();
                });
            }
            app.Use((context, next) =>
            {
                //   context.Request.PathBase = "/identity/";
                context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Test").LogInformation(context.Request.PathBase);
                if(context.Request.PathBase.HasValue && context.Request.PathBase.Value.EndsWith("/"))
                {
                    context.Request.PathBase = context.Request.PathBase.Value.TrimEnd('/');
                }
                return next();
            });

            app.UseIdentityServer();

            app.UseCors("IdentityServicePolicy");
            app.UseMvc();
        }
    }
}
