
import { KoLayout } from "si-kolayout";
import * as         CreateApplicationLayoutTemplate from "template!./templates/CreateApplicationLayout.html";
import "css!./content/CreateApplicationLayout.less";
import * as monaco from "monaco-editor";
import { ioc } from "si-dependency-injection";

const ra = [
    {
        "resourceAppId": "00000002-0000-0000-c000-000000000000",
        "resourceAccess": [
            {
                "id": "311a71cc-e848-46a1-bdf8-97ff7156d8e6",
                "type": "Scope"
            }
        ]
    }
];

export class CodePasteLayout {

    afterRender(n) {
      
    }
}

export class CreateApplicationLayout extends KoLayout {

    displayName = location.host;
    homepage = "https://" + location.host + "/explorer/";
    replyUrls = "https://" + location.host + "/identity/aad-signin-oidc";
    manifest = JSON.stringify(ra).replace(/"/g, '\\\"');
   

    editor: monaco.editor.IStandaloneCodeEditor;

    actionBar = {
        visible: true,
        disabled: false,
        primary: true,
        tabIndex: 0,
        text: "Create",
        onClick: this.create.bind(this)
    }

    constructor() {
        super({
            name: CreateApplicationLayoutTemplate,
            afterRender: (n) => this.afterRender(n)
        });
    }

    async create(model, event) {
        let ok = await fetch(ioc("AppContext").oidcOptions.authority + `authentications/azure-active-directory`, {
            method: "PUT",
            body: this.editor.getModel().getValue(),
            headers: {
                'Accept': 'application/json, text/plain, */*',
                'Content-Type': 'application/json',
                authorization: "Bearer " + ioc("AuthorizationManager").user.access_token
            }
        }).then(rsp => rsp.ok);
        console.log(ok);

        if (ok) {

            await ioc("UserManager").signoutRedirect({
                post_logout_redirect_uri: location.protocol + "//" + location.host + location.pathname,
                id_token_hint: ioc("AuthorizationManager").user.id_token
            });
        }


    }

    afterRender(n) {
        var model = monaco.editor.createModel("", "json");

        this.editor = monaco.editor.create(document.getElementById("aplication-paste-editor"), {
            model: model,
            readOnly: false,
            codeLens: false, 
            minimap: {
                enabled: false
            },
            //   theme: "vs-dark",
            roundedSelection: false,
            scrollBeyondLastLine: false,
        });
    }
}

export default CreateApplicationLayout;