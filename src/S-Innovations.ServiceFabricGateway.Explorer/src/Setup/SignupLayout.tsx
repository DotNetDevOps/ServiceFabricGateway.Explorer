

import { KnockoutJsxFactory, valueBinding, ValueUpdate, ValueAllowUnset, Binding, JSXLayout } from "si-kolayout-jsx"

import { observable, subscribe, Observable, subscribeOnce } from "si-decorators";
import { AppContext } from "../index";

import * as ko from "knockout";

import "css!flexboxgrid/flexboxgrid.min.css";
 
import { defaults } from "si-decorators";
import { KoLayout } from "si-kolayout";
import { Friend } from "si-friendly";


import { InputLayout, InputAttributes, InputAttributeInternal, ValidationResponse, PasswordInputLayout, EmailInputLayout, TextInputLayout } from "si-forms";
import { ioc } from "si-dependency-injection";
import { AppContextAuthorizationFailed } from "si-appbuilder-oidcmiddleware";


function isDefined(obj: any) {
    return !(typeof obj === "undefined" || obj === null);
}






export interface SignupLayoutOptions {
    context: AppContext;
}



export class SignupLayout extends JSXLayout<{ context: AppContext, key?: string }> {


    @observable get supportAzureAd() {
        return ioc("AppContext").authorization.providers.filter(p => p === "azuread").length > 0;
    }

    childLayouts: {
        email: EmailInputLayout,
        first_name: TextInputLayout,
        last_name: TextInputLayout,
        login_email: EmailInputLayout,
        login_password: PasswordInputLayout,
        password: PasswordInputLayout
    }

    @observable get hasInputs() {
        return this.childLayouts.email.fieldState.hasValue
            || this.childLayouts.first_name.fieldState.hasValue
            || this.childLayouts.last_name.fieldState.hasValue
            || this.childLayouts.password.fieldState.hasValue;
    }

    afterRender(elements: HTMLElement[]) {

        for (let link of Array.prototype.slice.call(elements[0].querySelectorAll(".ripplelink")) as HTMLButtonElement[]) {
            link.addEventListener("mousedown", (evt) => {
                if (link.querySelectorAll(".ink").length === 0) {
                    link.appendChild(<span class="ink"></span> as HTMLElement);

                }

                let ink = link.querySelector(".ink") as HTMLElement;

                ink.classList.remove("animate");
                setTimeout(() => {
                    //   let bb = ink.getBoundingClientRect();
                    // console.log(bb.width);
                    console.log(ink.clientWidth);

                    if (!ink.clientHeight && !ink.clientWidth) {
                        let d = Math.min(link.offsetWidth, link.offsetHeight, 100);
                        console.log(d);
                        ink.style.height = d + "px";
                        ink.style.width = d + "px";

                    }

                    let offset = link.getBoundingClientRect();
                    //bb = ink.getBoundingClientRect();
                    console.log([evt.pageX, offset.left, ink.clientWidth / 2]);
                    let x = evt.pageX - offset.left - ink.clientWidth / 2;
                    let y = evt.pageY - offset.top - ink.clientHeight / 2;


                    ink.style.top = y + 'px';
                    ink.style.left = x + 'px';
                    ink.classList.add("animate");
                });

                return false;
            });

        }

        super.afterRender(...Array.prototype.slice.call(arguments));
    }


    
    async loginAzureAd() {
        await this.attributes.context.userManager.signinRedirect({ acr_values: "idp:AAD" });
    
    }
    async login() {
        let user = await fetch(`${ioc("AppContext").oidcOptions.authority}authenticate`,
            {
                credentials: "include",
                headers: new Headers({
                    "Content-Type": "application/json"
                }),
                body: JSON.stringify({
                    userName: this.childLayouts.login_email.fieldState.value,
                    password: this.childLayouts.login_password.fieldState.value
                }), method: "POST"
            });
        if (user.ok) {
            console.log(user);
            let user1 = await this.attributes.context.userManager.signinSilent();
            console.log(user1);
            window.location.reload();
        }

    }
    async createAccount() {
        

        let ok = await fetch(ioc("AppContext").oidcOptions.authority + `authentications/local-users`, {
            method: "PUT",
            body: JSON.stringify({
                firstName: this.childLayouts.first_name.fieldState.value,
                lastName: this.childLayouts.last_name.fieldState.value,
                email: this.childLayouts.email.fieldState.value,
                password: this.childLayouts.password.fieldState.value
            }),
            headers: {
                'Accept': 'application/json, text/plain, */*',
                'Content-Type': 'application/json'
            }
        }).then(rsp => rsp.ok);
        console.log(ok);
        if (ok) {
            let user = await fetch(`${ioc("AppContext").oidcOptions.authority}authenticate`,
                {
                    credentials: "include",
                    headers: new Headers({
                        "Content-Type": "application/json"
                    }),
                    body: JSON.stringify({
                        userName: this.childLayouts.email.fieldState.value,
                        password: this.childLayouts.password.fieldState.value
                    }), method: "POST"
                });
            if (user.ok) {
                console.log(user);
                let user1 = await this.attributes.context.userManager.signinSilent();
                console.log(user1);
                window.location.reload();
            }
        }
    }



