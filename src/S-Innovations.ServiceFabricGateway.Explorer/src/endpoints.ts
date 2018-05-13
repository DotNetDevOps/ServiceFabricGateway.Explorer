import * as module from "module";

console.log(module.id);       
export interface EndpointsConfiguration {
    storageServiceEndpoint: string;
    resourceApiEndpoint: string;
}

export const endpoints = module.config() as EndpointsConfiguration;
console.log(endpoints);
