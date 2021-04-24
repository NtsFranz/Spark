using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Spark
{
	/// <summary>
	/// The idea is this will contain the entire set of data for the match, and will be used to review and then commit the data all at once 
	/// rather than little by little like before.
	/// 
	/// PlayerData represents the stats for a player only for a particular match
	/// </summary>
	public class MatchPlayer : DataContainer
	{
		public MatchPlayer(MatchData match, TeamData team, g_Player player)
		{
			matchData = match;
			teamData = team;
			Id = player.userid;
			Name = player.name;
			playspaceLocation = player.head.Position;
			PlayspaceAbuses = 0;
		}

		/// <summary>
		/// Function to transform player data into the desired format for firestore.
		/// </summary>
		/// <returns></returns>
		public Dictionary<string, object> ToDict()
		{
			Dictionary<string, object> values = new Dictionary<string, object>
			{
				{"session_id", matchData.firstFrame.sessionid},
				{"match_time", matchData.MatchTimeSQL },
				{"player_id", Id },
				{"player_name", Name },
				{"level", Level },
				{"player_number", Number },
				{"team_color", teamData.teamColor.ToString() },
				{"possession_time", PossessionTime},
				{"play_time", PlayTime},
				{"inverted_time", InvertedTime},
				{"points", Points},
				{"2_pointers", TwoPointers },
				{"3_pointers", ThreePointers },
				{"shots_taken", ShotsTaken },
				{"saves", Saves },
				{"goals", GoalsNum },
				{"stuns", Stuns },
				{"passes", Passes },
				{"catches", Catches },
				{"steals", Steals },
				{"blocks", Blocks },
				{"interceptions", Interceptions },
				{"assists", Assists },
				// {"turnovers", Turnovers },	// TODO enable once the db supports it
				{"average_speed", averageSpeed[0] },
				{"average_speed_lhand", averageSpeed[1] },
				{"average_speed_rhand", averageSpeed[2] },
				{"wingspan", DistanceBetweenHands },
				{"playspace_abuses", PlayspaceAbuses },
				{"wins", Won }
			};

			return values;
		}

		#region Get/Set Methods
		public long Id { get; set; }

		public string Name { get; set; }
		public int Level { get; set; }
		public int Number { get; set; }

		private g_PlayerStats currentStats = new g_PlayerStats();
		private g_PlayerStats cachedStats = new g_PlayerStats();
		private g_PlayerStats oldRoundStats = new g_PlayerStats();

		public float PossessionTime {
			get => cachedStats.possession_time + currentStats.possession_time - oldRoundStats.possession_time;
			set => currentStats.possession_time = value;
		}

		public float PlayTime { get; set; }
		public float InvertedTime { get; set; }

		public int Points {
			get => cachedStats.points + currentStats.points - oldRoundStats.points;
			set => currentStats.points = value;
		}

		public int ShotsTaken {
			get => cachedStats.shots_taken + currentStats.shots_taken - oldRoundStats.shots_taken;
			set => currentStats.shots_taken = value;
		}

		public int Saves {
			get => cachedStats.saves + currentStats.saves - oldRoundStats.saves;
			set => currentStats.saves = value;
		}

		public int GoalsNum { get; set; }
		public int TwoPointers { get; set; }
		public int ThreePointers { get; set; }

		public int Passes { get; set; }

		public int Catches { get; set; }

		public int Steals {
			get => cachedStats.steals + currentStats.steals - oldRoundStats.steals;
			set => currentStats.steals = value;
		}

		public int Stuns {
			get => cachedStats.stuns + currentStats.stuns - oldRoundStats.stuns;
			set => currentStats.stuns = value;
		}

		public int Blocks {
			get => cachedStats.blocks + currentStats.blocks - oldRoundStats.blocks;
			set => currentStats.blocks = value;
		}

		public int Interceptions {
			get => cachedStats.interceptions + currentStats.interceptions - oldRoundStats.interceptions;
			set => currentStats.interceptions = value;
		}

		public int Assists {
			get => cachedStats.assists + currentStats.assists - oldRoundStats.assists;
			set => currentStats.assists = value;
		}

		public float DistanceBetweenHands {
			get {
				distanceBetweenHands.Sort();
				if (distanceBetweenHands.Count > 100)
				{
					return distanceBetweenHands[(int)((distanceBetweenHands.Count - 1) * (99f / 100))];
				}

				if (distanceBetweenHands.Count > 0)
				{
					return distanceBetweenHands.Last();
				}

				return 0;
			}
		}
		public int Won { get; set; }
		public int Turnovers { get; set; }
		public MatchData matchData;
		public TeamData teamData;
		/// <summary>
		/// The location of the playspace within the arena. This is not the position of the player within the playspace
		/// </summary>
		public Vector3 playspaceLocation;
		public DateTime lastAbuse = DateTime.Now;
		public TimeSpan playspaceInvincibility = TimeSpan.Zero;
		public readonly TimeSpan joinPlayspaceInvincibility = TimeSpan.FromSeconds(5);
		public int PlayspaceAbuses { get; set; }

		// head, lhand, rhand
		public float[] averageSpeed = { 0, 0, 0 };

		/// <summary>
		/// Positions every 1s of game time
		/// </summary>
		public List<Vector3> sparsePositions = new List<Vector3>();

		public List<float> distanceBetweenHands = new List<float>();

		// three values for head, lhand, rhand
		float[] avgSpeedTotal = { 0, 0, 0 };
		int[] avgSpeedCount = { 0, 0, 0 };

		public void UpdateAverageSpeed(float newSpeed)
		{
			avgSpeedTotal[0] += newSpeed;
			averageSpeed[0] = (avgSpeedTotal[0]) / ++(avgSpeedCount[0]);
		}

		public void UpdateAverageSpeedLHand(float newSpeed)
		{
			avgSpeedTotal[1] += newSpeed;
			averageSpeed[1] = (avgSpeedTotal[1]) / ++(avgSpeedCount[1]);
		}

		public void UpdateAverageSpeedRHand(float newSpeed)
		{
			avgSpeedTotal[2] += newSpeed;
			averageSpeed[2] = (avgSpeedTotal[2]) / ++(avgSpeedCount[2]);
		}

		public bool exitedTube;

		public static float boostVelCutoff = 20;
		public static float boostVelStopCutoff = 10;
		public List<float> recentVelocities = new List<float>();
		public void AddRecentVelocity(float vel)
		{
			recentVelocities.Add(vel);
			// anything older than 10s at 60hz
			if (recentVelocities.Count > Program.StatsHz * 10)
			{
				recentVelocities.RemoveAt(0);
			}
		}
		public (float, float) GetMaxRecentVelocity(bool reset = false)
		{
			int index = (int)(.99f * (recentVelocities.Count - 1));
			List<float> sortedVelocities = new List<float>(recentVelocities);
			sortedVelocities.Sort();
			float maxVel = sortedVelocities[index];
			int maxVelIndex = recentVelocities.IndexOf(maxVel);
			if (maxVel < boostVelCutoff)
			{
				var asdf = 1;
			}
			if (reset)
			{
				recentVelocities.Clear();
			}
			return (maxVel, (float)(recentVelocities.Count - maxVelIndex) / Program.StatsHz);
		}
		public float GetSmoothedVelocity(float smoothTime = 1)
		{
			int N = (int)(smoothTime * Program.StatsHz);
			if (N > recentVelocities.Count - 1)
			{
				return recentVelocities.Average();
			}
			return recentVelocities.Skip(recentVelocities.Count() - N).Take(N-1).Average();
		}
		public bool boosting = false;

		#endregion

		/// <summary>
		/// Store players current stats in case we lose them from a crash.
		/// </summary>
		/// <param name="newPlayerStats"></param>
		public void CacheStats(g_PlayerStats newPlayerStats)
		{
			// if player joined back from spectator
			if ((newPlayerStats.possession_time + 
				newPlayerStats.points + 
				newPlayerStats.shots_taken + 
				newPlayerStats.saves + 
				newPlayerStats.passes + 
				newPlayerStats.catches + 
				newPlayerStats.steals + 
				newPlayerStats.stuns + 
				newPlayerStats.blocks + 
				newPlayerStats.interceptions + 
				newPlayerStats.assists) != 0)
			{
				return;
			}
			cachedStats += currentStats;
		}

		/// <summary>
		/// Store players stats from last round for later use in determining stats on a per round basis.
		/// </summary>
		/// <param name="lastPlayer"></param>
		public void StoreLastRoundStats(MatchPlayer lastPlayer)
		{
			oldRoundStats = lastPlayer.oldRoundStats;

			oldRoundStats += lastPlayer.currentStats;
		}
	}
}
