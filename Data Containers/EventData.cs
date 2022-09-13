using System;
using System.Collections.Generic;
using System.Numerics;
using EchoVRAPI;
using Spark;

namespace Spark
{
	/// <summary>
	/// Object containing data describing certain events like stuns, throws, quits, joins, etc.
	/// </summary>
	public class EventData : EventContainer
	{
		public EventData(AccumulatedFrame round, EventType eventType, float gameClock, Team team, Player player, long joustTimeMillis, Vector3 position, Vector3 vec2)
		{
			eventTime = DateTime.UtcNow;
			this.round = round;
			this.eventType = eventType;
			this.gameClock = gameClock;
			this.player = player;
			this.joustTimeMillis = joustTimeMillis;
			this.position = position;
			this.vec2 = vec2;
			this.team = team;
		}

		public EventData(AccumulatedFrame round, EventType eventType, float gameClock, Team team, Player player, Player otherPlayer, Vector3 position, Vector3 vec2)
		{
			eventTime = DateTime.UtcNow;
			this.round = round;
			this.eventType = eventType;
			this.gameClock = gameClock;
			this.player = player;
			this.otherPlayer = otherPlayer;
			this.position = position;
			this.vec2 = vec2;
			this.team = team;
		}
		

		public EventData(AccumulatedFrame round, EventType eventType, float gameClock, Team team, Player player, LastThrow lastThrow)
		{
			eventTime = DateTime.UtcNow;
			this.round = round;
			this.eventType = eventType;
			this.gameClock = gameClock;
			this.player = player;
			this.team = team;

			position = new Vector3(lastThrow.total_speed, lastThrow.speed_from_arm, lastThrow.speed_from_wrist);
			vec2 = new Vector3(lastThrow.speed_from_movement, lastThrow.arm_speed, lastThrow.rot_per_sec);
		}



		public AccumulatedFrame round;
		private DateTime eventTime;
		public float gameClock;
		public Player player;
		public Player otherPlayer;
		public Vector3 position;
		public Vector3 vec2;
		public long joustTimeMillis;
		public Team team;

		/// <summary>
		/// Function to transform event data into the desired format for databases.
		/// </summary>
		/// <returns></returns>
		public override Dictionary<string, object> ToDict()
		{
			try
			{
				return new Dictionary<string, object>
				{
					{ "session_id", round.frame.sessionid },
					{ "match_time", round.MatchTimeSQL },
					{ "event_time", eventTime.ToString("yyyy-MM-dd HH:mm:ss") },
					{ "game_clock", gameClock },
					{ "player_id", player?.userid },
					{ "player_name", player?.name },
					{ "event_type", eventType.ToString() },
					{ "other_player_id", eventType.IsJoust() ? joustTimeMillis : otherPlayer?.userid },
					{ "other_player_name", eventType.IsJoust() ? team.color.ToString() : otherPlayer?.name },
					{ "pos_x", position.X },
					{ "pos_y", position.Y },
					{ "pos_z", position.Z },
					{ "x2", vec2 != Vector3.Zero ? (float?)vec2.X : null },
					{ "y2", vec2 != Vector3.Zero ? (float?)vec2.Y : null },
					{ "z2", vec2 != Vector3.Zero ? (float?)vec2.Z : null }
				};
			}
			catch (Exception e)
			{
				Logger.LogRow(Logger.LogType.Error, "Can't serialize event.\n" + e.Message + "\n" + e.StackTrace);
				return new Dictionary<string, object>
				{
					{ "none", 0 }
				};
			}
		}

