using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.Windows.Navigation;
using System.Windows.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net;
using EchoVRAPI;

namespace Spark
{
	/// <summary>
	/// Interaction logic for UnifiedSettingsWindow.xaml
	/// </summary>
	public partial class UnifiedSettingsWindow
	{
		// set to false initially so that loading the settings from disk doesn't activate the events
		private bool initialized;

		/// <summary>
		/// Set to true once the opt in status fetched.
		/// </summary>
		private bool optInFound;


		public UnifiedSettingsWindow()
		{
			InitializeComponent();
		}

		private void WindowLoad(object sender, RoutedEventArgs e)
		{
			//Initialize();

			optInCheckbox.IsEnabled = false;
			optInStatusLabel.Content = "Fetching opt-in status...";
			_ = GetOptInStatus();


#if WINDOWS_STORE_RELEASE
			enableBetasCheckbox.Visibility = Visibility.Collapsed;
#endif


			ThisPCLocalIP.Text = $"This PC's Local IP: {QuestIPFetching.GetLocalIP()} (for PC-PC Spectate Me)";

			CameraModeDropdownChanged(SparkSettings.instance.spectatorCamera);

			if (SparkSettings.instance.mutePlayerComms)
			{
				MutePlayerCommsDropdown.SelectedIndex = 2;
			}
			else if (SparkSettings.instance.muteEnemyTeam)
			{
				MutePlayerCommsDropdown.SelectedIndex = 1;
			}
			else
			{
				MutePlayerCommsDropdown.SelectedIndex = 0;
			}

			initialized = true;
		}


		#region General

		public static bool StartWithWindows
		{
			get => SparkSettings.instance.startOnBoot;
			set
			{
				SparkSettings.instance.startOnBoot = value;

				RegistryKey rk =
					Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);

				if (value)
				{
					string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Spark.exe");
					rk?.SetValue(Properties.Resources.AppName, path);
				}
				else
					rk?.DeleteValue(Properties.Resources.AppName, false);
			}
		}

		private void CloseButtonEvent(object sender, RoutedEventArgs e)
		{
			SparkSettings.instance.Save();
			Close();
		}

		private void EchoVRIPChanged(object sender, TextChangedEventArgs e)
		{
			if (!initialized) return;
			Program.echoVRIP = ((TextBox)sender).Text;
			SparkSettings.instance.echoVRIP = Program.echoVRIP;
			SaveQuestIPButton.Visibility = Visibility.Visible;
		}

		private void EchoVRPortChanged(object sender, TextChangedEventArgs e)
		{
			if (!initialized) return;
			if (Program.overrideEchoVRPort)
			{
				EchoVRPortTextBox.Text = SparkSettings.instance.echoVRPort.ToString();
			}
			else
			{
				if (int.TryParse(((TextBox)sender).Text, out Program.echoVRPort))
				{
					SparkSettings.instance.echoVRPort = Program.echoVRPort;
				}
			}
		}

		private void ResetIP_Click(object sender, RoutedEventArgs e)
		{
			if (!initialized) return;
			Program.echoVRIP = "127.0.0.1";
			if (!Program.overrideEchoVRPort) Program.echoVRPort = 6721;
			EchoVRIPTextBox.Text = Program.echoVRIP;
			EchoVRPortTextBox.Text = Program.echoVRPort.ToString();
			SparkSettings.instance.echoVRIP = Program.echoVRIP;
			if (!Program.overrideEchoVRPort) SparkSettings.instance.echoVRPort = Program.echoVRPort;
			SaveQuestIPButton.Visibility = Visibility.Collapsed;
		}

		private void ExecutableLocationChanged(object sender, TextChangedEventArgs e)
		{
			if (!initialized) return;
			string path = ((TextBox)sender).Text;
			if (File.Exists(path))
			{
				ExeLocationLabel.Content = "EchoVR Executable Location:";
				SparkSettings.instance.echoVRPath = path;
			}
			else
			{
				ExeLocationLabel.Content = "EchoVR Executable Location:   (not valid)";
			}
		}

