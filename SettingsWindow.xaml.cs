using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using IgniteBot.Properties;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace IgniteBot
{
	/// <summary>
	/// Interaction logic for SettingsWindow.xaml
	/// </summary>
	public partial class SettingsWindow : Window
	{
		private bool initialized = false;
		public SettingsWindow()
		{
			InitializeComponent();

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

			onlyRecordPrivateMatches.IsChecked = Settings.Default.onlyRecordPrivateMatches;
			uploadToIgniteDB.IsChecked = Settings.Default.uploadToIgniteDB;
#if INCLUDE_FIRESTORE
			uploadToFirestoreCheckBox.Visibility = !Program.Personal ? Visibility.Visible : Visibility.Collapsed;
#else
			uploadToFirestoreCheckBox.Visibility = Visibility.Collapsed;
#endif
			uploadToFirestoreCheckBox.IsChecked = Settings.Default.uploadToFirestore;

			enableFullLoggingCheckbox.IsChecked = Settings.Default.enableFullLogging;
			currentFilenameLabel.Content = Program.fileName;
			batchWritesButton.IsChecked = Settings.Default.batchWrites;
			useCompressionButton.IsChecked = Settings.Default.useCompression;
			speedSelector.SelectedIndex = Settings.Default.targetDeltaTimeIndexFull;
			storageLocationTextBox.Text = Settings.Default.saveFolder;
			fullLoggingBox.IsEnabled = enableFullLoggingCheckbox.IsChecked == true;
			whenToSplitReplays.SelectedIndex = Settings.Default.whenToSplitReplays;
			statsLoggingBox.Opacity = Program.enableStatsLogging ? 1 : .5;
			fullLoggingBox.Opacity = Program.enableFullLogging ? 1 : .5;

			versionNum.Content = "v" + Program.AppVersion();

			initialized = true;
		}

		private void SettingsWindow_Load(object sender, RoutedEventArgs e)
		{
		}

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

		private void EnableFullLoggingEvent(object sender, RoutedEventArgs e)
		{
			if (!initialized) return;
			Program.enableFullLogging = ((CheckBox)sender).IsChecked == true;
			Settings.Default.enableFullLogging = Program.enableFullLogging;
			Settings.Default.Save();

			fullLoggingBox.IsEnabled = Program.enableFullLogging;
			fullLoggingBox.Opacity = Program.enableFullLogging ? 1 : .5;
		}

		private void CloseButtonEvent(object sender, RoutedEventArgs e)
		{
			if (!initialized) return;
			Close();
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

		private void echoVRSettings_Click(object sender, RoutedEventArgs e)
		{

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

		private void ShowTTSSettingsWindow(object sender, RoutedEventArgs e)
		{
			if (!initialized) return;
			if (Program.ttsWindow == null)
			{
				Program.ttsWindow = new TTSSettingsWindow();
				Program.ttsWindow.Closed += (sender, args) => Program.ttsWindow = null;
				Program.ttsWindow.Show();
			}
			else
			{
				Program.ttsWindow.Close();
			}
		}

		private void EnableDiscordRichPresenceEvent(object sender, RoutedEventArgs e)
		{
			if (!initialized) return;
			Settings.Default.discordRichPresence = ((CheckBox)sender).IsChecked == true;
			Settings.Default.Save();
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
	}
}