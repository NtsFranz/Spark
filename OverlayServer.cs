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


		[RestRoute("Get", "/position_map")]
		public async Task PositionMap(IHttpContext context)
		{
			// get the most recent file in the replay folder
			DirectoryInfo directory = new DirectoryInfo(Settings.Default.saveFolder);
			FileInfo[] files = directory.GetFiles().OrderByDescending(f => f.LastWriteTime).Where(f => f.Name.StartsWith("rec")).ToArray();
			FileInfo firstFile = files.First();
			FileInfo[] selectedFiles = files.Where(f => firstFile.LastWriteTime - f.LastWriteTime < TimeSpan.FromMinutes(20)).ToArray();
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
            transform: rotate(90deg);
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
						max: " + Math.Clamp(positions.Count/600,1,50) + @",
						data: t
					});

				};



			// histogram
            var zvals = "+JsonConvert.SerializeObject(positions.Select(p => p.y))+@";

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