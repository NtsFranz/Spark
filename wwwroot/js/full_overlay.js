const ip = "127.0.0.1"
const port = "6724"

const body = document.getElementsByTagName("body")[0];

// minimap
const minimap = document.getElementsByClassName("minimap_container")[0];
const disc = document.getElementById("disc");
const players = document.getElementsByClassName('player');
const player_numbers = document.getElementsByClassName('player_number');
const timeout_banner = document.getElementById('timeout_banner');
const timeout_banner_team = document.getElementById('timeout_banner_team');

// main banner
const main_banner = document.getElementById('main_banner');
const team_logo_orange = document.getElementById("team_logo_orange");
const team_name_orange = document.getElementById("team_name_orange");
const points_orange = document.getElementById("points_orange");
const team_name_blue = document.getElementById("team_name_blue");
const team_logo_blue = document.getElementById("team_logo_blue");
const round_score_blue = document.getElementById("round_score_blue");
const round_score_orange = document.getElementById("round_score_orange");
const points_blue = document.getElementById("points_blue");
const game_clock_display = document.getElementById("game_clock_display");

// math time
const mathematical_time_box = document.getElementById("mathematical_time_box");
const mathematical_time = document.getElementById("mathematical_time");

// player list
const player_lists = document.getElementById("player_lists");
const orange_players = document.getElementById("orange_players");
const blue_players = document.getElementById("blue_players");

// events
const orange_joust = document.getElementById("orange_joust");
const blue_joust = document.getElementById("blue_joust");

const orange_pause = document.getElementById("orange_pause");
const blue_pause = document.getElementById("blue_pause");

const orange_goal_banner = document.getElementById("orange_goal_banner");
const blue_goal_banner = document.getElementById("blue_goal_banner");

const orange_goal_banner_text = document.getElementById("orange_goal_banner_text");
const blue_goal_banner_text = document.getElementById("blue_goal_banner_text");
const orange_goal_banner_secondary = document.getElementById("orange_goal_banner_secondary");
const blue_goal_banner_secondary = document.getElementById("blue_goal_banner_secondary");

let lastData = null;
let lastClock = 0;
let runningClock = 0;
let clockRunning = false;

let lastBluePlayerList = "";
let lastOrangePlayerList = "";


let was_not_in_match = false;

function get_data() {
    const url = `http://${ip}:${port}/overlay_info`;
    httpGetAsync(url, process_data, not_in_match);
}

function not_in_match() {
    if (was_not_in_match) {
        return;
    }
    body.style.display = 'none';
    was_not_in_match = true;
}

function process_data(data) {
    data = JSON.parse(data);
    if (data == null) {
        return;
    }
    if (was_not_in_match) {
        body.style.display = 'block';
        was_not_in_match = false;
    }

    //console.log(data);
    if (data["session"]) {
        data["session"] = JSON.parse(data["session"]);
    }

    UpdateMinimap(data, lastData);
    UpdateMainBanner(data, lastData);
    UpdateMathematicalTime(data, lastData);
    UpdatePlayerLists(data, lastData);
    UpdateEvents(data, lastData);

    lastData = data;
}


function UpdateMinimap(data, lastData) {

    if (data["visibility"]["minimap"] && data["session"]) {
        minimap.style.display = "block";
    } else {
        minimap.style.display = "none";
        return;
    }

    set_pos(disc, data["session"]['disc']['position'][2], data["session"]['disc']['position'][0]);
    for (let i = 0; i < 10; i++) {
        if (data["session"]['teams'][Math.floor(i / 5)]['players'] &&
            data["session"]['teams'][Math.floor(i / 5)]['players'].length > i % 5) {
            const player_data = data["session"]['teams'][Math.floor(i / 5)]['players'][Math.floor(i % 5)];
            set_pos(players[i], player_data['head']['position'][2], player_data['head']['position'][0]);
            set_number(player_numbers[i], "" + player_data['number']);
            players[i].style.visibility = 'visible';
        } else {
            set_pos(players[i], 0, 0);
            players[i].style.visibility = 'hidden';
        }
    }
    if (data["session"]['pause']['paused_state'] === 'paused') {
        timeout_banner.style.visibility = 'visible';
        timeout_banner_team.innerText = data["session"]['pause']['paused_requested_team']
    } else {
        timeout_banner.style.visibility = 'hidden';
    }
}

