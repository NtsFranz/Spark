using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Spark
{
	class CameraWriteSettings
	{
		#region Settings

		public string activeAnimation { get; set; } = "";
		public Dictionary<string, CameraTransform> waypoints { get; } = new Dictionary<string, CameraTransform>();


		public float rotSpeed { get; set; } = 30;
		public float orbitRadius { get; set; } = 2;
		public float followSmoothing { get; set; } = 1f;
		public float lagCompDiscFollow { get; set; } = 0f;

		public float spaceMouseMoveSpeed { get; set; } = .5f;
		public float spaceMouseRotateSpeed { get; set; } = .2f;
		public float spaceMouseMoveExponential { get; set; } = 1.5f;
		public float spaceMouseRotateExponential { get; set; } = 1.5f;
		public float joystickMoveSpeed { get; set; } = .5f;
		public float joystickRotateSpeed { get; set; } = .1f;
		public float joystickMoveExponential { get; set; } = 1.2f;
		public float joystickRotateExponential { get; set; } = 1.2f;
		public float xPlanePosMultiplier { get; set; } = .1f;
		public bool enableHotKeys { get; set; } = false;

		#endregion


		public static CameraWriteSettings instance;
		public static Dictionary<string, AnimationKeyframes> animations;


		public void Save()
		{
			try
			{
				string settingsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IgniteVR", "Spark");
				string filename = Path.Combine(settingsFolder, "camerawrite_settings.json");
				string animationsFolder = Path.Combine(settingsFolder, "Animations");

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


						// animation files
						if (!Directory.Exists(animationsFolder))
						{
							Console.WriteLine("Animations folder doesn't exist, creating.");
							Directory.CreateDirectory(animationsFolder);
						}

						foreach (var anim in animations)
						{
							if (anim.Value != null)
							{
								string animJson = JsonConvert.SerializeObject(anim.Value, Formatting.Indented);
								File.WriteAllText(Path.Combine(animationsFolder, anim.Key + ".json"), animJson);
							}
						}
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
			string settingsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IgniteVR", "Spark");
			string filename = Path.Combine(settingsFolder, "camerawrite_settings.json");
			string animationsFolder = Path.Combine(settingsFolder, "Animations");


			try
			{
				// general settings file
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

			try
			{
				// animation files
				if (!Directory.Exists(animationsFolder))
				{
					Console.WriteLine("Animations folder doesn't exist, creating.");
					Directory.CreateDirectory(animationsFolder);
				}

				animations = new Dictionary<string, AnimationKeyframes>();
				string[] files = Directory.GetFiles(animationsFolder);
				foreach (string file in files)
				{
					string json = File.ReadAllText(file);
					AnimationKeyframes anim = JsonConvert.DeserializeObject<AnimationKeyframes>(json);
					if (anim != null)
					{
						animations[Path.GetFileNameWithoutExtension(file)] = anim;
					}
				}
			}
			catch (Exception e)
			{
				Console.WriteLine($"Error reading animation files\n{e}");
			}
		}


		public void SaveAnimation(string animName)
		{
			if (!animations.ContainsKey(animName)) return;
			if (animations[animName] == null) return;

			try
			{
				string settingsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IgniteVR", "Spark");
				string animationsFolder = Path.Combine(settingsFolder, "Animations");

				Task.Run(() =>
				{
					try
					{
						// animation files
						if (!Directory.Exists(animationsFolder))
						{
							Console.WriteLine("Animations folder doesn't exist, creating.");
							Directory.CreateDirectory(animationsFolder);
						}

						string animJson = JsonConvert.SerializeObject(animations[animName], Formatting.Indented);
						File.WriteAllText(Path.Combine(animationsFolder, animName + ".json"), animJson);
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
	}
}