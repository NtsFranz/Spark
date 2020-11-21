using System;
using System.Collections.Generic;
using System.Numerics;
using static IgniteBot2.g_Team;

namespace IgniteBot2
{
	/// <summary>
	/// A recreation of the JSON object given by EchoVR
	/// https://github.com/Graicc/echovr_api_docs
	/// </summary>
	public class g_Instance
	{
		/// <summary>
		/// This isn't in the api, just useful for recorded data
		/// </summary>
		public DateTime recorded_time { get; set; }
		/// <summary>
		/// Disc object at the given instance.
		/// </summary>
		public g_Disc disc { get; set; }
		public string sessionid { get; set; }
		public bool orange_team_restart_request { get; set; }
		public string sessionip { get; set; }
		/// <summary>
		/// The current state of the match
		/// { pre_match, round_start, playing, score, round_over, pre_sudden_death, sudden_death, post_sudden_death, post_match }
		/// </summary>
		public string game_status { get; set; }
		/// <summary>
		/// Game time as displayed in game.
		/// </summary>
		public string game_clock_display { get; set; }
		/// <summary>
		/// Time of remaining in match (in seconds)
		/// </summary>
		public float game_clock { get; set; }
		public string match_type { get; set; }
		public string map_name { get; set; }
		/// <summary>
		/// Name of the oculus username recording.
		/// </summary>
		public string client_name { get; set; }
		public g_Playspace player { get; set; }
		public int orange_points { get; set; }
		public bool private_match { get; set; }
		/// <summary>
		/// List of integers to determine who currently has possession.
		/// [ team, player ]
		/// </summary>
		public List<int> possession { get; set; }
		public bool tournament_match { get; set; }
		public bool blue_team_restart_request { get; set; }
		public int blue_points { get; set; }
		/// <summary>
		/// Object containing data from the last goal made.
		/// </summary>
		public g_Score last_score { get; set; }
		public List<g_Team> teams { get; set; }

		public List<g_Team> playerTeams {
			get {
				return new List<g_Team>()
				{
					teams[0], teams[1]
				};
			}
		}

		/// <summary>
		/// Gets all the g_Player objects from both teams
		/// </summary>
		public List<g_Player> GetAllPlayers(bool includeSpectators = false)
		{
			List<g_Player> list = new List<g_Player>();
			list.AddRange(teams[(int)TeamColor.blue].players);
			list.AddRange(teams[(int)TeamColor.orange].players);
			if (includeSpectators)
			{
				list.AddRange(teams[2].players);
			}
			return list;
		}

		/// <summary>
		/// Get a player from all players their name.
		/// </summary>
		/// <param name="name"></param>
		/// <returns></returns>
		public g_Player GetPlayer(string name)
		{
			foreach (var team in teams)
			{
				foreach (var player in team.players)
				{
					if (player.name == name) return player;
				}
			}

			return null;
		}

		/// <summary>
		/// Get a player from all players their userid.
		/// </summary>
		/// <param name="userid"></param>
		/// <returns></returns>
		public g_Player GetPlayer(long userid)
		{
			foreach (var team in teams)
			{
				foreach (var player in team.players)
				{
					if (player.userid == userid) return player;
				}
			}

			return null;
		}
	}

	public class g_InstanceSimple
	{
		public bool private_match { get; set; }

		/// <summary>
		/// Name of the oculus username spectating.
		/// </summary>
		public string client_name { get; set; }
	}

	/// <summary>
	/// Object describing the disc at the given instant. 
	/// </summary>
	public class g_Disc
	{
		/// <summary>
		/// A 3 element list of floats representing the disc's position relative to the center of the map.
		/// < X, Y, Z >
		/// </summary>
		public List<float> position { get; set; }
		public List<float> forward { get; set; }
		public List<float> left { get; set; }
		public List<float> up { get; set; }
		/// <summary>
		/// A 3 element list of floats representing the disc's velocity.
		/// < X, Y, Z >
		/// </summary>
		public List<float> velocity { get; set; }
		public int bounce_count { get; set; }
	}

	/// <summary>
	/// Object Containing basic player information and player stats 
	/// </summary>
	public class g_Player
	{
		/// <summary>
		/// Right hand position and rotation
		/// </summary>
		public g_Transform rhand { get; set; }
		/// <summary>
		/// Index of the player in the match, so [0-6] for 3v3 & [0-7] for 4v4
		/// </summary>
		public int playerid { get; set; }
		/// <summary>
		/// Display Name
		/// </summary>
		public string name { get; set; }
		/// <summary>
		/// Application-scoped Oculus userid
		/// </summary>
		public long userid { get; set; }
		/// <summary>
		/// Object describing a player's aggregated statistics throughout the match.
		/// </summary>
		public g_PlayerStats stats { get; set; }
		public int number { get; set; }
		public int level { get; set; }
		/// <summary>
		/// Boolean of player's stunned status.
		/// </summary>
		public bool stunned { get; set; }
		public int ping { get; set; }
		/// <summary>
		/// Boolean of the player's invulnerability after being stunned.
		/// </summary>
		public bool invulnerable { get; set; }
		public g_Transform head;
		/// <summary>
		/// Boolean determining whether or not this player has or had possession of the disc.
		/// possession will remain true until someone else grabs the disc or for 7 seconds (maybe?)
		/// </summary>
		public bool possession { get; set; }
		public g_Transform body;
		/// <summary>
		/// Left hand position and rotation
		/// </summary>
		public g_Transform lhand { get; set; }
		public bool blocking { get; set; }
		/// <summary>
		/// A 3 element list of floats representing the player's velocity.
		/// < X, Y, Z >
		/// </summary>
		public List<float> velocity { get; set; }
	}

