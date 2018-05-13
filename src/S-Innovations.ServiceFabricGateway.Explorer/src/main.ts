import * as module from "module";
console.log(module.id);

define("monaco-editor", ["vs/editor/editor.main"], function () {

    // var amdRequire = global.require;
    // global.require = nodeRequire;




    return window["monaco"];
});

function indexModule(name){
    return {
        name: name,
        location: "libs/"+name,
        main: "index"
    }   ;
}
requirejs.config({
    shim:{
        "oidc-client": {
            exports: "Oidc"
        },
        "adal-angular": {
            exports: "AuthenticationContext"
        }
    },
    paths:{
    "css": "libs/requirejs/css",
    "text": "libs/requirejs/text",
        "nprogress": "libs/nprogress/nprogress",
        "adal": "//secure.aadcdn.microsoftonline-p.com/lib/1.0.17/js/adal.min",
    "knockout": ["//cdnjs.cloudflare.com/ajax/libs/knockout/3.4.2/knockout-min", "libs/knockout/knockout-latest"],
    "template": "libs/si-kolayout/template",
    "stringTemplateEngine": "libs/si-kolayout/stringTemplateEngine",
    "si-appbuilder-application-insights-middleware": "libs/si-appbuilder-application-insights-middleware/ApplicationInsightsMiddleware",
    "si-appbuilder-oidcmiddleware": "libs/si-appbuilder-oidcmiddleware/OidcMiddleware",
    "oidc-client": ["//cdnjs.cloudflare.com/ajax/libs/oidc-client/1.4.1/oidc-client.min", "libs/oidc-client/oidc-client.min"],
        "animejs": "libs/animejs/anime.min",
        "Msal": "//secure.aadcdn.microsoftonline-p.com/lib/0.1.5/js/msal.min",
        "adal-angular": "//secure.aadcdn.microsoftonline-p.com/lib/1.0.17/js/adal.min",
        "flexboxgrid": "libs/flexboxgrid",
        "pako": "libs/pako/pako.min",
        "ResizeSensor": "libs/resizesensor/ResizeSensor",
        "draggabilly": "libs/draggabilly/draggabilly.pkgd.min",
        'vs': 'libs/vs',
    },
    packages:[
        {
            name: "ServiceFabricGateway",
            location: "src",
            main: "index"
        },
        indexModule("si-appbuilder"),
        indexModule("si-dependency-injection"),
        indexModule("si-kolayout"),
        indexModule("si-decorators"),
        indexModule("si-kolayout-jsx"),
        indexModule("si-splitlayout"),
        indexModule("si-portal"),
        indexModule("si-friendly"),
        indexModule("si-forms"),
        indexModule("si-logging")
    ]
});

require(["nprogress"], function (NProgress) {
    NProgress.configure({ minimum: 0.1 });
    NProgress.start();
    require([module.config().rootModule]);
});