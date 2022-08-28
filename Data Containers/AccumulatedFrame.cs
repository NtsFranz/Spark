using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using EchoVRAPI;

namespace Spark
{
	public class AccumulatedFrame
	{
		public readonly Frame frame;
		public readonly AccumulatedFrame lastRound;

		public float TotalPossessionTime => players.Values.Select(p => p.PossessionTime).Sum();

		public readonly Dictionary<long, MatchPlayer> players;

		public readonly Dictionary<Team.TeamColor, TeamData> teams = new Dictionary<Team.TeamColor, TeamData>
		{
			{ Team.TeamColor.blue, new TeamData() },
			{ Team.TeamColor.orange, new TeamData() },
			{ Team.TeamColor.spectator, new TeamData() },
		};

		public readonly ConcurrentQueue<GoalData> goals = new ConcurrentQueue<GoalData>();
		public readonly ConcurrentQueue<EventData> events = new ConcurrentQueue<EventData>();
		public readonly ConcurrentQueue<ThrowData> throws = new ConcurrentQueue<ThrowData>();

		public string serverLocationResponse;
		public string serverLocation;
		public float serverScore;
		public float smoothedServerScore;

		public List<Vector3> currentDiskTrajectory = new List<Vector3>();

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
		public FinishReason finishReason = FinishReason.not_finished;

		/// <summary>
		/// Get match time in UTC format for SQL usage.
		/// </summary>
		public string MatchTimeSQL => matchTime.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss");

		public AccumulatedFrame(Frame baseFrame, AccumulatedFrame lastRound = null)
		{
			frame = baseFrame;
			this.lastRound = lastRound;
			players = new Dictionary<long, MatchPlayer>();
			matchTime = baseFrame.recorded_time;
			if (matchTime == DateTime.MinValue)
			{
				matchTime = DateTime.UtcNow;
			}

			if (!string.IsNullOrWhiteSpace(frame.client_name) && frame.client_name != "anonymous")
			{
				SparkSettings.instance.client_name = frame.client_name;
			}

			teams[Team.TeamColor.blue].FindTeamNamesFromPlayerList(frame.teams[0]);
			teams[Team.TeamColor.orange].FindTeamNamesFromPlayerList(frame.teams[1]);

			serverLocation = lastRound?.serverLocation;

			if (lastRound != null)
			{
				// Loop through teams.
				foreach (Team team in frame.teams)
				{
					// Loop through players on team.
					foreach (Player player in team.players)
					{
						MatchPlayer oldPlayer = lastRound.GetPlayerData(player);
						if (oldPlayer != null)
						{
							// make a fresh player
							MatchPlayer newPlayer = new MatchPlayer(this, player);

							// if stats didn't get reset
							if (player.stats.Sum() >= oldPlayer.currentStats.Sum())
							{
								newPlayer.oldRoundStats += player.stats;
							}
							else
							{
								Logger.Error("Skipped assigning old round stats");
							}

							newPlayer.currentStats = player.stats;
							players.Add(player.userid, newPlayer);
						}
					}
				}
			}
		}


		/// <summary>
		/// Call this once per frame to add new data
		/// </summary>
		/// <param name="newFrame">The new frame to add</param>
		public void Accumulate(Frame newFrame, Frame lastFrame)
		{
			// these things shouldn't change during a round
			// frame.sessionid = newFrame.sessionid;
			// frame.sessionip = newFrame.sessionip;
			// frame.match_type = newFrame.match_type;
			// frame.map_name = newFrame.map_name;
			// frame.private_match = newFrame.private_match;
			// frame.tournament_match = newFrame.tournament_match;
			// frame.client_name = newFrame.client_name;

			frame.blue_points = newFrame.blue_points;
			frame.orange_points = newFrame.orange_points;
			frame.blue_round_score = newFrame.blue_round_score;
			frame.orange_round_score = newFrame.orange_round_score;
			frame.total_round_count = newFrame.total_round_count;
			for (int i = 0; i < 3; i++)
			{
				frame.teams[i].team = newFrame.teams[i].team;
			}

			foreach (Team team in newFrame.teams)
			{
				foreach (Player player in team.players)
				{
					if (!players.ContainsKey(player.userid))
					{
						players.Add(player.userid, new MatchPlayer(this, player));
					}

					if (player.stats == null)
					{
						Logger.LogRow(Logger.LogType.Error, "Player stats are null. Maybe in lobby?");
						return;
					}

					players[player.userid].Accumulate(newFrame, player, lastFrame);
				}
			}
		}

		public MatchPlayer GetPlayerData(long userid)
		{
			if (players.ContainsKey(userid))
			{
				return players[userid];
			}

			// Logger.LogRow(Logger.LogType.Error, $"Player not found: {userid}"); // TODO this happens a lot
			return null;
		}

		public MatchPlayer GetPlayerData(Player player)
		{
			return GetPlayerData(player.userid);
		}


		/// <summary>
		/// Function to transform match data into the desired format for firestore.
		/// </summary>
		/// <returns></returns>
		public Dictionary<string, object> ToDict()
		{
			Dictionary<string, object> values = new Dictionary<string, object>
			{
				{ "session_id", frame.sessionid },
				{ "match_time", MatchTimeSQL },
				{ "round", frame.blue_round_score + frame.orange_round_score },
				{ "private", frame.private_match },
				{ "client_name", frame.client_name },
				{ "hw_id", Logger.DeviceId },
				{ "version", Program.AppVersionString() },
				{ "ip", frame.sessionip },
				{ "blue_team_name", frame.teams[0].team },
				{ "orange_team_name", frame.teams[1].team },
				{ "game_clock_start", frame.game_clock },
				{ "blue_team_score", frame.blue_points },
				{ "orange_team_score", frame.orange_points },
				{ "winning_team", frame.blue_points > frame.orange_points ? Team.TeamColor.blue.ToString() : Team.TeamColor.orange.ToString() },
				{ "game_clock_end", endTime }, // TODO change value when reset or overtime
				{ "overtime_count", overtimeCount },
				{ "finish_reason", finishReason.ToString() },
				{ "disabled", false },
				{ "discord_userid", DiscordOAuth.DiscordUserID },
			};

			return values;
		}
	}

	public static class StatsExtensions
	{
		public static float Sum(this Stats stats)
		{
			return
				stats.possession_time +
				stats.points +
				stats.passes +
				stats.catches +
				stats.steals +
				stats.stuns +
				stats.blocks +
				stats.interceptions +
				stats.assists +
				stats.saves +
				stats.goals +
				stats.shots_taken;
		}
	}
}