	/// <summary>
	/// Object for position and rotation
	/// </summary>
	public class g_Transform
	{
		public Vector3 Position {
			get {
				if (pos != null) return pos.ToVector3();
				else if (position != null) return position.ToVector3();
				else throw new NullReferenceException("Neither pos nor position are set");
			}
		}
		/// <summary>
		/// Don't get this value. Use Position property instead
		/// </summary>
		public List<float> pos { get; set; }
		/// <summary>
		/// Don't get this value. Use Position property instead
		/// </summary>
		public List<float> position { get; set; }
		public List<float> forward;
		public List<float> left;
		public List<float> up;
	}

	/// <summary>
	/// Object containing the player's stats in the match.
	/// </summary>
	public class g_PlayerStats
	{
		public float possession_time { get; set; }
		public int points { get; set; }
		public int saves { get; set; }
		public int goals { get; set; }
		public int stuns { get; set; }
		public int passes { get; set; }
		public int catches { get; set; }
		public int steals { get; set; }
		public int blocks { get; set; }
		public int interceptions { get; set; }
		public int assists { get; set; }
		public int shots_taken { get; set; }

		public static g_PlayerStats operator +(g_PlayerStats a, g_PlayerStats b)
		{
			g_PlayerStats pStats = new g_PlayerStats();
			pStats.possession_time = a.possession_time + b.possession_time;
			pStats.points = a.points + b.points;
			pStats.passes = a.passes + b.passes;
			pStats.catches = a.catches + b.catches;
			pStats.steals = a.steals + b.steals;
			pStats.stuns = a.stuns + b.stuns;
			pStats.blocks = a.blocks + b.blocks;
			pStats.interceptions = a.blocks + b.interceptions;
			pStats.assists = a.assists + b.assists;
			pStats.saves = a.saves + b.assists;
			pStats.goals = a.goals + b.goals;
			pStats.shots_taken = a.shots_taken + b.shots_taken;
			return pStats;
		}
	}

	/// <summary>
	/// Object containing the total statistics for the entire team so far.
	/// </summary>
	public class g_TeamStats
	{
		public int points { get; set; }
		public float possession_time { get; set; }
		/// <summary>
		/// (Currently Broken in the API)
		/// </summary>
		public int interceptions { get; set; }
		/// <summary>
		/// (Currently Broken in the API)
		/// </summary>
		public int blocks { get; set; }
		public int steals { get; set; }
		/// <summary>
		/// (Currently Broken in the API)
		/// </summary>
		public int catches { get; set; }
		/// <summary>
		/// (Currently Broken in the API)
		/// </summary>
		public int passes { get; set; }
		public int saves { get; set; }
		public int goals { get; set; }
		public int stuns { get; set; }
		public int assists { get; set; }
		public int shots_taken { get; set; }
	}

	/// <summary>
	/// Object Containing basic team information and team stats
	/// </summary>
	public class g_Team
	{
		/// <summary>
		/// Enum declared for our own use.
		/// </summary>
		public enum TeamColor:byte { blue, orange, spectator };

		public List<g_Player> players { get; set; }
		/// <summary>
		/// Team name
		/// </summary>
		public string team { get; set; }
		public bool possession { get; set; }
		public g_TeamStats stats { get; set; }

		/// <summary>
		/// Not in the API, but add as soon as this frame is deserialized
		/// </summary>
		public TeamColor color { get; set; }
	}

	/// <summary>
	/// Object Containing basic relavant information on who scored last.
	/// </summary>
	public class g_Score
	{
		public float disc_speed { get; set; }
		public string team { get; set; }
		public string goal_type { get; set; }
		public int point_amount { get; set; }
		public float distance_thrown { get; set; }
		/// <summary>
		/// Name of person who scored last.
		/// </summary>
		public string person_scored { get; set; }
		/// <summary>
		/// Name of person who assisted in the resulting goal.
		/// </summary>
		public string assist_scored { get; set; }

		public override bool Equals(object o)
		{
			g_Score s = (g_Score)o;
			return
				//Math.Abs(s.disc_speed - disc_speed) < .01f &&
				s.team == team &&
				s.goal_type == goal_type &&
				s.point_amount == point_amount &&
				Math.Abs(s.distance_thrown - distance_thrown) < .01f &&
				s.person_scored == person_scored &&
				s.assist_scored == assist_scored;
		}

		public override int GetHashCode()
		{
			int hash = 17;
			hash = hash * 23 + disc_speed.GetHashCode();
			hash = hash * 23 + goal_type.GetHashCode();
			hash = hash * 23 + point_amount.GetHashCode();
			hash = hash * 23 + distance_thrown.GetHashCode();
			hash = hash * 23 + person_scored.GetHashCode();
			hash = hash * 23 + assist_scored.GetHashCode();
			return hash;
		}
	}

	public class g_Playspace
	{
		public float[] vr_left { get; set; }
		public float[] vr_position { get; set; }
		public float[] vr_forward { get; set; }
		public float[] vr_up { get; set; }

	}
}
