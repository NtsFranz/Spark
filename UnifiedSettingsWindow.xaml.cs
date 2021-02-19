using IgniteBot.Properties;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.Windows.Input;
using System.Text.RegularExpressions;
using System.Windows.Navigation;

namespace IgniteBot
{
	/// <summary>
	/// Interaction logic for UnifiedSettingsWindow.xaml
	/// </summary>
	public partial class UnifiedSettingsWindow : Window
	{
		// set to false initially so that loading the settings from disk doesn't activate the events
		private bool initialized;


		public UnifiedSettingsWindow()
		{
			InitializeComponent();
		}

		private void WindowLoad(object sender, RoutedEventArgs e)
		{
			#region General
			startWithWindowsCheckbox.IsChecked = Settings.Default.startOnBoot;
			startMinimizedCheckbox.IsChecked = Settings.Default.startMinimized;
			autorestartCheckbox.IsChecked = Settings.Default.autoRestart;
			capturevp2Checkbox.IsChecked = Settings.Default.capturevp2;
			discordRichPresenceCheckbox.IsChecked = Settings.Default.discordRichPresence;
			remoteLoggingCheckbox.IsChecked = Settings.Default.logToServer;
			exeLocationTextBox.Text = Settings.Default.echoVRPath;
			echoVRIPTextBox.Text = Settings.Default.echoVRIP;
			echoVRPortTextBox.Text = Program.echoVRPort.ToString();

			enableStatsLogging.IsChecked = Settings.Default.enableStatsLogging;
			statsLoggingBox.IsEnabled = enableStatsLogging.IsChecked == true;

			uploadToIgniteDB.IsChecked = Settings.Default.uploadToIgniteDB;
			uploadToFirestoreCheckBox.Visibility = !Program.Personal ? Visibility.Visible : Visibility.Collapsed;
			uploadToFirestoreCheckBox.IsChecked = Settings.Default.uploadToFirestore;

			statsLoggingBox.Opacity = Program.enableStatsLogging ? 1 : .5;
			#endregion

			#region Replays
			storageLocationTextBox.Text = Settings.Default.saveFolder;
			fullLoggingBox.IsEnabled = enableFullLoggingCheckbox.IsChecked == true;
			whenToSplitReplays.SelectedIndex = Settings.Default.whenToSplitReplays;
			enableFullLoggingCheckbox.IsChecked = Settings.Default.enableFullLogging;
			currentFilenameLabel.Content = Program.fileName;
			batchWritesButton.IsChecked = Settings.Default.batchWrites;
			useCompressionButton.IsChecked = Settings.Default.useCompression;
			speedSelector.SelectedIndex = Settings.Default.targetDeltaTimeIndexFull;
			onlyRecordPrivateMatches.IsChecked = Settings.Default.onlyRecordPrivateMatches;
			fullLoggingBox.Opacity = Program.enableFullLogging ? 1 : .5;
			#endregion

			#region TTS
			speechSpeed.SelectedIndex = Settings.Default.TTSSpeed;
			serverLocationCheckbox.IsChecked = Settings.Default.serverLocationTTS;
			joustTimeCheckbox.IsChecked = Settings.Default.joustTimeTTS;
			joustSpeedCheckbox.IsChecked = Settings.Default.joustSpeedTTS;
			tubeExitSpeedCheckbox.IsChecked = Settings.Default.tubeExitSpeedTTS;
			maxBoostSpeedCheckbox.IsChecked = Settings.Default.maxBoostSpeedTTS;
			goalSpeed.IsChecked = Settings.Default.goalSpeedTTS;
			goalDistance.IsChecked = Settings.Default.goalDistanceTTS;
			playerJoinCheckbox.IsChecked = Settings.Default.playerJoinTTS;
			playerLeaveCheckbox.IsChecked = Settings.Default.playerLeaveTTS;
			playerSwitchCheckbox.IsChecked = Settings.Default.playerSwitchTeamTTS;
			gamePausedCheckbox.IsChecked = Settings.Default.pausedTTS;
			discordLoginWarning.Visibility = DiscordOAuth.IsLoggedIn ? Visibility.Collapsed : Visibility.Visible;
			#endregion

			#region NVIDIA Highlights
			HighlightsHelper.isNVHighlightsEnabled &= HighlightsHelper.isNVHighlightsSupported;
			Settings.Default.isNVHighlightsEnabled = HighlightsHelper.isNVHighlightsEnabled;   // This shouldn't change anything
			Settings.Default.Save();

			enableAutoFocusCheckbox.IsChecked = Settings.Default.isAutofocusEnabled;
			enableNVHighlightsCheckbox.IsChecked = HighlightsHelper.isNVHighlightsEnabled;
			clearHighlightsOnExitCheckbox.IsChecked = Settings.Default.clearHighlightsOnExit;
			highlightScope.SelectedIndex = Settings.Default.clientHighlightScope;
			recordAllInSpectator.IsChecked = Settings.Default.nvHighlightsSpectatorRecord;
			clearHighlightsButton.IsEnabled = HighlightsHelper.DoNVClipsExist();

			enableNVHighlightsCheckbox.IsEnabled = HighlightsHelper.isNVHighlightsSupported;
			enableNVHighlightsCheckbox.Content = HighlightsHelper.isNVHighlightsSupported
				? "Enable NVIDIA Highlights"
				: "NVIDIA Highlights isn't supported by your PC";


			nvHighlightsBox.IsEnabled = HighlightsHelper.isNVHighlightsEnabled;
			nvHighlightsBox.Opacity = HighlightsHelper.isNVHighlightsEnabled ? 1 : .5;
			secondsBefore.Text = Settings.Default.nvHighlightsSecondsBefore.ToString();
			secondsAfter.Text = Settings.Default.nvHighlightsSecondsAfter.ToString();
			clearHighlightsButton.Content = $"Clear {HighlightsHelper.nvHighlightClipCount} Unsaved Highlights";
			#endregion

			versionNum.Content = "v" + Program.AppVersion();

			initialized = true;
		}

