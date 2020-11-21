using System;
using System.Collections.Generic;
using static IgniteBot2.g_Team;
using System.Numerics;
using System.Linq;

namespace IgniteBot2
{
	/// <summary>
	/// Object containing all match data.
	/// </summary>
	public class MatchData : DataContainer
	{
		public Dictionary<TeamColor, TeamData> teams = new Dictionary<TeamColor, TeamData>();

		/// <summary>
		/// Return new list of all players currently in the match.
		/// Doesn't seem that efficient. Don't use that often
		/// </summary>
		public List<MatchPlayer> AllPlayers {
			get {
				var list = new List<MatchPlayer>();
				foreach (TeamData team in teams.Values.ToList())
				{
					list.AddRange(team.players.Values.ToList());
				}
				return list;
			}
		}

		public List<GoalData> Goals { get; set; } = new List<GoalData>();
		public List<EventData> Events { get; set; } = new List<EventData>();
		public List<ThrowData> Throws { get; set; } = new List<ThrowData>();

		public List<Vector3> currentDiskTrajectory = new List<Vector3>();

		public g_Instance firstFrame;
		public string SessionId { get => firstFrame.sessionid; }
		public string ServerIP { get; set; }
		public string ServerLocation { get; set; }

		/// <summary>
		/// enum of all possible ways a game could have ended.
		/// </summary>
		public enum FinishReason
		{
			not_finished,
			game_time,
			mercy,
			reset,
			score_in_ot
		}

		public DateTime matchTime;
		public float startTime = 0;
		public float endTime = 0;
		public int overtimeCount = 0;
		public int round = 0;
		public FinishReason finishReason = FinishReason.not_finished;
		/// <summary>
		/// Get match time in UTC format for SQL usage.
		/// </summary>
		public string MatchTimeSQL {
			get {
				return matchTime.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss");
			}
		}

		/// <summary>
		/// Constructor used to initialize match data. 
		/// </summary>
		/// <param name="firstFrame"></param>
		public MatchData(g_Instance firstFrame)
		{
			this.firstFrame = firstFrame;
			matchTime = firstFrame.recorded_time;
			if (matchTime == null || matchTime == DateTime.MinValue)
			{
				matchTime = DateTime.Now;
			}

			teams = new Dictionary<TeamColor, TeamData> {
				{ TeamColor.blue, new TeamData(TeamColor.blue, firstFrame.teams[0].team) },
				{ TeamColor.orange, new TeamData(TeamColor.orange, firstFrame.teams[1].team) },
				{ TeamColor.spectator, new TeamData(TeamColor.spectator, firstFrame.teams[2].team) },
			};
			ServerIP = firstFrame.sessionip;

			//_ = InitializeInDatabase();
		}

		/// <summary>
		/// Fetches a player from this match
		/// </summary>
		/// <param name="team">The team that the player belongs to.</param>
		/// <param name="player">The player</param>
		/// <returns>The PlayerData about the requested player from this match.</returns>
		public MatchPlayer GetPlayerData(g_Team team, g_Player player)
		{
			if (!teams.ContainsKey(team.color) || !teams[team.color].players.ContainsKey(player.userid))
			{
				Console.WriteLine("Player not found");
				return null;
			}
			return teams[team.color].players[player.userid];
		}

		/// <summary>
		/// Fetches a player from this match
		/// </summary>
		/// <param name="player">The player</param>
		/// <returns>The PlayerData about the requested player from this match.</returns>
		public MatchPlayer GetPlayerData(g_Player player)
		{
			foreach (var team in teams)
			{
				if (team.Value.players.ContainsKey(player.userid))
				{
					return team.Value.players[player.userid];
				}
			}

			Console.WriteLine("Player not found");  // TODO this happens a lot
			return null;
		}
		/// <summary>
		/// Function to transform match data into the desired format for firestore.
		/// </summary>
		/// <returns></returns>
		public Dictionary<string, object> ToDict()
		{
			var values = new Dictionary<string, object>
			{
				{ "session_id", firstFrame.sessionid },
				{ "match_time", MatchTimeSQL },
				{ "round", round },	// TODO some way of discovering this?
				{ "private", firstFrame.private_match },
				{ "client_name", firstFrame.client_name },
				{ "hw_id", Logger.MacAddr },
				{ "version", GetType().Assembly.GetName().Version.ToString() },
				{ "ip", firstFrame.sessionip },
				{ "blue_team_name", teams[TeamColor.blue].teamName },
				{ "orange_team_name", teams[TeamColor.orange].teamName },
				{ "game_clock_start", firstFrame.game_clock },
				{ "blue_team_score", teams[TeamColor.blue].points },
				{ "orange_team_score", teams[TeamColor.orange].points },
				{ "winning_team", teams[TeamColor.blue].points > teams[TeamColor.orange].points ? TeamColor.blue.ToString() : TeamColor.orange.ToString() },
				{ "game_clock_end", endTime },	// TODO change value when reset or overtime
				{ "overtime_count", overtimeCount },	// TODO increment this
				{ "finish_reason", finishReason.ToString() },
				{ "custom_id", Program.customId },
				{ "disabled", false }
			};

			return values;
		}

		public int SumOfStats()
		{
			int sum = 0;
			foreach (var player in AllPlayers)
			{
				sum = player.Stuns + player.ShotsTaken + player.Points + (int)player.PossessionTime;
			}
			return sum;
		}

	}

}
