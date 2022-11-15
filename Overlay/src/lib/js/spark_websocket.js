import ReconnectingWebSocket from 'reconnecting-websocket';
import {WebSocket} from "ws";

const options = {
    WebSocket: WebSocket, // custom WebSocket constructor
    connectionTimeout: 1000,
    maxRetries: 10,
};

export class SparkWebsocket {
    ws;
    callbacks = {};
    initialized = false;

    constructor() {
        this.ws = new ReconnectingWebSocket("ws://127.0.0.1:6725", [], options);
        this.ws.addEventListener("message", event => {
            this.processMessage(event);
        });
        this.ws.addEventListener("open", event => {
            this.connect(event);
            this.initialized = true;
        });
    }

    close() {
        if (this) {
            this.ws.close();
            this.initialized = false;
        }
    }

    subscribe(eventType, callback) {
        if (callback === null) return;
        if (this.callbacks[eventType] === undefined) {
            this.callbacks[eventType] = [];
        }
        this.callbacks[eventType].push(callback)
        if (this.initialized) {
            this.ws.send("subscribe:" + eventType);
        }
    }

    unsubscribe(eventType, callback) {
        if (this.callbacks[eventType] === undefined) {
            this.callbacks[eventType] = [];
        }
        if (callback == null) {
            this.callbacks[eventType] = [];
        } else {
            this.callbacks[eventType].pop(callback)
        }
        if (this.initialized) {
            this.ws.send("unsubscribe:" + eventType);
        }
    }

    connect(event) {
        for (const [key, value] of Object.entries(this.callbacks)) {
            this.ws.send("subscribe:" + key)
        }
    }

    processMessage(event) {
        // split the message header from the rest of the json
        let parts = event.data.split(/:(.+)/)

        // if the message didn't have a header properly
        if (parts.length !== 3) return;

        if (parts[0] === "subscribe") {
            console.log("Subscribed: " + parts[1]);
        }

        if (this.callbacks[parts[0]] !== undefined) {
            this.callbacks[parts[0]].forEach(c => c(JSON.parse(parts[1])));
        }
    }
}