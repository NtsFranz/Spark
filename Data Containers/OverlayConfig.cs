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
			List<AccumulatedFrame> previousRounds = OverlayServer.GetPreviousRounds();
			return new Dictionary<string, object>()
			{
				{
					"visibility", new Dictionary<string, bool>
					{
						{ "minimap", SparkSettings.instance.configurableOverlaySettings.minimap },
						{ "compact_minimap", SparkSettings.instance.configurableOverlaySettings.compact_minimap },
						{ "player_rosters", SparkSettings.instance.configurableOverlaySettings.player_rosters },
						{ "main_banner", SparkSettings.instance.configurableOverlaySettings.main_banner },
						{ "neutral_jousts", SparkSettings.instance.configurableOverlaySettings.neutral_jousts },
						{ "defensive_jousts", SparkSettings.instance.configurableOverlaySettings.defensive_jousts },
						{ "event_log", SparkSettings.instance.configurableOverlaySettings.event_log },
						{ "playspace", SparkSettings.instance.configurableOverlaySettings.playspace },
						{ "player_speed", SparkSettings.instance.configurableOverlaySettings.player_speed },
						{ "disc_speed", SparkSettings.instance.configurableOverlaySettings.disc_speed },
						{ "show_team_logos", SparkSettings.instance.configurableOverlaySettings.show_team_logos },
						{ "show_team_names", SparkSettings.instance.configurableOverlaySettings.show_team_names },
					}
				},
				{ "caster_prefs", SparkSettings.instance.casterPrefs },
				{
					"round_scores", new Dictionary<string, object>
					{
						{ "manual_round_scores", SparkSettings.instance.overlaysRoundScoresManual },
						{ "round_count", SparkSettings.instance.overlaysRoundScoresManual ? SparkSettings.instance.overlaysManualRoundCount : Program.CurrentRound.frame.total_round_count },
						{ "round_scores_orange", SparkSettings.instance.overlaysRoundScoresManual ? SparkSettings.instance.overlaysManualRoundScoresOrange : previousRounds?.Select(m => m.frame.orange_points).ToArray() ?? Array.Empty<float>() },
						{ "round_scores_blue", SparkSettings.instance.overlaysRoundScoresManual ? SparkSettings.instance.overlaysManualRoundScoresBlue : previousRounds?.Select(m => m.frame.blue_points).ToArray() ?? Array.Empty<float>() },
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
								Program.CurrentRound.teams[Team.TeamColor.blue].vrmlTeamName ?? ""
							},
							{
								"vrml_team_logo",
								Program.CurrentRound.teams[Team.TeamColor.blue].vrmlTeamLogo ?? ""
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
								Program.CurrentRound.teams[Team.TeamColor.orange].vrmlTeamName ?? ""
							},
							{
								"vrml_team_logo",
								Program.CurrentRound.teams[Team.TeamColor.orange].vrmlTeamLogo ?? ""
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
							return Program.CurrentRound.teams[Team.TeamColor.blue]?.vrmlTeamName;
					}

					break;
				case Team.TeamColor.orange:
					switch (SparkSettings.instance.overlaysTeamSource)
					{
						case 0:
							return SparkSettings.instance.overlaysManualTeamNameOrange;
						case 1:
							return Program.CurrentRound.teams[Team.TeamColor.orange]?.vrmlTeamName;
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
							return Program.CurrentRound.teams[Team.TeamColor.blue]?.vrmlTeamLogo;
					}

					break;
				case Team.TeamColor.orange:
					switch (SparkSettings.instance.overlaysTeamSource)
					{
						case 0:
							return SparkSettings.instance.overlaysManualTeamLogoOrange;
						case 1:
							return Program.CurrentRound.teams[Team.TeamColor.orange]?.vrmlTeamLogo;
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