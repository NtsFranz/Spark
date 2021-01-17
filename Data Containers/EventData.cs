using System;
using System.Collections.Generic;
using System.Numerics;
using IgniteBot;

namespace IgniteBot
{
	/// <summary>
	/// Object containing data describing certain events like stuns, throws, quits, joins, etc.
	/// </summary>
	public class EventData : DataContainer
	{
		public EventData(MatchData match, EventType eventType, float gameClock, g_Team team, g_Player player, long joustTimeMillis, Vector3 position, Vector3 vec2)
		{
			matchData = match;
			this.eventType = eventType;
			this.gameClock = gameClock;
			this.player = player;
			this.joustTimeMillis = joustTimeMillis;
			this.position = position;
			this.vec2 = vec2;
			this.team = team;
		}

		public EventData(MatchData match, EventType eventType, float gameClock, g_Team team, g_Player player, g_Player otherPlayer, Vector3 position, Vector3 vec2)
		{
			matchData = match;
			this.eventType = eventType;
			this.gameClock = gameClock;
			this.player = player;
			this.otherPlayer = otherPlayer;
			this.position = position;
			this.vec2 = vec2;
			this.team = team;
		}

		public MatchData matchData;
		public enum EventType
		{
			stun,
			block,
			save,
			@catch,
			pass,
			@throw,
			shot_taken,
			steal,
			playspace_abuse,
			player_joined,
			player_left,
			joust_speed,
			defensive_joust,
			big_boost,
			restart_request,
			pause_request,
			unpause_request,
			interception,	// not in db yet
			turnover,		// not in db yet
			player_switched_teams,	// not in db yet
		}



		/// <summary>
		/// Whether or not this data has been sent to the DB or not
		/// </summary>
		public bool inDB = false;

		public EventType eventType;
		public float gameClock;
		public g_Player player;
		public g_Player otherPlayer;
		public Vector3 position;
		public Vector3 vec2;
		public long joustTimeMillis;
		public g_Team team;

		/// <summary>
		/// Function to transform event data into the desired format for firestore.
		/// </summary>
		/// <returns></returns>
		public Dictionary<string, object> ToDict()
		{
			try
			{
				var values = new Dictionary<string, object>
				{
					{"session_id", matchData.SessionId },
					{"match_time", matchData.MatchTimeSQL },
					{"event_time", DateTime.Now.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss") },
					{"game_clock", gameClock },
					{"player_id", player?.userid },
					{"player_name", player?.name },
					{"event_type", eventType.ToString() },
					{"other_player_id", eventType.IsJoust() ? joustTimeMillis : (otherPlayer != null ? (long?)otherPlayer.userid : null) },
					{"other_player_name", eventType.IsJoust() ? team.color.ToString() : otherPlayer?.name },
					{"pos_x", position.X },
					{"pos_y", position.Y },
					{"pos_z", position.Z },
					{"x2", vec2 != Vector3.Zero ? (float?)vec2.X : null },
					{"y2", vec2 != Vector3.Zero ? (float?)vec2.Y : null },
					{"z2", vec2 != Vector3.Zero ? (float?)vec2.Z : null }
				};
				return values;
			}
			catch (Exception e)
			{
				Logger.LogRow(Logger.LogType.Error, "Can't serialize event.\n" + e.Message + "\n" + e.StackTrace);
				return new Dictionary<string, object>
				{
					{"none", 0 }
				};
			}

		}
	}
}

static class EventTypeExtensions
{
	public static bool IsJoust(this EventData.EventType eventType)
	{
		if (eventType == EventData.EventType.joust_speed || eventType == EventData.EventType.defensive_joust)
		{
			return true;
		}

		return false;
	}
}