		private async void FindQuestClick(object sender, RoutedEventArgs e)
		{
			if (!initialized) return;
			FindQuestStatusLabel.Content = Properties.Resources.Searching_for_Quest_on_network;
			FindQuestStatusLabel.Visibility = Visibility.Visible;
			EchoVRIPTextBox.IsEnabled = false;
			EchoVRPortTextBox.IsEnabled = false;
			FindQuest.IsEnabled = false;
			ResetIP.IsEnabled = false;
			SaveQuestIPButton.Visibility = Visibility.Collapsed;

			Progress<string> progress = new Progress<string>(s => FindQuestStatusLabel.Content = s);
			await Task.Factory.StartNew(() => Program.echoVRIP = QuestIPFetching.FindQuestIP(progress), TaskCreationOptions.None);

			// if we failed with this method, scan the network with API requests instead
			if ((string)FindQuestStatusLabel.Content == Properties.Resources.Failed_to_find_Quest_on_network_)
			{
				FindQuestStatusLabel.Content = Properties.Resources.Failed__Scanning_for_Echo_VR_API_instead__This_may_take_a_while_;
				await Task.Run(async () =>
				{
					Progress<float> searchProgress = new Progress<float>();
					searchProgress.ProgressChanged += (o, val) => { Dispatcher.Invoke(() => { FindQuestStatusLabel.Content = $"{Properties.Resources.Failed__Scanning_for_Echo_VR_API_instead__This_may_take_a_while_}\t{val:P0}"; }); };
					List<IPAddress> ips = QuestIPFetching.GetPossibleLocalIPs();
					List<(IPAddress, string)> responses = await QuestIPFetching.PingEchoVRAPIAsync(ips, 20, searchProgress);
					IPAddress found = responses.FirstOrDefault(r => r.Item2 != null).Item1;
					Dispatcher.Invoke(() =>
					{
						if (found != null)
						{
							Program.echoVRIP = found.ToString();

							FindQuestStatusLabel.Content = Properties.Resources.Found_Quest_on_network_;
						}
						else
						{
							FindQuestStatusLabel.Content = Properties.Resources.Failed_to_find_Quest_on_network__Make_sure_you_are_in_a_private_public_match_and_API_Access_is_enabled_;
						}
					});
				});
			}

			EchoVRIPTextBox.IsEnabled = true;
			EchoVRPortTextBox.IsEnabled = true;
			FindQuest.IsEnabled = true;
			ResetIP.IsEnabled = true;
			if (!Program.overrideEchoVRPort) Program.echoVRPort = 6721;
			EchoVRIPTextBox.Text = Program.echoVRIP;
			EchoVRPortTextBox.Text = Program.echoVRPort.ToString();
			SparkSettings.instance.echoVRIP = Program.echoVRIP;
			if (!Program.overrideEchoVRPort) SparkSettings.instance.echoVRPort = Program.echoVRPort;
			SaveQuestIPButton.Visibility = Visibility.Collapsed;
		}

		private void ShowFirstTimeSetupWindowClicked(object sender, RoutedEventArgs e)
		{
			if (!initialized) return;

			Program.ToggleWindow(typeof(FirstTimeSetupWindow));
		}

		public static Visibility FirestoreVisible => !DiscordOAuth.Personal ? Visibility.Visible : Visibility.Collapsed;

		public static string ReplayFilename => string.IsNullOrEmpty(Program.replayFilesManager.fileName) ? "---" : Program.replayFilesManager.fileName;

		private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
		{
			try
			{
				Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
				e.Handled = true;
			}
			catch (Exception ex)
			{
				Logger.LogRow(Logger.LogType.Error, ex.ToString());
			}
		}

		public int SetTheme
		{
			get => SparkSettings.instance.theme;
			set
			{
				SparkSettings.instance.theme = value;

				ThemesController.SetTheme((ThemesController.ThemeTypes)value);
			}
		}

