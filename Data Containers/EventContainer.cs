using System.Collections.Generic;

namespace Spark
{
	public abstract class EventContainer : DataContainer
	{
		public enum EventType
		{
			
			overlay_config,	// used by websocket for overlays
			frame_1hz,		// used by websocket for overlays
			frame_10hz,		// used by websocket for overlays
			frame_30hz,		// used by websocket for overlays
			joined_game,	// used by websocket for overlays
			left_game,		// used by websocket for overlays
			event_log,		// used by websocket for overlays
			
			goal,
			
			stun,
			block,
			save,
			@catch,
			pass,
			@throw,
			local_throw,
			shot_taken,
			steal,
			playspace_abuse,
			player_joined,
			player_left,
			joust,			// used as an alias for both defensive and neutral jousts
			joust_speed,	// neutral joust
			defensive_joust,
			big_boost,
			restart_request,
			pause,			// used as an alias for any change in pause state
			pause_request,
			unpause_request,
			interception,	// not in db yet
			player_switched_teams,	// not in db yet
			turnover,
			emote,
		}

		/// <summary>
		/// Whether or not this data has been sent to the DB or not
		/// </summary>
		public bool inDB = false;
		public EventType eventType;
		
		public abstract Dictionary<string, object> ToDict();
		public abstract Dictionary<string, object> ToDict(bool useCustomKeyNames);
		// public abstract string ToShortString();
		// public abstract string ToLongString();
	}
}