		#region General
		void RestartOnCrashEvent(object sender, RoutedEventArgs e)
		{
			if (!initialized) return;
			Program.autoRestart = ((CheckBox)sender).IsChecked == true;
			Settings.Default.autoRestart = Program.autoRestart;
			Settings.Default.Save();
		}

		private void StartWithWindowsEvent(object sender, RoutedEventArgs e)
		{
			if (!initialized) return;
			Settings.Default.startOnBoot = ((CheckBox)sender).IsChecked == true;
			Settings.Default.Save();
			SetStartWithWindows(Settings.Default.startOnBoot);
		}

		private static void SetStartWithWindows(bool val)
		{
			RegistryKey rk = Registry.CurrentUser.OpenSubKey
						("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);

			if (val)
			{
				string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "IgniteBot.exe");
				rk.SetValue(Properties.Resources.AppName, path);
			}
			else
				rk.DeleteValue(Properties.Resources.AppName, false);
		}

		private void SlowModeEvent(object sender, RoutedEventArgs e)
		{
			if (!initialized) return;
			Program.deltaTimeIndexStats = ((CheckBox)sender).IsChecked == true ? 1 : 0;
			Settings.Default.targetDeltaTimeIndexStats = Program.deltaTimeIndexStats;
			Settings.Default.Save();
		}

		private void ShowDBLogEvent(object sender, RoutedEventArgs e)
		{
			if (!initialized) return;
			Settings.Default.showDatabaseLog = ((CheckBox)sender).IsChecked == true;
			Settings.Default.Save();

			Program.showDatabaseLog = Settings.Default.showDatabaseLog;
		}

		private void LogToServerEvent(object sender, RoutedEventArgs e)
		{
			if (!initialized) return;
			Settings.Default.logToServer = ((CheckBox)sender).IsChecked == true;
			Settings.Default.Save();

			Logger.enableLoggingRemote = Settings.Default.logToServer;
		}

		private void EnableStatsLoggingEvent(object sender, RoutedEventArgs e)
		{
			if (!initialized) return;
			Program.enableStatsLogging = ((CheckBox)sender).IsChecked == true;
			Settings.Default.enableStatsLogging = Program.enableStatsLogging;
			Settings.Default.Save();

			statsLoggingBox.IsEnabled = Program.enableStatsLogging;
			statsLoggingBox.Opacity = Program.enableStatsLogging ? 1 : .5;
		}

		private void CloseButtonEvent(object sender, RoutedEventArgs e)
		{
			if (!initialized) return;
			Close();
		}

		private void ShowConsoleOnStartEvent(object sender, RoutedEventArgs e)
		{
			if (!initialized) return;
			Settings.Default.showConsoleOnStart = ((CheckBox)sender).IsChecked == true;
			Settings.Default.Save();
		}