		private async Task GetOptInStatus()
		{
			if (string.IsNullOrEmpty(SparkSettings.instance.client_name))
			{
				optInFound = true;
				optInCheckbox.IsEnabled = false;
				optInStatusLabel.Content = "Run the game once to find your Oculus name.";
				return;
			}

			if (DiscordOAuth.oauthToken == string.Empty)
			{
				optInFound = true;
				optInCheckbox.IsEnabled = false;
				optInStatusLabel.Content = "Log into Discord to be able to opt in.";
				return;
			}

			try
			{
				string resp = await FetchUtils.GetRequestAsync(
					$"{Program.APIURL}/optin/get/{SparkSettings.instance.client_name}",
					new Dictionary<string, string> { { "x-api-key", DiscordOAuth.igniteUploadKey } });

				JToken objResp = JsonConvert.DeserializeObject<JToken>(resp);
				if (objResp?["opted_in"] != null)
				{
					optInCheckbox.IsChecked = (bool)objResp["opted_in"];
				}
				else
				{
					Logger.LogRow(Logger.LogType.Error, $"Couldn't get opt-in status.");
					optInStatusLabel.Content = "Failed to get opt-in status. Response invalid.";
				}
			}
			catch (Exception e)
			{
				Logger.LogRow(Logger.LogType.Error, $"Couldn't get opt-in status.\n{e}");
				optInStatusLabel.Content = "Failed to get opt-in status.";
			}

			optInFound = true;
			optInCheckbox.IsEnabled = true;
			optInStatusLabel.Content = $"Oculus Username: {SparkSettings.instance.client_name}";
		}

		private void OptIn(object sender, RoutedEventArgs e)
		{
			if (!optInFound) return;

			FetchUtils.PostRequestCallback(
				$"{Program.APIURL}/optin/set/{SparkSettings.instance.client_name}/{((CheckBox)sender).IsChecked}",
				new Dictionary<string, string>
				{
					{ "x-api-key", DiscordOAuth.igniteUploadKey }, { "token", DiscordOAuth.oauthToken }
				},
				string.Empty,
				(resp) =>
				{
					if (resp.Contains("opted in"))
					{
						optInCheckbox.IsChecked = true;
					}
					else if (resp.Contains("opted normal"))
					{
						optInCheckbox.IsChecked = false;
					}
				});
		}

		#endregion

		#region Replays

		private void OpenReplayFolder(object sender, RoutedEventArgs e)
		{
			if (!initialized) return;
			Process.Start(new ProcessStartInfo
			{
				FileName = SparkSettings.instance.saveFolder,
				UseShellExecute = true
			});
		}

		private void ResetReplayFolder(object sender, RoutedEventArgs e)
		{
			if (!initialized) return;
			SparkSettings.instance.saveFolder =
				Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Spark", "replays");
			Directory.CreateDirectory(SparkSettings.instance.saveFolder);
			storageLocationTextBox.Text = SparkSettings.instance.saveFolder;
		}

		private void SetStorageLocation(object sender, RoutedEventArgs e)
		{
			if (!initialized) return;
			string selectedPath = "";
			CommonOpenFileDialog folderBrowserDialog = new CommonOpenFileDialog
			{
				InitialDirectory = SparkSettings.instance.saveFolder,
				IsFolderPicker = true
			};
			if (folderBrowserDialog.ShowDialog() == CommonFileDialogResult.Ok)
			{
				selectedPath = folderBrowserDialog.FileName;
			}

			if (selectedPath != "")
			{
				SetStorageLocation(selectedPath);
				Console.WriteLine(selectedPath);
			}
		}

		private void SetStorageLocation(string path)
		{
			SparkSettings.instance.saveFolder = path;
			storageLocationTextBox.Text = SparkSettings.instance.saveFolder;
		}

		private void SplitFileButtonClicked(object sender, RoutedEventArgs e)
		{
			if (!initialized) return;
			Program.replayFilesManager.Split();
		}

		#endregion

		#region TTS

		public static bool DiscordLoggedIn => DiscordOAuth.IsLoggedIn;

