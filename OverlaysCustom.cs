using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
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
							try
							{
								File.Copy(file, tempFilePath, true);
							}
							catch (IOException e)
							{
								Logger.LogRow(Logger.LogType.Error, $"IO Exception: {e}");
							}

							data[file[(folder.Length + 1)..]] = tempFilePath;
						}
						else
						{
							data[file[(folder.Length + 1)..]] = await File.ReadAllTextAsync(file);
						}
					}
				}

				overlayData = data;
#else
				overlayData = await GetOverlays();
#endif
			}
			catch (Exception e)
			{
				Logger.LogRow(Logger.LogType.Error, e.ToString());
			}
			
			// write out overlayData to a temp folder
			// foreach (KeyValuePair<string, string> file in overlayData)
			// {
				// file.Value
			// }
		}



		public static async Task<Dictionary<string, string>> GetOverlays()
		{
			if (DiscordOAuth.Personal) return null;

			try
			{
				Dictionary<string, string> data = new Dictionary<string, string>();
				using HttpClient webClient = new HttpClient();
				byte[] result = await webClient.GetByteArrayAsync($"{Program.APIURL}/get_overlays/{DiscordOAuth.AccessCode.series_name}/{DiscordOAuth.oauthToken}");
				await using MemoryStream file = new MemoryStream(result);
				using ZipArchive zip = new ZipArchive(file, ZipArchiveMode.Read);
				foreach (ZipArchiveEntry entry in zip.Entries)
				{
					await using Stream stream = entry.Open();

					if (entry.Name.EndsWith(".png"))
					{
						string tempFolder = Path.Combine(Path.GetTempPath(), "Spark", "img");
						Directory.CreateDirectory(tempFolder);
						string tempFilePath = Path.Combine(tempFolder, SecretKeys.Hash(entry.Name) + ".png");
						await using MemoryStream reader = new MemoryStream();
						await stream.CopyToAsync(reader);
						byte[] bytes = reader.ToArray();
						await File.WriteAllBytesAsync(tempFilePath, bytes);
						data[entry.Name] = tempFilePath;
					}
					else
					{
						using StreamReader reader = new StreamReader(stream);
						data[entry.Name] = await reader.ReadToEndAsync();
					}
				}

				return data;
			}
			catch (Exception e)
			{
				Logger.LogRow(Logger.LogType.Error, e.ToString());
				return null;
			}
		}

		public static void MapRoutes(IEndpointRouteBuilder endpoints)
		{
			if (overlayData == null) return;
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