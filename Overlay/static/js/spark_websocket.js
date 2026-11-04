'use strict';

// Used by the legacy static overlays (minimap.html, full_overlay.js, ...).
class SparkWebsocket {
    ws;
    callbacks = {};
    initialized = false;

    constructor(url) {
        this.ws = new ReconnectingWebSocket(url || "ws://127.0.0.1:6725", null, {
            reconnectInterval: 500,
            maxReconnectInterval: 3000,
            timeoutInterval: 2000
        });
        this.ws.addEventListener("message", event => this.processMessage(event));
        this.ws.addEventListener("open", () => {
            this.initialized = true;
            this.connect();
        });
        this.ws.addEventListener("close", () => {
            this.initialized = false;
        });
    }

    close() {
        this.initialized = false;
        if (this.ws) this.ws.close();
    }

    subscribe(eventType, callback) {
        if (!callback) return;
        if (!this.callbacks[eventType]) this.callbacks[eventType] = [];
        const isNew = this.callbacks[eventType].length === 0;
        this.callbacks[eventType].push(callback);
        if (this.initialized && isNew) this.send("subscribe:" + eventType);
    }

    unsubscribe(eventType, callback) {
        const list = this.callbacks[eventType] || [];
        this.callbacks[eventType] = callback == null ? [] : list.filter(c => c !== callback);
        if (this.initialized && this.callbacks[eventType].length === 0) this.send("unsubscribe:" + eventType);
    }

    send(msg) {
        try {
            this.ws.send(msg);
        } catch (e) {
            // not connected yet; connect() resubscribes on open
        }
    }

    connect() {
        for (const [key, list] of Object.entries(this.callbacks)) {
            if (list.length > 0) this.send("subscribe:" + key);
        }
    }

    processMessage(event) {
        if (typeof event.data !== "string") return;
        // "<event_type>:<json>" - split on the first colon only; the JSON may span lines.
        const idx = event.data.indexOf(":");
        if (idx <= 0) return;
        const type = event.data.slice(0, idx);
        const body = event.data.slice(idx + 1);
        if (type === "subscribe" || type === "unsubscribe") return;
        if (type === "failed") {
            console.warn("Spark websocket rejected: " + body);
            return;
        }
        const list = this.callbacks[type];
        if (!list || list.length === 0) return;
        let data;
        try {
            data = JSON.parse(body);
        } catch (e) {
            console.warn("Spark websocket: bad JSON for " + type, e);
            return;
        }
        list.forEach(c => {
            try {
                c(data);
            } catch (e) {
                console.error("Spark websocket callback error (" + type + ")", e);
            }
        });
    }
}


// RECONNECTING WEBSOCKET
// https://github.com/joewalnes/reconnecting-websocket
!function (a, b) {
    "function" == typeof define && define.amd ? define([], b) : "undefined" != typeof module && module.exports ? module.exports = b() : a.ReconnectingWebSocket = b()
}(this, function () {
    function a(b, c, d) {
        function l(a, b) {
            var c = document.createEvent("CustomEvent");
            return c.initCustomEvent(a, !1, !1, b), c
        }

        var e = {
            debug: !1,
            automaticOpen: !0,
            reconnectInterval: 1e3,
            maxReconnectInterval: 3e4,
            reconnectDecay: 1.5,
            timeoutInterval: 2e3
        };
        d || (d = {});
        for (var f in e) this[f] = "undefined" != typeof d[f] ? d[f] : e[f];
        this.url = b, this.reconnectAttempts = 0, this.readyState = WebSocket.CONNECTING, this.protocol = null;
        var h, g = this, i = !1, j = !1, k = document.createElement("div");
        k.addEventListener("open", function (a) {
            g.onopen(a)
        }), k.addEventListener("close", function (a) {
            g.onclose(a)
        }), k.addEventListener("connecting", function (a) {
            g.onconnecting(a)
        }), k.addEventListener("message", function (a) {
            g.onmessage(a)
        }), k.addEventListener("error", function (a) {
            g.onerror(a)
        }), this.addEventListener = k.addEventListener.bind(k), this.removeEventListener = k.removeEventListener.bind(k), this.dispatchEvent = k.dispatchEvent.bind(k), this.open = function (b) {
            h = new WebSocket(g.url, c || []), b || k.dispatchEvent(l("connecting")), (g.debug || a.debugAll) && console.debug("ReconnectingWebSocket", "attempt-connect", g.url);
            var d = h, e = setTimeout(function () {
                (g.debug || a.debugAll) && console.debug("ReconnectingWebSocket", "connection-timeout", g.url), j = !0, d.close(), j = !1
            }, g.timeoutInterval);
            h.onopen = function () {
                clearTimeout(e), (g.debug || a.debugAll) && console.debug("ReconnectingWebSocket", "onopen", g.url), g.protocol = h.protocol, g.readyState = WebSocket.OPEN, g.reconnectAttempts = 0;
                var d = l("open");
                d.isReconnect = b, b = !1, k.dispatchEvent(d)
            }, h.onclose = function (c) {
                if (clearTimeout(e), h = null, i) g.readyState = WebSocket.CLOSED, k.dispatchEvent(l("close")); else {
                    g.readyState = WebSocket.CONNECTING;
                    var d = l("connecting");
                    d.code = c.code, d.reason = c.reason, d.wasClean = c.wasClean, k.dispatchEvent(d), b || j || ((g.debug || a.debugAll) && console.debug("ReconnectingWebSocket", "onclose", g.url), k.dispatchEvent(l("close")));
                    var e = g.reconnectInterval * Math.pow(g.reconnectDecay, g.reconnectAttempts);
                    setTimeout(function () {
                        g.reconnectAttempts++, g.open(!0)
                    }, e > g.maxReconnectInterval ? g.maxReconnectInterval : e)
                }
            }, h.onmessage = function (b) {
                (g.debug || a.debugAll) && console.debug("ReconnectingWebSocket", "onmessage", g.url, b.data);
                var c = l("message");
                c.data = b.data, k.dispatchEvent(c)
            }, h.onerror = function (b) {
                (g.debug || a.debugAll) && console.debug("ReconnectingWebSocket", "onerror", g.url, b), k.dispatchEvent(l("error"))
            }
        }, 1 == this.automaticOpen && this.open(!1), this.send = function (b) {
            if (h) return (g.debug || a.debugAll) && console.debug("ReconnectingWebSocket", "send", g.url, b), h.send(b);
            throw"INVALID_STATE_ERR : Pausing to reconnect websocket"
        }, this.close = function (a, b) {
            "undefined" == typeof a && (a = 1e3), i = !0, h && h.close(a, b)
        }, this.refresh = function () {
            h && h.close()
        }
    }

    return a.prototype.onopen = function () {
    }, a.prototype.onclose = function () {
    }, a.prototype.onconnecting = function () {
    }, a.prototype.onmessage = function () {
    }, a.prototype.onerror = function () {
    }, a.debugAll = !1, a.CONNECTING = WebSocket.CONNECTING, a.OPEN = WebSocket.OPEN, a.CLOSING = WebSocket.CLOSING, a.CLOSED = WebSocket.CLOSED, a
});