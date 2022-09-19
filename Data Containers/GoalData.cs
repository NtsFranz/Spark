using System.Collections.Generic;
using System.Numerics;
using EchoVRAPI;
using Newtonsoft.Json;

namespace Spark
{
	/// <summary>
	/// Object containing data of a goal.
	/// </summary>
	public class GoalData : EventContainer
	{
		public GoalData(
			AccumulatedFrame match,
			Player player,
			LastScore lastScore,
			float gameClock,
			Vector2 goalPos,
			float angleIntoGoal,
			bool backboard,
			Team.TeamColor goalColor,
			bool? leftHanded,
			float? underhandedness,
			List<Vector3> discTrajectory)
		{
			eventType = EventType.goal;
			matchData = match;
			GameClock = gameClock;
			Player = player;
			LastScore = lastScore;
			GoalPos = goalPos;
			GoalAngle = angleIntoGoal;
			Backboard = backboard;
			GoalColor = goalColor;
			LeftHanded = leftHanded;
			this.underhandedness = underhandedness;
			DiscTrajectory = new List<Vector3>(discTrajectory);
		}


		public AccumulatedFrame matchData;
		public List<Vector3> DiscTrajectory { get; set; }
		public Player Player { get; set; }
		/// <summary>
		/// Position of where the disc entered the goal.
		/// </summary>
		public Vector2 GoalPos { get; set; }
		public float GameClock { get; set; }
		public float GoalAngle { get; set; }
		public bool Backboard { get; set; }
		public bool? LeftHanded { get; set; }
		public float? underhandedness;
		public Team.TeamColor GoalColor { get; set; }
		public LastScore LastScore { get; set; }
		public Vector3 Position {
			get {
				return (DiscTrajectory != null && DiscTrajectory.Count > 0) ? DiscTrajectory[0] : Vector3.Zero;
			}
		}
		public string TrajectoryJSON {
			get
			{

				if (DiscTrajectory.Count > 0)
				{
					return JsonConvert.SerializeObject(DiscTrajectory);
				}

				return "";
			}
		}

		/// <summary>
		/// Function to transform goal data into the desired format for firestore.
		/// </summary>
		/// <returns></returns>
		public override Dictionary<string, object> ToDict()
		{
			Dictionary<string, object> values = new Dictionary<string, object>
			{
				{"session_id", matchData.frame.sessionid },
				{"match_time", matchData.MatchTimeSQL },
				{"game_clock", GameClock },
				{"player_id", Player?.userid },
				{"player_name", Player?.name },
				{"point_value", LastScore.point_amount },
				{"disc_speed", LastScore.disc_speed },
				{"goal_distance", LastScore.distance_thrown },
				{"assist_name", LastScore.assist_scored },
				{"goal_type", LastScore.goal_type },
				{"team_scored", LastScore.team },
				{"goal_pos_x", GoalPos.X },
				{"goal_pos_y", GoalPos.Y },
				{"pos_x", Position.X },
				{"pos_y", Position.Y },
				{"pos_z", Position.Z },
				{"goal_angle", GoalAngle },
				{"backboard", Backboard },
				{"goal_color", GoalColor.ToString() },
				{"left_handed", LeftHanded },
				{"underhandedness", underhandedness },
				//{"trajectory", TrajectoryJSON }	// TODO this is causing problems with json serialization of the rest of the data
			};

			return values;
		}

		public override Dictionary<string, object> ToDict(bool useCustomKeyNames)
		{
			return ToDict();
		}
	}

}
