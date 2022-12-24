export interface Frame {
	disc:                        Disc;
	orange_team_restart_request: number;
	sessionid:                   string;
	game_clock_display:          string;
	game_status:                 string;
	sessionip:                   string;
	match_type:                  string;
	map_name:                    string;
	right_shoulder_pressed2:     number;
	teams:                       Team[];
	blue_round_score:            number;
	orange_points:               number;
	player:                      VRPlayer;
	private_match:               boolean;
	blue_team_restart_request:   number;
	tournament_match:            boolean;
	orange_round_score:          number;
	rules_changed_by:            string;
	total_round_count:           number;
	left_shoulder_pressed2:      number;
	left_shoulder_pressed:       number;
	pause:                       Pause;
	right_shoulder_pressed:      number;
	blue_points:                 number;
	last_throw:                  { [key: string]: number };
	client_name:                 string;
	game_clock:                  number;
	possession:                  number[];
	last_score:                  LastScore;
	rules_changed_at:            number;
	err_code:                    number;
}

export interface Disc {
	position:     number[];
	forward:      number[];
	left:         number[];
	up:           number[];
	velocity:     number[];
	bounce_count: number;
}

export interface LastScore {
	disc_speed:      number;
	team:            string;
	goal_type:       string;
	point_amount:    number;
	distance_thrown: number;
	person_scored:   string;
	assist_scored:   string;
}

export interface Pause {
	paused_state:          string;
	unpaused_team:         PausedRequestedTeam;
	paused_requested_team: PausedRequestedTeam;
	unpaused_timer:        number;
	paused_timer:          number;
}

export enum PausedRequestedTeam {
	None = "none",
}

export interface VRPlayer {
	vr_left:     number[];
	vr_position: number[];
	vr_forward:  number[];
	vr_up:       number[];
}

export interface Team {
	players:    Player[];
	team:       string;
	possession: boolean;
	stats:      { [key: string]: number };
}

export interface Player {
	name:             string;
	rhand:            Body;
	playerid:         number;
	userid:           number;
	is_emote_playing: boolean;
	number:           number;
	level:            number;
	stunned:          boolean;
	ping:             number;
	packetlossratio:  number;
	invulnerable:     boolean;
	holding_left:     PausedRequestedTeam;
	possession:       boolean;
	head:             Body;
	body:             Body;
	holding_right:    PausedRequestedTeam;
	lhand:            Body;
	blocking:         boolean;
	velocity:         number[];
	stats:            { [key: string]: number };
}

export interface Body {
	position?: number[];
	forward:   number[];
	left:      number[];
	up:        number[];
	pos?:      number[];
}