		public static Visibility DiscordNotLoggedInVisible =>
			DiscordOAuth.IsLoggedIn ? Visibility.Collapsed : Visibility.Visible;

		public static bool JoustTime
		{
			get => SparkSettings.instance.joustTimeTTS;
			set
			{
				SparkSettings.instance.joustTimeTTS = value;

				if (value) Program.synth.SpeakAsync($"{Team.TeamColor.orange.ToLocalizedString()} 1.8");
				Console.WriteLine($"{Team.TeamColor.orange.ToLocalizedString()} 1.8");
			}
		}

		public static bool JoustSpeed
		{
			get => SparkSettings.instance.joustSpeedTTS;
			set
			{
				SparkSettings.instance.joustSpeedTTS = value;

				if (value)
					Program.synth.SpeakAsync(
						$"{Team.TeamColor.orange.ToLocalizedString()} 32 {Properties.Resources.tts_meters_per_second}");
			}
		}

		public static bool ServerLocation
		{
			get => SparkSettings.instance.serverLocationTTS;
			set
			{
				SparkSettings.instance.serverLocationTTS = value;

				if (value) Program.synth.SpeakAsync("Chicago, Illinois");
			}
		}

		public static bool MaxBoostSpeed
		{
			get => SparkSettings.instance.maxBoostSpeedTTS;
			set
			{
				SparkSettings.instance.maxBoostSpeedTTS = value;

				if (value) Program.synth.SpeakAsync($"32 {Properties.Resources.tts_meters_per_second}");
			}
		}

		public static bool TubeExitSpeed
		{
			get => SparkSettings.instance.tubeExitSpeedTTS;
			set
			{
				SparkSettings.instance.tubeExitSpeedTTS = value;

				if (value) Program.synth.SpeakAsync($"32 {Properties.Resources.tts_meters_per_second}");
			}
		}

		public static int SpeechSpeed
		{
			get => SparkSettings.instance.TTSSpeed;
			set
			{
				Program.synth.SetRate(value);

				if (value != SparkSettings.instance.TTSSpeed)
					Program.synth.SpeakAsync(Properties.Resources.This_is_the_new_speed);

				SparkSettings.instance.TTSSpeed = value;
			}
		}

		public static bool GamePaused
		{
			get => SparkSettings.instance.pausedTTS;
			set
			{
				SparkSettings.instance.pausedTTS = value;

				if (value)
					Program.synth.SpeakAsync(
						$"{Team.TeamColor.orange.ToLocalizedString()} {Properties.Resources.tts_paused}");
			}
		}

		public static bool PlayerJoin
		{
			get => SparkSettings.instance.playerJoinTTS;
			set
			{
				SparkSettings.instance.playerJoinTTS = value;

				if (value)
					Program.synth.SpeakAsync(
						$"NtsFranz {Properties.Resources.tts_join_1} {Team.TeamColor.orange.ToLocalizedString()} {Properties.Resources.tts_join_2}");
			}
		}

		public static bool PlayerLeave
		{
			get => SparkSettings.instance.playerLeaveTTS;
			set
			{
				SparkSettings.instance.playerLeaveTTS = value;

				if (value)
					Program.synth.SpeakAsync(
						$"NtsFranz {Properties.Resources.tts_leave_1} {Team.TeamColor.orange.ToLocalizedString()} {Properties.Resources.tts_leave_2}");
			}
		}

		public static bool PlayerSwitch
		{
			get => SparkSettings.instance.playerSwitchTeamTTS;
			set
			{
				SparkSettings.instance.playerSwitchTeamTTS = value;

				if (value)
					Program.synth.SpeakAsync(
						$"NtsFranz {Properties.Resources.tts_switch_1} {Team.TeamColor.blue.ToLocalizedString()} {Properties.Resources.tts_switch_2} {Team.TeamColor.orange.ToLocalizedString()} {Properties.Resources.tts_switch_3}");
			}
		}

		public static bool ThrowSpeed
		{
			get => SparkSettings.instance.throwSpeedTTS;
			set
			{
				SparkSettings.instance.throwSpeedTTS = value;

				if (value) Program.synth.SpeakAsync("19");
			}
		}

