const body = document.getElementsByTagName("body")[0];

// minimap
const minimap_canvas = document.getElementById("minimap_canvas");
if (minimap_canvas) {
    var minimap_canvas_ctx = minimap_canvas.getContext("2d");
}
const minimap = document.getElementsByClassName("minimap_container")[0];
const disc = document.getElementById("disc");
const players = document.getElementsByClassName('player');
const player_numbers = document.getElementsByClassName('player_number');
const timeout_banner = document.getElementById('timeout_banner');
const timeout_banner_team = document.getElementById('timeout_banner_team');

// main banner
const main_banner = document.getElementById('main_banner');
const team_logo_orange = document.getElementById("team_logo_orange");
const team_logo_background_orange = document.getElementById("team_logo_background_orange");
const team_name_orange = document.getElementById("team_name_orange");
const points_orange = document.getElementById("points_orange");
const team_name_blue = document.getElementById("team_name_blue");
const team_logo_blue = document.getElementById("team_logo_blue");
const team_logo_background_blue = document.getElementById("team_logo_background_blue");
const round_score_blue = document.getElementById("round_score_blue");
const round_score_orange = document.getElementById("round_score_orange");
const points_blue = document.getElementById("points_blue");
const game_clock_display = document.getElementById("game_clock_display");

const team_logos_collapsible = document.getElementsByClassName("team_logos_collapsible");
const team_names_collapsible = document.getElementsByClassName("team_names_collapsible");

const possession_orange = document.getElementById("possession_orange");
const possession_blue = document.getElementById("possession_blue");

// math time
const mathematical_time_box = document.getElementById("mathematical_time_content");
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

const orange_goal_type = document.getElementById("orange_goal_type");
const orange_goal_player_name = document.getElementById("orange_goal_player_name");
const orange_goal_assist_name = document.getElementById("orange_goal_assist_name");
const orange_goal_point_value = document.getElementById("orange_goal_point_value");

const blue_goal_type = document.getElementById("blue_goal_type");
const blue_goal_player_name = document.getElementById("blue_goal_player_name");
const blue_goal_assist_name = document.getElementById("blue_goal_assist_name");
const blue_goal_point_value = document.getElementById("blue_goal_point_value");


const orange_goal_banner_text = document.getElementById("orange_goal_banner_text");
const blue_goal_banner_text = document.getElementById("blue_goal_banner_text");
const orange_goal_banner_secondary = document.getElementById("orange_goal_banner_secondary");
const blue_goal_banner_secondary = document.getElementById("blue_goal_banner_secondary");
const orange_goal_banner_point_value = document.getElementById("orange_goal_banner_point_value");
const blue_goal_banner_point_value = document.getElementById("blue_goal_banner_point_value");

const event_log_container = document.getElementById("event_log_container");

const iframe_compact_main_banner = document.getElementById("iframe_compact_main_banner");
const iframe_default_minimap = document.getElementById("iframe_default_minimap");
const iframe_compact_minimap = document.getElementById("iframe_compact_minimap");
const iframe_player_lists = document.getElementById("iframe_player_lists");
const iframe_event_log = document.getElementById("iframe_event_log");

const visibility_options = [
    [iframe_compact_main_banner, "main_banner"],
    [iframe_default_minimap, "minimap"],
    [iframe_compact_minimap, "compact_minimap"],
    [iframe_player_lists, "player_rosters"],
    [iframe_event_log, "event_log"],
];

let lastData = null;
let lastClock = 0;
let runningClock = 0;
let clockRunning = false;

let lastBluePlayerList = "";
let lastOrangePlayerList = "";

let was_not_in_match = true;


let sw = new SparkWebsocket();

sw.subscribe("frame_10hz", data => {

    if (was_not_in_match) {
        body.style.display = 'block';
        was_not_in_match = false;
    }

    UpdateMinimap(data, lastData);
    UpdateMainBanner(data, lastData);
    UpdateMathematicalTime(data, lastData);
    UpdatePlayerLists(data, lastData);

    lastData = data;
});

