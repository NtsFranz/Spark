using NAudio.SoundFont;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Spark
{
	public static class OverlaysCustom
	{
		private static Dictionary<string, string> overlayData;
		public static bool downloading;

		public static async Task FetchOverlayData()
		{
			downloading = true;
			try
			{
#if DEBUG
				await DownloadOverlaysDev();
#else
				await DownloadOverlays();
#endif

				CopyObsSceneCollection();
			}
			catch (Exception e)
			{
				Logger.LogRow(Logger.LogType.Error, e.ToString());
			}

			downloading = false;
		}

		private static async Task DownloadOverlaysDev()
		{
			string route = DiscordOAuth.AccessCode.series_name;
			if (route.Contains("vrml"))
			{
				route = "vrml";
			}

			string folder = @"S:\git_repo\IgniteVR-Overlays\SparkOverlays\" + route;
			if (Directory.Exists(folder))
			{
				if (Directory.Exists(OverlayServer.StaticOverlayFolder)) Directory.Delete(OverlayServer.StaticOverlayFolder, true);

				Directory.CreateDirectory(OverlayServer.StaticOverlayFolder);
				CopyDirectory(folder, Path.Combine(OverlayServer.StaticOverlayFolder, route), true);
				RemoveHtmlExt(Path.Combine(OverlayServer.StaticOverlayFolder, route));
			}
			else
			{
				await DownloadOverlays();
			}
		}


		private static async Task DownloadOverlays()
		{
			if (DiscordOAuth.Personal) return;

			string route = DiscordOAuth.AccessCode.series_name;
			if (route.Contains("vrml"))
			{
				route = "vrml";
			}

			if (Directory.Exists(OverlayServer.StaticOverlayFolder)) Directory.Delete(OverlayServer.StaticOverlayFolder, true);
			Directory.CreateDirectory(OverlayServer.StaticOverlayFolder);

			try
			{
				using HttpClient webClient = new HttpClient();
				byte[] result = await webClient.GetByteArrayAsync($"{Program.APIURL}/get_overlays/{DiscordOAuth.AccessCode.series_name}/{DiscordOAuth.oauthToken}", CancellationToken.None);
				await using MemoryStream file = new MemoryStream(result);
				using ZipArchive zip = new ZipArchive(file, ZipArchiveMode.Read);
				zip.ExtractToDirectory(Path.Combine(OverlayServer.StaticOverlayFolder, route));
				RemoveHtmlExt(Path.Combine(OverlayServer.StaticOverlayFolder, route));
			}
			catch (InvalidDataException)
			{
				// an access code without overlays
			}
			catch (Exception e)
			{
				Logger.LogRow(Logger.LogType.Error, e.ToString());
			}
		}

		/// <summary>
		/// https://learn.microsoft.com/en-us/dotnet/standard/io/how-to-copy-directories
		/// </summary>
		static void CopyDirectory(string sourceDir, string destinationDir, bool recursive)
		{
			// Get information about the source directory
			DirectoryInfo dir = new DirectoryInfo(sourceDir);

			// Check if the source directory exists
			if (!dir.Exists)
			{
				throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");
			}

			// Cache directories before we start copying
			DirectoryInfo[] dirs = dir.GetDirectories();

			// Create the destination directory
			Directory.CreateDirectory(destinationDir);

			// Get the files in the source directory and copy to the destination directory
			foreach (FileInfo file in dir.GetFiles())
			{
				string targetFilePath = Path.Combine(destinationDir, file.Name);
				file.CopyTo(targetFilePath);
			}

			// If recursive and copying subdirectories, recursively call this method
			if (recursive)
			{
				foreach (DirectoryInfo subDir in dirs)
				{
					string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
					CopyDirectory(subDir.FullName, newDestinationDir, true);
				}
			}
		}

		private static void CopyObsSceneCollection()
		{
			string route = DiscordOAuth.AccessCode.series_name;
			if (route.Contains("vrml"))
			{
				route = "vrml";
			}

			string sceneCollectionFile = Path.Combine(OverlayServer.StaticOverlayFolder, route, route + ".json");
			if (route == "personal")
			{
				route = $"Spark v{Program.AppVersion().Major}.{Program.AppVersion().Minor}";
				sceneCollectionFile = Path.Combine(Path.GetDirectoryName(SparkSettings.instance.sparkExeLocation) ?? string.Empty, "resources", "obs_scene_collection.json");
			}

			string sceneCollectionDestPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "obs-studio", "basic", "scenes", route + ".json");
			if (File.Exists(sceneCollectionFile))
			{
				File.Copy(sceneCollectionFile, sceneCollectionDestPath, true);
			}
		}

		private static void RemoveHtmlExt(string path)
		{
			// Get information about the source directory
			DirectoryInfo dir = new DirectoryInfo(path);

			// Check if the source directory exists
			if (!dir.Exists)
			{
				throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");
			}

			// Cache directories before we start copying
			DirectoryInfo[] dirs = dir.GetDirectories();

			// Get the files in the source directory and copy to the destination directory
			foreach (FileInfo file in dir.GetFiles())
			{
				string fileName = file.FullName;
				if (file.Name != "index.html" && file.Name.EndsWith(".html"))
				{
					fileName = fileName.Replace(".html", "");
					if (!Directory.Exists(fileName))
					{
						file.CopyTo(fileName, true);
					}
				}
			}

			foreach (DirectoryInfo subDir in dirs)
			{
				RemoveHtmlExt(subDir.FullName);
			}
		}

// 		public static void MapRoutes(IEndpointRouteBuilder endpoints)
// 		{
// 			if (overlayData == null) return;
// 			foreach (string str in overlayData.Keys)
// 			{
// 				string[] ignoreEnds = { "index.html", ".html" };
// 				string route = DiscordOAuth.AccessCode.series_name;
// 				if (route.Contains("vrml"))
// 				{
// 					route = "vrml";
// 				}
//
// 				string url = $"/{route}/";
// 				bool ended = false;
// 				foreach (string e in ignoreEnds)
// 				{
// 					if (str.EndsWith(e))
// 					{
// 						url += str[..^e.Length];
// 						ended = true;
// 						break;
// 					}
// 				}
//
// 				if (!ended)
// 				{
// 					url += str;
// 				}
//
// 				url = url.Replace("\\", "/");
//
// 				endpoints.MapGet(url, async context =>
// 				{
// #if DEBUG
// 					await FetchOverlayData();
// #endif
//
// 					string contentType = str.Split('.').Last() switch
// 					{
// 						"js" => "application/javascript",
// 						"css" => "text/css",
// 						"png" => "image/png",
// 						"jpg" => "image/jpeg",
// 						_ => ""
// 					};
//
// 					context.Response.Headers.Add("content-type", contentType);
// 					await context.Response.WriteAsync(overlayData[str]);
// 				});
// 			}
// 		}
	}
}