		public static bool GoalSpeed
		{
			get => SparkSettings.instance.goalSpeedTTS;
			set
			{
				SparkSettings.instance.goalSpeedTTS = value;

				if (value) Program.synth.SpeakAsync($"19 {Properties.Resources.tts_meters_per_second}");
			}
		}

		public static bool GoalDistance
		{
			get => SparkSettings.instance.goalDistanceTTS;
			set
			{
				SparkSettings.instance.goalDistanceTTS = value;

				if (value) Program.synth.SpeakAsync($"23 {Properties.Resources.tts_meters}");
			}
		}


		public static bool RulesChanged
		{
			get => SparkSettings.instance.rulesChangedTTS;
			set
			{
				SparkSettings.instance.rulesChangedTTS = value;

				if (value)
				{
					Program.synth.SpeakAsync($"NtsFranz changed the rules");
				}
			}
		}

		#endregion


		#region EchoVR Settings

		public Visibility EchoVRSettingsProgramOpenWarning =>
			Program.GetEchoVRProcess()?.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

		public Visibility EchoVRInstalled =>
			File.Exists(SparkSettings.instance.echoVRPath) ? Visibility.Visible : Visibility.Collapsed;

		public Visibility EchoVRNotInstalled => !File.Exists(SparkSettings.instance.echoVRPath)
			? Visibility.Visible
			: Visibility.Collapsed;

		public bool Fullscreen
		{
			get => EchoVRSettingsManager.Fullscreen;
			set => EchoVRSettingsManager.Fullscreen = value;
		}

		public bool MultiResShading
		{
			get => EchoVRSettingsManager.MultiResShading;
			set => EchoVRSettingsManager.MultiResShading = value;
		}

		public bool AutoRes
		{
			get => EchoVRSettingsManager.AutoRes;
			set => EchoVRSettingsManager.AutoRes = value;
		}

		public bool TemporalAA
		{
			get => EchoVRSettingsManager.TemporalAA;
			set => EchoVRSettingsManager.TemporalAA = value;
		}

		public bool Volumetrics
		{
			get => EchoVRSettingsManager.Volumetrics;
			set => EchoVRSettingsManager.Volumetrics = value;
		}

		public bool Bloom
		{
			get => EchoVRSettingsManager.Bloom;
			set => EchoVRSettingsManager.Bloom = value;
		}

		public string Monitor
		{
			get => EchoVRSettingsManager.Monitor.ToString();
			set => EchoVRSettingsManager.Monitor = int.Parse(value);
		}

		public string Resolution
		{
			get => EchoVRSettingsManager.Resolution.ToString();
			set => EchoVRSettingsManager.Resolution = float.Parse(value);
		}

		public string FoV
		{
			get => EchoVRSettingsManager.FoV.ToString();
			set => EchoVRSettingsManager.FoV = float.Parse(value);
		}

		public string Sharpening
		{
			get => EchoVRSettingsManager.Sharpening.ToString();
			set => EchoVRSettingsManager.Sharpening = float.Parse(value);
		}

		public int AA
		{
			get => EchoVRSettingsManager.AA;
			set => EchoVRSettingsManager.AA = value;
		}

		public int ShadowQuality
		{
			get => EchoVRSettingsManager.ShadowQuality;
			set => EchoVRSettingsManager.ShadowQuality = value;
		}

		public int MeshQuality
		{
			get => EchoVRSettingsManager.MeshQuality;
			set => EchoVRSettingsManager.MeshQuality = value;
		}

		public int FXQuality
		{
			get => EchoVRSettingsManager.FXQuality;
			set => EchoVRSettingsManager.FXQuality = value;
		}

		public int TextureQuality
		{
			get => EchoVRSettingsManager.TextureQuality;
			set => EchoVRSettingsManager.TextureQuality = value;
		}

		public int LightingQuality
		{
			get => EchoVRSettingsManager.LightingQuality;
			set => EchoVRSettingsManager.LightingQuality = value;
		}


