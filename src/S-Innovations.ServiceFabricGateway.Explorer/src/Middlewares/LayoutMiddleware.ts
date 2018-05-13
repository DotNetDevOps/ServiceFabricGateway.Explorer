
import { Middleware, AppFunc } from "si-appbuilder";
import * as module from "module";
//import { AppContext, AppMiddleware } from "../index";

 

export async function LayoutMiddleware(ctx: any, next: any) {
    console.log(ctx);
    console.log(next);
    console.log(module);
    try {
        let m = ctx.authorization && ctx.authorization.isSignedIn ? 
            "ServiceFabricGateway/Portal/PortalLayout" : "ServiceFabricGateway/Setup/LoadingLayout";
        console.log(m);
        let es6Module = await import(m);

        ctx.rootLayout = new es6Module.default({ context: ctx });

        return await next(ctx);
    } catch (err) {
        console.log(err);
    }

}