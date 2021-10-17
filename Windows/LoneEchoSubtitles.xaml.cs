using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Timers;
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
		private readonly Timer outputUpdateTimer = new Timer();

		public enum LoneEchoVersion
		{
			LoneEcho1,
			LoneEcho2
		}


		public LoneEchoSubtitles()
		{
			InitializeComponent();

			grid.Background = SparkSettings.instance.loneEchoSubtitlesStreamerMode ? Brushes.Green : Brushes.Black;

			outputUpdateTimer.Interval = 100;
			outputUpdateTimer.Elapsed += Update;
			outputUpdateTimer.Enabled = true;
		}

		private void Update(object sender, EventArgs e)
		{
			try
			{
				FindLoneEchoInstallationLocation((LoneEchoVersion)SparkSettings.instance.loneEchoVersion);

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
				switch ((LoneEchoVersion) SparkSettings.instance.loneEchoVersion)
				{
					case LoneEchoVersion.LoneEcho1:
					{
						if (string.IsNullOrEmpty(SparkSettings.instance.loneEchoPath))
						{
							statusLabel.Text = "Lone Echo installation not found";
							statusLabel.Foreground = Brushes.Red;
							logFileFound = false;
							return;
						}

						logPathBase = SparkSettings.instance.loneEchoPath[..SparkSettings.instance.loneEchoPath.LastIndexOf("\\bin", StringComparison.Ordinal)] + "\\_local\\r14logs\\";
						break;
					}
					case LoneEchoVersion.LoneEcho2:
					{
						if (string.IsNullOrEmpty(SparkSettings.instance.loneEcho2Path))
						{
							statusLabel.Text = "Lone Echo 2 installation not found";
							statusLabel.Foreground = Brushes.Red;
							logFileFound = false;
							return;
						}

						logPathBase = SparkSettings.instance.loneEcho2Path[..SparkSettings.instance.loneEcho2Path.LastIndexOf("\\bin", StringComparison.Ordinal)] + "\\_local\\r14logs\\";
						break;
					}
				}


				directory = new DirectoryInfo(logPathBase);
				string newLogPath = directory.GetFiles().OrderByDescending(f => f.LastWriteTime).First()
					.ToString();

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

		private static void FindLoneEchoInstallationLocation(LoneEchoVersion loneEchoVersion)
		{
			// skip if we already have a valid path
			if (File.Exists(SparkSettings.instance.loneEchoPath)) return;

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

				switch (loneEchoVersion)
				{
					case LoneEchoVersion.LoneEcho1:
					{
						const string echoDir = "Software\\ready-at-dawn-lone-echo\\bin\\win7\\loneecho.exe";
						foreach (string file in paths.Select(path => Path.Combine(path, echoDir)).Where(File.Exists))
						{
							SparkSettings.instance.loneEchoPath = file;
							return;
						}

						break;
					}
					case LoneEchoVersion.LoneEcho2:
					{
						const string echoDir = "Software\\ready-at-dawn-lone-echo-2\\bin\\win10\\loneecho2.exe";
						foreach (string file in paths.Select(path => Path.Combine(path, echoDir)).Where(File.Exists))
						{
							SparkSettings.instance.loneEcho2Path = file;
							return;
						}

						break;
					}
				}
			}
			catch (Exception e)
			{
				Logger.LogRow(Logger.LogType.Error, $"Can't get Lone Echo path from registry\n{e}");
			}
		}

		public bool StreamerModeChecked
		{
			get => SparkSettings.instance.loneEchoSubtitlesStreamerMode;
			set
			{
				grid.Background = value ? Brushes.Green : Brushes.Black; //(Brush) FindResource("ContainerBackground");
				SparkSettings.instance.loneEchoSubtitlesStreamerMode = value;
			}
		}

		public int LoneEchoVersionDropdown
		{
			get => SparkSettings.instance.loneEchoVersion;
			set { SparkSettings.instance.loneEchoVersion = value; }
		}


		private void CloseClick(object sender, RoutedEventArgs e)
		{
			SparkSettings.instance.Save();
			Close();
		}
	}
}