
import * as module from "module";
import { AppBuilder, Middleware } from "si-appbuilder";
import { KoLayout } from "si-kolayout";
import * as ko from "knockout";
import { AppInsightsManager, AppInsightsContext, ApplicationInsightsOptions, ApplicationInsightsMiddleware } from "si-appbuilder-application-insights-middleware";
import { OidcMiddleware, OIDCAppContext, Subscription, AppContextAuthorizationSuccess, AppContextAuthorizationFailed } from "si-appbuilder-oidcmiddleware";
import { endpoints, EndpointsConfiguration } from "./endpoints";
import { ServiceCollection } from "si-dependency-injection";
import { LayoutMiddleware } from "./Middlewares/LayoutMiddleware";
import { KnockoutApplyBindingsMiddleware } from "./Middlewares/KnockoutApplyBindingsMiddleware";


import "css!./core.less";




function fadeIn(elem, ms = 1000) {
    if (!elem)
        return;

    elem.style.opacity = 0;
    elem.style.filter = "alpha(opacity=0)";
    elem.style.display = null;
    elem.style.visibility = "visible";


    let a = elem.querySelector(".svgContainer") as HTMLDivElement;

    if (ms) {
        var opacity = 0;
        var timer = setInterval(function () {
            opacity += 50 / ms;
            if (opacity >= 1) {
                clearInterval(timer);
                opacity = 1;

            }
            elem.style.opacity = opacity;
            elem.style.filter = "alpha(opacity=" + opacity * 100 + ")";
            if (a) {
                a.style.transform = opacity === 1 ? null : `rotateY(${(opacity < 0.5 ? 180 * opacity : 180 - 180 * opacity)}deg)`;
            }
        }, 50);

    }
    else {
        elem.style.opacity = 1;
        elem.style.filter = "alpha(opacity=1)";

    }
}

function fadeOut(elem: HTMLDivElement, ms = 400) {
    if (!elem)
        return;



    return new Promise((resolve, reject) => {

        if (ms) {
            var opacity = 1;
            var timer = setInterval(function () {
                opacity -= 50 / ms;
                if (opacity <= 0) {
                    clearInterval(timer);
                    opacity = 0;
                    elem.style.display = "none";
                    elem.style.visibility = "hidden";
                    resolve(true);
                }
                elem.style.opacity = opacity.toString();
                elem.style.filter = "alpha(opacity=" + opacity * 100 + ")";

            }, 50);
        }
        else {
            elem.style.opacity = "0";
            elem.style.filter = "alpha(opacity=0)";
            elem.style.display = "none";
            elem.style.visibility = "hidden";
            resolve(true);
        }
    });
}
ko.virtualElements.allowedBindings.fadedIf = true;

ko.bindingHandlers.fadedIf = {
    init: function (element: Comment, valueAccessor, allBindingsAccessor, data, bindingContext) {
        // Initially set the element to be instantly visible/hidden depending on the value
        var value = valueAccessor();
        //If the value is a normal function make it a computed so that it updates properly
        if (!ko.isObservable(value)) {
            value = ko.computed({ read: valueAccessor });
        }
        //attach our observable property to the accessor so that it can be used in the update function
        valueAccessor["domShown"] = ko.observable(ko.unwrap(value));

        //Wrap any contents of the element in a new div, and then bind that div using the "if" binding.
        //This way the element and its event hooks for fading in/out never leaves the dom, but all content does.
        //it also prevents applying multiple bindings to the same element.
        let contentWrapper = document.createElement("div");

        while (element.childNodes.length) {
            contentWrapper.appendChild(element.childNodes.item(0));
        }
        //element.appendChild(contentWrapper);
        setTimeout(() => {
            element.appendChild(contentWrapper);
        });
        // var contentWrapper = $(element).children().wrapAll().parent()[0];
        ko.applyBindingAccessorsToNode(contentWrapper, { 'if': function () { return valueAccessor["domShown"] } }, bindingContext);
        //
    },
    update: function (element, valueAccessor) {
        // Whenever the value subsequently changes, slowly fade the element in or out
        var value = valueAccessor();

        if (ko.unwrap(value)) {
            valueAccessor["domShown"](true); //restore the element to the DOM
            fadeIn(element);
        } else {
            fadeOut(element, 0).then(() => {

                valueAccessor["domShown"](false); //remove the element from the DOM

            });
        }
    }
};

//export interface AdalAppContextAuthorizationSuccess {
//    isSignedIn: true;
//    user: any;
//}
//export interface AdalAppContextAuthorizationFailed {
//    isSignedIn: false;
//    reason: string;
//}

