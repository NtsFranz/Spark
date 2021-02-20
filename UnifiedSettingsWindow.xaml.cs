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
using System.Windows.Data;

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
			//InitializeGeneral();
			// InitializeReplaysTab();
			// InitializeTTSTab();
			// InitializeNVHighlightsTab();

			// versionNum.Content = "v" + Program.AppVersion();

			initialized = true;
		}

		private void InitializeGeneral()
		{
			startWithWindowsCheckbox.IsChecked = Settings.Default.startOnBoot;
			startMinimizedCheckbox.IsChecked = Settings.Default.startMinimized;
			autorestartCheckbox.IsChecked = Settings.Default.autoRestart;
			capturevp2Checkbox.IsChecked = Settings.Default.capturevp2;
			discordRichPresenceCheckbox.IsChecked = Settings.Default.discordRichPresence;
			remoteLoggingCheckbox.IsChecked = Settings.Default.logToServer;
			exeLocationTextBox.Text = Settings.Default.echoVRPath;
			echoVRIPTextBox.Text = Settings.Default.echoVRIP;
			echoVRPortTextBox.Text = Program.echoVRPort.ToString();

			enableStatsLoggingCheckBox.IsChecked = Settings.Default.enableStatsLogging;
			statsLoggingBox.IsEnabled = enableStatsLoggingCheckBox.IsChecked == true;

			uploadToIgniteDBCheckBox.IsChecked = Settings.Default.uploadToIgniteDB;
			uploadToFirestoreCheckBox.Visibility = !Program.Personal ? Visibility.Visible : Visibility.Collapsed;
			uploadToFirestoreCheckBox.IsChecked = Settings.Default.uploadToFirestore;

			statsLoggingBox.Opacity = Settings.Default.enableStatsLogging ? 1 : .5;
		}

		private void InitializeReplaysTab()
		{
			storageLocationTextBox.Text = Settings.Default.saveFolder;
			whenToSplitReplaysDropdown.SelectedIndex = Settings.Default.whenToSplitReplays;
			enableFullLoggingCheckbox.IsChecked = Settings.Default.enableFullLogging;
			fullLoggingBox.IsEnabled = enableFullLoggingCheckbox.IsChecked == true;
			currentFilenameLabel.Content = Program.fileName;
			batchWritesButton.IsChecked = Settings.Default.batchWrites;
			useCompressionButton.IsChecked = Settings.Default.useCompression;
			speedSelector.SelectedIndex = Settings.Default.targetDeltaTimeIndexFull;
			onlyRecordPrivateMatchesCheckBox.IsChecked = Settings.Default.onlyRecordPrivateMatches;
			fullLoggingBox.Opacity = Settings.Default.enableFullLogging ? 1 : .5;
		}

		private void InitializeTTSTab()
		{
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
		}

		private void InitializeNVHighlightsTab()
		{
			HighlightsHelper.isNVHighlightsEnabled &= HighlightsHelper.isNVHighlightsSupported;
			Settings.Default.isNVHighlightsEnabled =
				HighlightsHelper.isNVHighlightsEnabled; // This shouldn't change anything
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
		}

		#region General

		public static bool StartWithWindows
		{
			get => Settings.Default.startOnBoot;
			set
			{
				Settings.Default.startOnBoot = value;
				Settings.Default.Save();

				RegistryKey rk =
					Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);

				if (value)
				{
					string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "IgniteBot.exe");
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
			Program.echoVRIP = ((TextBox) sender).Text;
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
				if (int.TryParse(((TextBox) sender).Text, out Program.echoVRPort))
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
			string path = ((TextBox) sender).Text;
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
			Settings.Default.saveFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal),
				"IgniteBot\\replays");
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

			currentFilenameLabel.Content = Program.fileName;
		}

		#endregion

		#region TTS

		public static Visibility DiscordLoginWarningVisible =>
			DiscordOAuth.IsLoggedIn ? Visibility.Collapsed : Visibility.Visible;

		public static bool JoustTime
		{
			get => Settings.Default.joustTimeTTS;
			set
			{
				Settings.Default.joustTimeTTS = value;
				Settings.Default.Save();

				if (value) Program.synth.SpeakAsync("orange 1.8");
			}
		}

		public static bool JoustSpeed
		{
			get => Settings.Default.joustSpeedTTS;
			set
			{
				Settings.Default.joustSpeedTTS = value;
				Settings.Default.Save();

				if (value) Program.synth.SpeakAsync("orange 32 meters per second");
			}
		}

		public static bool ServerLocation
		{
			get => Settings.Default.serverLocationTTS;
			set
			{
				Settings.Default.serverLocationTTS = value;
				Settings.Default.Save();

				if (value) Program.synth.SpeakAsync("Chicago, Illinois");
			}
		}

		public static bool MaxBoostSpeed
		{
			get => Settings.Default.maxBoostSpeedTTS;
			set
			{
				Settings.Default.maxBoostSpeedTTS = value;
				Settings.Default.Save();

				if (value) Program.synth.SpeakAsync("32 meters per second");
			}
		}

		public static bool TubeExitSpeed
		{
			get => Settings.Default.tubeExitSpeedTTS;
			set
			{
				Settings.Default.tubeExitSpeedTTS = value;
				Settings.Default.Save();

				if (value) Program.synth.SpeakAsync("32 meters per second");
			}
		}

		public static int SpeechSpeed
		{
			get => Settings.Default.TTSSpeed;
			set
			{
				Program.synth.SetRate(value);

				if (value != Settings.Default.TTSSpeed)
					Program.synth.SpeakAsync("This is the new speed");

				Settings.Default.TTSSpeed = value;
				Settings.Default.Save();
			}
		}

		public static bool GamePaused
		{
			get => Settings.Default.pausedTTS;
			set
			{
				Settings.Default.pausedTTS = value;
				Settings.Default.Save();

				if (value) Program.synth.SpeakAsync("Game Paused");
			}
		}

		public static bool PlayerJoin
		{
			get => Settings.Default.playerJoinTTS;
			set
			{
				Settings.Default.playerJoinTTS = value;
				Settings.Default.Save();

				if (value) Program.synth.SpeakAsync("NtsFranz joined");
			}
		}

		public static bool PlayerLeave
		{
			get => Settings.Default.playerLeaveTTS;
			set
			{
				Settings.Default.playerLeaveTTS = value;
				Settings.Default.Save();

				if (value) Program.synth.SpeakAsync("NtsFranz left");
			}
		}

		public static bool PlayerSwitch
		{
			get => Settings.Default.playerSwitchTeamTTS;
			set
			{
				Settings.Default.playerSwitchTeamTTS = value;
				Settings.Default.Save();

				if (value) Program.synth.SpeakAsync("NtsFranz switched to orange team");
			}
		}

		public static bool ThrowSpeed
		{
			get => Settings.Default.throwSpeedTTS;
			set
			{
				Settings.Default.throwSpeedTTS = value;
				Settings.Default.Save();

				if (value) Program.synth.SpeakAsync("19");
			}
		}

		public static bool GoalSpeed
		{
			get => Settings.Default.goalSpeedTTS;
			set
			{
				Settings.Default.goalSpeedTTS = value;
				Settings.Default.Save();

				if (value) Program.synth.SpeakAsync("19 meters per second");
			}
		}

		public static bool GoalDistance
		{
			get => Settings.Default.goalDistanceTTS;
			set
			{
				Settings.Default.goalDistanceTTS = value;
				Settings.Default.Save();

				if (value) Program.synth.SpeakAsync("23 meters");
			}
		}

		#endregion

		#region NVIDIA Highlights

		public bool NVHighlightsEnabled
		{
			get => Settings.Default.isNVHighlightsEnabled;
			set
			{
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
							enableNVHighlightsCheckbox.Content =
								"NVIDIA Highlights failed to initialize or isn't supported by your PC";
							return;
						}

						enableNVHighlightsCheckbox.Content = "Enable NVIDIA Highlights";
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
			new MessageBox(
					"Highlights Cleared: All unsaved highlights have been cleared from the temporary highlights directory.")
				.Show();
		}

		private void SecondsBeforeChanged(object sender, TextChangedEventArgs e)
		{
			if (!initialized) return;
			if (!float.TryParse(((TextBox) sender).Text, out float value)) return;
			Settings.Default.nvHighlightsSecondsBefore = value;
			Settings.Default.Save();
		}

		private void SecondsAfterChanged(object sender, TextChangedEventArgs e)
		{
			if (!initialized) return;
			if (!float.TryParse(((TextBox) sender).Text, out float value)) return;
			Settings.Default.nvHighlightsSecondsAfter = value;
			Settings.Default.Save();
		}

		#endregion
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