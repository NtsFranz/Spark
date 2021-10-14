using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
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

						string[] columns =
						{
							"player_name",
							"points",
							"assists",
							"saves",
							"steals",
							"stuns",
							"possession_time",
							"shot_attempts",
						};

						List<List<Dictionary<string, object>>> matchStats = OverlayServer4.GetMatchStats();

						string[] teamHTMLs = new string[2];
						for (int i = 0; i < 2; i++)
						{
							StringBuilder html = new StringBuilder();
							html.Append("<thead>");
							foreach (string column in columns)
							{
								html.Append("<th>");
								if (column == "player_name")
								{
									if (Program.matchData != null &&
									    Program.matchData.teams[(g_Team.TeamColor) i].vrmlTeamName != "")
									{
										html.Append(Program.matchData.teams[(g_Team.TeamColor) i].vrmlTeamName);
									}
									else
									{
										html.Append(i == 0 ? "BLUE TEAM" : "ORANGE TEAM");
									}
								}
								else
								{
									html.Append(column);
								}

								html.Append("</th>");
							}

							html.Append("</thead>");

							html.Append("<body>");


							foreach (Dictionary<string, object> player in matchStats[i])
							{
								html.Append("<tr>");
								foreach (string column in columns)
								{
									html.Append("<td>");
									html.Append(player[column]);
									html.Append("</td>");
								}

								html.Append("</tr>");
							}

							html.Append("<body>");
							teamHTMLs[i] = html.ToString();
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
					await context.Response.WriteAsync("");
				}
			});
			
			
			endpoints.MapGet("/vrml/disc_position_heatmap", async context =>
			{
				string css = "";
				if (overlayData.ContainsKey("disc_position_heatmap.css"))
				{
					css = overlayData["disc_position_heatmap.css"];
				}
				await OverlayServer4.GenerateDiscPositionHeatMap(context, css);
			});

			// resources
		}
	}
}