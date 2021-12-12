using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;

namespace Spark
{
	public class OverlayServer4
	{
		private readonly IWebHost server;

		public OverlayServer4()
		{
			server = WebHost
				.CreateDefaultBuilder()
				.UseKestrel(x => { x.ListenAnyIP(6724); })
				.UseStartup<Routes>()
				.Build();


			Task.Run(() => { server.RunAsync(); });
		}

		public void Stop()
		{
			server.StopAsync();
		}

		public class Routes
		{
			public void ConfigureServices(IServiceCollection services)
			{
				services.AddDirectoryBrowser();
			}

			public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
			{
				if (env.IsDevelopment())
				{
					app.UseDeveloperExceptionPage();
				}

				app.UseDefaultFiles();
				app.UseStaticFiles();
				app.UseRouting();

				app.UseEndpoints(endpoints =>
				{
					OverlaysVRML.MapRoutes(endpoints);
					SparkAPI.MapRoutes(endpoints);


					endpoints.MapGet("/spark_info", async context =>
					{
						context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
						context.Response.Headers.Add("Access-Control-Allow-Headers",
							"Content-Type, Accept, X-Requested-With");

						await context.Response.WriteAsJsonAsync(
							new Dictionary<string, object>
							{
								{"version", Program.AppVersion()},
								{"windows_store", Program.IsWindowsStore()},
								{"ess_version", Program.InstalledSpeakerSystemVersion},
							});
					});


					endpoints.MapGet("/session", async context =>
					{
						context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
						context.Response.Headers.Add("Access-Control-Allow-Headers",
							"Content-Type, Accept, X-Requested-With");
						context.Response.Headers.Add("Content-Type", "application/json");

						if (Program.inGame)
						{
							await context.Response.WriteAsync(Program.lastJSON);
						}
						else
						{
							context.Response.StatusCode = 404;
							await context.Response.WriteAsync("");
						}
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
						context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
						context.Response.Headers.Add("Access-Control-Allow-Headers",
							"Content-Type, Accept, X-Requested-With");

						Dictionary<string, object> response = new Dictionary<string, object>();

						response["stats"] = GetStatsResponse();
						response["visibility"] = new Dictionary<string, object>()
						{
							{"minimap", true},
							{"main_banner", true},
							{"neutral_jousts", true},
							{"defensive_jousts", true},
							{"event_log", true},
							{"playspace", true},
							{"player_speed", true},
							{"disc_speed", true},
						};

						if (Program.inGame && Program.matchData != null)
						{
							response["session"] = Program.lastJSON;
						}
						
						await context.Response.WriteAsJsonAsync(response);
					});

					endpoints.MapGet("/midmatch_overlay", async context =>
					{
						string file = ReadResource("midmatch_overlay.html");
						await context.Response.WriteAsync(file);
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
								if (Program.matchData != null)
								{
									overlayOrangeTeamName = Program.matchData.teams[g_Team.TeamColor.orange].vrmlTeamName;
									overlayBlueTeamName = Program.matchData.teams[g_Team.TeamColor.blue].vrmlTeamName;
								}
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


					endpoints.MapGet("/disc_position_heatmap",
						async context => { await GenerateDiscPositionHeatMap(context, ""); });


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

					endpoints.MapGet("/minimap",
						async context =>
						{
							string file = ReadResource("default_minimap.html");
							await context.Response.WriteAsync(file);
						});
					
					
					endpoints.MapGet("/full_overlay",
						async context =>
						{
							string file = ReadResource("full_overlay.html");
							await context.Response.WriteAsync(file);
						});
				});
			}

			private static Dictionary<string, object> GetStatsResponse()
			{
				List<MatchData> selectedMatches = null;
				List<List<Dictionary<string, object>>> matchStats = null;
				if (Program.inGame && Program.matchData != null)
				{
					// gets a list of all previous matches in memory that are for the current set
					selectedMatches = Program.lastMatches
						.Where(m => m.customId == Program.matchData.customId &&
						            m.firstFrame.sessionid == Program.matchData.firstFrame.sessionid)
						.ToList();
					selectedMatches.Add(Program.matchData);


					BatchOutputFormat data = new BatchOutputFormat
					{
						match_data = Program.matchData.ToDict()
					};

					selectedMatches.ForEach(m =>
					{
						m.players.Values.ToList().ForEach(e => data.match_players.Add(e.ToDict()));
						m.Events.ForEach(e => data.events.Add(e.ToDict()));
						m.Goals.ForEach(e => data.goals.Add(e.ToDict()));
						m.Throws.ForEach(e => data.throws.Add(e.ToDict()));
					});


					matchStats = GetMatchStats();

					// var bluePlayers = selectedMatches
					// 	.SelectMany(m => m.players)
					// 	.Where(p => p.Value.teamData.teamColor == g_Team.TeamColor.blue)
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
						TeamData orangeTeam = selectedMatches?.Last().teams[g_Team.TeamColor.orange];
						TeamData blueTeam = selectedMatches?.Last().teams[g_Team.TeamColor.blue];
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
									selectedMatches?.Last().teams[g_Team.TeamColor.blue].vrmlTeamName ?? ""
								},
								{
									"vrml_team_logo",
									selectedMatches?.Last().teams[g_Team.TeamColor.blue].vrmlTeamLogo ?? ""
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
									selectedMatches?.Last().teams[g_Team.TeamColor.orange].vrmlTeamName ?? ""
								},
								{
									"vrml_team_logo",
									selectedMatches?.Last().teams[g_Team.TeamColor.orange].vrmlTeamLogo ?? ""
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
							.SelectMany(m => m.Events)
							.Where(e =>
								e.eventType is EventData.EventType.joust_speed or EventData.EventType
									.defensive_joust)
							.Select(e => e.ToDict())
					},
					{ "goals", selectedMatches?.SelectMany(m => m.Goals).Select(e => e.ToDict()) }
				};
				return response;
			}
		}


