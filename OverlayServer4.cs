using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
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
			// This method gets called by the runtime. Use this method to add services to the container.
			// For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
			public void ConfigureServices(IServiceCollection services)
			{
			}

			// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
			public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
			{
				if (env.IsDevelopment())
				{
					app.UseDeveloperExceptionPage();
				}

				app.UseRouting();

				app.UseEndpoints(endpoints =>
				{
					endpoints.MapGet("/",
						async context => { await context.Response.WriteAsync("Locally hosted Spark routes."); });

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

						if (Program.inGame && Program.matchData != null)
						{
							// gets a list of all previous matches in memory that are for the current set
							List<MatchData> selectedMatches = Program.lastMatches
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


							List<List<Dictionary<string, object>>> matchStats = GetMatchStats();

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


							Dictionary<string, object> response = new Dictionary<string, object>
							{
								{
									"teams", new[]
									{
										new Dictionary<string, object>
										{
											{
												"vrml_team_name",
												selectedMatches.Last().teams[g_Team.TeamColor.blue].vrmlTeamName
											},
											{
												"vrml_team_logo",
												selectedMatches.Last().teams[g_Team.TeamColor.blue].vrmlTeamLogo
											},
											{
												"players",
												matchStats[0]
											}
										},
										new Dictionary<string, object>
										{
											{
												"vrml_team_name",
												selectedMatches.Last().teams[g_Team.TeamColor.orange].vrmlTeamName
											},
											{
												"vrml_team_logo",
												selectedMatches.Last().teams[g_Team.TeamColor.orange].vrmlTeamLogo
											},
											{
												"players",
												matchStats[1]
											}
										}
									}
								},
								{
									"joust_events", selectedMatches
										.SelectMany(m => m.Events)
										.Where(e =>
											e.eventType is EventData.EventType.joust_speed or EventData.EventType
												.defensive_joust)
										.Select(e => e.ToDict())
								},
								{"goals", selectedMatches.SelectMany(m => m.Goals).Select(e => e.ToDict())}
							};
							await context.Response.WriteAsJsonAsync(response);
						}
						else
						{
							await context.Response.WriteAsJsonAsync(new object());
						}
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
					});


					endpoints.MapGet("/Inconsolata.ttf", async context =>
					{
						string file = ReadResource("Inconsolata.ttf");
						await context.Response.WriteAsync(file);
					});


					endpoints.MapGet("/position_map", async context =>
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
									Console.WriteLine(nframes);
									for (int i = 0; i < nframes; i += n)
									{
										g_Instance frame = replayFile.GetFrame(i);

										if (frame.game_status != "playing") continue;
										Vector3 pos = frame.disc.position.ToVector3();
										positions.Add(new xyPos((int) (pos.X * 5 + 100), (int) (pos.Z * 5 + 225)));
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

        @font-face {
            font-family: goodtimes;
            src: url(https://cdn.discordapp.com/attachments/706393776918364211/838618091817795634/goodtimes.ttf);
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
</head>

<body>
    <div id='heatmapContainer'></div>
    <img id='backgroundImage'
        src='https://cdn.discordapp.com/attachments/706393776918364211/838605247487279134/minimap.png' />
    <div id='discPositionHistogram'>
        <!-- Plotly chart will be drawn inside this DIV -->
    </div>
    <div id='backgrounddiv'></div>
    <h2 id='title'>Disc Position</h2>
					<script src=""https://cdnjs.cloudflare.com/ajax/libs/heatmap.js/2.0.2/heatmap.min.js"" integrity=""sha512-R35I7hl+fX4IeSVk1c99L/SW0RkDG5dyt2EgU/OY2t0Bx16wC89HGkiXqYykemT0qAYmZOsO5JtxPgv0uzSyKQ=="" crossorigin=""anonymous""></script>
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
					});
				});
			}

			private static List<List<Dictionary<string, object>>> GetMatchStats()
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


			public string ReadResource(string name)
			{
				// Determine path
				var assembly = Assembly.GetExecutingAssembly();
				string resourcePath = name;
				// Format: "{Namespace}.{Folder}.{filename}.{Extension}"
				resourcePath = assembly.GetManifestResourceNames().Single(str => str.EndsWith(name));

				using (Stream stream = assembly.GetManifestResourceStream(resourcePath))
				using (StreamReader reader = new StreamReader(stream))
				{
					return reader.ReadToEnd();
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
		}
	}
}