		private void RefreshAllSettings(object sender, SelectionChangedEventArgs e)
		{
			if (initialized)
			{
				DataContext = null;
				DataContext = this;
			}
		}

		#endregion


		public static string AppVersionLabelText => $"v{Program.AppVersionString()}";


		private void CameraModeDropdownChanged(object sender, SelectionChangedEventArgs e)
		{
			if (!initialized) return;

			// setting already handled in binding
			ComboBox box = (ComboBox)sender;
			CameraModeDropdownChanged(box.SelectedIndex);
		}

		private void CameraModeDropdownChanged(int index)
		{
			switch (index)
			{
				case 0:
				case 1:
					followSpecificPlayerPanel.Visibility = Visibility.Collapsed;
					followCameraModeLabel.Visibility = Visibility.Collapsed;
					followCameraModeDropdown.Visibility = Visibility.Collapsed;
					discholderFollowTeamCheckbox.Visibility = Visibility.Collapsed;
					discholderFollowCameraModeLabel.Visibility = Visibility.Collapsed;
					discholderFollowCameraModeDropdown.Visibility = Visibility.Collapsed;
					break;
				case 2:
					followSpecificPlayerPanel.Visibility = Visibility.Collapsed;
					followCameraModeLabel.Visibility = Visibility.Visible;
					followCameraModeDropdown.Visibility = Visibility.Visible;
					discholderFollowTeamCheckbox.Visibility = Visibility.Collapsed;
					discholderFollowCameraModeLabel.Visibility = Visibility.Collapsed;
					discholderFollowCameraModeDropdown.Visibility = Visibility.Collapsed;
					break;
				case 3:
					followSpecificPlayerPanel.Visibility = Visibility.Visible;
					followCameraModeLabel.Visibility = Visibility.Visible;
					followCameraModeDropdown.Visibility = Visibility.Visible;
					discholderFollowTeamCheckbox.Visibility = Visibility.Collapsed;
					discholderFollowCameraModeLabel.Visibility = Visibility.Collapsed;
					discholderFollowCameraModeDropdown.Visibility = Visibility.Collapsed;
					break;
				case 4:
					followSpecificPlayerPanel.Visibility = Visibility.Collapsed;
					followCameraModeLabel.Visibility = Visibility.Collapsed;
					followCameraModeDropdown.Visibility = Visibility.Collapsed;
					discholderFollowTeamCheckbox.Visibility = Visibility.Visible;
					discholderFollowCameraModeLabel.Visibility = Visibility.Visible;
					discholderFollowCameraModeDropdown.Visibility = Visibility.Visible;
					break;
			}
		}

		private void SpectatorCameraFindNow(object sender, RoutedEventArgs e)
		{
			CameraWriteController.UseCameraControlKeys();
		}

		private void HideEchoVRUINow(object sender, RoutedEventArgs e)
		{
			if (!Program.InGame) return;
			CameraWriteController.SetUIVisibility(HideUICheckbox.IsChecked != true);
		}

		private void HideMinimapNow(object sender, RoutedEventArgs e)
		{
			if (!Program.InGame) return;
			CameraWriteController.SetMinimapVisibility(HideMinimapCheckbox.IsChecked != true);
		}

		private void ToggleNameplatesNow(object sender, RoutedEventArgs e)
		{
			if (!Program.InGame) return;
			CameraWriteController.SetNameplatesVisibility(HideNameplatesCheckbox.IsChecked != true);
		}

		private void UploadTabletStats(object sender, RoutedEventArgs e)
		{
			List<TabletStats> stats = Program.FindTabletStats();

			if (stats != null)
			{
				new UploadTabletStatsMenu(stats) { Owner = this }.Show();
			}
		}

		private void ResetAllSettings(object sender, RoutedEventArgs e)
		{
			EchoVRSettingsManager.ResetAllSettingsToDefault();

			RefreshAllSettings(sender, null);
		}

