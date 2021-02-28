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

		public string vrmlTeamName = "";
		public string vrmlTeamLogo = "";

		public TeamData(TeamColor teamColor, string teamName)
		{
			this.teamColor = teamColor;
			this.teamName = teamName;
		}

	}

}
