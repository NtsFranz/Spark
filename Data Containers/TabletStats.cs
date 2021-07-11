using System;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Spark
{
	[Serializable]
	public class TabletStats
	{
		public static bool IsValid(JToken serverprofile)
		{
			int version = serverprofile["_version"] != null ? (int) serverprofile["_version"] : -1;
			if (version is > 0 and < 4)
			{
				Logger.LogRow(Logger.LogType.Error, $"Version of file is {version}");
			}
			return version > 0;			
		}
		
		public TabletStats(JToken serverprofile)
		{
			if (!IsValid(serverprofile)) return;

			discord_id = DiscordOAuth.DiscordUserID;
			
			player_id = long.Parse(((string) serverprofile["xplatformid"])?.Split("-").Last() ?? "0");
			player_name = (string) serverprofile["displayname"];
			if (serverprofile["social"] != null)
			{
				ghosted_count = (int) serverprofile["social"]["ghostcount"];
				muted_count = (int) serverprofile["social"]["mutecount"];
			}

			update_time = (int) serverprofile["updatetime"];
			creation_time = (int) serverprofile["creationtime"];
			if (serverprofile["purchasedcombat"] != null)
				purchased_combat = (int) serverprofile["purchasedcombat"];
			if (serverprofile["stats"]["arena"] == null) return;
			level = (int) serverprofile["stats"]["arena"]["Level"]?["val"];
			if (level > 1)
			{
				highest_stuns = (int) (serverprofile["stats"]["arena"]["HighestStuns"]?["val"] ?? 0);
				goal_score_percentage = (float) (serverprofile["stats"]["arena"]["GoalScorePercentage"]?["val"] ?? 0);
				two_point_goals = (int) (serverprofile["stats"]["arena"]["TwoPointGoals"]?["val"] ?? 0);
				highest_saves = (int) (serverprofile["stats"]["arena"]["HighestSaves"]?["val"] ?? 0);
				avg_points_per_game =  (float) (serverprofile["stats"]["arena"]["AveragePointsPerGame"]?["val"] ?? 0) / (int) (serverprofile["stats"]["arena"]["AveragePointsPerGame"]?["cnt"] ?? 0);
				stuns = (int) (serverprofile["stats"]["arena"]["Stuns"]?["val"] ?? 0);
				stun_percentage = (float) (serverprofile["stats"]["arena"]["StunPercentage"]?["val"] ?? 0);
				arena_wins = (int) (serverprofile["stats"]["arena"]["ArenaWins"]?["val"] ?? 0);
				arena_win_percentage = (float) (serverprofile["stats"]["arena"]["ArenaWinPercentage"]?["val"] ?? 0);
				shots_on_goal_against = (int) (serverprofile["stats"]["arena"]["ShotsOnGoalAgainst"]?["val"] ?? 0);
				shots_on_goal = (int) (serverprofile["stats"]["arena"]["ShotsOnGoal"]?["val"] ?? 0);
				hat_tricks = (int) (serverprofile["stats"]["arena"]["HatTricks"]?["val"] ?? 0);
				highest_points = (int) (serverprofile["stats"]["arena"]["HighestPoints"]?["val"] ?? 0);
				possession_time = (float) (serverprofile["stats"]["arena"]["PossessionTime"]?["val"] ?? 0);
				blocks = (int) (serverprofile["stats"]["arena"]["Blocks"]?["val"] ?? 0);
				bounce_goals = (int) (serverprofile["stats"]["arena"]["BounceGoals"]?["val"] ?? 0);
				stuns_per_game = (float) (serverprofile["stats"]["arena"]["StunsPerGame"]?["val"] ?? 0) / (int) (serverprofile["stats"]["arena"]["StunsPerGame"]?["cnt"] ?? 0);
				highest_arena_mvp_streak = (int) (serverprofile["stats"]["arena"]["HighestArenaMVPStreak"]?["val"] ?? 0);
				arena_ties = (int) (serverprofile["stats"]["arena"]["ArenaTies"]?["val"] ?? 0);
				saves_per_game = (float) (serverprofile["stats"]["arena"]["SavesPerGame"]?["val"] ?? 0) / (int) (serverprofile["stats"]["arena"]["SavesPerGame"]?["cnt"] ?? 0);
				catches = (int) (serverprofile["stats"]["arena"]["Catches"]?["val"] ?? 0);
				goal_save_percentage = (float) (serverprofile["stats"]["arena"]["GoalSavePercentage"]?["val"] ?? 0);
				goals_per_game = (float) (serverprofile["stats"]["arena"]["GoalsPerGame"]?["val"] ?? 0) / (int) (serverprofile["stats"]["arena"]["GoalsPerGame"]?["cnt"] ?? 0);
				current_arena_mvp_streak = (int) (serverprofile["stats"]["arena"]["CurrentArenaMVPStreak"]?["val"] ?? 0);
				jousts_won = (int) (serverprofile["stats"]["arena"]["JoustsWon"]?["val"] ?? 0);
				passes = (int) (serverprofile["stats"]["arena"]["Passes"]?["val"] ?? 0);
				three_point_goals = (int) (serverprofile["stats"]["arena"]["ThreePointGoals"]?["val"] ?? 0);
				assists_per_game = (float) (serverprofile["stats"]["arena"]["AssistsPerGame"]?["val"] ?? 0) / (int) (serverprofile["stats"]["arena"]["AssistsPerGame"]?["cnt"] ?? 0);
				current_arena_win_streak = (int) (serverprofile["stats"]["arena"]["CurrentArenaWinStreak"]?["val"] ?? 0);
				assists = (int) (serverprofile["stats"]["arena"]["Assists"]?["val"] ?? 0);
				clears = (int) (serverprofile["stats"]["arena"]["Clears"]?["val"] ?? 0);
				average_top_speed_per_game = (float) (serverprofile["stats"]["arena"]["AverageTopSpeedPerGame"]?["val"] ?? 0) / (int) (serverprofile["stats"]["arena"]["AverageTopSpeedPerGame"]?["cnt"] ?? 0);
				average_possession_time_per_game = (float) (serverprofile["stats"]["arena"]["AveragePossessionTimePerGame"]?["val"] ?? 0) / (int) (serverprofile["stats"]["arena"]["AveragePossessionTimePerGame"]?["cnt"] ?? 0);
				arena_losses = (int) (serverprofile["stats"]["arena"]["ArenaLosses"]?["val"] ?? 0);
				top_speeds_total = (float) (serverprofile["stats"]["arena"]["TopSpeedsTotal"]?["val"] ?? 0);
				arena_mvps = (int) (serverprofile["stats"]["arena"]["ArenaMVPs"]?["val"] ?? 0);
				arena_mvp_percentage = (float) (serverprofile["stats"]["arena"]["ArenaMVPPercentage"]?["val"] ?? 0);
				punches_recieved = (int) (serverprofile["stats"]["arena"]["PunchesReceived"]?["val"] ?? 0);
				block_percentage = (float) (serverprofile["stats"]["arena"]["BlockPercentage"]?["val"] ?? 0);
				points = (int) (serverprofile["stats"]["arena"]["Points"]?["val"] ?? 0);
				saves = (int) (serverprofile["stats"]["arena"]["Saves"]?["val"] ?? 0);
				interceptions = (int) (serverprofile["stats"]["arena"]["Interceptions"]?["val"] ?? 0);
				goals = (int) (serverprofile["stats"]["arena"]["Goals"]?["val"] ?? 0);
				steals = (int) (serverprofile["stats"]["arena"]["Steals"]?["val"] ?? 0);
			}
		}

		public string discord_id;
		
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