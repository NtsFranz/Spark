using Grapevine;
using Spark.Properties;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http.Json;
using System.Numerics;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Globalization;

namespace Spark
{
	[RestResource]
	public class OverlayServer
	{
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

		[RestRoute("Get", "/test")]
		public async Task Test(IHttpContext context)
		{
			await context.Response.SendResponseAsync("Successfully hit the test route!");
		}

		[RestRoute("Get", "/session")]
		public async Task Session(IHttpContext context)
		{
			context.Response.AddHeader("Access-Control-Allow-Origin", "*");
			context.Response.AddHeader("Access-Control-Allow-Headers", "Content-Type, Accept, X-Requested-With");
			context.Response.AddHeader("Content-Type", "application/json");

			if (Program.inGame)
			{
				await context.Response.SendResponseAsync(Program.lastJSON);
			}
			else
			{
				await context.Response.SendResponseAsync(HttpStatusCode.NotFound);
			}
		}


		[RestRoute("Get", "/stats")]
		public async Task Stats(IHttpContext context)
		{
			context.Response.AddHeader("Access-Control-Allow-Origin", "*");
			context.Response.AddHeader("Access-Control-Allow-Headers", "Content-Type, Accept, X-Requested-With");
			context.Response.AddHeader("Content-Type", "application/json");

			if (Program.inGame)
			{
				// gets a list of all previous matches in memory that are for the current set
				List<MatchData> selectedMatches = Program.lastMatches
					.Where(m => m.customId == Program.matchData.customId &&
						   m.firstFrame.sessionid == Program.matchData.firstFrame.sessionid)
					.ToList();
				selectedMatches.Add(Program.matchData);



				BatchOutputFormat data = new BatchOutputFormat();
				data.match_data = Program.matchData.ToDict();

				selectedMatches.ForEach(m =>
				{
					m.players.Values.ToList().ForEach(e => data.match_players.Add(e.ToDict()));
					m.Events.ForEach(e => data.events.Add(e.ToDict()));
					m.Goals.ForEach(e => data.goals.Add(e.ToDict()));
					m.Throws.ForEach(e => data.throws.Add(e.ToDict()));
				});
				string dataString = JsonConvert.SerializeObject(data);


				Dictionary<string, object> response = new Dictionary<string, object>
				{
					{ "teams", new Dictionary<string, object>[]
						{
							new Dictionary<string, object>{
								{ "vrml_team_name", selectedMatches.Last().teams[g_Team.TeamColor.blue].vrmlTeamName },
								{ "vrml_team_logo", selectedMatches.Last().teams[g_Team.TeamColor.blue].vrmlTeamLogo },

							}
							,
							new Dictionary<string, object>{
								{ "vrml_team_name", selectedMatches.Last().teams[g_Team.TeamColor.orange].vrmlTeamName },
								{ "vrml_team_logo", selectedMatches.Last().teams[g_Team.TeamColor.orange].vrmlTeamLogo },

							}
						}
					},
					{
						"joust_events", selectedMatches
							.SelectMany(m=> m.Events)
							.Where(e=>e.eventType is EventData.EventType.joust_speed or EventData.EventType.defensive_joust)
							.Select(e=>e.ToDict())
					},
					{"goals", selectedMatches.SelectMany(m=> m.Goals).Select(e=>e.ToDict()) }
				};
				string responseString = JsonConvert.SerializeObject(response);
				
				await context.Response.SendResponseAsync(responseString);
			}
			else
			{
				await context.Response.SendResponseAsync(HttpStatusCode.NotFound);
			}
		}


		[RestRoute("Get", "/position_map")]
		public async Task PositionMap(IHttpContext context)
		{

			if (Program.matchData == null)
			{
				await context.Response.SendResponseAsync("Not in match yet").ConfigureAwait(false);
				return;
			}


			// get the most recent file in the replay folder
			DirectoryInfo directory = new DirectoryInfo(SparkSettings.instance.saveFolder);
			FileInfo[] files = directory.GetFiles().OrderByDescending(f => f.LastWriteTime).Where(f => f.Name.StartsWith("rec")).ToArray();



			// gets a list of all the times of previous matches in memory that are for the current set
			List<DateTime> selectedMatchTimes = Program.lastMatches
				.Where(m => m.customId == Program.matchData.customId &&
					   m.firstFrame.sessionid == Program.matchData.firstFrame.sessionid)
				.Select(m => m.matchTime).ToList();
			selectedMatchTimes.Add(Program.matchData.matchTime);

			string s = files[0].Name.Substring(4, 19);

			// finds all the files that match one of the matches in memory
			FileInfo[] selectedFiles = files
				.Where(f => DateTime.TryParseExact(f.Name.Substring(4, 19), "yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime time)
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
				await context.Response.SendResponseAsync("No recent replay file found.").ConfigureAwait(false);
				return;
			}

			List<xyPos> positions = new List<xyPos>();
			foreach (FileInfo file in selectedFiles)
			{
				ReplayFileReader reader = new ReplayFileReader();
				ReplayFile replayFile = await reader.LoadFileAsync(file.FullName, processFrames: true);
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
			await context.Response.SendResponseAsync(resp).ConfigureAwait(false);
		}
	}
}