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
				highest_stuns = (int) serverprofile["stats"]["arena"]["HighestStuns"]?["val"];
				goal_score_percentage = (float) serverprofile["stats"]["arena"]["GoalScorePercentage"]?["val"];
				two_point_goals = (int) serverprofile["stats"]["arena"]["TwoPointGoals"]?["val"];
				highest_saves = (int) serverprofile["stats"]["arena"]["HighestSaves"]?["val"];
				avg_points_per_game = (float) serverprofile["stats"]["arena"]["AveragePointsPerGame"]?["val"] / (int) serverprofile["stats"]["arena"]["AveragePointsPerGame"]?["cnt"];
				stuns = (int) serverprofile["stats"]["arena"]["Stuns"]?["val"];
				stun_percentage = (float) serverprofile["stats"]["arena"]["StunPercentage"]?["val"];
				arena_wins = (int) serverprofile["stats"]["arena"]["ArenaWins"]?["val"];
				arena_win_percentage = (float) serverprofile["stats"]["arena"]["ArenaWinPercentage"]?["val"];
				shots_on_goal_against = (int) serverprofile["stats"]["arena"]["ShotsOnGoalAgainst"]?["val"];
				shots_on_goal = (int) serverprofile["stats"]["arena"]["ShotsOnGoal"]?["val"];
				hat_tricks = (int) serverprofile["stats"]["arena"]["HatTricks"]?["val"];
				highest_points = (int) serverprofile["stats"]["arena"]["HighestPoints"]?["val"];
				possession_time = (float) serverprofile["stats"]["arena"]["PossessionTime"]?["val"];
				blocks = (int) serverprofile["stats"]["arena"]["Blocks"]?["val"];
				bounce_goals = (int) serverprofile["stats"]["arena"]["BounceGoals"]?["val"];
				stuns_per_game = (float) serverprofile["stats"]["arena"]["StunsPerGame"]?["val"] / (int) serverprofile["stats"]["arena"]["StunsPerGame"]?["cnt"];
				highest_arena_mvp_streak = (int) serverprofile["stats"]["arena"]["HighestArenaMVPStreak"]?["val"];
				arena_ties = (int) serverprofile["stats"]["arena"]["ArenaTies"]?["val"];
				saves_per_game = (float) serverprofile["stats"]["arena"]["SavesPerGame"]?["val"] / (int) serverprofile["stats"]["arena"]["SavesPerGame"]?["cnt"];
				catches = (int) serverprofile["stats"]["arena"]["Catches"]?["val"];
				goal_save_percentage = (float) serverprofile["stats"]["arena"]["GoalSavePercentage"]?["val"];
				goals_per_game = (float) serverprofile["stats"]["arena"]["GoalsPerGame"]?["val"] / (int) serverprofile["stats"]["arena"]["GoalsPerGame"]?["cnt"];
				current_arena_mvp_streak = (int) serverprofile["stats"]["arena"]["CurrentArenaMVPStreak"]?["val"];
				jousts_won = (int) serverprofile["stats"]["arena"]["JoustsWon"]?["val"];
				passes = (int) serverprofile["stats"]["arena"]["Passes"]?["val"];
				three_point_goals = (int) serverprofile["stats"]["arena"]["ThreePointGoals"]?["val"];
				assists_per_game = (float) serverprofile["stats"]["arena"]["AssistsPerGame"]?["val"] / (int) serverprofile["stats"]["arena"]["AssistsPerGame"]?["cnt"];
				current_arena_win_streak = (int) serverprofile["stats"]["arena"]["CurrentArenaWinStreak"]?["val"];
				assists = (int) serverprofile["stats"]["arena"]["Assists"]?["val"];
				clears = (int) serverprofile["stats"]["arena"]["Clears"]?["val"];
				average_top_speed_per_game = (float) serverprofile["stats"]["arena"]["AverageTopSpeedPerGame"]?["val"] / (int) serverprofile["stats"]["arena"]["AverageTopSpeedPerGame"]?["cnt"];
				average_possession_time_per_game = (float) serverprofile["stats"]["arena"]["AveragePossessionTimePerGame"]?["val"] / (int) serverprofile["stats"]["arena"]["AveragePossessionTimePerGame"]?["cnt"];
				arena_losses = (int) serverprofile["stats"]["arena"]["ArenaLosses"]?["val"];
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