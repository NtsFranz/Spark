using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Windows;
using EchoVRAPI;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Routing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Spark
{
	public static class SparkAPI
	{
		private static readonly object vrmlClientLock = new object();
		private static HttpClient vrmlAPIClient;
		private static Dictionary<string, (DateTime, string)> vrmlAPICache = new Dictionary<string, (DateTime, string)>();

		public static void MapRoutes(IEndpointRouteBuilder endpoints)
		{
			//string file = OverlayServer.ReadResource("api_index.html");
			//await context.Response.WriteAsync(file);

			endpoints.MapGet("/api/go_to_waypoint/{index}", async context =>
			{
				context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
				context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Accept, X-Requested-With");
				if (int.TryParse(context.Request.RouteValues["index"]?.ToString(), out int index))
				{
					CameraWrite.TryGoToWaypoint(index);
					await context.Response.WriteAsync($"Going to waypoint {index}");
				}
				else
				{
					await context.Response.WriteAsync($"Waypoint index must be an int!");
				}
			});


			endpoints.MapGet("/api/play_animation/{index}", async context =>
			{
				context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
				context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Accept, X-Requested-With");
				if (int.TryParse(context.Request.RouteValues["index"]?.ToString(), out int index))
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
				if (bool.TryParse(context.Request.RouteValues["enabled"]?.ToString(), out bool enabled))
				{
					Program.cameraWriteWindow.OrbitDisc(enabled);
					await context.Response.WriteAsync(enabled ? "Enabled disc orbit" : "Disabled disc orbit");
				}
				else
				{
					await context.Response.WriteAsync($"Must provide true or false!");
				}
			});


			endpoints.MapGet("/api/go_to_discholder_pov", async context =>
			{
				context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
				context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Accept, X-Requested-With");

				try
				{
					if (Program.lastFrame != null)
					{
						CameraWriteController.SpectatorCamFindPlayer(Program.lastFrame.GetAllPlayers().Find(p => p.possession)?.name);
					}
				}
				catch (Exception)
				{
					// ignored
				}

				await context.Response.WriteAsync($"Done.");
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

			endpoints.MapPost("/api/set_caster_prefs", async context =>
			{
				try
				{
					context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
					context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Accept, X-Requested-With");
					string body = await new StreamReader(context.Request.Body).ReadToEndAsync();
					Dictionary<string, object> data = JsonConvert.DeserializeObject<Dictionary<string, object>>(body);

					if (data == null)
					{
						await context.Response.WriteAsync("No data");
						return;
					}


					SetKey(SparkSettings.instance.casterPrefs, data);

					void SetKey(Dictionary<string, object> setting, Dictionary<string, object> dictionary)
					{
						foreach ((string key, object value) in dictionary)
						{
							if (value is JObject j)
							{
								if (!setting.ContainsKey(key))
								{
									setting[key] = new Dictionary<string, object>();
								} else if (setting[key] is not Dictionary<string, object>)
								{
									setting[key] = new Dictionary<string, object>();
								}
								SetKey((Dictionary<string, object>)setting[key], JsonConvert.DeserializeObject<Dictionary<string, object>>(j.ToString()));
							}
							else
							{
								setting[key] = value;
							}
						}
					}

					Program.OverlayConfigChanged?.Invoke();


					await context.Response.WriteAsync("Set caster prefs");
				}
				catch (Exception e)
				{
					Logger.LogRow(Logger.LogType.Error, $"{e}");
				}
			});

			endpoints.MapPost("/api/set_team_names_source/{source}", async context =>
			{
				try
				{
					context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
					context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Accept, X-Requested-With");

					if (int.TryParse(context.Request.RouteValues["source"]?.ToString(), out int source))
					{
						SparkSettings.instance.overlaysTeamSource = source;
						await context.Response.WriteAsync("Set team names source");
						Program.OverlayConfigChanged?.Invoke();
					}
					else
					{
						await context.Response.WriteAsync("Failed to set team names source");
					}
				}
				catch (Exception e)
				{
					Logger.LogRow(Logger.LogType.Error, $"{e}");
				}
			});

			endpoints.MapPost("/api/set_round_scores", async context =>
			{
				try
				{
					context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
					context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Accept, X-Requested-With");
					string body = await new StreamReader(context.Request.Body).ReadToEndAsync();
					Dictionary<string, object> data = JsonConvert.DeserializeObject<Dictionary<string, object>>(body);

					if (data == null)
					{
						await context.Response.WriteAsync("No data");
						return;
					}

					if (data.ContainsKey("round_scores_orange"))
					{
						List<int> ret = JsonConvert.DeserializeObject<List<int?>>(data["round_scores_orange"].ToString() ?? string.Empty)?.Where(x => x != null).Cast<int>().ToList();
						SparkSettings.instance.overlaysManualRoundScoresOrange = ret?.ToArray() ?? Array.Empty<int>();
					}

					if (data.ContainsKey("round_scores_blue"))
					{
						List<int> ret = JsonConvert.DeserializeObject<List<int?>>(data["round_scores_blue"].ToString() ?? string.Empty)?.Where(x => x != null).Cast<int>().ToList();
						SparkSettings.instance.overlaysManualRoundScoresBlue = ret?.ToArray() ?? Array.Empty<int>();
					}

					if (data.ContainsKey("round_count"))
					{
						SparkSettings.instance.overlaysManualRoundCount = (int)(long)data["round_count"];
					}

					if (data.ContainsKey("round_scores_manual"))
					{
						SparkSettings.instance.overlaysRoundScoresManual = (bool)data["round_scores_manual"];
					}

					Program.OverlayConfigChanged?.Invoke();


					await context.Response.WriteAsync("Set round scores data");
				}
				catch (Exception e)
				{
					Logger.LogRow(Logger.LogType.Error, $"{e}");
				}
			});


			endpoints.MapGet("/api/get_overlay_config", async context =>
			{
				try
				{
					context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
					context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Accept, X-Requested-With");

					await context.Response.WriteAsJsonAsync(OverlayConfig.ToDict());
				}
				catch (Exception e)
				{
					Logger.LogRow(Logger.LogType.Error, $"{e}");
				}
			});


			endpoints.MapGet("/api/vrml_api/{**route}", async context =>
			{
				try
				{
					context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
					context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Accept, X-Requested-With");

					// initialize
					if (vrmlAPIClient == null)
					{
						lock (vrmlClientLock)
						{
							vrmlAPIClient = new HttpClient();
							vrmlAPIClient.DefaultRequestHeaders.Add("version", Program.AppVersionString());
							vrmlAPIClient.DefaultRequestHeaders.Add("User-Agent", "Spark/" + Program.AppVersionString());
							vrmlAPIClient.BaseAddress = new Uri("https://apiignite.vrmasterleague.com/");
						}
					}

					string request = context.Request.GetEncodedUrl();
					request = request[(request.IndexOf("vrml_api", StringComparison.Ordinal) + "vrml_api".Length)..];
					if (request == null) return;
					if (!vrmlAPICache.ContainsKey(request) || DateTime.UtcNow - vrmlAPICache[request].Item1 >= TimeSpan.FromMinutes(30))
					{
						string ret = await vrmlAPIClient.GetStringAsync(request);
						vrmlAPICache[request] = (DateTime.UtcNow, ret);
					}

					string val = vrmlAPICache[request].Item2;
					if (val.StartsWith("{") || val.StartsWith("["))
					{
						context.Response.Headers.Add("Content-Type", "application/json");
					}

					await context.Response.WriteAsync(val);
				}
				catch (Exception e)
				{
					Logger.LogRow(Logger.LogType.Error, $"{e}");
				}
			});


			endpoints.MapGet("/api/focus_spark", async context =>
			{
				try
				{
					Application.Current.Dispatcher.Invoke(() =>
					{
						Program.liveWindow.Show();
						Program.liveWindow.Activate();
					});

					context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
					context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Accept, X-Requested-With");

					await context.Response.WriteAsJsonAsync(new Dictionary<string, string>()
					{
						{ "message", "Focused this window." }
					});
				}
				catch (Exception e)
				{
					Logger.LogRow(Logger.LogType.Error, $"{e}");
				}
			});

			endpoints.MapPost("/api/settings/set", async context =>
			{
				try
				{
					context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
					context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Accept, X-Requested-With");
					string body = await new StreamReader(context.Request.Body).ReadToEndAsync();
					Dictionary<string, object> data = JsonConvert.DeserializeObject<Dictionary<string, object>>(body);

					if (data == null)
					{
						await context.Response.WriteAsync("No data");
						return;
					}

					data.OverwriteObject(SparkSettings.instance);

					if (data.ContainsKey("configurableOverlaySettings"))
					{
						Program.OverlayConfigChanged?.Invoke();
					}


					// update the UI to match
					Window window = Program.GetWindowIfOpen(typeof(UnifiedSettingsWindow));
					((UnifiedSettingsWindow)window)?.OverlaysConfigWindow.SetUIToSettings();

					await context.Response.WriteAsync("Applied new settings.");
				}
				catch (Exception e)
				{
					Logger.LogRow(Logger.LogType.Error, $"{e}");
				}
			});

			endpoints.MapGet("/api/settings/get/{**setting_name}", async context =>
			{
				try
				{
					context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
					context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Accept, X-Requested-With");

					string request = context.Request.GetEncodedUrl();
					request = request[(request.IndexOf("settings/get/", StringComparison.Ordinal) + "settings/get/".Length)..];
					if (request == null) return;

					// IDictionary<string, object> settings = SparkSettings.instance.AsDictionary();
					string settingsStr = JsonConvert.SerializeObject(SparkSettings.instance);
					JObject settings = JsonConvert.DeserializeObject<JObject>(settingsStr);

					string[] parms = request.Split('/');

					// disgusting 🤢
					JToken result = settings;
					foreach (string parm in parms)
					{
						result = ((JObject)result)?[parm];
						if (result == null)
						{
							await context.Response.WriteAsync($"Setting not found. {parm}");
							return;
						}
					}

					string str = result?.ToString();

					// PropertyInfo prop = typeof(SparkSettings).GetProperty(request);
					// object val = prop?.GetValue(SparkSettings.instance);
					// string str = val?.ToString();

					if (str != null)
					{
						await context.Response.WriteAsync(str);
					}
					else
					{
						await context.Response.WriteAsync("Setting not found.");
					}
				}
				catch (Exception e)
				{
					Logger.LogRow(Logger.LogType.Error, $"{e}");
				}
			});
			
			
			endpoints.MapGet("/api/db/jousts", async (context) =>
			{
				try
				{
					
					await context.Response.WriteAsJsonAsync(Program.localDatabase.GetJousts());
				}
				catch (Exception e)
				{
					Logger.LogRow(Logger.LogType.Error, $"{e}");
				}
			});

			// resources
		}
	}

	/// <summary>
	/// https://stackoverflow.com/a/4944547
	/// </summary>
	public static class ObjectExtensions
	{
		public static T ToObject<T>(this IDictionary<string, object> source)
			where T : class, new()
		{
			T someObject = new T();
			Type someObjectType = someObject.GetType();

			foreach (KeyValuePair<string, object> item in source)
			{
				someObjectType.GetProperty(item.Key)?.SetValue(someObject, item.Value, null);
			}

			return someObject;
		}

		public static void OverwriteObject<T>(this IDictionary<string, object> source, T destination)
			where T : class, new()
		{
			Type someObjectType = destination.GetType();

			foreach (KeyValuePair<string, object> item in source)
			{
				if (item.Value is JObject)
				{
					Type subType = someObjectType.GetProperty(item.Key)?.GetValue(destination)?.GetType();
					Dictionary<string, object> subDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(item.Value.ToString() ?? string.Empty);
					if (subDict != null)
					{
						foreach (KeyValuePair<string, object> subItem in subDict)
						{
							subType?.GetProperty(subItem.Key)?.SetValue(someObjectType.GetProperty(item.Key)?.GetValue(destination), subItem.Value, null);
						}
					}
				}
				else
				{
					someObjectType.GetProperty(item.Key)?.SetValue(destination, item.Value, null);
				}
			}
		}

		public static IDictionary<string, object> AsDictionary(this object source, BindingFlags bindingAttr = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance)
		{
			return source.GetType().GetProperties(bindingAttr).ToDictionary
			(
				propInfo => propInfo.Name,
				propInfo => propInfo.GetValue(source, null)
			);
		}
	}
}