function UpdateMainBanner(data, lastData) {


    if (data["visibility"]["main_banner"] && data["session"]) {
        main_banner.style.display = "block";
    } else {
        main_banner.style.display = "none";
        return;
    }
    if (data == null) return;

    if (lastData != null && lastData["session"] !== undefined) {
        clockRunning = lastData["session"]["game_clock"] > data["session"]["game_clock"];
    }
    lastClock = data["session"]["game_clock"];
    game_clock_display.innerText = data["session"]["game_clock_display"];
    points_blue.innerText = data["session"]["blue_points"];
    points_orange.innerText = data["session"]["orange_points"];

    // round scores
    let round_count = data["session"]["total_round_count"];
    removeAllChildNodes(round_score_blue);
    for (let i = 0; i < round_count; i++) {
        let div = document.createElement("div");
        if (i < data["session"]["blue_round_score"]) {
            div.classList.add("active");
        }
        round_score_blue.append(div);
    }

    removeAllChildNodes(round_score_orange);
    // for (let i = 0; i < data["session"]["orange_round_score"]; i++) {
    for (let i = 0; i < round_count; i++) {
        let div = document.createElement("div");
        if (i < data["session"]["orange_round_score"]) {
            div.classList.add("active");
        }
        round_score_orange.append(div);

    }

    // if the team has changed
    if (lastData == null || lastData["stats"] == null ||
        lastData["stats"]["teams"][0]["team_name"] !== data["stats"]["teams"][0]["team_name"] ||
        lastData["stats"]["teams"][0]["team_logo"] !== data["stats"]["teams"][0]["team_logo"]) {
        team_name_blue.innerText = data["stats"]["teams"][0]["team_name"];
        team_logo_blue.src = data["stats"]["teams"][0]["team_logo"];
        if (data["stats"]["teams"][0]["team_logo"] === "") {
            team_logo_blue.src = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=";
        }
    }
    // if the team has changed
    if (lastData == null || lastData["stats"] == null ||
        lastData["stats"]["teams"][1]["team_name"] !== data["stats"]["teams"][1]["team_name"] ||
        lastData["stats"]["teams"][1]["team_logo"] !== data["stats"]["teams"][1]["team_logo"]) {
        team_logo_orange.src = data["stats"]["teams"][1]["team_logo"];
        team_name_orange.innerText = data["stats"]["teams"][1]["team_name"];
        if (data["stats"]["teams"][1]["team_logo"] === "") {
            team_logo_orange.src = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=";
        }
    }


}

function UpdateMathematicalTime(data, lastData) {

    if (data["session"] === undefined || lastData == null || lastData["session"] === undefined) {
        mathematical_time_box.style.display = "none";
        return;
    }

    let orange_score = data["session"]["orange_points"];
    let blue_score = data["session"]["blue_points"];
    let game_clock = data["session"]["game_clock"];

    let mathematicalTime = game_clock - (Math.ceil(Math.abs(orange_score - blue_score) / 3) * 20 - 20);
    // if within n seconds of mathematical time ending
    if (mathematicalTime < 30) {
        mathematical_time_box.style.display = "block";

        if (mathematicalTime < 0) {
            mathematical_time.innerText = "NO TIME";
        } else {
            if (lastData["session"]["game_status"] === "playing") {
                mathematical_time.innerText = Math.round(mathematicalTime) + " s";
            }
        }
    } else {
        mathematical_time_box.style.display = "none";
    }
}

function UpdatePlayerLists(data, lastData) {

    if (data["session"] === undefined || lastData == null || lastData["session"] === undefined) {
        player_lists.style.display = "none";
        return;
    }

    player_lists.style.display = "block";


    let bluePlayers = data["session"]["teams"][0]["players"];
    let orangePlayers = data["session"]["teams"][1]["players"];
    if (bluePlayers === undefined) bluePlayers = [];
    if (orangePlayers === undefined) orangePlayers = [];

    let newBluePlayerList = Array.from(bluePlayers, p => p['name'] + p['possession'] + p['stunned']).join('');
    let newOrangePlayerList = Array.from(orangePlayers, p => p['name'] + p['possession'] + p['stunned']).join('');


    function CreatePlayers(playerList, appendList) {
        for (let i = 0; i < playerList.length; i++) {
            let div = document.createElement("div");

            let nameDiv = document.createElement("div");
            nameDiv.innerText = playerList[i]["name"];
            div.append(nameDiv);

            let numberDiv = document.createElement("div");
            numberDiv.innerText = playerList[i]["number"];
            div.append(numberDiv);

            if (playerList[i]["possession"]) {
                div.classList.add("possession");
            }
            if (playerList[i]["stunned"]) {
                div.classList.add("stunned");
            }

            appendList.append(div);
        }
    }

    if (newBluePlayerList !== lastBluePlayerList) {
        removeAllChildNodes(blue_players);
        CreatePlayers(bluePlayers, blue_players);
    }

    if (newOrangePlayerList !== lastOrangePlayerList) {
        removeAllChildNodes(orange_players);
        CreatePlayers(orangePlayers, orange_players);
    }


    lastBluePlayerList = newBluePlayerList;
    lastOrangePlayerList = newOrangePlayerList;

}

