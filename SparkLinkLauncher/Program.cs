using System;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;

namespace SparkLinkLauncher
{
	class Program
	{
		static void Main(string[] args)
		{
			try
			{
				string filename = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IgniteVR", "Spark", "settings.json");
				if (File.Exists(filename))
				{
					string json = File.ReadAllText(filename);
					SparkSettings settings = JsonSerializer.Deserialize<SparkSettings>(json);


					if (!File.Exists(settings.sparkExeLocation))
					{
						Console.WriteLine($"Path doesn't exist: {settings.sparkExeLocation}");
					}
					else
					{
						RegisterUriScheme("ignitebot", "IgniteBot Protocol", settings.sparkExeLocation);
						RegisterUriScheme("atlas", "ATLAS Protocol", settings.sparkExeLocation);
						RegisterUriScheme("spark", "Spark Protocol", settings.sparkExeLocation);
					}

				}
				else
				{
					Console.WriteLine($"Settings file doesn't exist.");

					Console.WriteLine("Press Enter to close...");
					Console.ReadLine();
				}
			}
			catch (Exception e)
			{
				Console.WriteLine($"Error reading settings file\n{e}");

				Console.WriteLine("Press Enter to close...");
				Console.ReadLine();
			}
		}

		private static void RegisterUriScheme(string UriScheme, string FriendlyName, string exePath)
		{
			try
			{
				Console.WriteLine($"[URI ASSOC] Spark path: {exePath}");

				using RegistryKey key = Registry.CurrentUser.CreateSubKey("SOFTWARE\\Classes\\" + UriScheme);

				key.SetValue("", "URL:" + FriendlyName);
				key.SetValue("URL Protocol", "");

				using RegistryKey defaultIcon = key.CreateSubKey("DefaultIcon");
				defaultIcon.SetValue("", exePath + ",1");

				using RegistryKey commandKey = key.CreateSubKey(@"shell\open\command");
				commandKey.SetValue("", "\"" + exePath + "\" \"%1\"");

				string actualValue = (string)commandKey.GetValue("");

				Console.WriteLine($"[URI ASSOC] {UriScheme} path: {actualValue}");
			}
			catch (Exception e)
			{
				Console.WriteLine($"Failed to set URI scheme\n{e}");

				Console.WriteLine("Press Enter to close...");
				Console.ReadLine();
			}
		}

		public class SparkSettings
		{
			public string sparkExeLocation { get; set; }
		}
	}
}