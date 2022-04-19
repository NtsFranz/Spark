using System.Collections.Generic;
using System.Numerics;
using EchoVRAPI;
using Newtonsoft.Json;

namespace Spark
{
	/// <summary>
	/// Object containing a player's throw of the disc.
	/// </summary>
	public class ThrowData : DataContainer
	{
		/// <summary>
		/// Whether or not this data has been sent to the DB or not
		/// </summary>
		public bool inDB = false;

		public AccumulatedFrame matchData;
		public Player player;
		public float gameClock;
		public Vector3 position;
		public Vector3 velocity;
		public bool isLeftHanded;
		public float underhandedness;
		public List<Vector3> trajectory;
		public string TrajectoryJSON {
			get => trajectory.Count > 0 ? JsonConvert.SerializeObject(trajectory) : string.Empty;
		}
		public bool scored;

		public ThrowData(AccumulatedFrame matchData, float gameClock, Player player, Vector3 position, Vector3 velocity, bool isLeftHanded, float underhandedness)
		{
			this.matchData = matchData;
			this.player = player;
			this.gameClock = gameClock;
			this.position = position;
			this.velocity = velocity;
			this.isLeftHanded = isLeftHanded;
			this.underhandedness = underhandedness;
			trajectory = new List<Vector3>();
		}
		/// <summary>
		/// Function to transform throw data into the desired format for firestore.
		/// </summary>
		/// <returns></returns>
		public Dictionary<string, object> ToDict()
		{
			var values = new Dictionary<string, object>
			{
				{"session_id", matchData.frame.sessionid },
				{"match_time", matchData.MatchTimeSQL },
				{"game_clock", gameClock },
				{"player_id", player.userid },
				{"player_name", player.name },
				{"pos_x", position.X },
				{"pos_y", position.Y },
				{"pos_z", position.Z },
				{"vel_x", velocity.X },
				{"vel_y", velocity.Y },
				{"vel_z", velocity.Z },
				{"left_handed", isLeftHanded },
				{"underhandedness", underhandedness },
				{"trajectory", TrajectoryJSON }
			};

			return values;
		}
	}

}