		private void UploadToIgniteDBChanged(object sender, RoutedEventArgs e)
		{
			if (!initialized) return;
			Settings.Default.uploadToIgniteDB = ((CheckBox)sender).IsChecked == true;
			Settings.Default.Save();
		}

		private void UploadToFirestoreChanged(object sender, RoutedEventArgs e)
		{
			if (!initialized) return;
			Settings.Default.uploadToFirestore = ((CheckBox)sender).IsChecked == true;
			Settings.Default.Save();
		}

		private void StartMinimizedEvent(object sender, RoutedEventArgs e)
		{
			if (!initialized) return;
			Settings.Default.startMinimized = ((CheckBox)sender).IsChecked == true;
			Settings.Default.Save();
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
				echoVRPortTextBox.Text = Program.echoVRPort.ToString();
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

		private void EnableDiscordRichPresenceEvent(object sender, RoutedEventArgs e)
		{
			if (!initialized) return;
			Settings.Default.discordRichPresence = ((CheckBox)sender).IsChecked == true;
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

		private void capturevp2CheckedEvent(object sender, RoutedEventArgs e)
		{
			if (!initialized) return;
			Settings.Default.capturevp2 = ((CheckBox)sender).IsChecked == true;
			Settings.Default.Save();
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
			var progress = new Progress<string>(s => findQuestStatusLabel.Content = s);
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
			if (Program.firstTimeSetupWindow == null)
			{
				Program.firstTimeSetupWindow = new FirstTimeSetupWindow();
				Program.firstTimeSetupWindow.Closed += (sender, args) => Program.firstTimeSetupWindow = null;
				Program.firstTimeSetupWindow.Show();
			}
			else
			{
				Program.firstTimeSetupWindow.Close();
			}
		}

		private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
		{
			try
			{
				Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
				e.Handled = true;
			}
			catch (Exception ex)
			{
				Logger.LogRow(Logger.LogType.Error, ex.ToString());
			}
		}
		#endregion

		#region Replays


		private void EnableFullLoggingEvent(object sender, RoutedEventArgs e)
		{
			if (!initialized) return;
			Program.enableFullLogging = ((CheckBox)sender).IsChecked == true;
			Settings.Default.enableFullLogging = Program.enableFullLogging;
			Settings.Default.Save();

			fullLoggingBox.IsEnabled = Program.enableFullLogging;
			fullLoggingBox.Opacity = Program.enableFullLogging ? 1 : .5;
		}

		private void OpenReplayFolder(object sender, RoutedEventArgs e)
		{
			if (!initialized) return;
			Process.Start(new ProcessStartInfo
			{
				FileName = Settings.Default.saveFolder,
				UseShellExecute = true
			});
		}

		private void onlyRecordPrivateMatches_CheckedChanged(object sender, RoutedEventArgs e)
		{
			if (!initialized) return;
			Settings.Default.onlyRecordPrivateMatches = ((CheckBox)sender).IsChecked == true;
			Settings.Default.Save();
		}

		private void resetReplayFolder_Click(object sender, RoutedEventArgs e)
		{
			if (!initialized) return;
			Program.saveFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "IgniteBot\\replays");
			Directory.CreateDirectory(Program.saveFolder);
			storageLocationTextBox.Text = Program.saveFolder;
			Settings.Default.saveFolder = Program.saveFolder;
			Settings.Default.Save();
		}

		private void whenToSplitReplaysChanged(object sender, SelectionChangedEventArgs e)
		{
			if (!initialized) return;
			int index = ((ComboBox)sender).SelectedIndex;
			Settings.Default.whenToSplitReplays = index;
			Settings.Default.Save();
		}

		private void SetStorageLocation(object sender, RoutedEventArgs e)
		{
			if (!initialized) return;
			string selectedPath = "";
			var folderBrowserDialog = new CommonOpenFileDialog
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
			Program.saveFolder = path;
			Settings.Default.saveFolder = Program.saveFolder;
			Settings.Default.Save();
			storageLocationTextBox.Text = Program.saveFolder;
		}

		private void SpeedChangeEvent(object sender, SelectionChangedEventArgs e)
		{
			if (!initialized) return;
			int index = ((ComboBox)sender).SelectedIndex;

			Program.deltaTimeIndexFull = index;
			Settings.Default.targetDeltaTimeIndexFull = index;
			Settings.Default.Save();
		}

		private void UseCompressionEvent(object sender, RoutedEventArgs e)
		{
			if (!initialized) return;
			Settings.Default.useCompression = ((CheckBox)sender).IsChecked == true;
			Settings.Default.Save();

			Program.useCompression = Settings.Default.useCompression;
		}

		private void BatchWritesEvent(object sender, RoutedEventArgs e)
		{
			if (!initialized) return;
			Settings.Default.batchWrites = ((CheckBox)sender).IsChecked == true;
			Settings.Default.Save();

			Program.batchWrites = Settings.Default.batchWrites;
		}

		private void SplitFileEvent(object sender, RoutedEventArgs e)
		{
			if (!initialized) return;
			Program.NewFilename();

			currentFilenameLabel.Content = Program.fileName;
		}
		#endregion

		#region TTS
		private void JoustTimeClicked(object sender, RoutedEventArgs e)
		{
			if (!initialized) return;
			bool newVal = ((CheckBox)sender).IsChecked == true;
			Settings.Default.joustTimeTTS = newVal;
			Settings.Default.Save();

			if (newVal)
				Program.synth.SpeakAsync("orange 1.8");
		}

		private void JoustSpeedClicked(object sender, RoutedEventArgs e)
		{
			if (!initialized) return;
			bool newVal = ((CheckBox)sender).IsChecked == true;
			Settings.Default.joustSpeedTTS = newVal;
			Settings.Default.Save();

			if (newVal)
				Program.synth.SpeakAsync("orange 32 meters per second");
		}

		private void ServerLocationClicked(object sender, RoutedEventArgs e)
		{
			if (!initialized) return;
			bool newVal = ((CheckBox)sender).IsChecked == true;
			Settings.Default.serverLocationTTS = newVal;
			Settings.Default.Save();

			if (newVal)
				Program.synth.SpeakAsync("Chicago, Illinois");
		}

		private void MaxBoostClicked(object sender, RoutedEventArgs e)
		{
			if (!initialized) return;
			bool newVal = ((CheckBox)sender).IsChecked == true;
			Settings.Default.maxBoostSpeedTTS = newVal;
			Settings.Default.Save();

			if (newVal)
				Program.synth.SpeakAsync("32 meters per second");
		}

		private void TubeExitSpeedClicked(object sender, RoutedEventArgs e)
		{
			if (!initialized) return;
			bool newVal = ((CheckBox)sender).IsChecked == true;
			Settings.Default.tubeExitSpeedTTS = newVal;
			Settings.Default.Save();

			if (newVal)
				Program.synth.SpeakAsync("32 meters per second");
		}

		private void SpeechSpeedChanged(object sender, SelectionChangedEventArgs e)
		{
			if (!initialized) return;
			var newVal = ((ComboBox)sender).SelectedIndex;
			Program.synth.SetRate(newVal);

			if (newVal != Settings.Default.TTSSpeed)
				Program.synth.SpeakAsync("This is the new speed");

			Settings.Default.TTSSpeed = newVal;
			Settings.Default.Save();
		}

		private void GamePausedClicked(object sender, RoutedEventArgs e)
		{
			if (!initialized) return;
			bool newVal = ((CheckBox)sender).IsChecked == true;
			Settings.Default.pausedTTS = newVal;
			Settings.Default.Save();

			if (newVal) Program.synth.SpeakAsync("Game Paused");
		}

		private void PlayerJoinClicked(object sender, RoutedEventArgs e)
		{
			if (!initialized) return;
			bool newVal = ((CheckBox)sender).IsChecked == true;
			Settings.Default.playerJoinTTS = newVal;
			Settings.Default.Save();

			if (newVal) Program.synth.SpeakAsync("NtsFranz joined");
		}

		private void PlayerLeaveClicked(object sender, RoutedEventArgs e)
		{
			if (!initialized) return;
			bool newVal = ((CheckBox)sender).IsChecked == true;
			Settings.Default.playerLeaveTTS = newVal;
			Settings.Default.Save();

			if (newVal) Program.synth.SpeakAsync("NtsFranz left");
		}

		private void PlayerSwitchClicked(object sender, RoutedEventArgs e)
		{
			if (!initialized) return;
			bool newVal = ((CheckBox)sender).IsChecked == true;
			Settings.Default.playerSwitchTeamTTS = newVal;
			Settings.Default.Save();

			if (newVal) Program.synth.SpeakAsync("NtsFranz switched to orange team");
		}

		private void throwSpeedCheckbox_CheckedChanged(object sender, RoutedEventArgs e)
		{
			if (!initialized) return;
			bool newVal = ((CheckBox)sender).IsChecked == true;
			Settings.Default.throwSpeedTTS = newVal;
			Settings.Default.Save();

			if (newVal)
				Program.synth.SpeakAsync("19");
		}

		private void goalSpeed_CheckedChanged(object sender, RoutedEventArgs e)
		{
			if (!initialized) return;
			bool newVal = ((CheckBox)sender).IsChecked == true;
			Settings.Default.goalSpeedTTS = newVal;
			Settings.Default.Save();

			if (newVal)
				Program.synth.SpeakAsync("19 meters per second");
		}

		private void goalDistance_CheckedChanged(object sender, RoutedEventArgs e)
		{
			if (!initialized) return;
			bool newVal = ((CheckBox)sender).IsChecked == true;
			Settings.Default.goalDistanceTTS = newVal;
			Settings.Default.Save();

			if (newVal)
				Program.synth.SpeakAsync("23 meters");
		}
		#endregion

		#region NVIDIA Highlights
		private void HighlightScopeChanged(object sender, SelectionChangedEventArgs e)
		{
			if (!initialized) return;
			int index = ((ComboBox)sender).SelectedIndex;
			HighlightsHelper.ClientHighlightScope = (HighlightLevel)index;
			Settings.Default.clientHighlightScope = index;
			Settings.Default.Save();
		}

		private void ClearHighlightsOnExitEvent(object sender, RoutedEventArgs e)
		{
			if (!initialized) return;
			HighlightsHelper.clearHighlightsOnExit = ((CheckBox)sender).IsChecked == true;
			Settings.Default.clearHighlightsOnExit = HighlightsHelper.clearHighlightsOnExit;
			Settings.Default.Save();

			clearHighlightsOnExitCheckbox.IsEnabled = HighlightsHelper.clearHighlightsOnExit;
		}

		private void EnableAutofocusEvent(object sender, RoutedEventArgs e)
		{
			if (!initialized) return;
			Settings.Default.isAutofocusEnabled = ((CheckBox)sender).IsChecked == true;
			Settings.Default.Save();
		}

		private void EnableNVHighlightsEvent(object sender, RoutedEventArgs e)
		{
			if (!initialized) return;
			if (HighlightsHelper.isNVHighlightsEnabled && !((CheckBox)sender).IsChecked == true)
			{
				HighlightsHelper.CloseNVHighlights(true);
			}
			else if (!HighlightsHelper.isNVHighlightsEnabled && ((CheckBox)sender).IsChecked == true)
			{
				if (HighlightsHelper.SetupNVHighlights() < 0)
				{
					HighlightsHelper.isNVHighlightsEnabled = false;
					Settings.Default.isNVHighlightsEnabled = false;
					Settings.Default.Save();
					enableNVHighlightsCheckbox.IsChecked = false;
					enableNVHighlightsCheckbox.IsEnabled = false;
					enableNVHighlightsCheckbox.Content = "NVIDIA Highlights failed to initialize or isn't supported by your PC";
					return;
				}
				else
				{
					enableNVHighlightsCheckbox.Content = "Enable NVIDIA Highlights";
				}
			}

			HighlightsHelper.isNVHighlightsEnabled = ((CheckBox)sender).IsChecked == true;
			Settings.Default.isNVHighlightsEnabled = HighlightsHelper.isNVHighlightsEnabled;
			Settings.Default.Save();

			nvHighlightsBox.IsEnabled = HighlightsHelper.isNVHighlightsEnabled;
			nvHighlightsBox.Opacity = HighlightsHelper.isNVHighlightsEnabled ? 1 : .5;
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
			new MessageBox("Highlights Cleared: All unsaved highlights have been cleared from the temporary highlights directory.").Show();
		}

		private void SecondsBeforeChanged(object sender, TextChangedEventArgs e)
		{
			if (!initialized) return;
			if (float.TryParse(((TextBox)sender).Text, out float value))
			{
				Settings.Default.nvHighlightsSecondsBefore = value;
				Settings.Default.Save();
			}
		}

		private void SecondsAfterChanged(object sender, TextChangedEventArgs e)
		{
			if (!initialized) return;
			if (float.TryParse(((TextBox)sender).Text, out float value))
			{
				Settings.Default.nvHighlightsSecondsAfter = value;
				Settings.Default.Save();
			}
		}

		private void RecordAllInSpectatorEvent(object sender, RoutedEventArgs e)
		{
			if (!initialized) return;
			Settings.Default.nvHighlightsSpectatorRecord = ((CheckBox)sender).IsChecked == true;
			Settings.Default.Save();
		}
		#endregion
	}
}