function UpdateEvents(data, lastData) {

    if (lastData == null) return;
    if (data == null) return;
    if (data["stats"] === undefined) return;
    if (data["stats"]["joust_events"] == null) data["stats"]["joust_events"] = [];
    if (data["stats"]["goals"] == null) data["stats"]["goals"] = [];

    let lastJousts = lastData["stats"]["joust_events"];
    if (lastJousts === undefined) lastJousts = [];
    let lastJoustsLength = lastJousts.length;

    // if the number of jousts has changed
    if (data["stats"]["joust_events"].length > lastJoustsLength) {
        for (let i = lastJoustsLength; i < data["stats"]["joust_events"].length; i++) {
            let joust = data["stats"]["joust_events"][i];
            if (joust["other_player_name"] === "orange") {
                orange_joust.classList.add("visible");
                orange_joust.innerText = Math.round(joust["z2"] * 100) / 100 + " s";
                // hide it after a delay
                setTimeout(function () {
                    orange_joust.classList.remove("visible");
                }, 10000);
            } else if (joust["other_player_name"] === "blue") {
                blue_joust.classList.add("visible");
                blue_joust.innerText = Math.round(joust["z2"] * 100) / 100 + " s";
                // hide it after a delay
                setTimeout(function () {
                    blue_joust.classList.remove("visible");
                }, 10000);
            }
        }
    }


    let lastGoals = lastData["stats"]["goals"];
    if (lastGoals === undefined) lastGoals = [];
    
    // if the number of goals has changed
    if (data["stats"]["goals"].length > lastGoals.length) {
        for (let i = lastGoals.length; i < data["stats"]["goals"].length; i++) {
            let goal = data["stats"]["goals"][i];
            if (goal["goal_color"] === "orange") {
                orange_goal_banner.classList.add("visible");
                orange_goal_banner_text.innerText = `${goal["point_value"]}\n${goal["goal_type"]}`;
                orange_goal_banner_secondary.innerText = goal["goal_type"];
                // hide it after a delay
                setTimeout(function () {
                    orange_goal_banner.classList.remove("visible");
                }, 10000);
            } else if (goal["goal_color"] === "blue") {
                blue_goal_banner.classList.add("visible");
                blue_goal_banner_text.innerText = goal["goal_type"];
                blue_goal_banner_secondary.innerText = goal["goal_type"];
                // hide it after a delay
                setTimeout(function () {
                    blue_goal_banner.classList.remove("visible");
                }, 10000);
            }
        }
    }


    if (data["session"] !== undefined &&
        lastData["session"] !== undefined &&
        data["session"]["pause"]["paused_state"] !== lastData["session"]["pause"]["paused_state"]) {
        if (data["session"]["pause"]["paused_state"] === "paused") {
            if (data["session"]["pause"]["paused_requested_team"] === "orange") {
                orange_pause.classList.add("visible");
            } else if (data["session"]["pause"]["paused_requested_team"] === "blue") {
                blue_pause.classList.add("visible");
            }
        } else {
            orange_pause.classList.remove("visible");
            blue_pause.classList.remove("visible");
        }
    }
}

function set_clock() {
    if (clockRunning) {
        lastClock -= .033;

        let minutes = Math.trunc(lastClock / 60);
        let seconds = Math.trunc(lastClock % 60);
        let milliseconds = Math.trunc((lastClock - Math.trunc(lastClock)) * 100);
        game_clock_display.innerText = `${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}.${String(milliseconds).padStart(2, '0')}`;
    }
}


// width:
//      min: 5
//      max: 170
// length:
//      min: 68
//      max: 495
function set_pos(elem, z, x) {
    elem.style.left = (-z / 80 + .5) * 427 + 68 + "px";
    elem.style.top = (x / 32 + .5) * 165 + 5 + "px";
}

function set_number(elem, text) {
    if (text.length === 1) {
        text = "0" + text;
    }
    elem.innerText = text;
}

setInterval(get_data, 100);
setInterval(set_clock, 33);