    //toggleLogin(model, event: MouseEvent) {
    //    this.mode = "login";

    //}
    //toggleSignup() {
    //    this.mode = "signup";
    //}

    @observable get mode() {
        console.log(this.attributes);
        if (!this.attributes.context.authorization.isSignedIn) {
            let auth = this.attributes.context.authorization as AppContextAuthorizationFailed;
            if (auth.providers) {
                if (auth.providers.length) {
                    return "login";
                }
            }
        }

        return "signup";
    }
    @observable get headline() {
        return this.mode === "signup" ? "Create an account" : "Welcome back!"
    }

    friend = new Friend({ coverEyes: () => this.childLayouts.password.hasFocus || this.childLayouts.login_password.hasFocus, lookAt: [this.childLayouts.email, this.childLayouts.first_name, this.childLayouts.last_name, this.childLayouts.login_email] });

    constructor(attributes: SignupLayoutOptions) {
        super(attributes, (
            <section class="signup-form flex flex-column">
                
                <form class="space-bottom1 flex-item flex flex-column">
                    <ko if="mode === 'signup'" class="flex-fade flex-column-fade flex-item-fade">
                        <div class="row center-xs">
                            <ko layout="friend" />
                        </div>
                        <div class="row">
                            <div class="col-sm-6">
                                <TextInputLayout name="first_name" autoComplete="given-name" label="First Name" />

                            </div>
                            <div class="col-sm-6">
                                <TextInputLayout name="last_name" autoComplete="family-name" label="Last Name" />
                            </div>
                        </div>
                        <div class="row">
                            <div class="col-sm-12">
                                <EmailInputLayout name="email" label="Email" type="text" />
                            </div>
                        </div>
                        <div class="row">
                            <div class="col-sm-12">
                                <PasswordInputLayout name="password" label="Password" />
                            </div>
                        </div>
                        <div class="row flex-bottom">
                            <div class="col-sm-12">
                                <button class="btn btn-primary btn-block ripplelink shadow bolder uppercase" data-bind="click:createAccount">Create Account</button>
                            </div>
                        </div>


                    </ko>
                    <ko if="mode === 'login'" class="flex-fade flex-column-fade flex-item-fade">
                        <div class="row center-xs">
                            <div class="col-xs">
                                <ko layout="friend" />
                            </div>
                        </div>
                        <div class="row">
                            <div class="col-sm-12">
                                <EmailInputLayout name="email" label="Email" key="login_email" />
                            </div>
                        </div>
                        <div class="row">
                            <div class="col-sm-12">
                                <PasswordInputLayout name="password" label="Password" key="login_password" />
                            </div>
                        </div>
                        <div class="row end-xs">
                            <div class="col-sm-6 ">
                                <p class="forgot"><a href="#">Forgot Password?</a></p>
                            </div>
                        </div>

                        <div class="row flex-bottom">
                            <div class="col-sm-12 space-bottom1">
                                <button class="btn btn-primary btn-block ripplelink shadow bolder uppercase" data-bind="click:login" >Log in</button>
                            </div>
                            
                            <div class="col-sm-12" data-bind="if:supportAzureAd">
                                <button class="btn btn-primary btn-block ripplelink shadow bolder uppercase" data-bind="click:loginAzureAd" >Login with Azure AD</button>
                            </div>
                          
                        </div>
                    </ko>
                </form>

            </section>
        ));


    }
}