if (orange_joust !== null && blue_joust !== null) {
    sw.subscribe("joust", data => {
        if (data["team_color"] === "orange") {
            orange_joust.classList.add("visible");
            orange_joust.innerText = Math.round(data["joust_time"] * 100) / 100 + " s";
            // hide it after a delay
            setTimeout(function () {
                orange_joust.classList.remove("visible");
            }, 10000);
        } else if (data["team_color"] === "blue") {
            blue_joust.classList.add("visible");
            blue_joust.innerText = Math.round(data["joust_time"] * 100) / 100 + " s";
            // hide it after a delay
            setTimeout(function () {
                blue_joust.classList.remove("visible");
            }, 10000);
        }
    });
}

if (orange_pause !== null && blue_pause !== null) {
    sw.subscribe("pause", data => {
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
    });
}

if (orange_goal_banner !== null && blue_goal_banner !== null) {
    sw.subscribe("goal", goal => {
        if (goal["team_scored"] === "orange") {
            orange_goal_banner.classList.add("visible");
            orange_goal_type.innerText = goal["goal_type"];
            orange_goal_player_name.innerText = goal["player_name"];
            orange_goal_point_value.innerText = goal["point_value"];
            // hide it after a delay
            setTimeout(function () {
                orange_goal_banner.classList.remove("visible");
            }, 10000);
        } else if (goal["team_scored"] === "blue") {
            blue_goal_banner.classList.add("visible");
            blue_goal_type.innerText = goal["goal_type"];
            blue_goal_player_name.innerText = goal["player_name"];
            blue_goal_point_value.innerText = goal["point_value"];
            // hide it after a delay
            setTimeout(function () {
                blue_goal_banner.classList.remove("visible");
            }, 10000);
        }
    });
}

sw.subscribe("overlay_config", data => {

    if (main_banner !== null) {
        if (data["visibility"]["main_banner"]) {
            main_banner.style.display = "block";
        } else {
            main_banner.style.display = "none";
        }
    }

    // if the team has changed
    if (team_name_blue !== null) {
        team_name_blue.innerText = data["teams"][0]["team_name"];
        team_name_orange.innerText = data["teams"][1]["team_name"];
    }
    if (team_logo_blue !== null) {
        team_logo_blue.src = data["teams"][0]["team_logo"];
        team_logo_orange.src = data["teams"][1]["team_logo"];

        if (data["teams"][0]["team_logo"] === "") {
            team_logo_blue.src = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=";
        }
        // if the team has changed
        if (data["teams"][1]["team_logo"] === "") {
            team_logo_orange.src = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=";
        }
    }
    if (team_logo_background_blue !== null) {
        team_logo_background_blue.style.backgroundImage = "url(" + data["teams"][0]["team_logo"] + ")";
        team_logo_background_orange.style.backgroundImage = "url(" + data["teams"][1]["team_logo"] + ")";
    }

    if (team_logos_collapsible !== null) {
        for (const doc of team_logos_collapsible) {
            if (data["visibility"]["show_team_logos"] === true) {
                doc.classList.add("visible");
            } else {
                doc.classList.remove("visible");
            }
        }
    }
    if (team_names_collapsible !== null) {
        for (const doc of team_names_collapsible) {
            if (data["visibility"]["show_team_names"] === true) {
                doc.classList.add("visible");
            } else {
                doc.classList.remove("visible");
            }
        }
    }


    visibility_options.forEach(v => {
        if (v[0] !== null) {
            if (data["visibility"][v[1]]) {
                v[0].style.display = "block";
            } else {
                v[0].style.display = "none";
            }
        }
    });

});

sw.subscribe("left_game", goal => {
    body.style.display = 'none';
    was_not_in_match = true;
});


if (event_log_container !== null) {
    sw.subscribe("event_log", data => {
        console.log(data);

        let elem = document.createElement("div");
        elem.innerText = data["message"];
        event_log_container.prepend(elem);

        setTimeout(() => {
            elem.classList.add("invisible");
            setTimeout(() => {
                elem.remove();
            }, 200);
        }, 5000);
    });
}