		[Serializable]
		private class xyPos
		{
			public float x;
			public float y;

			public xyPos(float x, float y)
			{
				this.x = x;
				this.y = y;
			}
		}

		public static string ReadResource(string name)
		{
			// Determine path
			Assembly assembly = Assembly.GetExecutingAssembly();
			// Format: "{Namespace}.{Folder}.{filename}.{Extension}"
			string resourcePath = assembly.GetManifestResourceNames().Single(str => str.EndsWith(name));

			using Stream stream = assembly.GetManifestResourceStream(resourcePath);
			if (stream == null) return "";
			using StreamReader reader = new StreamReader(stream);
			return reader.ReadToEnd();
		}

		public static async Task<Dictionary<string, string>> GetOverlays(string accessCode)
		{
			try
			{
				Dictionary<string, string> data = new Dictionary<string, string>();
				using WebClient webClient = new WebClient();
				byte[] result = webClient.DownloadData(Program.API_URL_2 + "get_overlays/" + accessCode + "/" +
													   DiscordOAuth.oauthToken);
				// Stream resp = await Program.GetRequestAsyncStream(Program.API_URL_2 + "get_overlays/" + accessCode + "/" + DiscordOAuth.oauthToken, null);
				// await using MemoryStream file = new MemoryStream();
				// await resp.CopyToAsync(file);
				await using MemoryStream file = new MemoryStream(result);
				using ZipArchive zip = new ZipArchive(file, ZipArchiveMode.Read);
				foreach (ZipArchiveEntry entry in zip.Entries)
				{
					await using Stream stream = entry.Open();
					using StreamReader reader = new StreamReader(stream);
					data[entry.Name] = await reader.ReadToEndAsync();
				}

				return data;
			}
			catch (Exception e)
			{
				Debug.WriteLine(e.ToString());
				return null;
			}
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
			// gets a list of all previous matches in memory that are for the current set
			List<MatchData> selectedMatches = Program.lastMatches
				.Where(m => m.customId == Program.matchData.customId &&
							m.firstFrame.sessionid == Program.matchData.firstFrame.sessionid)
				.ToList();
			if (Program.matchData != null) selectedMatches.Add(Program.matchData);

			Dictionary<string, MatchPlayer> bluePlayers = new Dictionary<string, MatchPlayer>();
			IEnumerable<MatchPlayer> blueRoundPlayers = selectedMatches
				.SelectMany(m => m.players.Values)
				.Where(p => p.teamData.teamColor == g_Team.TeamColor.blue);
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
				.Where(p => p.teamData.teamColor == g_Team.TeamColor.orange);
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
			context.Response.Headers.Add("Access-Control-Allow-Headers",
				"Content-Type, Accept, X-Requested-With");

			if (Program.matchData == null)
			{
				await context.Response.WriteAsync("Not in match yet");
			}
			else
			{
				// get the most recent file in the replay folder
				DirectoryInfo directory = new DirectoryInfo(SparkSettings.instance.saveFolder);
				FileInfo[] files = directory.GetFiles().OrderByDescending(f => f.LastWriteTime)
					.Where(f => f.Name.StartsWith("rec")).ToArray();


				// gets a list of all the times of previous matches in memory that are for the current set
				List<DateTime> selectedMatchTimes = Program.lastMatches
					.Where(m => m.customId == Program.matchData.customId &&
								m.firstFrame.sessionid == Program.matchData.firstFrame.sessionid)
					.Select(m => m.matchTime).ToList();
				Debug.Assert(Program.matchData != null, "Program.matchData != null");
				selectedMatchTimes.Add(Program.matchData.matchTime);

				// finds all the files that match one of the matches in memory
				FileInfo[] selectedFiles = files
					.Where(f => DateTime.TryParseExact(f.Name.Substring(4, 19), "yyyy-MM-dd_HH-mm-ss",
									CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime time)
								&& MatchesOneTime(time, selectedMatchTimes, TimeSpan.FromSeconds(10))
					)
					.ToArray();

				// function to check if a time matches fuzzily
				bool MatchesOneTime(DateTime time, List<DateTime> timeList, TimeSpan diff)
				{
					foreach (DateTime time2 in timeList)
					{
						if ((time - time2).Duration() < diff)
						{
							return true;
						}
					}

					return false;
				}

				//FileInfo firstFile = files.First();
				//FileInfo[] selectedFiles = files.Where(f => firstFile.LastWriteTime - f.LastWriteTime < TimeSpan.FromMinutes(20)).ToArray();
				if (selectedFiles.Length == 0)
				{
					await context.Response.WriteAsync("No recent replay file found.");
				}
				else
				{
					List<xyPos> positions = new List<xyPos>();
					foreach (FileInfo file in selectedFiles)
					{
						ReplayFileReader reader = new ReplayFileReader();
						ReplayFile replayFile =
							reader.LoadFileAsync(file.FullName, processFrames: true).Result;
						if (replayFile == null) continue;

						// loop through every nth frame
						const int n = 10;
						int nframes = replayFile.nframes;
						for (int i = 0; i < nframes; i += n)
						{
							g_Instance frame = replayFile.GetFrame(i);

							if (frame.game_status != "playing") continue;
							Vector3 pos = frame.disc.position.ToVector3();
							positions.Add(new xyPos((int)(pos.X * 5 + 100), (int)(pos.Z * 5 + 225)));
						}
					}

					string resp = @"<!DOCTYPE html>
                    <html lang=""en"">

					<head>
					    <meta charset=""utf-8"">
									<title>Player Positions Heatmap</title>
									<style>
        body,
        html,
        h2 {
            margin: 0;
            padding: 0;
            height: 100%;
        }

        body {
            animation-name: fade_in;
            animation-duration: 2s;
        }

        @keyframes fade_in {
            from {
                opacity: 0;
            }

            10% {
                opacity: 0;
            }

            to {
                opacity: 1;
            }
        }

        #heatmapContainer {
            width: 225px;
            height: 450px;
            top: 60px;
            left: 130px;
        }

        #backgroundImage {
            position: absolute;
            top: 203px;
            left: -37px;
            transform: rotate(-90deg);
            width: 533px;
        }

        #discPositionHistogram {
            position: absolute;
            top: 0px;
            left: -80px;
            transform: rotate(180deg);
            width: 300px;
            height: 590px;
            opacity: .5;
        }

