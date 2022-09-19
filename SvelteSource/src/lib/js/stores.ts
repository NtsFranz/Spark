import {readable, derived, writable} from 'svelte/store';
import {empty_frame, empty_last_score} from "./empty_frame";
import {SparkWebsocket} from './spark_websocket.js';


export const frame = readable(empty_frame, function start(set) {
    let sw = new SparkWebsocket();
    sw.subscribe("frame_10hz", data => {
        if (data) {
            clockRunning = gameClock - data['game_clock'] > .001;
            lastClock = data['game_clock'];
            gameClock = data['game_clock'];
        }
        set(data);
    });

    return function stop() {
        sw.close();
    }
});

export const mathematical_time = derived(frame, $frame =>
    gameClock - (Math.ceil(Math.abs($frame['orange_score'] - $frame['blue_score']) / 3) * 20 - 20)
);

let lastClock = 0;
let gameClock = 0;
let clockRunning = false;
export const game_clock_display = readable("-- : --", function start(set) {

    const interval = setInterval(() => {
        if (clockRunning) {
            lastClock -= .033;
            if (lastClock < 0) lastClock = 0;
        }

        let minutes = Math.trunc(lastClock / 60);
        let seconds = Math.trunc(lastClock % 60);
        let milliseconds = Math.trunc((lastClock - Math.trunc(lastClock)) * 100);
        set(`${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}.${String(milliseconds).padStart(2, '0')}`);
    }, 33);

    return function stop() {
        clearInterval(interval);
    };
});


export const last_score = readable(empty_last_score, function start(set) {
    let sw = new SparkWebsocket();
    sw.subscribe("goal", data => {
        set(data);
    });

    return function stop() {
        sw.close();
    }
});

export const overlay_config = writable(null, function start(set) {
    let sw = new SparkWebsocket();
    sw.subscribe("overlay_config", data => {
        set(data);
    });

    return function stop() {
        sw.close();
    }
});

export const event_log = readable(null, function start(set) {
    let sw = new SparkWebsocket();
    sw.subscribe("event_log", data => {
        set(data);
    });

    return function stop() {
        sw.close();
    }
});

export const pause = readable(null, function start(set) {
    let sw = new SparkWebsocket();
    sw.subscribe("pause", data => {
        set(data);
    });

    return function stop() {
        sw.close();
    }
});

export const joust = readable(null, function start(set) {
    let sw = new SparkWebsocket();
    sw.subscribe("joust", data => {
        set(data);
    });

    return function stop() {
        sw.close();
    }
});