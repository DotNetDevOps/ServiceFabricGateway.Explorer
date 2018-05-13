using IdentityModel;
using IdentityServer4;
using IdentityServer4.Extensions;
using IdentityServer4.Services;
using IdentityServer4.Validation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using IdentityServer4.Extensions;
using Microsoft.AspNetCore.Authorization;
using SInnovations.ServiceFabric.Storage.Services;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using SInnovations.ServiceFabric.RegistrationMiddleware.AspNetCore.Services;
using Microsoft.WindowsAzure.Storage.Blob;
using System.IO;

namespace ServiceFabricGateway.IdentityService.Controllers
{
    public class SigninModel
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }
    public class SecurityHeadersAttribute : ActionFilterAttribute
    {
        public override void OnResultExecuting(ResultExecutingContext context)
        {
            var result = context.Result;
            if (result is ViewResult)
            {
                // https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/X-Content-Type-Options
                if (!context.HttpContext.Response.Headers.ContainsKey("X-Content-Type-Options"))
                {
                    context.HttpContext.Response.Headers.Add("X-Content-Type-Options", "nosniff");
                }

                // https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/X-Frame-Options
                if (!context.HttpContext.Response.Headers.ContainsKey("X-Frame-Options"))
                {
                    context.HttpContext.Response.Headers.Add("X-Frame-Options", "SAMEORIGIN");
                }

                // https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Content-Security-Policy
                var csp = "default-src 'self'; object-src 'none'; frame-ancestors 'none'; sandbox allow-forms allow-same-origin allow-scripts; base-uri 'self';";
                // also consider adding upgrade-insecure-requests once you have HTTPS in place for production
                //csp += "upgrade-insecure-requests;";
                // also an example if you need client images to be displayed from twitter
                // csp += "img-src 'self' https://pbs.twimg.com;";

                // once for standards compliant browsers
                if (!context.HttpContext.Response.Headers.ContainsKey("Content-Security-Policy"))
                {
                    context.HttpContext.Response.Headers.Add("Content-Security-Policy", csp);
                }
                // and once again for IE
                if (!context.HttpContext.Response.Headers.ContainsKey("X-Content-Security-Policy"))
                {
                    context.HttpContext.Response.Headers.Add("X-Content-Security-Policy", csp);
                }

                // https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Referrer-Policy
                var referrer_policy = "no-referrer";
                if (!context.HttpContext.Response.Headers.ContainsKey("Referrer-Policy"))
                {
                    context.HttpContext.Response.Headers.Add("Referrer-Policy", referrer_policy);
                }
            }
        }
    }

    public class AzureActiveDirectoryProvisionModel
    {

        public string appId { get; set; }
    }
    public class LocalUsersProvisionModel
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }

        public string Password { get; set; }
        public string Email { get; set; }
    }
    [SecurityHeaders]
    public class AccountController : Controller
    {

        [HttpGet("authentications")]
        public async Task<IActionResult> ListAuthentications(
         [FromServices] IApplicationManager applicationManager,
         [FromServices] CloudStorageAccount storage)
        {


            var container = storage.CreateCloudBlobClient().GetContainerReference("identity");
            await container.CreateIfNotExistsAsync();
            var blobs = await container.ListBlobsSegmentedAsync(null);
            return Ok(blobs.Results.OfType<CloudBlockBlob>().Select(b=>Path.GetFileNameWithoutExtension( b.Name)));
        }


        [HttpPut("authentications/local-users")]
        public async Task<IActionResult> ProvisionLocalAuthentication(
           [FromServices] IApplicationManager applicationManager,
           [FromServices] CloudStorageAccount storage,
           [FromBody] LocalUsersProvisionModel model)
        {

            model.Password = model.Password.ToSha256();
          
            var container = storage.CreateCloudBlobClient().GetContainerReference("identity");
            await container.CreateIfNotExistsAsync();

            var blob = container.GetBlockBlobReference("local.json");

           

            await blob.UploadTextAsync(JsonConvert.SerializeObject(model),AccessCondition.GenerateIfNotExistsCondition(),null,null);
         //   await applicationManager.RestartRequestAsync(HttpContext.RequestAborted);

            return NoContent();
        }


        [Authorize(AuthenticationSchemes = IdentityServer4.AccessTokenValidation.IdentityServerAuthenticationDefaults.AuthenticationScheme)]
        [HttpPut("authentications/azure-active-directory")]
        public async Task<IActionResult> ProvisionAzureActiveDirectoryAuthentication(
            [FromServices] IApplicationManager applicationManager,
            [FromServices] CloudStorageAccount storage,
            [FromBody] AzureActiveDirectoryProvisionModel model)
        {

           
            var container = storage.CreateCloudBlobClient().GetContainerReference("identity");
            await container.CreateIfNotExistsAsync();

            var blob = container.GetBlockBlobReference("azuread.json");
            await blob.UploadTextAsync(JsonConvert.SerializeObject(model), AccessCondition.GenerateIfNotExistsCondition(), null, null);
            await applicationManager.RestartRequestAsync(HttpContext.RequestAborted);

            return NoContent();
        }


        [HttpGet("login")]
        public async Task<IActionResult> Login([FromServices] IIdentityServerInteractionService interaction, string returnUrl)
        {

            var context = await interaction.GetAuthorizationContextAsync(returnUrl);

            return ExternalLogin(context.IdP, returnUrl);
        
           
        }
        private void ProcessLoginCallbackForOidc(AuthenticateResult externalResult, List<Claim> localClaims, AuthenticationProperties localSignInProps)
        {
            // if the external system sent a session id claim, copy it over
            // so we can use it for single sign-out
            var sid = externalResult.Principal.Claims.FirstOrDefault(x => x.Type == JwtClaimTypes.SessionId);
            if (sid != null)
            {
                localClaims.Add(new Claim(JwtClaimTypes.SessionId, sid.Value));
            }

            // if the external provider issued an id_token, we'll keep it for signout
            var id_token = externalResult.Properties.GetTokenValue("id_token");
            if (id_token != null)
            {
                localSignInProps.StoreTokens(new[] { new AuthenticationToken { Name = "id_token", Value = id_token } });
            }
        }



        [HttpGet("externalLoginCallback")]
        public async Task<IActionResult> ExternalLoginCallback(
            [FromServices] IIdentityServerInteractionService interaction,
         string returnUrl)
        {
            var info = await HttpContext.AuthenticateAsync(IdentityServerConstants.ExternalCookieAuthenticationScheme);
            var tempUser = info?.Principal;
            if (tempUser == null)
            {
                throw new Exception("External authentication error");
            }

            var (provider, providerUserId, claims) = FindUserFromExternalProvider(info);

            var additionalLocalClaims = new List<Claim>();
            var localSignInProps = new AuthenticationProperties();
            ProcessLoginCallbackForOidc(info, additionalLocalClaims, localSignInProps);
      
            await HttpContext.SignInAsync(providerUserId, claims.FirstOrDefault(x=>x.Type=="name")?.Value, provider, localSignInProps,  additionalLocalClaims.ToArray());

            // delete temporary cookie used during external authentication
            await HttpContext.SignOutAsync(IdentityServer4.IdentityServerConstants.ExternalCookieAuthenticationScheme);


            if (interaction.IsValidReturnUrl(returnUrl) || Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return Content(JsonConvert.SerializeObject(new
            {
                provider,
                providerUserId,
                returnUrl,
                claims =

                claims.ToDictionary(c => c.Type, c => c.Value)
            }, Formatting.Indented));

          
        }

        private (string provider, string providerUserId, IEnumerable<Claim> claims) FindUserFromExternalProvider(AuthenticateResult result)
        {
            var externalUser = result.Principal;

            // try to determine the unique id of the external user (issued by the provider)
            // the most common claim type for that are the sub claim and the NameIdentifier
            // depending on the external provider, some other claim type might be used
            var userIdClaim = externalUser.FindFirst(JwtClaimTypes.Subject) ??
                              externalUser.FindFirst(ClaimTypes.NameIdentifier) ??
                              throw new Exception("Unknown userid");

            // remove the user id claim so we don't include it as an extra claim if/when we provision the user
            var claims = externalUser.Claims.ToList();
            claims.Remove(userIdClaim);

            var provider = result.Properties.Items["scheme"];
            var providerUserId = userIdClaim.Value;

            // find external user
            //  var user = _users.FindByExternalProvider(provider, providerUserId);

            return (provider, providerUserId, claims);
        }

        /// <summary>
        /// initiate roundtrip to external authentication provider
        /// </summary>
        [HttpGet]
        public IActionResult ExternalLogin(string provider, string returnUrl)
        {
            
           
                // start challenge and roundtrip the return URL and 
                var props = new AuthenticationProperties()
                {
                    RedirectUri = Url.Action("ExternalLoginCallback", new { returnUrl }),
                    Items =
                    {
                        { "returnUrl", returnUrl },
                        { "scheme", provider },
                    }
                };
                return Challenge(props, provider);
            
        }

        [HttpGet("logout")]
        
        public async Task<IActionResult> Logout(
                       [FromServices] IIdentityServerInteractionService interaction, 
                       string logoutId)
        {
            // build a model so the logged out page knows what to display
          
            var logout = await interaction.GetLogoutContextAsync(logoutId);

            return Redirect(logout.PostLogoutRedirectUri);
        }


        [HttpPost("authenticate")]
        public async Task<IActionResult> Authenticate(
            [FromBody] SigninModel signinModel,
            [FromServices] IResourceOwnerPasswordValidator resourceOwnerPasswordValidator, 
            [FromServices] ISystemClock systemClock
            )
        {
            var contx = new ResourceOwnerPasswordValidationContext { UserName = signinModel.Username, Password = signinModel.Password };

            await resourceOwnerPasswordValidator.ValidateAsync(contx);

            if (!contx.Result.IsError)
            {

           
                await HttpContext.SignInAsync(contx.Result.Subject.GetSubjectId(),contx.Result.Subject.Claims.ToArray());

                return NoContent();
            }

            return this.Unauthorized();
          
        }
    }
}