		private void MutePlayerCommsDropdownChanged(object sender, SelectionChangedEventArgs e)
		{
			if (!initialized) return;

			int index = ((ComboBox)sender).SelectedIndex;
			switch (index)
			{
				case 0: // leave default
					SparkSettings.instance.mutePlayerComms = false;
					SparkSettings.instance.muteEnemyTeam = false;
					CameraWriteController.SetTeamsMuted(false, false);
					break;
				case 1: // mute enemy team
					SparkSettings.instance.mutePlayerComms = false;
					SparkSettings.instance.muteEnemyTeam = true;
					break;
				case 2: // mute both teams
					SparkSettings.instance.mutePlayerComms = true;
					SparkSettings.instance.muteEnemyTeam = false;
					break;
			}

			CameraWriteController.SetPlayersMuted();
		}


		private void ClearTTSCacheButton(object sender, RoutedEventArgs e)
		{
			TTSController.ClearCacheFolder();
		}

		private void OpenTTSCacheButton(object sender, RoutedEventArgs e)
		{
			string folder = TTSController.CacheFolder;
			if (!Directory.Exists(Path.GetDirectoryName(folder)))
			{
				Directory.CreateDirectory(folder);
			}

			if (Directory.Exists(folder))
			{
				Process.Start(new ProcessStartInfo
				{
					FileName = folder,
					UseShellExecute = true
				});
			}
		}

		private void OpenSettingsFileFolder(object sender, RoutedEventArgs e)
		{
			string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IgniteVR", "Spark");
			if (!Directory.Exists(Path.GetDirectoryName(folder)))
			{
				Directory.CreateDirectory(folder);
			}

			if (Directory.Exists(folder))
			{
				Process.Start(new ProcessStartInfo
				{
					FileName = folder,
					UseShellExecute = true
				});
			}
		}

		private void InstallReshade(object sender, RoutedEventArgs e)
		{
			if (string.IsNullOrEmpty(SparkSettings.instance.echoVRPath)) return;
			if (!File.Exists(SparkSettings.instance.echoVRPath)) return;

			ReshadeProgress.Visibility = Visibility.Visible;
			ReshadeProgress.Value = 0;

			// delete the old temp file
			if (File.Exists(Path.Combine(Path.GetTempPath(), "reshade.zip")))
			{
				File.Delete(Path.Combine(Path.GetTempPath(), "reshade.zip"));
			}

			// download reshade
			try
			{
				WebClient webClient = new WebClient();
				webClient.DownloadFileCompleted += ReshadeDownloadCompleted;
				webClient.DownloadProgressChanged += ReshadeDownloadProgressChanged;
				webClient.DownloadFileAsync(new Uri("https://github.com/NtsFranz/Spark/raw/main/resources/reshade.zip"), Path.Combine(Path.GetTempPath(), "reshade.zip"));
			}
			catch (Exception)
			{
				new MessageBox("Something broke while trying to download update", Properties.Resources.Error).Show();
			}
		}

		private void ReshadeDownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
		{
			ReshadeProgress.Visibility = Visibility.Visible;
			ReshadeProgress.Value = e.ProgressPercentage;
		}

		private void ReshadeDownloadCompleted(object sender, AsyncCompletedEventArgs e)
		{
			try
			{
				// install reshade from the zip
				string dir = Path.GetDirectoryName(SparkSettings.instance.echoVRPath);
				if (dir != null)
				{
					ZipFile.ExtractToDirectory(Path.Combine(Path.GetTempPath(), "reshade.zip"), dir, true);
				}
			}
			catch (Exception)
			{
				new MessageBox("Something broke while trying to install Reshade. Report this to NtsFranz", Properties.Resources.Error).Show();
			}

			ReshadeProgress.Visibility = Visibility.Collapsed;
		}

