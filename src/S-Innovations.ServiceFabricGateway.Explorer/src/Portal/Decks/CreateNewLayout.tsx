
import { SiDeck } from "si-portal";
import * as a from "template!./templates/CreateNewLayoutTemplate.html";
import * as RequestTabLayoutTemplate from "template!./templates/RequestTabLayoutTemplate.html";


import PortalLayout from "../PortalLayout";
import { ioc } from "si-dependency-injection";
import { Factory, defaults, observable } from "si-decorators";
import { KoLayout } from "si-kolayout";



//import "css!./content/RequestTabLayout.less";

import { generateUUID } from "si-dependency-injection";
import GatewayPortalLayout from "../PortalLayout";



export interface CreateNewMapLayoutOptions {
    app: PortalLayout
}

const CreateNewMapLayoutDefaults = {
    //  layout: () => ioc("PortalLayout").deck,
    app: () => ioc("PortalLayout")
} as Factory<CreateNewMapLayoutOptions>;

export class RequestTabLayout extends KoLayout {
    constructor() {
        super({
            name: RequestTabLayoutTemplate
        });
    }
}

const add = '<svg viewBox="0 0 50 50" focusable="false" xmlns:xlink="http://www.w3.org/1999/xlink" xmlns:svg="http://www.w3.org/2000/svg" class="" role="presentation" id="FxSymbol0-02b" width="100%" height="100%" style="fill: white;"><g style=""><title></title><path class="msportalfx-svg-c15" d="M25.001 50.001a4.575 4.575 0 0 1-3.261-1.352L1.351 28.261A4.64 4.64 0 0 1 0 25c0-1.214.492-2.402 1.351-3.26L21.74 1.352A4.578 4.578 0 0 1 25.001 0c1.231 0 2.39.48 3.261 1.352L48.648 21.74A4.565 4.565 0 0 1 50 25a4.574 4.574 0 0 1-1.353 3.263L28.262 48.649a4.578 4.578 0 0 1-3.261 1.352" style="fill: rgb(89, 180, 217);"></path><path class="msportalfx-svg-c01" d="M38.614 21.093a3.91 3.91 0 0 0-3.91 3.909c0 .792.239 1.527.645 2.143l-7.744 7.744a4.55 4.55 0 0 0-.656-.373V14.759c1.167-.676 1.961-1.924 1.961-3.37a3.91 3.91 0 0 0-7.818 0c0 1.446.794 2.694 1.96 3.37v19.756a4.48 4.48 0 0 0-.632.353l-7.753-7.753a3.88 3.88 0 0 0 .628-2.113 3.909 3.909 0 1 0-3.908 3.909 3.88 3.88 0 0 0 1.274-.23l8.15 8.15a4.552 4.552 0 1 0 8.387.032l8.173-8.172c.392.132.804.22 1.241.22a3.909 3.909 0 0 0 .002-7.818z"></path><path class="msportalfx-svg-c01" d="M40.471 24.983l-1.784 1.785L24.065 12.15l1.784-1.784z" opacity=".5"></path><path class="msportalfx-svg-c01" d="M24.166 10.377l1.784 1.785-14.62 14.62-1.785-1.784z" opacity=".5"></path><path class="msportalfx-svg-c13" d="M27.665 38.614a2.71 2.71 0 1 1-5.42-.002 2.71 2.71 0 0 1 5.42.002m-.491-27.225a2.174 2.174 0 1 1-4.347 0 2.174 2.174 0 0 1 4.347 0M13.563 25.001a2.175 2.175 0 1 1-4.35-.002 2.175 2.175 0 0 1 4.35.002m27.225 0a2.175 2.175 0 1 1-4.35-.002 2.175 2.175 0 0 1 4.35.002" style="fill: rgb(184, 212, 50);"></path><path class="msportalfx-svg-c01" d="M28.262 1.352A4.578 4.578 0 0 0 25.001 0c-1.231 0-2.389.48-3.26 1.352L1.352 21.74A4.635 4.635 0 0 0 0 25c0 1.215.492 2.403 1.352 3.261l11.543 11.544L34.61 7.699l-6.348-6.347z" opacity=".1"></path></g></svg>';
const deployment = '<svg viewBox="0 0 50 50" focusable="false" xmlns:xlink="http://www.w3.org/1999/xlink" xmlns:svg="http://www.w3.org/2000/svg" class="svg-sf" role="presentation" aria-hidden="true"><g><path class="msportalfx-svg-c09" d="M36 35.1c-3.8 0-6.9 3.1-6.9 6.9s3.1 6.9 6.9 6.9 6.9-3.1 6.9-6.9-3.1-6.9-6.9-6.9zm6-20c-3.8 0-6.9 3.1-6.9 6.9s3.1 6.9 6.9 6.9 6.9-3.1 6.9-6.9-3.1-6.9-6.9-6.9zm-34 0c-3.8 0-6.9 3.1-6.9 6.9s3.1 6.9 6.9 6.9 6.9-3.1 6.9-6.9-3.1-6.9-6.9-6.9zm6 20c-3.8 0-6.9 3.1-6.9 6.9s3.1 6.9 6.9 6.9 6.9-3.1 6.9-6.9c-.1-3.8-3.2-6.9-6.9-6.9zm11-33c-3.8 0-6.9 3.1-6.9 6.9s3.1 6.9 6.9 6.9 6.9-3.1 6.9-6.9-3.1-6.9-6.9-6.9z"></path><path class="msportalfx-svg-c09" d="M25 10l16 13-5 18H14L9 23l16-13m0-5.5l-20.5 17L11.1 45H39l6.5-23.5L25 4.5z"></path></g></svg>'


