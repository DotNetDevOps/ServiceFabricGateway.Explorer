
declare module "template!*" {
    let b: string;
    export =b;
}

declare module "animejs";


interface DraggabilityOptions {
    grid?: number[];
    handle?: string;
}
 
declare module "draggabilly" {

    class Draggabilly {
        position: {
            x: number; y: number
        }

        constructor(el: HTMLElement, opt: DraggabilityOptions);
        on(evt: string, listener: Function, that?: any): void;
        off(evt: string, listener: Function): void;
        once(evt: string, listener: Function): void;

        enable(): void;
        disable(): void;
        _dragStart: any;
        dragStart(): void;

    }

    export = Draggabilly
}
