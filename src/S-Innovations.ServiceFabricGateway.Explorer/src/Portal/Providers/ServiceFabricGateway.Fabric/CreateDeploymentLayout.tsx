
import { ioc } from "si-dependency-injection";
import { Factory, defaults, observable } from "si-decorators";
import { generateUUID } from "si-dependency-injection";
import { KoLayout } from "si-kolayout";
import { TextInputLayout } from "si-forms";

import GatewayPortalLayout from "../../PortalLayout";

import * as monaco from "monaco-editor";
import "css!./content/CreateDeploymentLayout.less";
import * as CreateDeploymentLayoutTemplate from "template!./templates/CreateDeploymentLayoutTemplate.html";


const singleComment = 1;
const multiComment = 2;
const stripWithoutWhitespace = (str?) => '';
const stripWithWhitespace = (str, start?, end?) => str.slice(start, end).replace(/\S/g, ' ');

function stripcomments(str, opts: { whitespace?: boolean } = {}) {


    const strip = opts.whitespace === false ? stripWithoutWhitespace : stripWithWhitespace;

    let insideString = false;
    let insideComment = 0;
    let offset = 0;
    let ret = '';

    for (let i = 0; i < str.length; i++) {
        const currentChar = str[i];
        const nextChar = str[i + 1];

        if (!insideComment && currentChar === '"') {
            const escaped = str[i - 1] === '\\' && str[i - 2] !== '\\';
            if (!escaped) {
                insideString = !insideString;
            }
        }

        if (insideString) {
            continue;
        }

        if (!insideComment && currentChar + nextChar === '//') {
            ret += str.slice(offset, i);
            offset = i;
            insideComment = singleComment;
            i++;
        } else if (insideComment === singleComment && currentChar + nextChar === '\r\n') {
            i++;
            insideComment = 0;
            ret += strip(str, offset, i);
            offset = i;
            continue;
        } else if (insideComment === singleComment && currentChar === '\n') {
            insideComment = 0;
            ret += strip(str, offset, i);
            offset = i;
        } else if (!insideComment && currentChar + nextChar === '/*') {
            ret += str.slice(offset, i);
            offset = i;
            insideComment = multiComment;
            i++;
            continue;
        } else if (insideComment === multiComment && currentChar + nextChar === '*/') {
            i++;
            insideComment = 0;
            ret += strip(str, offset, i + 1);
            offset = i + 1;
            continue;
        }
    }

    return ret + (insideComment ? strip(str.substr(offset)) : str.substr(offset));
};


export function throttle(callback: Function, limit: number, skip = false) {
    var wait = false;                  // Initially, we're not waiting
    let run1 = skip;
    return function () {               // We return a throttled function
        if (!wait) {                   // If we're not waiting
            if (!skip)
                callback.apply(null, arguments);           // Execute users function
            skip = false;
            wait = true;               // Prevent future invocations
            setTimeout(function () {   // After a period of time
                wait = false;          // And allow future invocations  
                if (run1) {
                    run1 = false;
                    callback.apply(null, arguments);
                }
            }, limit);
        } else {
            run1 = true;
        }
    }
}


const obj = {
    "remoteUrl": "https://cdn.earthml.com/sfapps/DotNETDevOps.ApplicationType/CI/latest.sfpkg",
    "DeleteIfExists": true,
    "ApplicationName": "DotNETDevOps.Application",
    "Parameters": {

    },
    "ServiceDeployments": [
        {
            "ServiceTypeName": "DotNETDevOps.Web.ServiceType",
            "ServiceName": "DotNETDevOps.Web.Service"
        }]

}


const EditorLayoutDefaults = {
    id: () => "_" + generateUUID().replace(/-/g, ""),
}

function removeType(str: string) {
    if (str.endsWith("Type"))
        return str.substr(0, str.length - 4);

    return str;
}

@defaults(EditorLayoutDefaults, true)
export class CreateDeploymentLayout extends KoLayout {
    @observable id: string = null;

    get portal() { return ioc("PortalLayout") as GatewayPortalLayout; }
    remoteUrl = new TextInputLayout({ label: "remoteUrl" });
    async ok(model, event: Event) {


        let metadata = await fetch(ioc("AppContext").endpoints.resourceApiEndpoint + `/providers/ServiceFabricGateway.Fabric/metadata?remoteUrl=${encodeURIComponent(this.remoteUrl.fieldState.value)}`, {
            method: "POST",
            headers: {
                authorization: "Bearer " + ioc("AuthorizationManager").user.access_token
            }
        }).then(rsp => rsp.json());

        console.log(metadata);
        this.editor.getModel().setValue(
            JSON.stringify({
                remoteUrl: this.remoteUrl.fieldState.value,
                deleteIfExists: false,
                applicationName: removeType(metadata.applicationTypeName),
                applicationTypeName: metadata.applicationTypeName,
                applicationTypeVersion: metadata.applicationTypeVersion,
                parameters: metadata.parameters,
                serviceDeployments: metadata.services
            }, null, 4)
        );
        this.portal.destroyModal();
    }
    cancel(model, event: Event) {
        console.log(arguments);
        this.portal.destroyModal();
    }

    actionBar = {
        visible: true,
        disabled: false,
        primary: true,
        tabIndex: 0,
        text: "Create",
        onClick: this.create.bind(this)
    }

    async create(model, event) {


        let metadata = await fetch(ioc("AppContext").endpoints.resourceApiEndpoint + `/providers/ServiceFabricGateway.Fabric/deployments`, {
            method: "POST",
            body: this.editor.getModel().getValue(),
            headers: {
                'Accept': 'application/json, text/plain, */*',
                'Content-Type': 'application/json',
                authorization: "Bearer " + ioc("AuthorizationManager").user.access_token
            }
        }).then(rsp => rsp.json());
        console.log(metadata);
    }

    editor: monaco.editor.IStandaloneCodeEditor;
    constructor(options = {}) {
        super({ name: CreateDeploymentLayoutTemplate, afterRender: () => this.afterRender() })
    }
    afterRender() {

        this.portal.activateModal(this);



        var model = monaco.editor.createModel(JSON.stringify(obj, null, 4), "json");

        this.editor = monaco.editor.create(document.getElementById(this.id), {
            model: model,
            readOnly: false, codeLens: false,
            //   theme: "vs-dark",
            roundedSelection: false,
            scrollBeyondLastLine: false,
        });

        model.onDidChangeContent(throttle(async (e) => {

            try {

                if (monaco.editor.getModelMarkers({}).length === 0) {
                    let graphElement = JSON.parse(stripcomments(model.getValue()));
                    console.log(graphElement);

                    let remoteUrl = graphElement.remoteUrl;

                    //let metadata = await fetch(ioc("AppContext").endpoints.resourceApiEndpoint + `/providers/ServiceFabricGateway.Fabric/metadata?remoteUrl=${encodeURIComponent(remoteUrl)}`, {
                    //    method: "POST",
                    //    headers: {
                    //        authorization: "Bearer " + ioc("AuthorizationManager").user.access_token
                    //    }
                    //}).then(rsp => rsp.json());

                  //  console.log(metadata);
                }

            } catch (err) {
                console.log(err);
            }
        }, 1000, true));


        //setInterval(() => {
        //    console.log(monaco.editor.getModelMarkers({}).map(m => m.message).join(', '));
        //}, 2000);

        // var json = model.getValue
    }
}

export default CreateDeploymentLayout;