const deploymentResourceType = {
    resourceType: "/providers/ServiceFabricGateway.Fabric/deployment",
    title: "Create deployment",
    subtitle: "automated CI/CD flow integrated",
    tag: "No code required",
    icon: deployment,
};
const azureadResourceType = {
    icon: add,
    resourceType: "/providers/ServiceFabricGateway.AzureAd/application", title: "Link to Azure AD", tag: "Security", subtitle: "authenticate using your AD user"
};

export class test extends KoLayout {


    resourceTypes = ioc("AppContext").authorization.providers.filter(p => p === "azuread").length ?
        [deploymentResourceType] : [
            deploymentResourceType,
            azureadResourceType
        ]


    constructor() {
        super({ name: a })
    }

    createNewTable(mode: test, event: MouseEvent) {

    }

    createNew(obj: { resourceType: string }, event: MouseEvent) {
        console.log(arguments);
        location.hash = `/deck/create-new${obj.resourceType}`;
    }
}




@defaults(CreateNewMapLayoutDefaults)
export class CreateNewLayout extends SiDeck.SiDeckItemLayout {

    constructor(options?: CreateNewMapLayoutOptions) {
        super({ layout: options.app.deck, titleOptions: { title: "Create new resource", subtitle: "Thanks for using ServiceFabric Gateway by pksorensen" } });

        this.isMaximized = false;

        this.contentLayouts.push(new SiDeck.SiDeckItemContentLayout({ content: new test() }));
        //this.contentLayouts.push(new CreateNewMapContentLayout());
        //this.contentLayouts.push(new test());
        //this.contentLayouts.push(new test());
        //this.contentLayouts.push(new test());
        //this.contentLayouts.push(new test());
        //this.contentLayouts.push(new test());
        //this.contentLayouts.push(new test());
        //this.contentLayouts.push(new test());
    }


    async processRequest(context: { hash: string, newHash: string; oldHash: string }) {
        //if (context.newHash.endsWith("deployment")) {
        //    this.isMaximized = true;
        //    this.contentLayouts.push(new SiDeck.SiDeckItemContentLayout({ content: new CreateDeploymentLayout() }));
        //} 
        if (context.newHash.startsWith("#/deck/create-new/providers/")) {
            let providerHash = context.newHash.substr("#/deck/create-new/providers/".length);
            console.log(providerHash);
            let namespace = providerHash.split('/').shift();
            let resourceType = providerHash.substr(namespace.length + 1);
            resourceType = resourceType.substr(0, 1).toUpperCase() + resourceType.substr(1);

            let path = `../Providers/${namespace}/Create${resourceType}Layout`;

            let module = await import(path);

            this.isMaximized = true;
            this.contentLayouts.splice(1);
            this.contentLayouts.push(new SiDeck.SiDeckItemContentLayout({ content: new module.default() }));
        }


    }

}

export default CreateNewLayout;