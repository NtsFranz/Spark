using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Spark
{
	public static class OverlaysCustom
	{
		private static Dictionary<string, string> overlayData;

		public static async Task FetchOverlayData()
		{
			try
			{
#if DEBUG
				Dictionary<string, string> data = new Dictionary<string, string>();

				string route = DiscordOAuth.AccessCode.series_name;
				if (route.Contains("vrml"))
				{
					route = "vrml";
				}
				else if (route.Contains("vrsn"))
				{
					route = "nepatv";
				}

				string folder = @"S:\git_repo\IgniteVR-Overlays\SparkOverlays\" + route;
				if (Directory.Exists(folder))
				{
					foreach (string file in Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories))
					{
						if (file.EndsWith(".png"))
						{
							string tempFolder = Path.Combine(Path.GetTempPath(), "Spark", "img");
							Directory.CreateDirectory(tempFolder);
							string tempFilePath = Path.Combine(tempFolder, SecretKeys.Hash(file) + ".png");
							File.Copy(file, tempFilePath, true);
							data[file.Substring(folder.Length + 1)] = tempFilePath;
						}
						else
						{
							data[file.Substring(folder.Length + 1)] = await File.ReadAllTextAsync(file);
						}
					}
				}

				overlayData = data;
#else
				overlayData ??= await OverlayServer.GetOverlays();
#endif
			}
			catch (Exception e)
			{
				Logger.LogRow(Logger.LogType.Error, e.ToString());
			}
			
			// write out overlayData to a temp folder
			foreach (KeyValuePair<string, string> file in overlayData)
			{
				// file.Value
			}
		}

		public static void MapRoutes(IEndpointRouteBuilder endpoints)
		{
			foreach (string str in overlayData.Keys)
			{
				string[] ignoreEnds = { "index.html", ".html" };
				string route = DiscordOAuth.AccessCode.series_name;
				if (route.Contains("vrml"))
				{
					route = "vrml";
				}
				else if (route.Contains("vrsn"))
				{
					route = "nepatv";
				}

				string url = $"/{route}/";
				bool ended = false;
				foreach (string e in ignoreEnds)
				{
					if (str.EndsWith(e))
					{
						url += str[..^e.Length];
						ended = true;
						break;
					}
				}

				if (!ended)
				{
					url += str;
				}

				url = url.Replace("\\", "/");

				endpoints.MapGet(url, async context =>
				{
#if DEBUG
					await FetchOverlayData();
#endif
					if (url.EndsWith(".png"))
					{
						await context.Response.SendFileAsync(overlayData[str]);
						// context.Response.out = new HeaderDictionary()
						// {
						// 	{}
						// }
						// context.Response.
					}
					else
					{
						await context.Response.WriteAsync(overlayData[str]);
					}
				});
			}
		}
	}
}