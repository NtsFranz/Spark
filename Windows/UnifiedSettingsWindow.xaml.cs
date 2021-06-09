using Spark.Properties;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.Windows.Input;
using System.Text.RegularExpressions;
using System.Windows.Navigation;
using System.Windows.Data;
using System.Windows.Markup;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Types;
using System.Linq;

namespace Spark
{
	/// <summary>
	/// Interaction logic for UnifiedSettingsWindow.xaml
	/// </summary>
	public partial class UnifiedSettingsWindow : Window
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

			Program.obs.instance.Connected += OBSConnected;
			Program.obs.instance.Disconnected += OBSDisconnected;

			obsConnectButton.Content = Program.obs.instance.IsConnected ? Properties.Resources.Disconnect : Properties.Resources.Connect;

			try
			{
				if (Program.obs.instance.IsConnected)
				{
					ReplayBufferChanged(Program.obs.instance, Program.obs.instance.GetReplayBufferStatus() ? OutputState.Started : OutputState.Stopped);

					List<string> sceneNames = Program.obs.instance.GetSceneList().Scenes.Select((scene) => scene.Name).ToList();
					for (int i = 0; i < sceneNames.Count; i++)
					{
						inGameScene.Items.Add(new ComboBoxItem
						{
							Content = sceneNames[i]
						});
						if (SparkSettings.instance.obsInGameScene == sceneNames[i])
						{
							inGameScene.SelectedIndex = i + 1;
						}
						betweenGameScene.Items.Add(new ComboBoxItem
						{
							Content = sceneNames[i]
						});
						if (SparkSettings.instance.obsBetweenGameScene == sceneNames[i])
						{
							betweenGameScene.SelectedIndex = i + 1;
						}
					};
				}
				else
				{
					ReplayBufferChanged(Program.obs.instance, OutputState.Stopped);
				}
			}
			catch (Exception ex)
			{
				Logger.LogRow(Logger.LogType.Error, $"Failed getting replay buffer status in startup\n{ex}");
			}
			Program.obs.instance.ReplayBufferStateChanged += ReplayBufferChanged;

			inGameScene.SelectionChanged += InGameSceneChanged;
			betweenGameScene.SelectionChanged += BetweenGameSceneChanged;

			// passwordbox can't use binding
			obsPasswordBox.Password = SparkSettings.instance.obsPassword;
			DoClipLengthSum();

#if WINDOWS_STORE_RELEASE
			enableBetasCheckbox.Visibility = Visibility.Collapsed;
#endif

			CameraModeDropdownChanged(SparkSettings.instance.spectatorCamera);

