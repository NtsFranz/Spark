using Spark.Properties;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;

namespace Spark
{
	/// <summary>
	/// Interaction logic for LoneEchoSubtitles.xaml
	/// </summary>
	public partial class LoneEchoSubtitles : Window
	{
		private string logPathBase;
		private string logPath;
		private DirectoryInfo directory;
		private StreamReader reader;
		private bool logFileFound;
		private readonly System.Timers.Timer outputUpdateTimer = new();


		public LoneEchoSubtitles()
		{
			InitializeComponent();

			grid.Background = Settings.Default.loneEchoSubtitlesStreamerMode ? Brushes.Green : Brushes.Black;

			outputUpdateTimer.Interval = 100;
			outputUpdateTimer.Elapsed += Update;
			outputUpdateTimer.Enabled = true;
		}

		private void Update(object sender, EventArgs e)
		{
			try
			{
				FindLoneEchoInstallationLocation();

				Dispatcher.Invoke(() =>
				{
					FindLogFile();

					if (!logFileFound) return;

					string line = reader.ReadLine();
					if (line == null) return;

					if (line.Contains("[DIALOGUE]") &&
					    !line.Contains("[REQUEST]") &&
					    !line.Contains("Aborting dialogue") &&
					    !line.Contains("Finishing dialogue"))
					{
						subtitlesText.Text = line[(line.IndexOf("[DIALOGUE]", StringComparison.Ordinal) + 10)..];
					}
				});
			}
			catch (Exception ex)
			{
				Logger.LogRow(Logger.LogType.Error, $"Error in Lone Echo subtitles Update. {ex}");
			}
		}

		private void FindLogFile()
		{
			try
			{
				if (string.IsNullOrEmpty(Settings.Default.loneEchoPath))
				{
					statusLabel.Text = "Lone Echo installation not found";
					statusLabel.Foreground = Brushes.Red;
					logFileFound = false;
					return;
				}

				logPathBase =
					Settings.Default.loneEchoPath[
						..Settings.Default.loneEchoPath.LastIndexOf("\\bin", StringComparison.Ordinal)] +
					"\\_local\\r14logs\\";

				directory = new DirectoryInfo(logPathBase);
				string newLogPath = directory.GetFiles().OrderByDescending(f => f.LastWriteTime).First().ToString();

				if (newLogPath.Equals(logPath)) return;
				logPath = newLogPath;


				reader = new StreamReader(new FileStream(logPath, FileMode.Open, FileAccess.Read,
					FileShare.ReadWrite));

				reader.ReadToEnd();

				statusLabel.Text = "Using log file: " + logPath;
				statusLabel.Foreground = Brushes.White;

				logFileFound = true;
			}
			catch (Exception e)
			{
				Logger.LogRow(Logger.LogType.Error, $"Error finding log file. {e}");
				statusLabel.Text = "No log file found";
				statusLabel.Foreground = Brushes.Yellow;
				logFileFound = false;
			}
		}

		private static void FindLoneEchoInstallationLocation()
		{
			// skip if we already have a valid path
			if (File.Exists(Settings.Default.loneEchoPath)) return;

			try
			{
				if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
				const string key = "Software\\Oculus VR, LLC\\Oculus\\Libraries";
				RegistryKey oculusReg = Registry.CurrentUser.OpenSubKey(key);
				if (oculusReg == null)
				{
					// Oculus not installed
					return;
				}

				List<string> paths = oculusReg.GetSubKeyNames()
					.Select(subkey => (string) oculusReg.OpenSubKey(subkey)?.GetValue("OriginalPath")).ToList();

				const string echoDir = "Software\\ready-at-dawn-lone-echo\\bin\\win7\\loneecho.exe";
				foreach (string file in paths.Select(path => Path.Combine(path, echoDir)).Where(File.Exists))
				{
					Settings.Default.loneEchoPath = file;
					Settings.Default.Save();
					return;
				}
			}
			catch (Exception e)
			{
				Logger.LogRow(Logger.LogType.Error, $"Can't get Lone Echo path from registry\n{e}");
			}
		}

		public bool StreamerModeChecked
		{
			get => Settings.Default.loneEchoSubtitlesStreamerMode;
			set
			{
				grid.Background = value ? Brushes.Green : Brushes.Black; //(Brush) FindResource("ContainerBackground");
				Settings.Default.loneEchoSubtitlesStreamerMode = value;
				Settings.Default.Save();
			}
		}


		private void CloseClick(object sender, RoutedEventArgs e)
		{
			Settings.Default.Save();
			Close();
		}
	}
}