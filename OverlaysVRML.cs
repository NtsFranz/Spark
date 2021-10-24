using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Grapevine.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Spark
{
	// ReSharper disable once InconsistentNaming
	public static class OverlaysVRML
	{
		private static Dictionary<string, string> overlayData;

		private static async Task FetchOverlayData()
		{
#if DEBUG
			overlayData = await OverlayServer4.GetOverlays("vrml");
#endif
			overlayData ??= await OverlayServer4.GetOverlays("vrml");
		}

		public static void MapRoutes(IEndpointRouteBuilder endpoints)
		{
			endpoints.MapGet("/vrml", async context =>
			{
				if (DiscordOAuth.AccessCode.Contains("vrml"))
				{
					await FetchOverlayData();
					if (overlayData != null && overlayData.ContainsKey("index.html"))
					{
						await context.Response.WriteAsync(overlayData["index.html"]);
					}
					else
					{
						context.Response.StatusCode = 404;
						await context.Response.WriteAsync("");
					}
				}
				else
				{
					context.Response.StatusCode = 403;
					await context.Response.WriteAsync("");
				}
			});
			endpoints.MapGet("/vrml/scoreboard", async context =>
			{
				if (DiscordOAuth.AccessCode.Contains("vrml"))
				{
					await FetchOverlayData();
					if (overlayData != null && overlayData.ContainsKey("scoreboard.html"))
					{
						string file = overlayData["scoreboard.html"];

						Dictionary<string, string> columns = new Dictionary<string, string>()
						{
							{"player_name", "Player"},
							{"points", "PTS"},
							{"assists", "AST"},
							{"saves", "SAV"},
							{"steals", "STL"},
							{"stuns", "STN"},
							{"possession_time", "POSS"},
							{"shots_taken", "ATT"},
						};
						Dictionary<string, string> totalsReplace = new Dictionary<string, string>()
						{
							{"player_name", "player_name_perc\">"}, // keep this to make loop work
							{"points", "points_perc\">"},
							{"assists", "assists_perc\">"},
							{"saves", "saves_perc\">"},
							{"steals", "steals_perc\">"},
							{"stuns", "stuns_perc\">"},
							{"possession_time", "possession_time_perc\">"},
							{"shots_taken", "shots_taken_perc\">"},
						};

						Dictionary<g_Team.TeamColor, Dictionary<string, float>> teamTotals =
							new Dictionary<g_Team.TeamColor, Dictionary<string, float>>()
							{
								{g_Team.TeamColor.blue, new Dictionary<string, float>()},
								{g_Team.TeamColor.orange, new Dictionary<string, float>()}
							};


						List<List<Dictionary<string, object>>> matchStats = OverlayServer4.GetMatchStats();

						string[] teamHTMLs = new string[2];
						for (int i = 0; i < 2; i++)
						{
							StringBuilder html = new StringBuilder();

							foreach (Dictionary<string, object> player in matchStats[i])
							{
								html.Append("<tr>");
								foreach (string column in columns.Keys)
								{
									html.Append("<td>");
									if (column == "possession_time")
									{
										html.Append(TimeSpan.FromSeconds((float) player[column]).ToString(@"m\:ss"));
									}
									else
									{
										html.Append(player[column]);
									}

									// add up team totals
									if (column != "player_name")
									{
										if (!teamTotals[(g_Team.TeamColor) i].ContainsKey(column))
										{
											teamTotals[(g_Team.TeamColor) i][column] = Convert.ToSingle(player[column]);
										}
										else
										{
											teamTotals[(g_Team.TeamColor) i][column] +=
												Convert.ToSingle(player[column]);
										}
									}

									html.Append("</td>");
								}

								html.Append("</tr>");
							}

							teamHTMLs[i] = html.ToString();
						}


						// enter team totals
						for (int i = 0; i < 2; i++)
						{
							foreach (string statName in teamTotals[(g_Team.TeamColor) i].Keys)
							{
								string repl = (i == 0 ? "blue_" : "orange_") + totalsReplace[statName];
								float thisStat = teamTotals[(g_Team.TeamColor) i][statName];
								float otherStat = teamTotals[(g_Team.TeamColor) ((i + 1) % 2)][statName];
								float statValue = 0;
								if (thisStat + otherStat != 0)
								{
									statValue = thisStat / (thisStat + otherStat);
								}

								file = file.Replace(repl, repl + MathF.Round(statValue * 100) + "%");
							}
						}

						file = file.Replace("{{ BLUE_TEAM }}", teamHTMLs[0]);
						file = file.Replace("{{ ORANGE_TEAM }}", teamHTMLs[1]);

						await context.Response.WriteAsync(file);
					}
					else
					{
						context.Response.StatusCode = 404;
						await context.Response.WriteAsync("");
					}
				}
				else
				{
					context.Response.StatusCode = 403;
					await context.Response.WriteAsync("Not authorized");
				}
			});


			endpoints.MapGet("/vrml/disc_position_heatmap", async context =>
			{
				if (DiscordOAuth.AccessCode.Contains("vrml"))
				{
					await FetchOverlayData();
					string css = "";
					if (overlayData.ContainsKey("disc_position_heatmap.css"))
					{
						css = overlayData["disc_position_heatmap.css"];
					}

					await OverlayServer4.GenerateDiscPositionHeatMap(context, css);
				}
				else
				{
					context.Response.StatusCode = 403;
					await context.Response.WriteAsync("Not authorized");
				}
			});


			endpoints.MapGet("/vrml/minimap",
				async context =>
				{
					if (DiscordOAuth.AccessCode.Contains("vrml"))
					{
						await FetchOverlayData();
						if (overlayData.ContainsKey("minimap.html"))
						{
							await context.Response.WriteAsync(overlayData["minimap.html"]);
						}
					}
					else
					{
						context.Response.StatusCode = 403;
						await context.Response.WriteAsync("Not authorized");
					}
				});


			endpoints.MapGet("/vrml/most_recent_goal",
				async context =>
				{
					if (DiscordOAuth.AccessCode.Contains("vrml"))
					{
						await FetchOverlayData();
						if (overlayData.ContainsKey("most_recent_goal.html"))
						{
							await context.Response.WriteAsync(overlayData["most_recent_goal.html"]);
						}
					}
					else
					{
						context.Response.StatusCode = 403;
						await context.Response.WriteAsync("Not authorized");
					}
				});

			// resources
		}
	}
}