			initialized = true;
		}

		private void ReplayBufferChanged(OBSWebsocket sender, OutputState type)
		{
			Dispatcher.Invoke(() =>
			{
				if (!sender.IsConnected)
				{
					obsStartReplayBufferButton.Visibility = Visibility.Visible;
					obsStartReplayBufferButton.IsEnabled = false;
					obsStopReplayBufferButton.Visibility = Visibility.Collapsed;
					obsStopReplayBufferButton.IsEnabled = true;

					return;
				}
				switch (type)
				{
					case OutputState.Starting:
						obsStartReplayBufferButton.Visibility = Visibility.Visible;
						obsStartReplayBufferButton.IsEnabled = false;
						obsStopReplayBufferButton.Visibility = Visibility.Collapsed;
						obsStopReplayBufferButton.IsEnabled = true;
						break;
					case OutputState.Started:
						obsStartReplayBufferButton.Visibility = Visibility.Collapsed;
						obsStartReplayBufferButton.IsEnabled = true;
						obsStopReplayBufferButton.Visibility = Visibility.Visible;
						obsStopReplayBufferButton.IsEnabled = true;
						break;
					case OutputState.Stopping:
						obsStartReplayBufferButton.Visibility = Visibility.Collapsed;
						obsStartReplayBufferButton.IsEnabled = false;
						obsStopReplayBufferButton.Visibility = Visibility.Visible;
						obsStopReplayBufferButton.IsEnabled = false;
						break;
					case OutputState.Stopped:
						obsStartReplayBufferButton.Visibility = Visibility.Visible;
						obsStartReplayBufferButton.IsEnabled = true;
						obsStopReplayBufferButton.Visibility = Visibility.Collapsed;
						obsStopReplayBufferButton.IsEnabled = true;
						break;
				}
			});
		}

		private void OBSConnected(object sender, EventArgs e)
		{
			Dispatcher.Invoke(() =>
			{
				obsConnectButton.Content = Properties.Resources.Disconnect;
			});
		}

		private void OBSDisconnected(object sender, EventArgs e)
		{
			Dispatcher.Invoke(() =>
			{
				obsConnectButton.Content = Properties.Resources.Connect;
			});
		}

		private void Initialize()
		{
			// TODO add this in binding form
			statsLoggingBox.Opacity = SparkSettings.instance.enableStatsLogging ? 1 : .5;
			// TODO add this in binding form
			fullLoggingBox.Opacity = SparkSettings.instance.enableFullLogging ? 1 : .5;
			// TODO add this in binding form
			nvHighlightsBox.Opacity = HighlightsHelper.isNVHighlightsEnabled ? 1 : .5;
		}

		#region General

		public static bool StartWithWindows {
			get => SparkSettings.instance.startOnBoot;
			set {
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
		}

		private void EchoVRPortChanged(object sender, TextChangedEventArgs e)
		{
			if (!initialized) return;
			if (Program.overrideEchoVRPort)
			{
				echoVRPortTextBox.Text = SparkSettings.instance.echoVRPort.ToString();
			}
			else
			{
				if (int.TryParse(((TextBox)sender).Text, out Program.echoVRPort))
				{
					SparkSettings.instance.echoVRPort = Program.echoVRPort;
				}
			}
		}

		private void resetIP_Click(object sender, RoutedEventArgs e)
		{
			if (!initialized) return;
			Program.echoVRIP = "127.0.0.1";
			if (!Program.overrideEchoVRPort) Program.echoVRPort = 6721;
			echoVRIPTextBox.Text = Program.echoVRIP;
			echoVRPortTextBox.Text = Program.echoVRPort.ToString();
			SparkSettings.instance.echoVRIP = Program.echoVRIP;
			if (!Program.overrideEchoVRPort) SparkSettings.instance.echoVRPort = Program.echoVRPort;
		}

		private void ExecutableLocationChanged(object sender, TextChangedEventArgs e)
		{
			if (!initialized) return;
			string path = ((TextBox)sender).Text;
			if (File.Exists(path))
			{
				exeLocationLabel.Content = "EchoVR Executable Location:";
				SparkSettings.instance.echoVRPath = path;
			}
			else
			{
				exeLocationLabel.Content = "EchoVR Executable Location:   (not valid)";
			}
		}

		private async void FindQuestClick(object sender, RoutedEventArgs e)
		{
			if (!initialized) return;
			findQuestStatusLabel.Content = "Searching for Quest on network";
			findQuestStatusLabel.Visibility = Visibility.Visible;
			echoVRIPTextBox.IsEnabled = false;
			echoVRPortTextBox.IsEnabled = false;
			findQuest.IsEnabled = false;
			resetIP.IsEnabled = false;
			Progress<string> progress = new Progress<string>(s => findQuestStatusLabel.Content = s);
			await Task.Factory.StartNew(() => Program.echoVRIP = Program.FindQuestIP(progress),
				TaskCreationOptions.None);
			echoVRIPTextBox.IsEnabled = true;
			echoVRPortTextBox.IsEnabled = true;
			findQuest.IsEnabled = true;
			resetIP.IsEnabled = true;
			if (!Program.overrideEchoVRPort) Program.echoVRPort = 6721;
			echoVRIPTextBox.Text = Program.echoVRIP;
			echoVRPortTextBox.Text = Program.echoVRPort.ToString();
			SparkSettings.instance.echoVRIP = Program.echoVRIP;
			if (!Program.overrideEchoVRPort) SparkSettings.instance.echoVRPort = Program.echoVRPort;
		}

		private void ShowFirstTimeSetupWindowClicked(object sender, RoutedEventArgs e)
		{
			if (!initialized) return;

			Program.ToggleWindow(typeof(FirstTimeSetupWindow));
		}

		public static Visibility FirestoreVisible {
			get => !Program.Personal ? Visibility.Visible : Visibility.Collapsed;
		}

		public static string ReplayFilename {
			get => string.IsNullOrEmpty(Program.fileName) ? "---" : Program.fileName;
		}

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

		public int SetTheme {
			get => SparkSettings.instance.theme;
			set {
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
				string resp = await Program.GetRequestAsync(
					$"{Program.APIURL}/optin/get/{SparkSettings.instance.client_name}",
					new Dictionary<string, string> { { "x-api-key", DiscordOAuth.igniteUploadKey } });

				JToken objResp = JsonConvert.DeserializeObject<JToken>(resp);
				if (objResp["opted_in"] != null)
				{
					optInCheckbox.IsChecked = (bool)objResp["opted_in"];
				} else
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
			optInStatusLabel.Visibility = Visibility.Collapsed;
		}

		private void OptIn(object sender, RoutedEventArgs e)
		{
			if (!optInFound) return;

			Program.PostRequestCallback(
				$"{Program.APIURL}/optin/set/{SparkSettings.instance.client_name}/{((CheckBox)sender).IsChecked}",
				new Dictionary<string, string>
				{
					{"x-api-key", DiscordOAuth.igniteUploadKey}, {"token", DiscordOAuth.oauthToken}
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
			SparkSettings.instance.saveFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Spark", "replays");
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

		private void SplitFileEvent(object sender, RoutedEventArgs e)
		{
			if (!initialized) return;
			Program.NewFilename();
		}

		#endregion

		#region TTS

		public static bool DiscordLoggedIn => DiscordOAuth.IsLoggedIn;
		public static Visibility DiscordNotLoggedInVisible => DiscordOAuth.IsLoggedIn ? Visibility.Collapsed : Visibility.Visible;

		public static bool JoustTime {
			get => SparkSettings.instance.joustTimeTTS;
			set {
				SparkSettings.instance.joustTimeTTS = value;

				if (value) Program.synth.SpeakAsync($"{g_Team.TeamColor.orange.ToLocalizedString()} 1.8");
				Console.WriteLine($"{g_Team.TeamColor.orange.ToLocalizedString()} 1.8");
			}
		}

		public static bool JoustSpeed {
			get => SparkSettings.instance.joustSpeedTTS;
			set {
				SparkSettings.instance.joustSpeedTTS = value;

				if (value) Program.synth.SpeakAsync($"{g_Team.TeamColor.orange.ToLocalizedString()} 32 {Properties.Resources.tts_meters_per_second}");
			}
		}

		public static bool ServerLocation {
			get => SparkSettings.instance.serverLocationTTS;
			set {
				SparkSettings.instance.serverLocationTTS = value;

				if (value) Program.synth.SpeakAsync("Chicago, Illinois");
			}
		}

		public static bool MaxBoostSpeed {
			get => SparkSettings.instance.maxBoostSpeedTTS;
			set {
				SparkSettings.instance.maxBoostSpeedTTS = value;

				if (value) Program.synth.SpeakAsync($"32 {Properties.Resources.tts_meters_per_second}");
			}
		}

		public static bool TubeExitSpeed {
			get => SparkSettings.instance.tubeExitSpeedTTS;
			set {
				SparkSettings.instance.tubeExitSpeedTTS = value;

				if (value) Program.synth.SpeakAsync($"32 {Properties.Resources.tts_meters_per_second}");
			}
		}

		public static int SpeechSpeed {
			get => SparkSettings.instance.TTSSpeed;
			set {
				Program.synth.SetRate(value);

				if (value != SparkSettings.instance.TTSSpeed)
					Program.synth.SpeakAsync(Properties.Resources.This_is_the_new_speed);

				SparkSettings.instance.TTSSpeed = value;
			}
		}

		public static bool GamePaused {
			get => SparkSettings.instance.pausedTTS;
			set {
				SparkSettings.instance.pausedTTS = value;

				if (value)
					Program.synth.SpeakAsync(
						$"{g_Team.TeamColor.orange.ToLocalizedString()} {Properties.Resources.tts_paused}");
			}
		}

		public static bool PlayerJoin {
			get => SparkSettings.instance.playerJoinTTS;
			set {
				SparkSettings.instance.playerJoinTTS = value;

				if (value)
					Program.synth.SpeakAsync(
						$"NtsFranz {Properties.Resources.tts_join_1} {g_Team.TeamColor.orange.ToLocalizedString()} {Properties.Resources.tts_join_2}");
			}
		}

		public static bool PlayerLeave {
			get => SparkSettings.instance.playerLeaveTTS;
			set {
				SparkSettings.instance.playerLeaveTTS = value;

				if (value)
					Program.synth.SpeakAsync($"NtsFranz {Properties.Resources.tts_leave_1} {g_Team.TeamColor.orange.ToLocalizedString()} {Properties.Resources.tts_leave_2}");
			}
		}

		public static bool PlayerSwitch {
			get => SparkSettings.instance.playerSwitchTeamTTS;
			set {
				SparkSettings.instance.playerSwitchTeamTTS = value;

				if (value)
					Program.synth.SpeakAsync($"NtsFranz {Properties.Resources.tts_switch_1} {g_Team.TeamColor.blue.ToLocalizedString()} {Properties.Resources.tts_switch_2} {g_Team.TeamColor.orange.ToLocalizedString()} {Properties.Resources.tts_switch_3}");
			}
		}

		public static bool ThrowSpeed {
			get => SparkSettings.instance.throwSpeedTTS;
			set {
				SparkSettings.instance.throwSpeedTTS = value;

				if (value) Program.synth.SpeakAsync("19");
			}
		}

		public static bool GoalSpeed {
			get => SparkSettings.instance.goalSpeedTTS;
			set {
				SparkSettings.instance.goalSpeedTTS = value;

				if (value) Program.synth.SpeakAsync($"19 {Properties.Resources.tts_meters_per_second}");
			}
		}

		public static bool GoalDistance {
			get => SparkSettings.instance.goalDistanceTTS;
			set {
				SparkSettings.instance.goalDistanceTTS = value;

				if (value) Program.synth.SpeakAsync($"23 {Properties.Resources.tts_meters}");
			}
		}

		#endregion

		#region NVIDIA Highlights

		public bool NVHighlightsEnabled {
			get => SparkSettings.instance.isNVHighlightsEnabled;
			set {
				switch (HighlightsHelper.isNVHighlightsEnabled)
				{
					case true when !value:
						HighlightsHelper.CloseNVHighlights(true);
						break;
					case false when value:
						{
							if (HighlightsHelper.SetupNVHighlights() < 0)
							{
								HighlightsHelper.isNVHighlightsEnabled = false;
								SparkSettings.instance.isNVHighlightsEnabled = false;
								enableNVHighlightsCheckbox.IsChecked = false;
								enableNVHighlightsCheckbox.IsEnabled = false;
								enableNVHighlightsCheckbox.Content = Properties.Resources.NVIDIA_Highlights_failed_to_initialize_or_isn_t_supported_by_your_PC;
								return;
							}

							enableNVHighlightsCheckbox.Content = Properties.Resources.Enable_NVIDIA_Highlights;
							break;
						}
				}

				HighlightsHelper.isNVHighlightsEnabled = value;
				SparkSettings.instance.isNVHighlightsEnabled = HighlightsHelper.isNVHighlightsEnabled;

				nvHighlightsBox.IsEnabled = HighlightsHelper.isNVHighlightsEnabled;
				nvHighlightsBox.Opacity = HighlightsHelper.isNVHighlightsEnabled ? 1 : .5;
			}
		}

		private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
		{
			Regex regex = new Regex("[^0-9]+");
			e.Handled = regex.IsMatch(e.Text);
		}

		private void ClearHighlightsEvent(object sender, RoutedEventArgs e)
		{
			if (!initialized) return;
			HighlightsHelper.ClearUnsavedNVHighlights(true);
			clearHighlightsButton.IsEnabled = false;
			clearHighlightsButton.Content = "Clear 0 Unsaved Highlights";
			new MessageBox(Properties.Resources.highlights_cleared).Show();
		}

		public string ClearHighlightsButtonContent => $"Clear {HighlightsHelper.nvHighlightClipCount} Unsaved Highlights";

		public string EnableHighlightsContent => HighlightsHelper.isNVHighlightsSupported
			? Properties.Resources.Enable_NVIDIA_Highlights
			: Properties.Resources.Highlights_isn_t_supported_by_your_PC;

		public bool HighlightsSupported => HighlightsHelper.isNVHighlightsSupported;
		public bool DoNVClipsExist => HighlightsHelper.DoNVClipsExist();


		private void ClipNow(object sender, RoutedEventArgs e)
		{
			Program.SaveReplayClip("manual");
		}

		private void OBSConnect(object sender, RoutedEventArgs e)
		{
			if (!Program.obs.instance.IsConnected)
			{
				try
				{
					Program.obs.instance.Connect(SparkSettings.instance.obsIP, SparkSettings.instance.obsPassword);
					Program.obs.instance.GetReplayBufferStatus();
				}
				catch (AuthFailureException)
				{
					Logger.LogRow(Logger.LogType.Error, "Failed to connect to OBS. AuthFailure");
					new MessageBox("Authentication failed.", Properties.Resources.Error).Show();
					Program.obs.instance.Disconnect();
					return;
				}
				catch (ErrorResponseException ex)
				{
					Logger.LogRow(Logger.LogType.Error, $"Failed to connect to OBS.\n{ex}");
					new MessageBox("Connect failed.", Properties.Resources.Error).Show();
					Program.obs.instance.Disconnect();
					return;
				}
				catch (Exception ex)
				{
					Logger.LogRow(Logger.LogType.Error, $"Failed to connect to OBS for another reason.\n{ex}");
					new MessageBox("Connect failed.", Properties.Resources.Error).Show();
					Program.obs.instance.Disconnect();
					return;
				}
				if (!Program.obs.instance.IsConnected)
				{
					new MessageBox("Connect failed.\nMake sure OBS is open and you have installed the OBS Websocket plugin.", Properties.Resources.Error).Show();
				}
			}
			else
			{
				Program.obs.instance.Disconnect();
			}
		}

		private enum ClipsTab { NVHighlights, echoreplay, OBS }
		private ClipsTab clipsTab;

		private void ClipTypeTabChanged(object sender, SelectionChangedEventArgs e)
		{
			if (!initialized) return;
			TabControl control = (TabControl)sender;
			clipsEventsBox.Header = $"Clip Settings ({((TabItem)control.SelectedValue).Header})";
			clipsTab = (ClipsTab)control.SelectedIndex;

			// if NV Highlights
			if (clipsTab == ClipsTab.NVHighlights)
			{
				nvHighlightsChooseEventsInOverlayLabel.Visibility = Visibility.Visible;
				eventsToChoose.Visibility = Visibility.Collapsed;
			}
			else
			{
				nvHighlightsChooseEventsInOverlayLabel.Visibility = Visibility.Collapsed;
				eventsToChoose.Visibility = Visibility.Visible;
			}

			// if obs
			if (clipsTab == ClipsTab.OBS)
			{
				labelBefore.Visibility = Visibility.Collapsed;
				secondsBefore.Visibility = Visibility.Collapsed;
				labelTotal.Visibility = Visibility.Collapsed;
				totalSeconds.Visibility = Visibility.Collapsed;
			}
			else
			{
				labelBefore.Visibility = Visibility.Visible;
				secondsBefore.Visibility = Visibility.Visible;
				labelTotal.Visibility = Visibility.Visible;
				totalSeconds.Visibility = Visibility.Visible;
			}

			RefreshAllSettings(null, null);
			DoClipLengthSum();
		}

		private void OBSPasswordChanged(object sender, RoutedEventArgs e)
		{
			SparkSettings.instance.obsPassword = ((PasswordBox)sender).Password;
		}

		private void OBSStartReplayBuffer(object sender, RoutedEventArgs e)
		{
			if (Program.obs.instance.IsConnected)
			{
				try
				{
					Program.obs.instance.StartReplayBuffer();
				}
				catch (Exception exp)
				{
					Logger.LogRow(Logger.LogType.Error, $"Couldn't start replay buffer\n{exp}");
				}
			}
		}
		private void OBSStopReplayBuffer(object sender, RoutedEventArgs e)
		{
			if (Program.obs.instance.IsConnected)
			{
				try
				{
					Program.obs.instance.StopReplayBuffer();
				}
				catch (Exception exp)
				{
					Logger.LogRow(Logger.LogType.Error, $"Couldn't stop replay buffer\n{exp}");
				}
			}
		}

		public int ClipsPlayerScope {
			get {
				return clipsTab switch
				{
					ClipsTab.NVHighlights => SparkSettings.instance.nvHighlightsPlayerScope,
					ClipsTab.echoreplay => SparkSettings.instance.replayClipPlayerScope,
					ClipsTab.OBS => SparkSettings.instance.obsPlayerScope,
					_ => 0,
				};
			}
			set {
				switch (clipsTab)
				{
					case ClipsTab.NVHighlights:
						SparkSettings.instance.nvHighlightsPlayerScope = value;
						break;
					case ClipsTab.echoreplay:
						SparkSettings.instance.replayClipPlayerScope = value;
						break;
					case ClipsTab.OBS:
						SparkSettings.instance.obsPlayerScope = value;
						break;
				}
			}
		}

		public bool ClipsSpectatorRecord {
			get {
				return clipsTab switch
				{
					ClipsTab.NVHighlights => SparkSettings.instance.nvHighlightsSpectatorRecord,
					ClipsTab.echoreplay => SparkSettings.instance.replayClipSpectatorRecord,
					ClipsTab.OBS => SparkSettings.instance.obsSpectatorRecord,
					_ => false,
				};
			}
			set {
				switch (clipsTab)
				{
					case ClipsTab.NVHighlights:
						SparkSettings.instance.nvHighlightsSpectatorRecord = value;
						break;
					case ClipsTab.echoreplay:
						SparkSettings.instance.replayClipSpectatorRecord = value;
						break;
					case ClipsTab.OBS:
						SparkSettings.instance.obsSpectatorRecord = value;
						break;
				}
			}
		}

		private void DoClipLengthSum()
		{
			totalSeconds.Text = (float.Parse(secondsBefore.Text) + float.Parse(secondsAfter.Text)).ToString();
		}

		public string SecondsBefore {
			get {
				return clipsTab switch
				{
					ClipsTab.NVHighlights => SparkSettings.instance.nvHighlightsSecondsBefore.ToString(),
					ClipsTab.echoreplay => SparkSettings.instance.replayClipSecondsBefore.ToString(),
					ClipsTab.OBS => SparkSettings.instance.obsClipSecondsBefore.ToString(),
					_ => "0",
				};
			}
			set {
				if (!float.TryParse(value, out float sec)) return;
				switch (clipsTab)
				{
					case ClipsTab.NVHighlights:
						SparkSettings.instance.nvHighlightsSecondsBefore = sec;
						break;
					case ClipsTab.echoreplay:
						SparkSettings.instance.replayClipSecondsBefore = sec;
						break;
					case ClipsTab.OBS:
						SparkSettings.instance.obsClipSecondsBefore = sec;
						break;
				}
				DoClipLengthSum();
			}
		}

		public string SecondsAfter {
			get {
				return clipsTab switch
				{
					ClipsTab.NVHighlights => SparkSettings.instance.nvHighlightsSecondsAfter.ToString(),
					ClipsTab.echoreplay => SparkSettings.instance.replayClipSecondsAfter.ToString(),
					ClipsTab.OBS => SparkSettings.instance.obsClipSecondsAfter.ToString(),
					_ => "0",
				};
			}
			set {
				if (!float.TryParse(value, out float sec)) return;
				switch (clipsTab)
				{
					case ClipsTab.NVHighlights:
						SparkSettings.instance.nvHighlightsSecondsAfter = sec;
						break;
					case ClipsTab.echoreplay:
						SparkSettings.instance.replayClipSecondsAfter = sec;
						break;
					case ClipsTab.OBS:
						SparkSettings.instance.obsClipSecondsAfter = sec;
						break;
				}
				DoClipLengthSum();
			}
		}

		#region Event type settings
		public bool ClipPlayspaceSetting {
			get {
				return clipsTab switch
				{
					ClipsTab.echoreplay => SparkSettings.instance.replayClipPlayspace,
					ClipsTab.OBS => SparkSettings.instance.obsClipPlayspace,
					_ => false,
				};
			}
			set {
				switch (clipsTab)
				{
					case ClipsTab.echoreplay:
						SparkSettings.instance.replayClipPlayspace = value;
						break;
					case ClipsTab.OBS:
						SparkSettings.instance.obsClipPlayspace = value;
						break;
				}
			}
		}

		public bool ClipGoalSetting {
			get {
				return clipsTab switch
				{
					ClipsTab.echoreplay => SparkSettings.instance.replayClipGoal,
					ClipsTab.OBS => SparkSettings.instance.obsClipGoal,
					_ => false,
				};
			}
			set {
				switch (clipsTab)
				{
					case ClipsTab.echoreplay:
						SparkSettings.instance.replayClipGoal = value;
						break;
					case ClipsTab.OBS:
						SparkSettings.instance.obsClipGoal = value;
						break;
				}
			}
		}

		public bool ClipSaveSetting {
			get {
				return clipsTab switch
				{
					ClipsTab.echoreplay => SparkSettings.instance.replayClipSave,
					ClipsTab.OBS => SparkSettings.instance.obsClipSave,
					_ => false,
				};
			}
			set {
				switch (clipsTab)
				{
					case ClipsTab.echoreplay:
						SparkSettings.instance.replayClipSave = value;
						break;
					case ClipsTab.OBS:
						SparkSettings.instance.obsClipSave = value;
						break;
				}
			}
		}

		public bool ClipAssistSetting {
			get {
				return clipsTab switch
				{
					ClipsTab.echoreplay => SparkSettings.instance.replayClipAssist,
					ClipsTab.OBS => SparkSettings.instance.obsClipAssist,
					_ => false,
				};
			}
			set {
				switch (clipsTab)
				{
					case ClipsTab.echoreplay:
						SparkSettings.instance.replayClipAssist = value;
						break;
					case ClipsTab.OBS:
						SparkSettings.instance.obsClipAssist = value;
						break;
				}
			}
		}

		public bool ClipInterceptionSetting {
			get {
				return clipsTab switch
				{
					ClipsTab.echoreplay => SparkSettings.instance.replayClipInterception,
					ClipsTab.OBS => SparkSettings.instance.obsClipInterception,
					_ => false,
				};
			}
			set {
				switch (clipsTab)
				{
					case ClipsTab.echoreplay:
						SparkSettings.instance.replayClipInterception = value;
						break;
					case ClipsTab.OBS:
						SparkSettings.instance.obsClipInterception = value;
						break;
				}
			}
		}
		#endregion

		#endregion

		#region EchoVR Settings

		public Visibility EchoVRSettingsProgramOpenWarning => Program.GetEchoVRProcess()?.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
		public Visibility EchoVRInstalled => File.Exists(SparkSettings.instance.echoVRPath) ? Visibility.Visible : Visibility.Collapsed;
		public Visibility EchoVRNotInstalled => !File.Exists(SparkSettings.instance.echoVRPath) ? Visibility.Visible : Visibility.Collapsed;

		public bool Fullscreen { get => EchoVRSettingsManager.Fullscreen; set { EchoVRSettingsManager.Fullscreen = value; } }
		public bool MultiResShading { get => EchoVRSettingsManager.MultiResShading; set { EchoVRSettingsManager.MultiResShading = value; } }
		public bool AutoRes { get => EchoVRSettingsManager.AutoRes; set { EchoVRSettingsManager.AutoRes = value; } }
		public bool TemporalAA { get => EchoVRSettingsManager.TemporalAA; set { EchoVRSettingsManager.TemporalAA = value; } }
		public bool Volumetrics { get => EchoVRSettingsManager.Volumetrics; set { EchoVRSettingsManager.Volumetrics = value; } }
		public bool Bloom { get => EchoVRSettingsManager.Bloom; set { EchoVRSettingsManager.Bloom = value; } }
		public string Monitor { get => EchoVRSettingsManager.Monitor.ToString(); set { EchoVRSettingsManager.Monitor = int.Parse(value); } }
		public string Resolution { get => EchoVRSettingsManager.Resolution.ToString(); set { EchoVRSettingsManager.Resolution = float.Parse(value); } }
		public string FoV { get => EchoVRSettingsManager.FoV.ToString(); set { EchoVRSettingsManager.FoV = float.Parse(value); } }
		public string Sharpening { get => EchoVRSettingsManager.Sharpening.ToString(); set { EchoVRSettingsManager.Sharpening = float.Parse(value); } }
		public int AA { get => EchoVRSettingsManager.AA; set { EchoVRSettingsManager.AA = value; } }
		public int ShadowQuality { get => EchoVRSettingsManager.ShadowQuality; set { EchoVRSettingsManager.ShadowQuality = value; } }
		public int MeshQuality { get => EchoVRSettingsManager.MeshQuality; set { EchoVRSettingsManager.MeshQuality = value; } }
		public int FXQuality { get => EchoVRSettingsManager.FXQuality; set { EchoVRSettingsManager.FXQuality = value; } }
		public int TextureQuality { get => EchoVRSettingsManager.TextureQuality; set { EchoVRSettingsManager.TextureQuality = value; } }
		public int LightingQuality { get => EchoVRSettingsManager.LightingQuality; set { EchoVRSettingsManager.LightingQuality = value; } }


		private void RefreshAllSettings(object sender, SelectionChangedEventArgs e)
		{
			if (initialized)
			{
				DataContext = null;
				DataContext = this;
			}
		}

		#endregion


		public static string AppVersionLabelText => $"v{Program.AppVersion()}";

		private void InGameSceneChanged(object sender, SelectionChangedEventArgs e)
		{
			SparkSettings.instance.obsInGameScene = (string)((ComboBoxItem)((ComboBox)sender).SelectedValue).Content;
		}

		private void BetweenGameSceneChanged(object sender, SelectionChangedEventArgs e)
		{
			SparkSettings.instance.obsBetweenGameScene = (string)((ComboBoxItem)((ComboBox)sender).SelectedValue).Content;
		}

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
					break;
				case 2:
					followSpecificPlayerPanel.Visibility = Visibility.Collapsed;
					followCameraModeLabel.Visibility = Visibility.Visible;
					followCameraModeDropdown.Visibility = Visibility.Visible;
					break;
				case 3:
					followSpecificPlayerPanel.Visibility = Visibility.Visible;
					followCameraModeLabel.Visibility = Visibility.Visible;
					followCameraModeDropdown.Visibility = Visibility.Visible;
					break;
			}
		}

		private void SpectatorCameraFindNow(object sender, RoutedEventArgs e)
		{
			Program.UseCameraControlKeys(false);
		}

		private void HideEchoVRUINow(object sender, RoutedEventArgs e)
		{
			if (Program.inGame)
			{
				Program.FocusEchoVR();
				Keyboard.SendKey(Keyboard.DirectXKeyStrokes.DIK_U, false, Keyboard.InputType.Keyboard);
				Task.Delay(10).ContinueWith((_) => { Keyboard.SendKey(Keyboard.DirectXKeyStrokes.DIK_U, true, Keyboard.InputType.Keyboard); });
				Logger.LogRow(Logger.LogType.File, Program.lastFrame?.sessionid, "Tried to Hide EchoVR UI");
			}
		}

		private void ToggleTeamMuteNow(object sender, RoutedEventArgs e)
		{
			if (Program.inGame)
			{
				Program.FocusEchoVR();
				Keyboard.SendKey(Keyboard.DirectXKeyStrokes.DIK_F5, false, Keyboard.InputType.Keyboard);
				Task.Delay(10).ContinueWith((_) => { Keyboard.SendKey(Keyboard.DirectXKeyStrokes.DIK_F5, true, Keyboard.InputType.Keyboard); });
				Logger.LogRow(Logger.LogType.File, Program.lastFrame?.sessionid, "Tried to mute team comms (manual)");
			}
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