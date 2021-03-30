using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using static Logger;

namespace Spark
{
	class EchoVRSettingsManager
	{
		private static JToken settings;

		public static void Reload()
		{
			settings = ReadEchoVRSettings();
		}

		public static JToken ReadEchoVRSettings()
		{
			try
			{
				string file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "rad",
				"loneecho", "settings_mp_v2.json");
				if (!File.Exists(file))
				{
					LogRow(LogType.Error, "Can't find the EchoVR settings file");
					return null;
				}
				return JsonConvert.DeserializeObject<JToken>(File.ReadAllText(file));
			}
			catch (Exception e)
			{
				LogRow(LogType.Error, "Error when reading Arena settings.\n" + e.ToString());
			}
			return null;
		}

		public static void WriteEchoVRSettings(JToken settings)
		{
			try
			{
				string file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "rad",
					"loneecho", "settings_mp_v2.json");
				if (!File.Exists(file))
				{
					throw new NullReferenceException("Can't find the EchoVR settings file");
				}

				var settingsString = JsonConvert.SerializeObject(settings, Formatting.Indented);
				File.WriteAllText(file, settingsString);
			}
			catch (Exception e)
			{
				LogRow(LogType.Error, "Error when writing Arena settings.\n" + e.ToString());
			}
		}

		public static bool GetBool(params string[] settingsPath)
		{
			if (settings == null) Reload();
			JToken localSettings = settings;

			foreach (string path in settingsPath)
			{
				if (localSettings[path] != null)
				{
					localSettings = localSettings[path];
				}
				else
				{
					LogRow(LogType.Error, "Error finding game setting");
					return false;
				}
			}
			return (bool)localSettings;
		}
		public static float GetFloat(params string[] settingsPath)
		{
			if (settings == null) Reload();
			JToken localSettings = settings;

			foreach (string path in settingsPath)
			{
				if (localSettings[path] != null)
				{
					localSettings = localSettings[path];
				}
				else
				{
					LogRow(LogType.Error, "Error finding game setting");
					return 0;
				}
			}
			return (float)localSettings;
		}
		public static int GetInt(params string[] settingsPath)
		{
			if (settings == null) Reload();
			JToken localSettings = settings;

			foreach (string path in settingsPath)
			{
				if (localSettings[path] != null)
				{
					localSettings = localSettings[path];
				}
				else
				{
					LogRow(LogType.Error, "Error finding game setting");
					return 0;
				}
			}
			return (int)localSettings;
		}
		public static void SetBool(bool value, params string[] settingsPath)
		{
			if (settings == null) Reload();
			JToken localSettings = settings;

			for (int i = 0; i < settingsPath.Length; i++)
			{
				string path = settingsPath[i];
				if (localSettings[path] != null)
				{
					// skip the last one
					if (i < settingsPath.Length - 1)
					{
						localSettings = localSettings[path];
					}
				}
				else
				{
					LogRow(LogType.Error, "Error finding game setting");
					return;
				}
			}
			localSettings[settingsPath.Last()] = value;
			WriteEchoVRSettings(settings);
		}
		public static void SetFloat(float value, params string[] settingsPath)
		{
			if (settings == null) Reload();
			JToken localSettings = settings;

			for (int i = 0; i < settingsPath.Length; i++)
			{
				string path = settingsPath[i];
				if (localSettings[path] != null)
				{
					// skip the last one
					if (i < settingsPath.Length - 1)
					{
						localSettings = localSettings[path];
					}
				}
				else
				{
					LogRow(LogType.Error, "Error finding game setting");
					return;
				}
			}
			localSettings[settingsPath.Last()] = value;
			WriteEchoVRSettings(settings);
		}
		public static void SetInt(int value, params string[] settingsPath)
		{
			if (settings == null) Reload();
			JToken localSettings = settings;

			for (int i = 0; i < settingsPath.Length; i++)
			{
				string path = settingsPath[i];
				if (localSettings[path] != null)
				{
					// skip the last one
					if (i < settingsPath.Length - 1)
					{
						localSettings = localSettings[path];
					}
				}
				else
				{
					LogRow(LogType.Error, "Error finding game setting");
					return;
				}
			}
			localSettings[settingsPath.Last()] = value;
			WriteEchoVRSettings(settings);
		}

		#region Settings 

		public static bool EnableAPIAccess {
			get => GetBool("game", "EnableAPIAccess");
			set => SetBool(value, "game", "EnableAPIAccess");
		}
		public static bool Fullscreen {
			get => GetBool("graphics", "fullscreen");
			set => SetBool(value, "graphics", "fullscreen");
		}
		public static bool MultiResShading {
			get => GetBool("graphics", "multires");
			set => SetBool(value, "graphics", "multires");
		}
		public static bool AutoRes {
			get => GetBool("graphics", "adaptiveresolution");
			set => SetBool(value, "graphics", "adaptiveresolution");
		}
		public static bool TemporalAA {
			get => GetBool("graphics", "temporalaa");
			set => SetBool(value, "graphics", "temporalaa");
		}
		public static bool Volumetrics {
			get => GetBool("graphics", "quality", "volumetrics");
			set => SetBool(value, "graphics", "quality", "volumetrics");
		}
		public static bool Bloom {
			get => GetBool("graphics", "quality", "bloom");
			set => SetBool(value, "graphics", "quality", "bloom");
		}
		public static string Monitor {
			get => GetInt("graphics", "display").ToString();
			set => SetInt(int.Parse(value), "graphics", "display");
		}
		public static string Resolution {
			get => GetFloat("graphics", "resolutionscale").ToString();
			set => SetFloat(float.Parse(value), "graphics", "resolutionscale");
		}
		public static string FoV {
			get => GetFloat("graphics", "capturefov").ToString();
			set => SetFloat(float.Parse(value), "graphics", "capturefov");
		}
		public static string Sharpening {
			get => GetFloat("graphics", "sharpening").ToString();
			set => SetFloat(float.Parse(value), "graphics", "sharpening");
		}
		public static int AA {
			get => GetInt("graphics", "msaa");
			set => SetInt(value, "graphics", "msaa");
		}
		public static int ShadowQuality {
			get => GetInt("graphics", "quality", "shadows");
			set => SetInt(value, "graphics", "quality", "shadows");
		}
		public static int MeshQuality {
			get => GetInt("graphics", "quality", "meshes");
			set => SetInt(value, "graphics", "quality", "meshes");
		}
		public static int FXQuality {
			get => GetInt("graphics", "quality", "fx");
			set => SetInt(value, "graphics", "quality", "fx");
		}
		public static int TextureQuality {
			get => GetInt("graphics", "quality", "textures");
			set => SetInt(value, "graphics", "quality", "textures");
		}
		public static int LightingQuality {
			get => GetInt("graphics", "quality", "lights");
			set => SetInt(value, "graphics", "quality", "lights");
		}


		#endregion
	}
}
