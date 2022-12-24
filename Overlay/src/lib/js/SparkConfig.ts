export let Config = {
	visibility: {
		minimap: false,
		compact_minimap: false,
		player_rosters: false,
		main_banner: false,
		neutral_jousts: false,
		defensive_jousts: false,
		event_log: false,
		playspace: false,
		player_speed: false,
		disc_speed: false,
		show_team_logos: false,
		show_team_names: false
	},
	caster_prefs: {
		num_casters: 4,
		caster_0_name: '',
		caster_0_img: '',
		caster_0_vdo: '',

		allow_mathematical_time: false,

		round_title: '',
		round_title_alt: '',
		segment_title: '',
		segment_subtitle: '',

		alt_orange_team_name: '',
		alt_orange_team_logo: '',
		alt_blue_team_name: '',
		alt_blue_team_logo: '',

		waiting_message: '',
		show_echo_unit: false,
		free_cam_fov: 80
	},
	round_scores: {
		round_scores_manual: false, // this is just for backwards compatibility
		manual_round_scores: false,
		round_count: 3,
		round_scores_orange: [0],
		round_scores_blue: [0]
	},
	team_names_source: 1,
	teams: [
		{
			vrml_team_name: '',
			vrml_team_logo: '',
			team_name: '',
			team_logo: ''
		},
		{
			vrml_team_name: '',
			vrml_team_logo: '',
			team_name: '',
			team_logo: ''
		}
	]
};