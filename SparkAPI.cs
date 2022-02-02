using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using EchoVRAPI;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Newtonsoft.Json;

namespace Spark
{
	public static class SparkAPI
	{

		public static void MapRoutes(IEndpointRouteBuilder endpoints)
		{
				//string file = OverlayServer.ReadResource("api_index.html");
				//await context.Response.WriteAsync(file);

			endpoints.MapGet("/api/go_to_waypoint/{index}", async context =>
			{
				context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
				context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Accept, X-Requested-With");
				if (int.TryParse(context.Request.RouteValues["index"].ToString(), out int index)) {
					CameraWrite.TryGoToWaypoint(index);
					await context.Response.WriteAsync($"Going to waypoint {index}");
				} else{
					await context.Response.WriteAsync($"Waypoint index must be an int!");
				}
			});


			endpoints.MapGet("/api/play_animation/{index}", async context =>
			{
				context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
				context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Accept, X-Requested-With");
				if (int.TryParse(context.Request.RouteValues["index"].ToString(), out int index))
				{
					Program.cameraWriteWindow.TryPlayAnim(index);
					await context.Response.WriteAsync($"Playing animation {index}");
				}
				else
				{
					await context.Response.WriteAsync($"Animation index must be an int!");
				}
			});


			endpoints.MapGet("/api/orbit_disc_enabled/{enabled}", async context =>
			{
				context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
				context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Accept, X-Requested-With");
				if (bool.TryParse(context.Request.RouteValues["enabled"].ToString(), out bool enabled))
				{
					Program.cameraWriteWindow.OrbitDisc(enabled);
					await context.Response.WriteAsync(enabled ? "Enabled disc orbit" : "Disabled disc orbit");
				}
				else
				{
					await context.Response.WriteAsync($"Must provide true or false!");
				}
			});


			endpoints.MapGet("/api/reload_camera_settings", async context =>
			{
				try
				{
					context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
					context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Accept, X-Requested-With");
					CameraWriteSettings.Load();
					await context.Response.WriteAsync("Reloaded camera settings");
				}
				catch (Exception e)
				{
					Logger.LogRow(Logger.LogType.Error, $"{e}");
				}
			});
			


			endpoints.MapPost("/api/set_team_name/{team_color}/{team_name}", async context =>
			{
				try
				{
					context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
					context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Accept, X-Requested-With");
					string teamName = context.Request.RouteValues["team_name"]?.ToString();
					if (Enum.TryParse(context.Request.RouteValues["team_color"]?.ToString(), out Team.TeamColor teamColor) && 
					    !string.IsNullOrEmpty(teamName))
					{
						switch (teamColor)
						{
							case Team.TeamColor.blue:
								SparkSettings.instance.overlaysManualTeamNameBlue = teamName;
								break;
							case Team.TeamColor.orange:
								SparkSettings.instance.overlaysManualTeamNameOrange = teamName; 
								break;
							default:
								await context.Response.WriteAsync("Invalid team color");
								return;
						}
						Program.OverlayConfigChanged?.Invoke();
					}

					// update the UI to match
					Window window = Program.GetWindowIfOpen(typeof(UnifiedSettingsWindow));
					((UnifiedSettingsWindow)window)?.OverlaysConfigWindow.SetUIToSettings();
					
					await context.Response.WriteAsync("Set team name");
					
				}
				catch (Exception e)
				{
					Logger.LogRow(Logger.LogType.Error, $"{e}");
				}
			});

			endpoints.MapPost("/api/set_team_logo/{team_color}/{team_logo}", async context =>
			{
				try
				{
					context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
					context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Accept, X-Requested-With");
					string teamLogo = context.Request.RouteValues["team_logo"]?.ToString();
					if (Enum.TryParse(context.Request.RouteValues["team_color"]?.ToString(), out Team.TeamColor teamColor) && 
					    !string.IsNullOrEmpty(teamLogo))
					{
						switch (teamColor)
						{
							case Team.TeamColor.blue:
								SparkSettings.instance.overlaysManualTeamLogoBlue = teamLogo;
								break;
							case Team.TeamColor.orange:
								SparkSettings.instance.overlaysManualTeamLogoOrange = teamLogo; 
								break;
							default:
								await context.Response.WriteAsync("Invalid team color");
								return;
						}
						Program.OverlayConfigChanged?.Invoke();
					}

					// update the UI to match
					Window window = Program.GetWindowIfOpen(typeof(UnifiedSettingsWindow));
					((UnifiedSettingsWindow)window)?.OverlaysConfigWindow.SetUIToSettings();
					
					await context.Response.WriteAsync("Set team team_logo");
					
				}
				catch (Exception e)
				{
					Logger.LogRow(Logger.LogType.Error, $"{e}");
				}
			});
			
			endpoints.MapPost("/api/set_team_details/{team_color}", async context =>
			{
				try
				{
					context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
					context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Accept, X-Requested-With");
					string body = await new StreamReader(context.Request.Body).ReadToEndAsync();
					Dictionary<string, string> data = JsonConvert.DeserializeObject<Dictionary<string, string>>(body);

					if (data == null)
					{
						await context.Response.WriteAsync("No data");
						return;
					}
					if (data.ContainsKey("team_logo"))
					{
						string teamLogo = data["team_logo"];
						if (Enum.TryParse(context.Request.RouteValues["team_color"]?.ToString(), out Team.TeamColor teamColor) &&
						    !string.IsNullOrEmpty(teamLogo))
						{
							switch (teamColor)
							{
								case Team.TeamColor.blue:
									SparkSettings.instance.overlaysManualTeamLogoBlue = teamLogo;
									break;
								case Team.TeamColor.orange:
									SparkSettings.instance.overlaysManualTeamLogoOrange = teamLogo;
									break;
								default:
									await context.Response.WriteAsync("Invalid team color");
									return;
							}
							Program.OverlayConfigChanged?.Invoke();
						}
					}

					if (data.ContainsKey("team_name"))
					{
						string teamName = data["team_name"];
						if (Enum.TryParse(context.Request.RouteValues["team_color"]?.ToString(), out Team.TeamColor teamColor) && 
						    !string.IsNullOrEmpty(teamName))
						{
							switch (teamColor)
							{
								case Team.TeamColor.blue:
									SparkSettings.instance.overlaysManualTeamNameBlue = teamName;
									break;
								case Team.TeamColor.orange:
									SparkSettings.instance.overlaysManualTeamNameOrange = teamName; 
									break;
								default:
									await context.Response.WriteAsync("Invalid team color");
									return;
							}
							Program.OverlayConfigChanged?.Invoke();
						}
					}

					// update the UI to match
					Window window = Program.GetWindowIfOpen(typeof(UnifiedSettingsWindow));
					((UnifiedSettingsWindow)window)?.OverlaysConfigWindow.SetUIToSettings();
					
					await context.Response.WriteAsync("Set team logo or name");
					
				}
				catch (Exception e)
				{
					Logger.LogRow(Logger.LogType.Error, $"{e}");
				}
			});

			// resources
		}
	}
}