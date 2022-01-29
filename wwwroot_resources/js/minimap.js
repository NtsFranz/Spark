const disc = document.getElementById("disc");
const players = document.getElementsByClassName('player');
const player_numbers = document.getElementsByClassName('player_number');
const timeout_banner = document.getElementById('timeout_banner');
const timeout_banner_team = document.getElementById('timeout_banner_team');


let sw = new SparkWebsocket();
sw.subscribe("frame_10hz", data => {
	
	set_pos(disc, data['disc']['position'][2], data['disc']['position'][0]);
	for (let i = 0; i < 10; i++) {
		if (data['teams'][Math.floor(i / 5)]['players'] &&
			data['teams'][Math.floor(i / 5)]['players'].length > i % 5) {
			const player_data = data['teams'][Math.floor(i / 5)]['players'][Math.floor(i % 5)];
			set_pos(players[i], player_data['head']['position'][2], player_data['head']['position'][0]);
			set_number(player_numbers[i], "" + player_data['number']);
			players[i].style.visibility = 'visible';
		}
		else {
			set_pos(players[i], 0, 0);
			players[i].style.visibility = 'hidden';
		}
	}

	if (data['pause']['paused_state'] === 'paused') {
		timeout_banner.style.visibility = 'visible';
		timeout_banner_team.innerText = data['pause']['paused_requested_team']
	} else {
		timeout_banner.style.visibility = 'hidden';
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