function UpdateMinimap(session) {

    if (minimap_canvas !== null) {
    }

    if (disc === null) return;
    set_pos(disc, session['disc']['position'][2], session['disc']['position'][0]);
    for (let i = 0; i < 10; i++) {
        if (session['teams'][Math.floor(i / 5)]['players'] &&
            session['teams'][Math.floor(i / 5)]['players'].length > i % 5) {
            const player_data = session['teams'][Math.floor(i / 5)]['players'][Math.floor(i % 5)];
            set_pos(players[i], player_data['head']['position'][2], player_data['head']['position'][0]);
            set_number(player_numbers[i], "" + player_data['number']);
            players[i].style.visibility = 'visible';
        } else {
            set_pos(players[i], 0, 0);
            players[i].style.visibility = 'hidden';
        }
    }

    if (timeout_banner) {
        if (session['pause']['paused_state'] === 'paused') {
            timeout_banner.style.visibility = 'visible';
            timeout_banner_team.innerText = session['pause']['paused_requested_team']
        } else {
            timeout_banner.style.visibility = 'hidden';
        }
    }
}

function UpdateMainBanner(data, lastData) {
    if (data == null) return;

    if (lastData !== null) {
        clockRunning = lastData["game_clock"] > data["game_clock"];
    }
    lastClock = data["game_clock"];
    if (game_clock_display !== null) game_clock_display.innerText = data["game_clock_display"];
    if (points_blue !== null) points_blue.innerText = data["blue_points"];
    if (points_orange !== null) points_orange.innerText = data["orange_points"];

    // round scores
    let round_count = data["total_round_count"];
    if (round_score_blue !== null) {
        removeAllChildNodes(round_score_blue);
        for (let i = 0; i < round_count; i++) {
            let div = document.createElement("div");
            if (i < data["blue_round_score"]) {
                div.classList.add("active");
            }
            round_score_blue.append(div);
        }
    }

    if (round_score_orange !== null) {
        removeAllChildNodes(round_score_orange);
        // for (let i = 0; i < data["orange_round_score"]; i++) {
        for (let i = 0; i < round_count; i++) {
            let div = document.createElement("div");
            if (i < data["orange_round_score"]) {
                div.classList.add("active");
            }
            round_score_orange.append(div);
        }
    }

    // possession
    if (possession_blue !== null) {
        if (data["possession"][0] === 0 && data["game_status"] === "playing") {
            possession_blue.classList.add("active");
            possession_orange.classList.remove("active");
        } else if (data["possession"][0] === 1 && data["game_status"] === "playing") {
            possession_blue.classList.remove("active");
            possession_orange.classList.add("active");
        } else {
            possession_blue.classList.remove("active");
            possession_orange.classList.remove("active");
        }
    }

}

function UpdateMathematicalTime(data, lastData) {
    if (mathematical_time_box === null) return;

    if (data === undefined || lastData == null) {
        mathematical_time_box.style.display = "none";
        return;
    }

    let orange_score = data["orange_points"];
    let blue_score = data["blue_points"];
    let game_clock = data["game_clock"];

    let mathematicalTime = game_clock - (Math.ceil(Math.abs(orange_score - blue_score) / 3) * 20 - 20);
    // if within n seconds of mathematical time ending
    if (mathematicalTime < 30) {
        mathematical_time_box.style.display = "block";

        if (mathematicalTime < 0) {
            mathematical_time.innerText = "NO TIME";
        } else {
            if (lastData["game_status"] === "playing") {
                mathematical_time.innerText = Math.round(mathematicalTime) + " s";
            }
        }
    } else {
        mathematical_time_box.style.display = "none";
    }
}

function UpdatePlayerLists(data, lastData) {

    if (player_lists === null) return;

    if (data === undefined || lastData == null) {
        player_lists.style.display = "none";
        return;
    }

    player_lists.style.display = "block";


    let bluePlayers = data["teams"][0]["players"];
    let orangePlayers = data["teams"][1]["players"];
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

function set_clock() {
    if (clockRunning) {
        lastClock -= .033;
        if (lastClock < 0) lastClock = 0;

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
    elem.style.left = (-z / 80 + .5) * 26.6875 + 4.25 + "em";
    elem.style.top = (x / 32 + .5) * 10.3125 + 0.3125 + "em";
}

function set_number(elem, text) {
    if (text.length === 1) {
        text = "0" + text;
    }
    elem.innerText = text;
}

if (game_clock_display !== null) {
    setInterval(set_clock, 33);
}