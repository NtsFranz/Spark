const disc = document.getElementById("disc");
const players = document.getElementsByClassName('player');
const player_numbers = document.getElementsByClassName('player_number');
const timeout_banner = document.getElementById('timeout_banner');
const timeout_banner_team = document.getElementById('timeout_banner_team');


function api_pos(obj) {
	const p = obj && ((obj.head && obj.head.position) || obj.position);
	return Array.isArray(p) && p.length >= 3 ? p : [0, 0, 0];
}

let sw = new SparkWebsocket();
sw.subscribe("frame_10hz", data => {
	if (!data) return;

	if (data.disc) {
		const d = api_pos(data.disc);
		set_pos(disc, d[2], d[0]);
	}

	const teams = Array.isArray(data.teams) ? data.teams : [];
	for (let i = 0; i < 10; i++) {
		if (!players[i]) continue;
		const team = teams[Math.floor(i / 5)];
		const list = team && Array.isArray(team.players) ? team.players : [];
		const player_data = list[i % 5];
		if (player_data) {
			const pp = api_pos(player_data);
			set_pos(players[i], pp[2], pp[0]);
			set_number(player_numbers[i], "" + (player_data.number ?? 0));
			players[i].style.visibility = 'visible';
		} else {
			set_pos(players[i], 0, 0);
			players[i].style.visibility = 'hidden';
		}
	}

	if (timeout_banner) {
		if (data.pause && data.pause.paused_state === 'paused') {
			timeout_banner.style.visibility = 'visible';
			if (timeout_banner_team) timeout_banner_team.innerText = data.pause.paused_requested_team;
		} else {
			timeout_banner.style.visibility = 'hidden';
		}
	}
});



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