		private void RemoveReshade(object sender, RoutedEventArgs e)
		{
			if (string.IsNullOrEmpty(SparkSettings.instance.echoVRPath)) return;
			if (!File.Exists(SparkSettings.instance.echoVRPath)) return;
			string dir = Path.GetDirectoryName(SparkSettings.instance.echoVRPath);
			if (dir == null) return;

			try
			{
				File.Delete(Path.Combine(dir, "DefaultPreset.ini"));
				File.Delete(Path.Combine(dir, "dxgi.dll"));
				File.Delete(Path.Combine(dir, "dxgi.log"));
				File.Delete(Path.Combine(dir, "ReShade.ini"));
				File.Delete(Path.Combine(dir, "Reshade.log"));
				File.Delete(Path.Combine(dir, "ReshadePreset.ini"));
				if (Directory.Exists(Path.Combine(dir, "reshade-shaders")))
				{
					Directory.Delete(Path.Combine(dir, "reshade-shaders"), true);
				}
			}
			catch (UnauthorizedAccessException)
			{
				new MessageBox("Can't uninstall Reshade. Try closing EchoVR and trying again.", Properties.Resources.Error).Show();
			}
		}		
		
		private void InstallRumble(object sender, RoutedEventArgs e)
		{
			if (string.IsNullOrEmpty(SparkSettings.instance.echoVRPath)) return;
			if (!File.Exists(SparkSettings.instance.echoVRPath)) return;

			RumbleProgress.Visibility = Visibility.Visible;
			RumbleProgress.Value = 0;

			// delete the old temp file
			if (File.Exists(Path.Combine(Path.GetTempPath(), "dbgcore.dll")))
			{
				File.Delete(Path.Combine(Path.GetTempPath(), "dbgcore.dll"));
			}

			// download reshade
			try
			{
				WebClient webClient = new WebClient();
				webClient.DownloadFileCompleted += RumbleDownloadCompleted;
				webClient.DownloadProgressChanged += RumbleDownloadProgressChanged;
				webClient.DownloadFileAsync(new Uri("https://github.com/NtsFranz/Spark/raw/main/resources/dbgcore.dll"), Path.Combine(Path.GetTempPath(), "dbgcore.dll"));
			}
			catch (Exception)
			{
				new MessageBox("Something broke while trying to download update", Properties.Resources.Error).Show();
			}
		}

		private void RumbleDownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
		{
			RumbleProgress.Visibility = Visibility.Visible;
			RumbleProgress.Value = e.ProgressPercentage;
		}

		private void RumbleDownloadCompleted(object sender, AsyncCompletedEventArgs e)
		{
			try
			{
				// install rumble from the zip
				string dir = Path.GetDirectoryName(SparkSettings.instance.echoVRPath);
				if (dir != null)
				{
					File.Copy(Path.Combine(Path.GetTempPath(), "dbgcore.dll"), Path.Combine(dir, "dbgcore.dll"), true);
				}
			}
			catch (Exception)
			{
				new MessageBox("Something broke while trying to install Rumble. Report this to NtsFranz", Properties.Resources.Error).Show();
			}

			RumbleProgress.Visibility = Visibility.Collapsed;
		}

		private void RemoveRumble(object sender, RoutedEventArgs e)
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
				new MessageBox("Can't uninstall Rumble. Try closing EchoVR and trying again.", Properties.Resources.Error).Show();
			}
		}

		private void SaveQuestIPClicked(object sender, RoutedEventArgs e)
		{
			SaveQuestIPButton.Visibility = Visibility.Collapsed;
		}

		private void PlayTestSound(object sender, RoutedEventArgs e)
		{
			Program.synth.SpeakAsync("THIS IS A TEST SOUND!");
		}
	}


	public class SettingBindingExtension : Binding
	{
		public SettingBindingExtension()
		{
			Initialize();
		}

		public SettingBindingExtension(string path) : base(path)
		{
			Initialize();
		}

		private void Initialize()
		{
			Source = SparkSettings.instance;
			Mode = BindingMode.TwoWay;
		}
	}

	public class SettingLoadExtension : Binding
	{
		public SettingLoadExtension()
		{
			Initialize();
		}

		public SettingLoadExtension(string path) : base(path)
		{
			Initialize();
		}

		private void Initialize()
		{
			Source = SparkSettings.instance;
			Mode = BindingMode.OneWay;
		}
	}
}