//export interface AdalAppContext {
//    userManager?: ApplicationContext;
//    authorization?: AdalAppContextAuthorizationSuccess | AdalAppContextAuthorizationFailed;
//}
//var getCurrentUser = function (access_token) {
//    console.log('Calling API...');
//    var xhr = new XMLHttpRequest();
//    xhr.open('GET', 'https://graph.microsoft.com/v1.0/me', true);
//    xhr.setRequestHeader('Authorization', 'Bearer ' + access_token);
//    xhr.onreadystatechange = function () {
//        if (xhr.readyState === 4 && xhr.status === 200) {
//            // Do something with the response
//            console.log(JSON.stringify(JSON.parse(xhr.responseText), null, '  '));
//            ;
//        } else if (xhr.readyState === 4) {
//            console.log(xhr);
//            // TODO: Do something with the error (or non-200 responses)
//            console.log(xhr.responseText);
//        }
//    };
//    xhr.send();
//}

//var getSubscriptions = function (access_token) {
//    console.log('Calling API...');
//    var xhr = new XMLHttpRequest();
//    xhr.open('GET', 'https://management.azure.com/subscriptions?api-version=2016-06-01', true);
//    xhr.setRequestHeader('Authorization', 'Bearer ' + access_token);
//    xhr.onreadystatechange = function () {
//        if (xhr.readyState === 4 && xhr.status === 200) {
//            // Do something with the response
//            console.log(JSON.stringify(JSON.parse(xhr.responseText), null, '  '));
//            ;
//        } else if (xhr.readyState === 4) {
//            console.log(xhr);
//            // TODO: Do something with the error (or non-200 responses)
//            console.log(xhr.responseText);
//        }
//    };
//    xhr.send();
//}

//async function tryWithMSAL() {
//    let Msal = await import("Msal");
//    var applicationConfig = {


//        authority: `https://login.microsoftonline.com/802626c6-0f5c-4293-a8f5-198ecd481fe3/`,
//        clientID: "e620857d-78f0-4f13-a478-bab918c1be9c",
//        graphScopes: ["user.read"]
//    };

//    function loggerCallback(logLevel, message, piiLoggingEnabled) {
//        console.log(message);
//    }

//    var logger = new Msal.Logger(loggerCallback, { level: Msal.LogLevel.Verbose, correlationId: '12345' }); // level and correlationId are optional parameters.
//    //Logger has other optional parameters like piiLoggingEnabled which can be assigned as shown aabove. Please refer to the docs to see the full list and their default values.

//    function authCallback(errorDesc, token, error, tokenType) {
//        console.log(arguments); console.log("TEST");
//        debugger;
//        if (token) {
//        }
//        else {
//            console.log(error + ":" + errorDesc);
//        }
//    }

//    var userAgentApplication = new Msal.UserAgentApplication(applicationConfig.clientID, applicationConfig.authority, authCallback, { logger: logger, cacheLocation: 'localStorage' }); //logger and cacheLocation are optional parameters.
//    //userAgentApplication has other optional parameters like redirectUri which can be assigned as shown above.Please refer to the docs to see the full list and their default values.

//    window["userAgentApplication"] = userAgentApplication;
//    if (userAgentApplication.isCallback(window.location.hash)) {
//        debugger;
//    } else {

//        let user = userAgentApplication.getUser();
//        if (!user) {

//            window["test"] = () =>
//                userAgentApplication.loginRedirect(["user.read"]);

//        } else {

//            let token = await userAgentApplication.acquireTokenSilent(
//                ['user.read'], null, user);
//            console.log(token);
//            getCurrentUser(token);
//        }
//    }
//}

declare module "si-appbuilder-oidcmiddleware" {
    interface AppContextAuthorizationFailed {
        providers: string[];
    }
    interface AppContextAuthorizationSuccess {
        providers: string[];
    }
}

export interface AppContext extends AppInsightsContext, OIDCAppContext {

    rootLayout?: KoLayout
    endpoints: EndpointsConfiguration,
    serviceCollection: ServiceCollection
}

//function acquireTokenSilent(adal: ApplicationContext, resource: string) {
//    return new Promise((resolve, reject) => {
//        let original = adal.config.redirectUri;
//        adal.config.redirectUri = original + "silent";
//        adal.acquireToken(
//            resource,
//            function (error, token) {
              

//                if (error || !token) {
                    
//                    // TODO: Handle error obtaining access token
//                    console.log(error);
//                    reject(error);
                    
//                    return;
//                }
//                // Use the access token
//                resolve(token);
//            }
//        );

//        adal.config.redirectUri = adal.config.redirectUri.substr(0, adal.config.redirectUri.length - 6);

//    });
//}

//async function tryWithAdal(ctx: AdalAppContext) {
//    let adal = await import("adal-angular");

//    console.log(adal);
//    var context = new adal({
//        tenant: "common",
//        clientId: "e620857d-78f0-4f13-a478-bab918c1be9c"
//    });
//    ctx.userManager = context;



