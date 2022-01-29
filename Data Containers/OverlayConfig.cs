using System.Collections.Generic;
using EchoVRAPI;

namespace Spark
{
	public class OverlayConfig
	{
		public static Dictionary<string, object> ToDict()
		{
			return new Dictionary<string, object>()
			{
				{
					"visibility", new Dictionary<string, bool>
					{
						{ "minimap", true },
						{ "main_banner", true },
						{ "neutral_jousts", true },
						{ "defensive_jousts", true },
						{ "event_log", true },
						{ "playspace", true },
						{ "player_speed", true },
						{ "disc_speed", true },
					}
				},
				{ "caster_prefs", SparkSettings.instance.casterPrefs },
				{
					"teams", new[]
					{
						new Dictionary<string, object>()
						{
							{
								"vrml_team_name",
								Program.matchData?.teams[Team.TeamColor.blue].vrmlTeamName ?? ""
							},
							{
								"vrml_team_logo",
								Program.matchData?.teams[Team.TeamColor.blue].vrmlTeamLogo ?? ""
							},
							{
								"team_name",
								GetOverlayTeamName(Team.TeamColor.blue)
							},
							{
								"team_logo",
								GetOverlayTeamLogo(Team.TeamColor.blue)
							},
						},
						new Dictionary<string, object>()
						{
							{
								"vrml_team_name",
								Program.matchData?.teams[Team.TeamColor.orange].vrmlTeamName ?? ""
							},
							{
								"vrml_team_logo",
								Program.matchData?.teams[Team.TeamColor.orange].vrmlTeamLogo ?? ""
							},
							{
								"team_name",
								GetOverlayTeamName(Team.TeamColor.orange)
							},
							{
								"team_logo",
								GetOverlayTeamLogo(Team.TeamColor.orange)
							},
						}
					}
				}
			};
		}

		public static string GetOverlayTeamName(Team.TeamColor team)
		{
			switch (team)
			{
				case Team.TeamColor.blue:
					switch (SparkSettings.instance.overlaysTeamSource)
					{
						case 0:
							return SparkSettings.instance.overlaysManualTeamNameBlue;
						case 1:
							return Program.matchData?.teams[Team.TeamColor.blue]?.vrmlTeamName;
					}

					break;
				case Team.TeamColor.orange:
					switch (SparkSettings.instance.overlaysTeamSource)
					{
						case 0:
							return SparkSettings.instance.overlaysManualTeamNameOrange;
						case 1:
							return Program.matchData?.teams[Team.TeamColor.orange]?.vrmlTeamName;
					}

					break;
				case Team.TeamColor.spectator:
				default:
					Logger.LogRow(Logger.LogType.Error, "Can't get team name from spectator team");
					break;
			}

			return "";
		}

		public static string GetOverlayTeamLogo(Team.TeamColor team)
		{
			switch (team)
			{
				case Team.TeamColor.blue:
					switch (SparkSettings.instance.overlaysTeamSource)
					{
						case 0:
							return SparkSettings.instance.overlaysManualTeamLogoBlue;
						case 1:
							return Program.matchData?.teams[Team.TeamColor.blue]?.vrmlTeamLogo;
					}

					break;
				case Team.TeamColor.orange:
					switch (SparkSettings.instance.overlaysTeamSource)
					{
						case 0:
							return SparkSettings.instance.overlaysManualTeamLogoOrange;
						case 1:
							return Program.matchData?.teams[Team.TeamColor.orange]?.vrmlTeamLogo;
					}

					break;
				case Team.TeamColor.spectator:
				default:
					Logger.LogRow(Logger.LogType.Error, "Can't get team name from spectator team");
					break;
			}

			return "";
		}
	}
}