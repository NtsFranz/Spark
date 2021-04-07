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
			_ = GetOptInStatus();

			Program.obs.instance.Connected += OBSConnected;
			Program.obs.instance.Disconnected += OBSDisconnected;

			obsConnectButton.Content = Program.obs.instance.IsConnected ? "Disconnect" : "Connect";

			// passwordbox can't use binding
			obsPasswordBox.Password = Settings.Default.obsPassword;
			DoClipLengthSum();

			initialized = true;
		}

		private void OBSConnected(object sender, EventArgs e)
		{
			Dispatcher.Invoke(() =>
			{
				obsConnectButton.Content = "Disconnect";
			});
		}

		private void OBSDisconnected(object sender, EventArgs e)
		{
			Dispatcher.Invoke(() =>
			{
				obsConnectButton.Content = "Connect";
			});
		}

		private void Initialize()
		{
			// TODO add this in binding form
			statsLoggingBox.Opacity = Settings.Default.enableStatsLogging ? 1 : .5;
			// TODO add this in binding form
			fullLoggingBox.Opacity = Settings.Default.enableFullLogging ? 1 : .5;
			// TODO add this in binding form
			nvHighlightsBox.Opacity = HighlightsHelper.isNVHighlightsEnabled ? 1 : .5;
		}

		#region General

		public static bool StartWithWindows {
			get => Settings.Default.startOnBoot;
			set {
				Settings.Default.startOnBoot = value;
				Settings.Default.Save();

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
			Settings.Default.Save();
			Close();
		}

		private void EchoVRIPChanged(object sender, TextChangedEventArgs e)
		{
			if (!initialized) return;
			Program.echoVRIP = ((TextBox)sender).Text;
			Settings.Default.echoVRIP = Program.echoVRIP;
			Settings.Default.Save();
		}

		private void EchoVRPortChanged(object sender, TextChangedEventArgs e)
		{
			if (!initialized) return;
			if (Program.overrideEchoVRPort)
			{
				echoVRPortTextBox.Text = Settings.Default.echoVRPort.ToString();
			}
			else
			{
				if (int.TryParse(((TextBox)sender).Text, out Program.echoVRPort))
				{
					Settings.Default.echoVRPort = Program.echoVRPort;
					Settings.Default.Save();
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
			Settings.Default.echoVRIP = Program.echoVRIP;
			if (!Program.overrideEchoVRPort) Settings.Default.echoVRPort = Program.echoVRPort;
			Settings.Default.Save();
		}

		private void ExecutableLocationChanged(object sender, TextChangedEventArgs e)
		{
			if (!initialized) return;
			string path = ((TextBox)sender).Text;
			if (File.Exists(path))
			{
				exeLocationLabel.Content = "EchoVR Executable Location:";
				Settings.Default.echoVRPath = path;
				Settings.Default.Save();
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
			Settings.Default.echoVRIP = Program.echoVRIP;
			if (!Program.overrideEchoVRPort) Settings.Default.echoVRPort = Program.echoVRPort;
			Settings.Default.Save();
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
			get => Settings.Default.theme;
			set {
				Settings.Default.theme = value;
				Settings.Default.Save();

				ThemesController.SetTheme((ThemesController.ThemeTypes)value);
			}
		}

		private async Task GetOptInStatus()
		{
			if (Settings.Default.client_name == string.Empty ||
				DiscordOAuth.oauthToken == string.Empty)
			{
				optInFound = true;
				optInCheckbox.IsEnabled = false;
				return;
			}

			try
			{
				string resp = await Program.GetRequestAsync(
					$"{Program.APIURL}/optin/get/{Settings.Default.client_name}",
					new Dictionary<string, string> { { "x-api-key", DiscordOAuth.igniteUploadKey } });

				JToken objResp = JsonConvert.DeserializeObject<JToken>(resp);
				if (objResp["opted_in"] != null)
				{
					optInCheckbox.IsChecked = (bool)objResp["opted_in"];
				}
			}
			catch (Exception e)
			{
				Logger.LogRow(Logger.LogType.Error, $"Couldn't check for update.\n{e}");
			}

			optInFound = true;
			optInCheckbox.IsEnabled = true;
		}

		private void OptIn(object sender, RoutedEventArgs e)
		{
			if (!optInFound) return;

			Program.PostRequestCallback(
				$"{Program.APIURL}/optin/set/{Settings.Default.client_name}/{((CheckBox)sender).IsChecked}",
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
				FileName = Settings.Default.saveFolder,
				UseShellExecute = true
			});
		}

		private void ResetReplayFolder(object sender, RoutedEventArgs e)
		{
			if (!initialized) return;
			Settings.Default.saveFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Spark", "replays");
			Directory.CreateDirectory(Settings.Default.saveFolder);
			storageLocationTextBox.Text = Settings.Default.saveFolder;
			Settings.Default.Save();
		}

		private void SetStorageLocation(object sender, RoutedEventArgs e)
		{
			if (!initialized) return;
			string selectedPath = "";
			CommonOpenFileDialog folderBrowserDialog = new CommonOpenFileDialog
			{
				InitialDirectory = Settings.Default.saveFolder,
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
			Settings.Default.saveFolder = path;
			Settings.Default.Save();
			storageLocationTextBox.Text = Settings.Default.saveFolder;
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
			get => Settings.Default.joustTimeTTS;
			set {
				Settings.Default.joustTimeTTS = value;
				Settings.Default.Save();

				if (value) Program.synth.SpeakAsync($"{g_Team.TeamColor.orange.ToLocalizedString()} 1.8");
				Console.WriteLine($"{g_Team.TeamColor.orange.ToLocalizedString()} 1.8");
			}
		}

		public static bool JoustSpeed {
			get => Settings.Default.joustSpeedTTS;
			set {
				Settings.Default.joustSpeedTTS = value;
				Settings.Default.Save();

				if (value) Program.synth.SpeakAsync($"{g_Team.TeamColor.orange.ToLocalizedString()} 32 {Properties.Resources.tts_meters_per_second}");
			}
		}

		public static bool ServerLocation {
			get => Settings.Default.serverLocationTTS;
			set {
				Settings.Default.serverLocationTTS = value;
				Settings.Default.Save();

				if (value) Program.synth.SpeakAsync("Chicago, Illinois");
			}
		}

		public static bool MaxBoostSpeed {
			get => Settings.Default.maxBoostSpeedTTS;
			set {
				Settings.Default.maxBoostSpeedTTS = value;
				Settings.Default.Save();

				if (value) Program.synth.SpeakAsync($"32 {Properties.Resources.tts_meters_per_second}");
			}
		}

		public static bool TubeExitSpeed {
			get => Settings.Default.tubeExitSpeedTTS;
			set {
				Settings.Default.tubeExitSpeedTTS = value;
				Settings.Default.Save();

				if (value) Program.synth.SpeakAsync($"32 {Properties.Resources.tts_meters_per_second}");
			}
		}

		public static int SpeechSpeed {
			get => Settings.Default.TTSSpeed;
			set {
				Program.synth.SetRate(value);

				if (value != Settings.Default.TTSSpeed)
					Program.synth.SpeakAsync(Properties.Resources.This_is_the_new_speed);

				Settings.Default.TTSSpeed = value;
				Settings.Default.Save();
			}
		}

		public static bool GamePaused {
			get => Settings.Default.pausedTTS;
			set {
				Settings.Default.pausedTTS = value;
				Settings.Default.Save();

				if (value)
					Program.synth.SpeakAsync(
						$"{g_Team.TeamColor.orange.ToLocalizedString()} {Properties.Resources.tts_paused}");
			}
		}

		public static bool PlayerJoin {
			get => Settings.Default.playerJoinTTS;
			set {
				Settings.Default.playerJoinTTS = value;
				Settings.Default.Save();

				if (value)
					Program.synth.SpeakAsync(
						$"NtsFranz {Properties.Resources.tts_join_1} {g_Team.TeamColor.orange.ToLocalizedString()} {Properties.Resources.tts_join_2}");
			}
		}

		public static bool PlayerLeave {
			get => Settings.Default.playerLeaveTTS;
			set {
				Settings.Default.playerLeaveTTS = value;
				Settings.Default.Save();

				if (value)
					Program.synth.SpeakAsync($"NtsFranz {Properties.Resources.tts_leave_1} {g_Team.TeamColor.orange.ToLocalizedString()} {Properties.Resources.tts_leave_2}");
			}
		}

		public static bool PlayerSwitch {
			get => Settings.Default.playerSwitchTeamTTS;
			set {
				Settings.Default.playerSwitchTeamTTS = value;
				Settings.Default.Save();

				if (value)
					Program.synth.SpeakAsync($"NtsFranz {Properties.Resources.tts_switch_1} {g_Team.TeamColor.blue.ToLocalizedString()} {Properties.Resources.tts_switch_2} {g_Team.TeamColor.orange.ToLocalizedString()} {Properties.Resources.tts_switch_3}");
			}
		}

		public static bool ThrowSpeed {
			get => Settings.Default.throwSpeedTTS;
			set {
				Settings.Default.throwSpeedTTS = value;
				Settings.Default.Save();

				if (value) Program.synth.SpeakAsync("19");
			}
		}

		public static bool GoalSpeed {
			get => Settings.Default.goalSpeedTTS;
			set {
				Settings.Default.goalSpeedTTS = value;
				Settings.Default.Save();

				if (value) Program.synth.SpeakAsync($"19 {Properties.Resources.tts_meters_per_second}");
			}
		}

		public static bool GoalDistance {
			get => Settings.Default.goalDistanceTTS;
			set {
				Settings.Default.goalDistanceTTS = value;
				Settings.Default.Save();

				if (value) Program.synth.SpeakAsync($"23 {Properties.Resources.tts_meters}");
			}
		}

		#endregion

		#region NVIDIA Highlights

		public bool NVHighlightsEnabled {
			get => Settings.Default.isNVHighlightsEnabled;
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
								Settings.Default.isNVHighlightsEnabled = false;
								Settings.Default.Save();
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
				Settings.Default.isNVHighlightsEnabled = HighlightsHelper.isNVHighlightsEnabled;
				Settings.Default.Save();

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
				Task.Run(() =>
				{
					try
					{
						Program.obs.instance.Connect(Settings.Default.obsIP, Settings.Default.obsPassword);
					}
					catch (AuthFailureException)
					{
						Logger.LogRow(Logger.LogType.Error, "Failed to connect to OBS. AuthFailure");
						new MessageBox("Authentication failed.", Properties.Resources.Error).Show();
						return;
					}
					catch (ErrorResponseException ex)
					{
						Logger.LogRow(Logger.LogType.Error, $"Failed to connect to OBS.\n{ex}");
						new MessageBox("Connect failed.", Properties.Resources.Error).Show();
						return;
					}
					catch (Exception ex)
					{
						Logger.LogRow(Logger.LogType.Error, $"Failed to connect to OBS for another reason.\n{ex}");
						new MessageBox("Connect failed.", Properties.Resources.Error).Show();
						return;
					}
					if (!Program.obs.instance.IsConnected)
					{
						new MessageBox("Connect failed.\nMake sure OBS is open and you have installed the OBS Websocket plugin.", Properties.Resources.Error).Show();
					}
				});
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
				secondsBefore.IsEnabled = false;
			}
			else
			{
				secondsBefore.IsEnabled = true;
			}

			RefreshAllSettings(null, null);
			DoClipLengthSum();
		}

		private void OBSPasswordChanged(object sender, RoutedEventArgs e)
		{
			Settings.Default.obsPassword = ((PasswordBox)sender).Password;
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

		public int ClipsPlayerScope
		{
			get
			{
				return clipsTab switch
				{
					ClipsTab.NVHighlights => Settings.Default.nvHighlightsPlayerScope,
					ClipsTab.echoreplay => Settings.Default.replayClipPlayerScope,
					ClipsTab.OBS => Settings.Default.obsPlayerScope,
					_ => 0,
				};
			}
			set
			{
				switch (clipsTab)
				{
					case ClipsTab.NVHighlights:
						Settings.Default.nvHighlightsPlayerScope = value;
						break;
					case ClipsTab.echoreplay:
						Settings.Default.replayClipPlayerScope = value;
						break;
					case ClipsTab.OBS:
						Settings.Default.obsPlayerScope = value;
						break;
				}
			}
		}

		public bool ClipsSpectatorRecord
		{
			get
			{
				return clipsTab switch
				{
					ClipsTab.NVHighlights => Settings.Default.nvHighlightsSpectatorRecord,
					ClipsTab.echoreplay => Settings.Default.replayClipSpectatorRecord,
					ClipsTab.OBS => Settings.Default.obsSpectatorRecord,
					_ => false,
				};
			}
			set
			{
				switch (clipsTab)
				{
					case ClipsTab.NVHighlights:
						Settings.Default.nvHighlightsSpectatorRecord = value;
						break;
					case ClipsTab.echoreplay:
						Settings.Default.replayClipSpectatorRecord = value;
						break;
					case ClipsTab.OBS:
						Settings.Default.obsSpectatorRecord = value;
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
					ClipsTab.NVHighlights => Settings.Default.nvHighlightsSecondsBefore.ToString(),
					ClipsTab.echoreplay => Settings.Default.replayClipSecondsBefore.ToString(),
					ClipsTab.OBS => Settings.Default.obsClipSecondsBefore.ToString(),
					_ => "0",
				};
			}
			set {
				if (!float.TryParse(value, out float sec)) return;
				switch (clipsTab)
				{
					case ClipsTab.NVHighlights:
						Settings.Default.nvHighlightsSecondsBefore = sec;
						break;
					case ClipsTab.echoreplay:
						Settings.Default.replayClipSecondsBefore = sec;
						break;
					case ClipsTab.OBS:
						Settings.Default.obsClipSecondsBefore = sec;
						break;
				}
				DoClipLengthSum();
			}
		}

		public string SecondsAfter {
			get {
				return clipsTab switch
				{
					ClipsTab.NVHighlights => Settings.Default.nvHighlightsSecondsAfter.ToString(),
					ClipsTab.echoreplay => Settings.Default.replayClipSecondsAfter.ToString(),
					ClipsTab.OBS => Settings.Default.obsClipSecondsAfter.ToString(),
					_ => "0",
				};
			}
			set {
				if (!float.TryParse(value, out float sec)) return;
				switch (clipsTab)
				{
					case ClipsTab.NVHighlights:
						Settings.Default.nvHighlightsSecondsAfter = sec;
						break;
					case ClipsTab.echoreplay:
						Settings.Default.replayClipSecondsAfter = sec;
						break;
					case ClipsTab.OBS:
						Settings.Default.obsClipSecondsAfter = sec;
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
					ClipsTab.echoreplay => Settings.Default.replayClipPlayspace,
					ClipsTab.OBS => Settings.Default.obsClipPlayspace,
					_ => false,
				};
			}
			set {
				switch (clipsTab)
				{
					case ClipsTab.echoreplay:
						Settings.Default.replayClipPlayspace = value;
						break;
					case ClipsTab.OBS:
						Settings.Default.obsClipPlayspace = value;
						break;
				}
			}
		}

		public bool ClipGoalSetting {
			get {
				return clipsTab switch
				{
					ClipsTab.echoreplay => Settings.Default.replayClipGoal,
					ClipsTab.OBS => Settings.Default.obsClipGoal,
					_ => false,
				};
			}
			set {
				switch (clipsTab)
				{
					case ClipsTab.echoreplay:
						Settings.Default.replayClipGoal = value;
						break;
					case ClipsTab.OBS:
						Settings.Default.obsClipGoal = value;
						break;
				}
			}
		}

		public bool ClipSaveSetting {
			get {
				return clipsTab switch
				{
					ClipsTab.echoreplay => Settings.Default.replayClipSave,
					ClipsTab.OBS => Settings.Default.obsClipSave,
					_ => false,
				};
			}
			set {
				switch (clipsTab)
				{
					case ClipsTab.echoreplay:
						Settings.Default.replayClipSave = value;
						break;
					case ClipsTab.OBS:
						Settings.Default.obsClipSave = value;
						break;
				}
			}
		}

		public bool ClipAssistSetting {
			get {
				return clipsTab switch
				{
					ClipsTab.echoreplay => Settings.Default.replayClipAssist,
					ClipsTab.OBS => Settings.Default.obsClipAssist,
					_ => false,
				};
			}
			set {
				switch (clipsTab)
				{
					case ClipsTab.echoreplay:
						Settings.Default.replayClipAssist = value;
						break;
					case ClipsTab.OBS:
						Settings.Default.obsClipAssist = value;
						break;
				}
			}
		}
		#endregion

		#endregion

		#region EchoVR Settings

		public Visibility EchoVRSettingsProgramOpenWarning => Program.GetEchoVRProcess()?.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
		public Visibility EchoVRInstalled => File.Exists(Settings.Default.echoVRPath) ? Visibility.Visible : Visibility.Collapsed;
		public Visibility EchoVRNotInstalled => !File.Exists(Settings.Default.echoVRPath) ? Visibility.Visible : Visibility.Collapsed;

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
			Source = Settings.Default;
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
			Source = Settings.Default;
			Mode = BindingMode.OneWay;
		}
	}
}