//    let err = null;
//    if (ctx.userManager.isCallback(window.location.hash)) {
//        // Handle redirect after token requests
//        ctx.userManager.handleWindowCallback();
//        location.hash = "";
//        err = ctx.userManager.getLoginError();
//        if (err) {
//            // TODO: Handle errors signing in and getting tokens
//            console.log(err);
//        }
//    }

//    if (!err) {
//        var user = ctx.userManager.getCachedUser();
//        if (user) {

//            try {
//                var graphToken = await acquireTokenSilent(ctx.userManager, 'https://graph.microsoft.com');

//                getCurrentUser(graphToken);
//            } catch (err) {
//                ctx.userManager.acquireTokenRedirect('https://graph.microsoft.com');
//                return;
//            }

//            try {

//                var managementToken = await acquireTokenSilent(ctx.userManager, 'https://management.azure.com/');

//                getSubscriptions(managementToken);
//            } catch (error) {
//                console.log(error);
//                if (error.indexOf("AADSTS65001: The user or administrator has not consented") === 0) {
//                    window.location.replace(`https://login.microsoftonline.com/${context.config.tenant}/oauth2/authorize?client_id=${context.config.clientId}&redirect_uri=${encodeURIComponent(context.config.redirectUri)}&response_type=code&prompt=admin_consent`);

//                }
//                //

//                //  ctx.userManager.acquireTokenRedirect('https://management.azure.com/');
//                return;
//            }



//            //ctx.userManager.acquireToken('', function (error, token) {
//            //    if (error || !token) {
//            //        console.log(error);
//            //    } else {

//            //        console.log(token);
//            //    }

//            //});

//        } else {
//            context.login();
//        }
//    }
//}
ko.bindingHandlers.asyncClick = {
    'init': function (element: HTMLElement, valueAccessor, allBindingsAccessor,
        viewModel, bindingContext) {
        var originalFunction = valueAccessor();
        let blocked = false;
        var newValueAccesssor = function () {
            return function () {
                if (blocked)
                    return;

                blocked = true;
                element.setAttribute("disabled", "disabled");
                let rv = originalFunction.apply(viewModel, arguments);
                if (rv && "then" in rv) {
                    setTimeout(async () => {
                        await rv;
                        element.removeAttribute("disabled");
                        blocked = false;

                    });

                } else {
                    element.removeAttribute("disabled");
                    blocked = false;
                }
            }
        }

        ko.bindingHandlers.click.init(element, newValueAccesssor,
            allBindingsAccessor, viewModel, bindingContext);
    }
}

export type AppMiddleware = Middleware<AppContext>;

let config = module.config();
console.log(module);
console.log(config);

declare module "si-dependency-injection" {
    interface IoC {
        (module: "AppContext"): AppContext;
        (module: "AuthorizationManager"): AppContextAuthorizationSuccess;
        (module: "RequestContext"): { hash: string };
        (module: "UserManager"): Oidc.UserManager;
    }

}


let appFunc = new AppBuilder<AppContext>()

    .use(ApplicationInsightsMiddleware)
    .use(OidcMiddleware)
    .use(async (ctx, next) => {

        await ctx.serviceCollection.addSingleton("AppContext", ctx);        
        await ctx.serviceCollection.addSingleton("UserManager", ctx.userManager);
       // await tryWithMSAL();

       
       

        let ok = await fetch(`${ctx.oidcOptions.authority}authentications`, {
            method: "GET"
        }).then(rsp => rsp.json());
        console.log(ok);

        ctx.authorization.providers = ok;

        if (ctx.authorization.isSignedIn) {
            await ctx.serviceCollection.addSingleton("AuthorizationManager", ctx.authorization);
            console.log(ctx.authorization.user);
        } else {

          
            //try {

            //    let ok = await fetch(`${ctx.oidcOptions.authority}providers/ServiceFabricGateway.Identity/authenticate`, {
            //        method: "POST",
            //        body: `username=foo&password=bar`,
            //        credentials: "include",
            //        headers: {
            //            'Content-Type': 'application/x-www-form-urlencoded'
            //        }
            //    }).then(rsp => rsp.ok);
            //    if (ok) {

            //        await ctx.userManager.signinRedirect();
            //    }

            //} catch (err) {
            //    console.log(err);

            // //   await ctx.userManager.signinRedirect();

            //}


       //     await ctx.userManager.signinRedirect({ acr_values:"idp:AAD"});
        }

   

      

        return next(ctx);
    })
    .use(LayoutMiddleware)
    .use(KnockoutApplyBindingsMiddleware)
    .build();

appFunc({
    endpoints: endpoints,
    oidcOptions :config.oidc,
    appInsightsOptions: config.applicationInsights,
    serviceCollection: new ServiceCollection(),
});
