using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using static Spark.CameraWrite;

namespace Spark
{
	class CameraWriteSettings
	{
		#region Settings

		public Dictionary<string, CameraTransform> waypoints { get; } = new Dictionary<string, CameraTransform>();
		public Dictionary<string, List<CameraTransform>> animations { get; } = new Dictionary<string, List<CameraTransform>>();
		public string activeAnimation { get; set; } = "";

		#endregion


		public static CameraWriteSettings instance;


		public void Save()
		{
			try
			{
				string filename = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IgniteVR", "Spark", "camerawrite_settings.json");

				Task.Run(() =>
				{
					try
					{
						if (!File.Exists(Path.GetDirectoryName(filename)))
						{
							Directory.CreateDirectory(Path.GetDirectoryName(filename));
						}

						string json = JsonConvert.SerializeObject(this, Formatting.Indented);
						File.WriteAllText(filename, json);
					}
					catch (Exception e)
					{
						Console.WriteLine($"Error writing to settings file\n{e}");
					}
				});
			}
			catch (Exception e)
			{
				Console.WriteLine($"Error writing to settings file (outside)\n{e}");
			}
		}

		public static void Load()
		{
			try
			{
				string filename = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IgniteVR", "Spark", "camerawrite_settings.json");
				if (File.Exists(filename))
				{
					string json = File.ReadAllText(filename);
					instance = JsonConvert.DeserializeObject<CameraWriteSettings>(json);
				}
				else
				{
					Console.WriteLine($"Settings file doesn't exist, creating.");
					instance = new CameraWriteSettings();
				}
			}
			catch (Exception e)
			{
				Console.WriteLine($"Error reading settings file\n{e}");
				instance = new CameraWriteSettings();
			}
		}
	}
}