using System;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Windows;
using System.Windows.Controls;

namespace Spark
{
	public partial class CreateServerControls : UserControl
	{
		public CreateServerControls()
		{
			InitializeComponent();
		}

		private static string IndexToRegion(int index)
		{
			return index switch
			{
				0 => "uscn",
				1 => "us-central-2",
				2 => "us-central-3",
				3 => "use",
				4 => "usw",
				5 => "euw",
				6 => "jp",
				7 => "aus",
				8 => "sin",
				_ => "",
			};
		}

		private static string IndexToMap(int index)
		{
			return index switch
			{
				0 => "mpl_arena_a",
				1 => "mpl_lobby_b2",
				2 => "mpl_combat_dyson",
				3 => "mpl_combat_combustion",
				4 => "mpl_combat_fission",
				5 => "mpl_combat_gauss",
				6 => "mpl_tutorial_lobby",
				7 => "mpl_tutorial_arena",
				_ => "",
			};
		}

		static readonly string[] gameTypes =
		{
			"",
			"Social_2.0_Private",
			"Social_2.0_NPE",
			"Social_2.0",
			"Echo_Arena",
			"Echo_Arena_Tournament",
			"Echo_Arena_Public_AI",
			"Echo_Arena_Practice_AI",
			"Echo_Arena_Private_AI",
			"Echo_Arena_First_Match",
			"Echo_Demo",
			"Echo_Demo_Public",
			"Echo_Arena_NPE",
			"Echo_Arena_Private",
			"Echo_Combat",
			"Echo_Combat_Tournament",
			"Echo_Combat_Private",
			"Echo_Combat_Public_AI",
			"Echo_Combat_Practice_AI",
			"Echo_Combat_Private_AI",
			"Echo_Combat_First_Match",
		};

		private static string IndexToGameType(int index)
		{
			return index < gameTypes.Length ? gameTypes[index] : "";
		}

		private void Create(object sender, RoutedEventArgs e)
		{
			// start client
			string echoPath = SparkSettings.instance.echoVRPath;
			if (!string.IsNullOrEmpty(echoPath))
			{
				try
				{
					Program.StartEchoVR(
						SparkSettings.instance.chooseRegionSpectator ? Program.JoinType.Spectator : Program.JoinType.Player,
						noovr: SparkSettings.instance.chooseRegionSpectator && SparkSettings.instance.chooseRegionNoOVR,
						level: IndexToMap(SparkSettings.instance.chooseMapIndex),
						region: IndexToRegion(SparkSettings.instance.chooseRegionIndex),
						gameType: IndexToGameType(SparkSettings.instance.chooseGameTypeIndex),
						port: 6721
					);
				}
				catch (Exception ex)
				{
					Logger.LogRow(Logger.LogType.Error, $"Error opening EchoVR Process for region selection\n{ex}");
				}
			}
			else
			{
				new MessageBox(Properties.Resources.echovr_path_not_set, Properties.Resources.Error, Program.Quit).Show();
			}
		}

		private void InstallOfflineEcho(object sender, RoutedEventArgs e)
		{
			if (string.IsNullOrEmpty(SparkSettings.instance.echoVRPath)) return;
			if (!File.Exists(SparkSettings.instance.echoVRPath)) return;

			// delete the old temp file
			if (File.Exists(Path.Combine(Path.GetTempPath(), "dbgcore.dll")))
			{
				File.Delete(Path.Combine(Path.GetTempPath(), "dbgcore.dll"));
			}

			try
			{
				WebClient webClient = new WebClient();
				webClient.DownloadFileCompleted += OfflineEchoDownloadCompleted;
				webClient.DownloadFileAsync(new Uri("https://echo-foundation.pages.dev/files/offline_echo/dbgcore.dll"), Path.Combine(Path.GetTempPath(), "dbgcore.dll"));
			}
			catch (Exception)
			{
				new MessageBox("Something broke while trying to download update", Properties.Resources.Error).Show();
			}
		}

		private void OfflineEchoDownloadCompleted(object sender, AsyncCompletedEventArgs e)
		{
			try
			{
				// install OfflineEcho from the zip
				string dir = Path.GetDirectoryName(SparkSettings.instance.echoVRPath);
				if (dir != null)
				{
					File.Copy(Path.Combine(Path.GetTempPath(), "dbgcore.dll"), Path.Combine(dir, "dbgcore.dll"), true);
				}
			}
			catch (Exception)
			{
				new MessageBox("Something broke while trying to install OfflineEcho. Report this to NtsFranz", Properties.Resources.Error).Show();
			}
		}

		private void RemoveOfflineEcho(object sender, RoutedEventArgs e)
		{
			if (string.IsNullOrEmpty(SparkSettings.instance.echoVRPath)) return;
			if (!File.Exists(SparkSettings.instance.echoVRPath)) return;
			string dir = Path.GetDirectoryName(SparkSettings.instance.echoVRPath);
			if (dir == null) return;

			try
			{
				File.Delete(Path.Combine(dir, "dbgcore.dll"));
			}
			catch (UnauthorizedAccessException)
			{
				new MessageBox("Can't uninstall OfflineEcho. Try closing EchoVR and trying again.", Properties.Resources.Error).Show();
			}
		}
	}
}