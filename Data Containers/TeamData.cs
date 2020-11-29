using System.Collections.Generic;
using static IgniteBot.g_Team;

namespace IgniteBot
{
	/// <summary>
	/// Object containing a teams basic data and MatchPlayer for the corresponding team.
	/// </summary>
	public class TeamData
	{
		public TeamColor teamColor;
		public string teamName;
		public int points;

		/// <summary>
		/// Dictionary of <userid, PlayerData>
		/// </summary>
		public Dictionary<long, MatchPlayer> players;

		public TeamData(TeamColor teamColor, string teamName)
		{
			this.teamColor = teamColor;
			players = new Dictionary<long, MatchPlayer>();
			this.teamName = teamName;
		}

	}

}
