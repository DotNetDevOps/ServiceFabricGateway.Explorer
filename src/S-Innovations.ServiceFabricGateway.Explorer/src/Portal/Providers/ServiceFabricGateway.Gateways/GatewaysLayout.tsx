

import { SiDeck, PortalLayout } from "si-portal";
import { defaults, Factory, observable } from "si-decorators";
import { KnockoutJsxFactory } from "si-kolayout-jsx";
import { JSXLayout } from "si-kolayout-jsx";
import { KoLayout, IKoLayout, KnockoutTemplateBindingHandlerOptions } from "si-kolayout";
import { ioc } from "si-dependency-injection";

import * as ko from "knockout";

export let { SiDeckItemLayout, SiDeckItemContentLayout } = SiDeck;

import * as    GatewayContentLayoutTemplateId from "template!./templates/GatewayContentLayoutTemplate.html";

import "css!./content/GatewayContentLayout.less";






export class GatewayContentLayout extends KoLayout {
    @observable lastUpdated;
    @observable reverseProxyLocation;
    @observable serviceVersion;
    @observable serviceName;
    @observable nodeCount;
    @observable serverName;
    constructor(proxies) {
        super({
            name: GatewayContentLayoutTemplateId
        })

        this.reverseProxyLocation = proxies[0].ReverseProxyLocation;
        this.lastUpdated                                                =   proxies[0].Time;
        this.serviceVersion = proxies[0].ServiceVersion;
        this.serviceName = proxies[0].ServiceName;
        this.serverName = proxies[0].ServerName;
        this.nodeCount = proxies.length;
    }


}



export interface GatewaysLayoutOptions {
    app: PortalLayout,
    hash: string;
}

const GatewaysLayoutDefaults = {
    app: () => ioc("PortalLayout")
} as Factory<GatewaysLayoutOptions>;

@defaults(GatewaysLayoutDefaults)
export class GatewaysLayout extends SiDeckItemLayout {

    private resourceName = "";
    constructor(options?: GatewaysLayoutOptions) {
        super({ layout: options.app.deck, titleOptions: { title: options.hash.split('/').pop(), subtitle: "Thanks for using Fabric Gateway by pksorensen" } });

        this.isMaximized = true;
        this.resourceName = options.hash.split('/').pop();
                                                    
       
        //this.contentLayouts.push(new test());
        //this.contentLayouts.push(new test());
        //this.contentLayouts.push(new test());
        //this.contentLayouts.push(new test());
        //this.contentLayouts.push(new test());
        //this.contentLayouts.push(new test());
        //this.contentLayouts.push(new test());
    }

    async rendered(elements: HTMLElement[]) {
        await super.rendered(elements);

        let gatewayEndtries = await fetch(ioc("AppContext").endpoints.resourceApiEndpoint + "/gateway/services", {
            method: "GET",
        }).then(rsp => rsp.json());

        let proxies = {};
        gatewayEndtries.forEach(gw => (proxies[gw.Key] = proxies[gw.Key] || []).push(gw));


        console.log();
        this.contentLayouts.push(new GatewayContentLayout(proxies[this.resourceName]));
    }

}

export default GatewaysLayout;