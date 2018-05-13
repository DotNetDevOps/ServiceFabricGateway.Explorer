
import { PortalLayout } from "si-portal";
import { AppContext } from "../index";

import * as ModalTemplate from "template!./templates/ModalTemplate.html";
import "css!./content/Modal.less";
import "css!./content/PortalLayout.less";
import * as ko from "knockout";


export class GatewayPortalLayout extends PortalLayout {

    constructor(options: { context: AppContext}) {
        super({
            context: options.context,
            topbar: {
                title: "ServiceFabric Gateway",
            },
            explorer: {
                allResourcesText: "All resources",
                collapsed:false
            }
        })

        window.onbeforeunload = () => {

            window.sessionStorage.setItem("_deck_queue", JSON.stringify( this.q.map(q => q.hash)));

        };
        let a = window.sessionStorage.getItem("_deck_queue");
        if (a) {
            this.q = JSON.parse(a).map(h => ({hash:h}));
        }
    }

    afterRender(nodes: Node[]) {
        


        return super.afterRender(nodes);
    }
    destroyModal() {
       
        ko.cleanNode(document.querySelector("#modal-container"));
        document.querySelector("#modal-container").remove();
    }
    activateModal(model) {

        let n = document.createElement("DIV");
        n.innerHTML = ko["templates"][ModalTemplate];
        let portal = document.querySelector(".si-portal-main");
        portal.insertBefore(n.firstChild, portal.firstChild);

        document.body.classList.add("modal-active");
        document.querySelector("#modal-container").classList.add("one");

        ko.applyBindings(model, document.querySelector("#modal-container"));
    }
    q = [];

    async processRequest(context: { hash: string, newHash: string; oldHash: string }) {
        
        await this.layoutOptions.context.serviceCollection.addSingleton("RequestContext", context);

        if (context.hash.startsWith("#/deck/")) {
              
            let part = context.hash.substr("#/deck".length);
            console.log("PROCESS");
            if (this.deck.item) {
                this.q.push({ item: this.deck.item, hash: context.oldHash });
            }

            let initialize = true;
            if (this.q.length && this.q[this.q.length - 1].hash === context.newHash ) {
                this.deck.item = this.q.pop().item;
                initialize = this.deck.item === undefined;
            }   
           
            if (initialize)
            {

                if (part.startsWith("/list-resources")) {


                    let { ListGatewayDeckLayout } = await import("./Providers/ServiceFabricGateway.Gateways/ListGatewayDeckLayout");
                    this.deck.item = new ListGatewayDeckLayout();

                }

                if (part.startsWith("/create-new")) {
                    console.log("create new");
                    {
                        let { CreateNewLayout } = await import("./Decks/CreateNewLayout");

                     

                        if (this.deck.item instanceof CreateNewLayout) {
                            this.deck.item.processRequest(context);
                        } else {
                            let test = this.deck.item = new CreateNewLayout();
                            test.processRequest(context);

                        }
                       
                    }

                }

                if (part.startsWith("/providers")) {
                    try {
                        let providerPath = part.substr("/providers/".length);
                        let provider = providerPath.substr(0, providerPath.indexOf('/'));
                        let resourceTypePath = providerPath.substr(provider.length + 1);
                        let resourceType = resourceTypePath.substr(0, resourceTypePath.indexOf('/'));
                        console.log(`Loading ${providerPath} ${provider} ${resourceType}`);
                        let module = await import(`./Providers/${provider}/${resourceType}Layout`);
                        let layout = module[`${resourceType}Layout`] || module.default;
                        console.log(layout);
                        this.deck.item = undefined;
                        setTimeout(() => {
                            this.deck.item = new layout(context);
                        }, 100);
                    } catch (err) {
                        console.log(err);
                    }
                }
            }

        } else {
         

            this.deck.item = undefined;
            if (this.q.length) {
                location.hash = this.q[this.q.length - 1].hash;
            }
        }
    }
}
export default GatewayPortalLayout;