        #backgrounddiv {
            width: 330px;
            height: 555px;
            position: absolute;
            top: 0;
            left: 0;
            background-color: #0005;
            z-index: -1;
        }

        #title {
            width: 330px;
            height: 20px;
            position: absolute;
            top: 0;
            left: 0;
            background-color: #0005;
            text-align: center;
            font-size: 20px;
            font-family: monospace;
            -webkit-text-stroke: .5px black;
            color: white;
            padding: 4px 0;
        }
    </style>

	<style>
		" + additionalCSS + @"
	</style>
</head>

<body>
    <div id='heatmapContainer'></div>
    <img id='backgroundImage'
        src='/img/minimap.png' />
    <div id='discPositionHistogram'>
        <!-- Plotly chart will be drawn inside this DIV -->
    </div>
    <div id='backgrounddiv'></div>
    <!--<h2 id='title'>Disc Position</h2>-->
					<script src=""/js/heatmap.min.js"" integrity=""sha512-R35I7hl+fX4IeSVk1c99L/SW0RkDG5dyt2EgU/OY2t0Bx16wC89HGkiXqYykemT0qAYmZOsO5JtxPgv0uzSyKQ=="" crossorigin=""anonymous""></script>
					<script src='https://cdn.plot.ly/plotly-latest.min.js'></script>
					<script>
					 window.onload = function () {
					// helper function
					function $(id) {
						return document.getElementById(id);
					};

					// create a heatmap instance
					var heatmap = h337.create({
						container: document.getElementById('heatmapContainer'),
						maxOpacity: 1,
						radius: 10,
						blur: 1,
					});

					t = "
								  +
								  JsonConvert.SerializeObject(positions)
								  + @";

					// set the generated dataset
					heatmap.setData({
						min: 0,
						max: " + Math.Clamp(positions.Count / 600, 1, 50) + @",
						data: t
					});

				};



			// histogram
            var zvals = " + JsonConvert.SerializeObject(positions.Select(p => p.y)) + @";

            var trace = {
                y: zvals,
                type: 'histogram',
                marker: {
                    color: 'rgb(0,0,0)'
                }
            };
            var layout = {
                showlegend: false,
                xaxis: {
                    autorange: true,
                    showgrid: false,
                    zeroline: false,
                    showline: false,
                    autotick: true,
                    ticks: '',
                    showticklabels: false
                },
                yaxis: {
                    autorange: true,
                    showgrid: false,
                    zeroline: false,
                    showline: false,
                    autotick: true,
                    ticks: '',
                    showticklabels: false
                },
                paper_bgcolor: 'rgba(0,0,0,0)',
                plot_bgcolor: 'rgba(0,0,0,0)'
            };
            var data = [trace];
            Plotly.newPlot('discPositionHistogram', data, layout, { staticPlot: true });


				</script>
					</body>

					</html>";
					await context.Response.WriteAsync(resp);
				}
			}
		}
	}
}