		/// <summary>
		/// Function to transform event data into the desired format for firestore.
		/// </summary>
		/// <returns></returns>
		public override Dictionary<string, object> ToDict(bool useCustomKeyNames)
		{
			if (!useCustomKeyNames) return ToDict();
			try
			{
				Dictionary<string, object> values = null;
				switch (eventType)
				{
					case EventType.stun:
						values = new Dictionary<string, object>
						{
							{ "session_id", round.frame.sessionid },
							{ "match_time", round.MatchTimeSQL },
							{ "event_time", eventTime.ToString("yyyy-MM-dd HH:mm:ss") },
							{ "game_clock", gameClock },
							{ "event_type", eventType.ToString() },
							{ "stunner_id", player?.userid },
							{ "stunner_name", player?.name },
							{ "stunner_team", player?.team_color },
							{ "victim_id", otherPlayer?.userid },
							{ "victim_name", otherPlayer?.name },
							{ "victim_team", otherPlayer?.team_color },
							{ "pos_x", position.X },
							{ "pos_y", position.Y },
							{ "pos_z", position.Z },
						};
						break;
					case EventType.block:
						break;
					case EventType.save:
						values = new Dictionary<string, object>
						{
							{ "session_id", round.frame.sessionid },
							{ "match_time", round.MatchTimeSQL },
							{ "event_time", eventTime.ToString("yyyy-MM-dd HH:mm:ss") },
							{ "game_clock", gameClock },
							{ "event_type", eventType.ToString() },
							{ "player_id", player?.userid },
							{ "player_name", player?.name },
							{ "player_team", player?.team_color },
							{ "pos_x", position.X },
							{ "pos_y", position.Y },
							{ "pos_z", position.Z },
						};
						break;
					case EventType.@catch:
						break;
					case EventType.pass:
						break;
					case EventType.@throw:
						break;
					case EventType.shot_taken:
						break;
					case EventType.steal:
						values = new Dictionary<string, object>
						{
							{ "session_id", round.frame.sessionid },
							{ "match_time", round.MatchTimeSQL },
							{ "event_time", eventTime.ToString("yyyy-MM-dd HH:mm:ss") },
							{ "game_clock", gameClock },
							{ "event_type", eventType.ToString() },
							{ "player_id", player?.userid },
							{ "player_name", player?.name },
							{ "player_team", player?.team_color },
							{ "pos_x", position.X },
							{ "pos_y", position.Y },
							{ "pos_z", position.Z },
						};
						break;
					case EventType.playspace_abuse:
						break;
					case EventType.player_joined:
						break;
					case EventType.player_left:
						break;
					case EventType.joust_speed:
						values = new Dictionary<string, object>
						{
							{ "session_id", round.frame.sessionid },
							{ "match_time", round.MatchTimeSQL },
							{ "event_time", eventTime.ToString("yyyy-MM-dd HH:mm:ss") },
							{ "game_clock", gameClock },
							{ "player_id", player?.userid },
							{ "player_name", player?.name },
							{ "event_type", eventType.ToString() },
							{ "joust_time_millis", joustTimeMillis },
							{ "team_color", team.color.ToString() },
							{ "max_speed", vec2.X },
							{ "max_tube_exit_speed", vec2.Y },
							{ "joust_time", vec2.Z }
						};
						break;
					case EventType.defensive_joust:
						values = new Dictionary<string, object>
						{
							{ "session_id", round.frame.sessionid },
							{ "match_time", round.MatchTimeSQL },
							{ "event_time", eventTime.ToString("yyyy-MM-dd HH:mm:ss") },
							{ "game_clock", gameClock },
							{ "player_id", player?.userid },
							{ "player_name", player?.name },
							{ "event_type", eventType.ToString() },
							{ "joust_time_millis", joustTimeMillis },
							{ "team_color", team.color.ToString() },
							{ "max_speed", vec2.X },
							{ "max_tube_exit_speed", vec2.Y },
							{ "joust_time", vec2.Z }
						};
						break;
					case EventType.big_boost:
						break;
					case EventType.restart_request:
						break;
					case EventType.pause_request:
						break;
					case EventType.unpause_request:
						break;
					case EventType.interception:
						break;
					case EventType.player_switched_teams:
						break;
				}

				values ??= ToDict();

				return values;
			}
			catch (Exception e)
			{
				Logger.LogRow(Logger.LogType.Error, "Can't serialize event.\n" + e.Message + "\n" + e.StackTrace);
				return new Dictionary<string, object>
				{
					{ "none", 0 }
				};
			}
		}
	}
}

static class EventTypeExtensions
{
	public static bool IsJoust(this EventContainer.EventType eventType)
	{
		return eventType is EventContainer.EventType.joust_speed or EventContainer.EventType.defensive_joust;
	}
}