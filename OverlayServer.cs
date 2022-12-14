using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using EchoVRAPI;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Spark
{
	public class OverlayServer
	{
		private IWebHost server;
		private bool serverRestarting = false;

		public static string StaticOverlayFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IgniteVR", "Spark", "Overlays");

		public OverlayServer()
		{
			Task.Run(async () => { await RestartServer(); });

			//DiscordOAuth.Authenticated += () =>
			//{
			//	Task.Run(async () => { await RestartServer(); });
			//};

			DiscordOAuth.AccessCodeChanged += (code) =>
			{
				Console.WriteLine("Access code changed");
				Task.Run(async () => { await RestartServer(); });
			};
		}

		private async Task RestartServer()
		{
			Random rand = new Random();
			int restartIndex = rand.Next();
			try
			{
				int counter = 0;
				while (serverRestarting && counter < 10)
				{
					counter++;
					// Logger.LogRow(Logger.LogType.Error, $"Already restarting server. Waiting to try again. {restartIndex}");
					await Task.Delay(100);
				}

				if (serverRestarting)
				{
					Logger.Error($"Already restarting server. Cancelling this restart. {restartIndex}");
					return;
				}

				// stop the server
				serverRestarting = true;

				// get new overlay data
				await OverlaysCustom.FetchOverlayData();

				if (server != null)
				{
					await server.StopAsync();
				}

				// restart the server
				server = WebHost
					.CreateDefaultBuilder()
					.UseKestrel(x => { x.ListenAnyIP(6724); })
					.UseStartup<Routes>()
					.Build();


				_ = server.RunAsync();
				// Logger.LogRow(Logger.LogType.Error, $"Done restarting server. {restartIndex}");
			}
			catch (Exception e)
			{
				Logger.LogRow(Logger.LogType.Error, $"Error when restarting server {restartIndex}\n{e}");
			}

			serverRestarting = false;
		}

		public void Stop()
		{
			server?.StopAsync();
		}

		public class Routes
		{
			public void ConfigureServices(IServiceCollection services)
			{
				services.AddCors(o => o.AddPolicy("CorsPolicy", builder =>
				{
					builder.WithOrigins("*")
						.AllowAnyMethod()
						.AllowAnyHeader();
				}));
			}

			public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ICorsService corsService, ICorsPolicyProvider corsPolicyProvider)
			{
				// OverlaysCustom.FetchOverlayData().Wait();

				// Set up custom content types - associating file extension to MIME type
				FileExtensionContentTypeProvider provider = new FileExtensionContentTypeProvider
				{
					Mappings =
					{
						[".yaml"] = "application/x-yaml"
					}
				};

				if (!Directory.Exists(StaticOverlayFolder))
				{
					Directory.CreateDirectory(StaticOverlayFolder);
				}
				app.UseFileServer(new FileServerOptions
				{
					FileProvider = new PhysicalFileProvider(StaticOverlayFolder),
					RequestPath = "",
					DefaultFilesOptions = { },
					EnableDefaultFiles = true,
					StaticFileOptions =
					{
						ServeUnknownFileTypes = true,
						ContentTypeProvider = provider,
						OnPrepareResponse = (ctx) =>
						{
							CorsPolicy policy = corsPolicyProvider.GetPolicyAsync(ctx.Context, "CorsPolicy")
								.ConfigureAwait(false)
								.GetAwaiter().GetResult();

							CorsResult corsResult = corsService.EvaluatePolicy(ctx.Context, policy);

							corsService.ApplyResult(corsResult, ctx.Context.Response);
						}
					},
				});
				app.UseFileServer(new FileServerOptions
				{
					FileProvider = new PhysicalFileProvider(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Overlay", "build")),
					RequestPath = "",
					StaticFileOptions =
					{
						ContentTypeProvider = provider,
						OnPrepareResponse = (ctx) =>
						{
							CorsPolicy policy = corsPolicyProvider.GetPolicyAsync(ctx.Context, "CorsPolicy")
								.ConfigureAwait(false)
								.GetAwaiter().GetResult();

							CorsResult corsResult = corsService.EvaluatePolicy(ctx.Context, policy);

							corsService.ApplyResult(corsResult, ctx.Context.Response);
						}
					},
					DefaultFilesOptions = { },
					EnableDefaultFiles = true,
				});

				app.UseCors("CorsPolicy");
				app.UseCors(x => x.SetIsOriginAllowed(origin => true));
				app.UseRouting();

				app.UseEndpoints(endpoints =>
				{
					SparkAPI.MapRoutes(endpoints);
					EchoVRAPIPassthrough.MapRoutes(endpoints);

					endpoints.MapGet("/spark_info", async context =>
					{
						context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
						context.Response.Headers.Add("Access-Control-Allow-Headers",
							"Content-Type, Accept, X-Requested-With");

						await context.Response.WriteAsJsonAsync(
							new Dictionary<string, object>
							{
								{ "version", Program.AppVersionString() },
								{ "windows_store", Program.IsWindowsStore() },
								{ "ess_version", Program.InstalledSpeakerSystemVersion },
							});
					});

					endpoints.MapGet("/stats", async context =>
					{
						context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
						context.Response.Headers.Add("Access-Control-Allow-Headers",
							"Content-Type, Accept, X-Requested-With");

						Dictionary<string, object> response = GetStatsResponse();
						await context.Response.WriteAsJsonAsync(response);
					});


					// TODO in progress. This will have settings for the overlay replacement
					endpoints.MapGet("/overlay_info", async context =>
					{
						try
						{
							context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
							context.Response.Headers.Add("Access-Control-Allow-Headers",
								"Content-Type, Accept, X-Requested-With");

							Dictionary<string, object> response = new Dictionary<string, object>();

							response["stats"] = GetStatsResponse();
							response["visibility"] = new Dictionary<string, object>()
							{
								{ "minimap", true },
								{ "main_banner", true },
								{ "neutral_jousts", true },
								{ "defensive_jousts", true },
								{ "event_log", true },
								{ "playspace", true },
								{ "player_speed", true },
								{ "disc_speed", true },
							};
							response["caster_prefs"] = SparkSettings.instance.casterPrefs;

							if (Program.InGame)
							{
								response["session"] = Program.lastJSON;
							}

							await context.Response.WriteAsJsonAsync(response);
						}
						catch (Exception e)
						{
							Logger.LogRow(Logger.LogType.Error, e.ToString());
						}
					});


					endpoints.MapGet("/scoreboard", async context =>
					{
						string file = ReadResource("default_scoreboard.html");

						string[] columns =
						{
							"player_name",
							"points",
							"assists",
							"saves",
							"stuns",
						};

						List<List<Dictionary<string, object>>> matchStats = GetMatchStats();


						string overlayOrangeTeamName = "";
						string overlayBlueTeamName = "";
						switch (SparkSettings.instance.overlaysTeamSource)
						{
							case 0:
								overlayOrangeTeamName = SparkSettings.instance.overlaysManualTeamNameOrange;
								overlayBlueTeamName = SparkSettings.instance.overlaysManualTeamNameBlue;
								break;
							case 1:
								overlayOrangeTeamName = Program.CurrentRound.teams[Team.TeamColor.orange].vrmlTeamName;
								overlayBlueTeamName = Program.CurrentRound.teams[Team.TeamColor.blue].vrmlTeamName;
								break;
						}

						if (string.IsNullOrWhiteSpace(overlayOrangeTeamName))
						{
							overlayOrangeTeamName = "ORANGE TEAM";
						}

						if (string.IsNullOrWhiteSpace(overlayBlueTeamName))
						{
							overlayBlueTeamName = "BLUE TEAM";
						}

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
									html.Append(i == 0 ? overlayBlueTeamName : overlayOrangeTeamName);
								}
								else
								{
									html.Append(column);
								}

								html.Append("</th>");
							}

							html.Append("</thead>");

							html.Append("<body>");


							if (matchStats[i].Count >= 8)
							{
								Logger.LogRow(Logger.LogType.Error, "8 or more players on a team. Must have failed to split.");
							}

							// cap out at 8 players. Anything more breaks the layout
							for (int playerIndex = 0; playerIndex < matchStats[i].Count && playerIndex < 8; playerIndex++)
							{
								Dictionary<string, object> player = matchStats[i][playerIndex];
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
					});


					endpoints.MapGet("/disc_positions",
						async context =>
						{
							context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
							context.Response.Headers.Add("Access-Control-Allow-Headers",
								"Content-Type, Accept, X-Requested-With");
							List<Dictionary<string, float>> positions = await GetDiscPositions();
							await context.Response.WriteAsJsonAsync(positions);
						});

					endpoints.MapGet("/disc_position_heatmap",
						async context =>
						{
							string file = ReadResource("disc_position_heatmap.html");
							await context.Response.WriteAsync(file);
						});


					endpoints.MapGet("/get_player_speed", async context =>
					{
						context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
						context.Response.Headers.Add("Access-Control-Allow-Headers",
							"Content-Type, Accept, X-Requested-With");

						await context.Response.WriteAsJsonAsync(
							new Dictionary<string, object>
							{
								{
									"speed",
									Program.lastFrame?.GetPlayer(Program.lastFrame.client_name)?.velocity.ToVector3()
										.Length() ?? -1
								},
							});
					});
					endpoints.MapGet("/get_disc_speed", async context =>
					{
						context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
						context.Response.Headers.Add("Access-Control-Allow-Headers",
							"Content-Type, Accept, X-Requested-With");

						await context.Response.WriteAsJsonAsync(
							new Dictionary<string, object>
							{
								{
									"speed", Program.lastFrame?.disc.velocity.ToVector3().Length() ?? -1
								},
							});
					});

					endpoints.MapGet("/speedometer/player",
						async context =>
						{
							string file = ReadResource("speedometer.html");
							file = file.Replace("FETCH_URL", "/get_player_speed");
							await context.Response.WriteAsync(file);
						});

					endpoints.MapGet("/speedometer/lone_echo_1",
						async context =>
						{
							string file = ReadResource("speedometer.html");
							file = file.Replace("FETCH_URL", "http://127.0.0.1:6723/le1/speed/");
							await context.Response.WriteAsync(file);
						});

					endpoints.MapGet("/speedometer/lone_echo_2",
						async context =>
						{
							string file = ReadResource("speedometer.html");
							file = file.Replace("FETCH_URL", "http://127.0.0.1:6723/le2/speed/");
							await context.Response.WriteAsync(file);
						});

					endpoints.MapGet("/speedometer/disc",
						async context =>
						{
							string file = ReadResource("speedometer.html");
							file = file.Replace("FETCH_URL", "/get_disc_speed");
							await context.Response.WriteAsync(file);
						});


					// add all the stuff in wwwroot

					// Determine path
					Assembly assembly = Assembly.GetExecutingAssembly();
					string sparkPath = Path.GetDirectoryName(assembly.Location);
					if (sparkPath != null)
					{
						// Format: "{Namespace}.{Folder}.{filename}.{Extension}"
						foreach (string str in assembly.GetManifestResourceNames())
						{
							string[] pieces = str.Split('.');
							if (pieces.Length > 2 && pieces?[1] == "wwwroot_resources")
							{
								List<string> folderPieces = pieces.Skip(2).SkipLast(2).Append(string.Join('.', pieces.TakeLast(2))).ToList();
								string url;

								if (folderPieces.Count > 1 && folderPieces[^1].Contains("min."))
								{
									// combine the min into the filename
									folderPieces[^2] = string.Join('.', folderPieces.TakeLast(2));
									folderPieces.RemoveAt(folderPieces.Count - 1);
								}

								if (folderPieces[^1] == "index.html")
								{
									url = "/" + string.Join('/', folderPieces.SkipLast(1));
								}
								else if (folderPieces[^1].EndsWith(".html"))
								{
									url = "/" + string.Join('/', folderPieces)[..^5];
								}
								else
								{
									url = "/" + string.Join('/', folderPieces);
								}

								endpoints.MapGet(url, async context =>
								{
									string contentType = folderPieces.Last().Split('.').Last() switch
									{
										"js" => "application/javascript",
										"css" => "text/css",
										"png" => "image/png",
										"jpg" => "image/jpeg",
										_ => ""
									};

									context.Response.Headers.Add("content-type", contentType);
									await context.Response.SendFileAsync(Path.Combine(sparkPath, "wwwroot_resources", Path.Combine(folderPieces.ToArray())));
									//await context.Response.WriteAsync(ReadResourceBytes(finalFileNameText));
								});
							}
						}
					}
				});
			}

			private static Dictionary<string, object> GetStatsResponse()
			{
				lock (Program.gameStateLock)
				{
					List<AccumulatedFrame> selectedMatches = null;
					List<List<Dictionary<string, object>>> matchStats = null;
					// if (Program.InGame)
					{
						selectedMatches = GetPreviousRounds();

						BatchOutputFormat data = new BatchOutputFormat
						{
							match_data = Program.CurrentRound.ToDict()
						};

						selectedMatches.ForEach(m =>
						{
							m.players.Values.ToList().ForEach(e => data.match_players.Add(e.ToDict()));
							m.events.ToList().ForEach(e => data.events.Add(e.ToDict()));
							m.goals.ToList().ForEach(e => data.goals.Add(e.ToDict()));
							m.throws.ToList().ForEach(e => data.throws.Add(e.ToDict()));
						});


						matchStats = GetMatchStats();

						// var bluePlayers = selectedMatches
						// 	.SelectMany(m => m.players)
						// 	.Where(p => p.Value.teamData.teamColor == Team.TeamColor.blue)
						// 	.GroupBy(
						// 		p => p.Key,
						// 		(key, values) => new 
						// 		{
						// 			Name = key,
						// 			Data = values.Sum((x1, x2) => x1.Value+x2.Value)
						// 		}
						// 	)
						// 	;
					}

					string overlayOrangeTeamName = "";
					string overlayBlueTeamName = "";
					string overlayOrangeTeamLogo = "";
					string overlayBlueTeamLogo = "";
					switch (SparkSettings.instance.overlaysTeamSource)
					{
						case 0:
							overlayOrangeTeamName = SparkSettings.instance.overlaysManualTeamNameOrange;
							overlayBlueTeamName = SparkSettings.instance.overlaysManualTeamNameBlue;
							overlayOrangeTeamLogo = SparkSettings.instance.overlaysManualTeamLogoOrange;
							overlayBlueTeamLogo = SparkSettings.instance.overlaysManualTeamLogoBlue;
							break;
						case 1:
							TeamData orangeTeam = selectedMatches?.Last().teams[Team.TeamColor.orange];
							TeamData blueTeam = selectedMatches?.Last().teams[Team.TeamColor.blue];
							overlayOrangeTeamName = orangeTeam?.vrmlTeamName ?? "";
							overlayBlueTeamName = blueTeam?.vrmlTeamName ?? "";
							overlayOrangeTeamLogo = orangeTeam?.vrmlTeamLogo ?? "";
							overlayBlueTeamLogo = blueTeam?.vrmlTeamLogo ?? "";
							break;
					}

					Dictionary<string, object> response = new Dictionary<string, object>
					{
						{
							"teams", new[]
							{
								new Dictionary<string, object>
								{
									{
										"vrml_team_name",
										selectedMatches?.Last().teams[Team.TeamColor.blue].vrmlTeamName ?? ""
									},
									{
										"vrml_team_logo",
										selectedMatches?.Last().teams[Team.TeamColor.blue].vrmlTeamLogo ?? ""
									},
									{
										"team_name",
										overlayBlueTeamName
									},
									{
										"team_logo",
										overlayBlueTeamLogo
									},
									{
										"players",
										matchStats?[0]
									}
								},
								new Dictionary<string, object>
								{
									{
										"vrml_team_name",
										selectedMatches?.Last().teams[Team.TeamColor.orange].vrmlTeamName ?? ""
									},
									{
										"vrml_team_logo",
										selectedMatches?.Last().teams[Team.TeamColor.orange].vrmlTeamLogo ?? ""
									},
									{
										"team_name",
										overlayOrangeTeamName
									},
									{
										"team_logo",
										overlayOrangeTeamLogo
									},
									{
										"players",
										matchStats?[1]
									}
								}
							}
						},
						{
							"joust_events", selectedMatches?
								.SelectMany(m => m.events)
								.Where(e =>
									e.eventType is EventContainer.EventType.joust_speed or EventContainer.EventType
										.defensive_joust)
								.Select(e => e.ToDict())
						},
						{ "goals", selectedMatches?.SelectMany(m => m.goals).Select(e => e.ToDict()) }
					};
					return response;
				}
			}
		}


		[Serializable]
		public class xyPos
		{
			public float x;
			public float y;

			public xyPos(float x, float y)
			{
				this.x = x;
				this.y = y;
			}
		}

		/// <summary>
		/// Gets a list of all previous matches in memory that are for the current set
		/// </summary>
		public static List<AccumulatedFrame> GetPreviousRounds()
		{
			// List<AccumulatedFrame> selectedMatches = Program.rounds
			//  .Where(m=>m!=null)
			// 	.Where(m => m.frame.sessionid == Program.CurrentRound.frame.sessionid)
			// 	.ToList();
			// List<AccumulatedFrame> selectedMatches = Program.rounds
			// 	.Where(m => m != null)
			// 	.TakeLast(1)
			// 	.ToList();

			List<AccumulatedFrame> selectedMatches = new List<AccumulatedFrame>();
			selectedMatches.Add(Program.CurrentRound);
			while (selectedMatches.Last().lastRound != null)
			{
				selectedMatches.Add(selectedMatches.Last().lastRound);
			}

			selectedMatches.Reverse();
			return selectedMatches;
		}

		public static string ReadResource(string name)
		{
			// Determine path
			Assembly assembly = Assembly.GetExecutingAssembly();
			// Format: "{Namespace}.{Folder}.{filename}.{Extension}"
			string[] resources = assembly.GetManifestResourceNames();
			string resourcePath = resources.Single(str => str.EndsWith(name));

			using Stream stream = assembly.GetManifestResourceStream(resourcePath);
			if (stream == null) return "";
			using StreamReader reader = new StreamReader(stream);
			return reader.ReadToEnd();
		}

		public static byte[] ReadResourceBytes(string name)
		{
			// Determine path
			Assembly assembly = Assembly.GetExecutingAssembly();
			// Format: "{Namespace}.{Folder}.{filename}.{Extension}"
			string[] resources = assembly.GetManifestResourceNames();
			string resourcePath = resources.Single(str => str.EndsWith(name));

			using Stream stream = assembly.GetManifestResourceStream(resourcePath);
			if (stream == null) return null;
			using (var memoryStream = new MemoryStream())
			{
				stream.CopyTo(memoryStream);
				return memoryStream.ToArray();
			}
		}

		public static string ReadResource(IEnumerable<string> name)
		{
			// Determine path
			Assembly assembly = Assembly.GetExecutingAssembly();
			// Format: "{Namespace}.{Folder}.{filename}.{Extension}"
			string resourcePath = assembly.GetManifestResourceNames().Single(str => str.EndsWith(string.Join('.', name)));

			using Stream stream = assembly.GetManifestResourceStream(resourcePath);
			if (stream == null) return "";
			using StreamReader reader = new StreamReader(stream);
			return reader.ReadToEnd();
		}


		public static Stream GenerateStreamFromString(string s)
		{
			MemoryStream stream = new MemoryStream();
			StreamWriter writer = new StreamWriter(stream);
			writer.Write(s);
			writer.Flush();
			stream.Position = 0;
			return stream;
		}

		public static List<List<Dictionary<string, object>>> GetMatchStats()
		{
			// Get the rounds for this match
			List<AccumulatedFrame> selectedMatches = GetPreviousRounds();

			Dictionary<string, MatchPlayer> bluePlayers = new Dictionary<string, MatchPlayer>();
			IEnumerable<MatchPlayer> blueRoundPlayers = selectedMatches
				.SelectMany(m => m.players.Values)
				.Where(p => p.TeamColor == Team.TeamColor.blue);
			foreach (MatchPlayer blueRoundPlayer in blueRoundPlayers)
			{
				if (bluePlayers.ContainsKey(blueRoundPlayer.Name))
				{
					bluePlayers[blueRoundPlayer.Name] += blueRoundPlayer;
				}
				else
				{
					bluePlayers[blueRoundPlayer.Name] = new MatchPlayer(blueRoundPlayer);
				}
			}

			Dictionary<string, MatchPlayer> orangePlayers = new Dictionary<string, MatchPlayer>();
			IEnumerable<MatchPlayer> orangeRoundPlayers = selectedMatches
				.SelectMany(m => m.players.Values)
				.Where(p => p.TeamColor == Team.TeamColor.orange);
			foreach (MatchPlayer orangeRoundPlayer in orangeRoundPlayers)
			{
				if (orangePlayers.ContainsKey(orangeRoundPlayer.Name))
				{
					orangePlayers[orangeRoundPlayer.Name] += orangeRoundPlayer;
				}
				else
				{
					orangePlayers[orangeRoundPlayer.Name] = new MatchPlayer(orangeRoundPlayer);
				}
			}

			return new List<List<Dictionary<string, object>>>
			{
				bluePlayers.Values.Select(p => p.ToDict()).ToList(),
				orangePlayers.Values.Select(p => p.ToDict()).ToList(),
			};
		}

		public static async Task GenerateDiscPositionHeatMap(HttpContext context, string additionalCSS)
		{
			context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
			context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Accept, X-Requested-With");


			string resp = @"";
			await context.Response.WriteAsync(resp);
		}

		public static async Task<List<Dictionary<string, float>>> GetDiscPositions()
		{
			// get the most recent file in the replay folder
			DirectoryInfo directory = new DirectoryInfo(SparkSettings.instance.saveFolder);

			// get .echoreplay files
			FileInfo[] echoreplayFiles = directory.GetFiles().OrderByDescending(f => f.LastWriteTime)
				.Where(f => f.Name.StartsWith("rec") && f.Name.EndsWith(".echoreplay")).ToArray();

			// get .butter files
			FileInfo[] butterFiles = directory.GetFiles().OrderByDescending(f => f.LastWriteTime)
				.Where(f => f.Name.StartsWith("rec") && f.Name.EndsWith(".butter")).ToArray();


			// gets a list of all the times of previous matches in memory that are for the current set
			List<DateTime> selectedMatchTimes = GetPreviousRounds().Select(m => m.matchTime).ToList();

			if (selectedMatchTimes.Count == 0)
			{
				return new List<Dictionary<string, float>>();
			}

			// finds all the files that match one of the matches in memory
			FileInfo[] selectedFiles = butterFiles.Where(
				f =>
					DateTime.TryParseExact(
						f.Name.Substring(4, 19),
						"yyyy-MM-dd_HH-mm-ss",
						CultureInfo.InvariantCulture,
						DateTimeStyles.AssumeLocal,
						out DateTime time)
					&& time.ToUniversalTime() > selectedMatchTimes.Min() - TimeSpan.FromSeconds(10)).ToArray();
			// && MatchesOneTime(time, selectedMatchTimes, TimeSpan.FromSeconds(10))).ToArray();

			// use .echoreplay files if .butter files not found
			if (selectedFiles.Length == 0)
			{
				selectedFiles = echoreplayFiles.Where(
					f =>
						DateTime.TryParseExact(
							f.Name.Substring(4, 19),
							"yyyy-MM-dd_HH-mm-ss",
							CultureInfo.InvariantCulture,
							DateTimeStyles.AssumeLocal,
							out DateTime time)
						&& MatchesOneTime(time, selectedMatchTimes, TimeSpan.FromSeconds(10))).ToArray();
			}

			// FileInfo[] selectedFiles = files.Take(1).ToArray();

			// function to check if a time matches fuzzily
			static bool MatchesOneTime(DateTime time, List<DateTime> timeList, TimeSpan diff)
			{
				foreach (DateTime time2 in timeList)
				{
					if ((time.ToUniversalTime() - time2.ToUniversalTime()).Duration() < diff)
					{
						return true;
					}
				}

				return false;
			}

			if (selectedFiles.Length == 0)
			{
				return new List<Dictionary<string, float>>();
			}
			else
			{
				List<Dictionary<string, float>> positions = new List<Dictionary<string, float>>();
				foreach (FileInfo file in selectedFiles)
				{
					ReplayFileReader reader = new ReplayFileReader();
					ReplayFile replayFile = await reader.LoadFileAsync(file.FullName, true);
					if (replayFile == null) continue;

					// loop through every nth frame
					const int n = 5;
					int nFrames = replayFile.nframes;
					for (int i = 0; i < nFrames; i += n)
					{
						Frame frame = replayFile.GetFrame(i);

						if (frame.game_status != "playing") continue;
						Vector3 pos = frame.disc.position.ToVector3();
						positions.Add(new Dictionary<string, float>
						{
							{ "x", pos.X },
							{ "y", pos.Y },
							{ "z", pos.Z },
						});
					}
				}

				return positions;
			}
		}
	}
}