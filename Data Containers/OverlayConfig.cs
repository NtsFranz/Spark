using System;
using System.Collections.Generic;
using System.Linq;
using EchoVRAPI;

namespace Spark
{
	public class OverlayConfig
	{
		public static Dictionary<string, object> ToDict()
		{
			List<MatchData> previousRounds = OverlayServer.GetPreviousRounds();
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
					"round_scores", new Dictionary<string, object>
					{
						{ "manual_round_scores", SparkSettings.instance.overlaysRoundScoresManual },
						{ "round_count", SparkSettings.instance.overlaysRoundScoresManual ? SparkSettings.instance.overlaysManualRoundCount : Program.matchData?.firstFrame.total_round_count ?? SparkSettings.instance.overlaysManualRoundCount },
						{ "round_scores_orange", SparkSettings.instance.overlaysRoundScoresManual ? SparkSettings.instance.overlaysManualRoundScoresOrange : previousRounds?.Select(m => m?.teams[Team.TeamColor.orange].points ?? 0).ToArray() ?? Array.Empty<int>() },
						{ "round_scores_blue", SparkSettings.instance.overlaysRoundScoresManual ? SparkSettings.instance.overlaysManualRoundScoresBlue : previousRounds?.Select(m => m?.teams[Team.TeamColor.blue].points ?? 0).ToArray() ?? Array.Empty<int>() },
					}
				},
				{ "team_names_source", SparkSettings.instance.overlaysTeamSource },
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