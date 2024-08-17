using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Spark
{
	[Serializable]
	public class TabletStats
	{
		public bool IsValid()
		{
			return player_name != null;
		}

		public static bool IsValid(JToken serverprofile)
		{
			int version = serverprofile["_version"] != null ? (int)serverprofile["_version"] : -1;
			if (version is > 0 and < 4)
			{
				Logger.LogRow(Logger.LogType.Error, $"Version of file is {version}");
			}

			return version > 0;
		}

		public TabletStats(string serverprofile)
		{
			try
			{
				if (string.IsNullOrEmpty(serverprofile)) return;

				raw = serverprofile;

				JToken data;
				try
				{
					data = JsonConvert.DeserializeObject<JToken>(serverprofile);
				}
				catch (Exception ex)
				{
					Logger.LogRow(Logger.LogType.Error, $"Error deserializing tablet stats file\n{serverprofile}\n{ex}");
					return;
				}

				if (data == null) return;
				if (!IsValid(data)) return;

				discord_id = DiscordOAuth.DiscordUserID;

				player_id = long.Parse(((string)data["xplatformid"])?.Split("-").Last() ?? "0");
				player_name = (string)data["displayname"];
				if (data["social"] != null)
				{
					ghosted_count = (int)data["social"]["ghostcount"];
					muted_count = (int)data["social"]["mutecount"];
				}

				update_time = (int)data["updatetime"];
				if (data.Contains("creationtime"))
				{
					creation_time = (int)data["creationtime"];
				}
				else if (data.Contains("createtime"))
				{
					creation_time = (int)data["createtime"];
				}

				if (data["purchasedcombat"] != null)
					purchased_combat = (int)data["purchasedcombat"];
				if (data["stats"]?["arena"] == null) return;
				level = (int)data["stats"]["arena"]["Level"]?["val"];
				if (level > 1)
				{
					highest_stuns = (int)(data["stats"]["arena"]["HighestStuns"]?["val"] ?? 0);
					goal_score_percentage = (float)(data["stats"]["arena"]["GoalScorePercentage"]?["val"] ?? 0);
					two_point_goals = (int)(data["stats"]["arena"]["TwoPointGoals"]?["val"] ?? 0);
					highest_saves = (int)(data["stats"]["arena"]["HighestSaves"]?["val"] ?? 0);
					avg_points_per_game = (float)(data["stats"]["arena"]["AveragePointsPerGame"]?["val"] ?? 0) / (int)(data["stats"]["arena"]["AveragePointsPerGame"]?["cnt"] ?? 0);
					stuns = (int)(data["stats"]["arena"]["Stuns"]?["val"] ?? 0);
					stun_percentage = (float)(data["stats"]["arena"]["StunPercentage"]?["val"] ?? 0);
					arena_wins = (int)(data["stats"]["arena"]["ArenaWins"]?["val"] ?? 0);
					arena_win_percentage = (float)(data["stats"]["arena"]["ArenaWinPercentage"]?["val"] ?? 0);
					shots_on_goal_against = (int)(data["stats"]["arena"]["ShotsOnGoalAgainst"]?["val"] ?? 0);
					shots_on_goal = (int)(data["stats"]["arena"]["ShotsOnGoal"]?["val"] ?? 0);
					hat_tricks = (int)(data["stats"]["arena"]["HatTricks"]?["val"] ?? 0);
					highest_points = (int)(data["stats"]["arena"]["HighestPoints"]?["val"] ?? 0);
					possession_time = (float)(data["stats"]["arena"]["PossessionTime"]?["val"] ?? 0);
					blocks = (int)(data["stats"]["arena"]["Blocks"]?["val"] ?? 0);
					bounce_goals = (int)(data["stats"]["arena"]["BounceGoals"]?["val"] ?? 0);
					stuns_per_game = (float)(data["stats"]["arena"]["StunsPerGame"]?["val"] ?? 0) / (int)(data["stats"]["arena"]["StunsPerGame"]?["cnt"] ?? 0);
					highest_arena_mvp_streak = (int)(data["stats"]["arena"]["HighestArenaMVPStreak"]?["val"] ?? 0);
					arena_ties = (int)(data["stats"]["arena"]["ArenaTies"]?["val"] ?? 0);
					saves_per_game = (float)(data["stats"]["arena"]["SavesPerGame"]?["val"] ?? 0) / (int)(data["stats"]["arena"]["SavesPerGame"]?["cnt"] ?? 0);
					catches = (int)(data["stats"]["arena"]["Catches"]?["val"] ?? 0);
					goal_save_percentage = (float)(data["stats"]["arena"]["GoalSavePercentage"]?["val"] ?? 0);
					goals_per_game = (float)(data["stats"]["arena"]["GoalsPerGame"]?["val"] ?? 0) / (int)(data["stats"]["arena"]["GoalsPerGame"]?["cnt"] ?? 0);
					current_arena_mvp_streak = (int)(data["stats"]["arena"]["CurrentArenaMVPStreak"]?["val"] ?? 0);
					jousts_won = (int)(data["stats"]["arena"]["JoustsWon"]?["val"] ?? 0);
					passes = (int)(data["stats"]["arena"]["Passes"]?["val"] ?? 0);
					three_point_goals = (int)(data["stats"]["arena"]["ThreePointGoals"]?["val"] ?? 0);
					assists_per_game = (float)(data["stats"]["arena"]["AssistsPerGame"]?["val"] ?? 0) / (int)(data["stats"]["arena"]["AssistsPerGame"]?["cnt"] ?? 0);
					current_arena_win_streak = (int)(data["stats"]["arena"]["CurrentArenaWinStreak"]?["val"] ?? 0);
					assists = (int)(data["stats"]["arena"]["Assists"]?["val"] ?? 0);
					clears = (int)(data["stats"]["arena"]["Clears"]?["val"] ?? 0);
					average_top_speed_per_game = (float)(data["stats"]["arena"]["AverageTopSpeedPerGame"]?["val"] ?? 0) / (int)(data["stats"]["arena"]["AverageTopSpeedPerGame"]?["cnt"] ?? 0);
					average_possession_time_per_game = (float)(data["stats"]["arena"]["AveragePossessionTimePerGame"]?["val"] ?? 0) / (int)(data["stats"]["arena"]["AveragePossessionTimePerGame"]?["cnt"] ?? 0);
					arena_losses = (int)(data["stats"]["arena"]["ArenaLosses"]?["val"] ?? 0);
					top_speeds_total = (float)(data["stats"]["arena"]["TopSpeedsTotal"]?["val"] ?? 0);
					arena_mvps = (int)(data["stats"]["arena"]["ArenaMVPs"]?["val"] ?? 0);
					arena_mvp_percentage = (float)(data["stats"]["arena"]["ArenaMVPPercentage"]?["val"] ?? 0);
					punches_recieved = (int)(data["stats"]["arena"]["PunchesReceived"]?["val"] ?? 0);
					block_percentage = (float)(data["stats"]["arena"]["BlockPercentage"]?["val"] ?? 0);
					points = (int)(data["stats"]["arena"]["Points"]?["val"] ?? 0);
					saves = (int)(data["stats"]["arena"]["Saves"]?["val"] ?? 0);
					interceptions = (int)(data["stats"]["arena"]["Interceptions"]?["val"] ?? 0);
					goals = (int)(data["stats"]["arena"]["Goals"]?["val"] ?? 0);
					steals = (int)(data["stats"]["arena"]["Steals"]?["val"] ?? 0);
				}
			}
			catch (Exception e)
			{
				Logger.LogRow(Logger.LogType.Error, "Error in TabletStats constructor\n" + e);
				player_name = null;
				return;
			}
		}

		public string discord_id;
		public string raw;

		public long player_id;
		public string player_name;
		public int ghosted_count;
		public int muted_count;
		public int update_time;
		public int creation_time;
		public int purchased_combat;
		public int highest_stuns;
		public int level;
		public float goal_score_percentage;
		public int two_point_goals;
		public int highest_saves;
		public float avg_points_per_game;
		public int stuns;
		public float stun_percentage;
		public int arena_wins;
		public float arena_win_percentage;
		public int shots_on_goal_against;
		public int shots_on_goal;
		public int hat_tricks;
		public int highest_points;
		public float possession_time;
		public int blocks;
		public int bounce_goals;
		public float stuns_per_game;
		public int highest_arena_mvp_streak;
		public int arena_ties;
		public float saves_per_game;
		public int catches;
		public float goal_save_percentage;
		public float goals_per_game;
		public int current_arena_mvp_streak;
		public int jousts_won;
		public int passes;
		public int three_point_goals;
		public float assists_per_game;
		public int current_arena_win_streak;
		public int assists;
		public int clears;
		public float average_top_speed_per_game;
		public float average_possession_time_per_game;
		public int arena_losses;
		public float top_speeds_total;
		public int arena_mvps;
		public float arena_mvp_percentage;
		public int punches_recieved;
		public float block_percentage;
		public int points;
		public int saves;
		public int interceptions;
		public int goals;
		public int steals;
	}
}