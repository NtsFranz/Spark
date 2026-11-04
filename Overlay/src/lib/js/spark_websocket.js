import ReconnectingWebSocket from 'reconnecting-websocket';

// Keep retrying forever: overlays are often loaded in OBS before Spark (or the game)
// is running, and a browser source that gave up after a few tries never recovers.
const options = {
    connectionTimeout: 2000,
    maxRetries: Infinity,
    minReconnectionDelay: 500,
    maxReconnectionDelay: 3000,
    reconnectionDelayGrowFactor: 1.5,
};

const DEFAULT_URL = 'ws://127.0.0.1:6725';

export class SparkWebsocket {
    ws;
    callbacks = {};
    initialized = false;

    constructor(url = DEFAULT_URL) {
        this.ws = new ReconnectingWebSocket(url, [], options);
        this.ws.addEventListener('message', event => this.processMessage(event));
        this.ws.addEventListener('open', () => {
            this.initialized = true;
            this.connect();
        });
        this.ws.addEventListener('close', () => {
            this.initialized = false;
        });
    }

    close() {
        this.initialized = false;
        this.ws?.close();
    }

    subscribe(eventType, callback) {
        if (!callback) return;
        const isNew = !this.callbacks[eventType] || this.callbacks[eventType].length === 0;
        (this.callbacks[eventType] ??= []).push(callback);
        if (this.initialized && isNew) {
            this.send('subscribe:' + eventType);
        }
    }

    unsubscribe(eventType, callback) {
        const list = this.callbacks[eventType] ?? [];
        this.callbacks[eventType] = callback == null ? [] : list.filter(c => c !== callback);
        if (this.initialized && this.callbacks[eventType].length === 0) {
            this.send('unsubscribe:' + eventType);
        }
    }

    send(msg) {
        try {
            this.ws.send(msg);
        } catch (e) {
            // socket not open yet; connect() resubscribes on the next open
        }
    }

    connect() {
        for (const [key, list] of Object.entries(this.callbacks)) {
            if (list.length > 0) this.send('subscribe:' + key);
        }
    }

    processMessage(event) {
        if (typeof event.data !== 'string') return;

        // Messages are "<event_type>:<json>". Split on the first colon only: the payload
        // is raw game API JSON, which can span multiple lines and contains colons itself.
        const idx = event.data.indexOf(':');
        if (idx <= 0) return;
        const type = event.data.slice(0, idx);
        const body = event.data.slice(idx + 1);

        if (type === 'subscribe' || type === 'unsubscribe') return;
        if (type === 'failed') {
            console.warn('Spark websocket rejected: ' + body);
            return;
        }

        const list = this.callbacks[type];
        if (!list || list.length === 0) return;

        let data;
        try {
            data = JSON.parse(body);
        } catch (e) {
            console.warn('Spark websocket: bad JSON for ' + type, e);
            return;
        }
        for (const c of list) {
            try {
                c(data);
            } catch (e) {
                console.error('Spark websocket callback error (' + type + ')', e);
            }
        }
    }
}
