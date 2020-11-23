using IgniteBot2.Properties;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using static Logger;

namespace IgniteBot2
{
	/// <summary>
	/// Interaction logic for LiveWindow.xaml
	/// </summary>
	public partial class LiveWindow : Window
	{
		private readonly Timer outputUpdateTimer = new Timer();

		List<ProgressBar> playerSpeedBars = new List<ProgressBar>();
		private string updateFilename = "";

		public static readonly object lastSnapshotLock = new object();
#if INCLUDE_FIRESTORE
		private QuerySnapshot lastSnapshot = null;
#endif
		private string lastIP;
		private Dictionary<Control, bool> speedsHoveringDict = new Dictionary<Control, bool>();
		private bool speedsHovering {
			get => speedsHoveringDict.Values.ToList().Contains(true);
		}

		private Dictionary<Control, bool> discordLoginHoveringDict = new Dictionary<Control, bool>();
		private bool discordLoginHovering {
			get => discordLoginHoveringDict.Values.ToList().Contains(true);
		}

		private string lastDiscordUsername = string.Empty;


		public LiveWindow()
		{
			InitializeComponent();

			outputUpdateTimer.Interval = 50;
			outputUpdateTimer.Elapsed += Update;
			outputUpdateTimer.Enabled = true;

			JToken gameSettings = Program.ReadEchoVRSettings();
			if (gameSettings != null)
			{
				enableAPIButton.Visibility = !(bool)gameSettings["game"]["EnableAPIAccess"] ? Visibility.Visible : Visibility.Collapsed;
			}
			//hostLiveReplayButton.Visible = !Program.Personal;

			accessCodeLabel.Content = "Mode: " + Settings.Default.accessMode;
			versionLabel.Content = "v" + Program.AppVersion();

			GenerateNewStatsId();

			for (int i = 0; i < 10; i++)
			{
				AddSpeedBar();
			}

			RefreshDiscordLogin();

			casterToolsBox.Visibility = !Program.Personal ? Visibility.Visible : Visibility.Collapsed;
		}

		private void Update(object source, ElapsedEventArgs e)
		{
			if (Program.running)
			{
				Dispatcher.Invoke(() =>
				{
					lock (Program.logOutputWriteLock)
					{
						string newText = FilterLines(unusedFileCache.ToString());
						if (newText != string.Empty && newText != Environment.NewLine)
						{
							try
							{
								mainOutputTextBox.AppendText(newText);

								if (Program.writeToOBSHTMLFile) // TODO this file path won't work
								{
									// write to html file for overlay as well
									File.WriteAllText("html_output/events.html", @"
								<html>
								<head>
								<meta http-equiv=""refresh"" content=""1"">
								<link rel=""stylesheet"" type=""text/css"" href=""styles.css"">
								</head>
								<body>

								<div id=""info""> " +
												newText
												+ @"
								</div>

								</body>
								</html>
							");
								}
							}
							catch (Exception) { }

							//ColorizeOutput("Entered state:", gameStateChangedCheckBox.ForeColor, mainOutputTextBox.Text.Length - newText.Length);
						}
						unusedFileCache.Clear();
					}


					// update the other labels in the stats box
					if (Program.lastFrame != null)  // 'mpl_lobby_b2' may change in the future
					{
						// session ID
						sessionIdTextBox.Text = Program.lastFrame.sessionid;

						// ip stuff
						if (Program.lastFrame.sessionip != lastIP)
						{
							serverLocationLabel.Content = "Server IP: " + Program.lastFrame.sessionip;
							_ = GetServerLocation(Program.lastFrame.sessionip);
						}
						lastIP = Program.lastFrame.sessionip;
					}
					else
					{
						serverLocationLabel.Content = "Server IP: ---";
					}

					if (Program.lastFrame != null && Program.lastFrame.map_name != "mpl_lobby_b2")  // 'mpl_lobby_b2' may change in the future
					{
						discSpeedLabel.Content = Program.lastFrame.disc.velocity.ToVector3().Length() + " m/s";
						//discSpeedProgressBar.Value = (int)Program.lastFrame.disc.Velocity.Length();
						//if (Program.lastFrame.teams[0].possession)
						//{
						//	discSpeedProgressBar.ForeColor = Color.Blue;
						//} else if (Program.lastFrame.teams[1].possession)
						//{
						//	discSpeedProgressBar.ForeColor = Color.Orange;
						//} else
						//{
						//	discSpeedProgressBar.ForeColor = Color.Gray;
						//}

						string playerSpeedHTML = @"
				<html>
				<head>
				<meta http-equiv=""refresh"" content=""0.2"">
				<link rel=""stylesheet"" type=""text/css"" href=""styles.css"">
				</head>
				<body>
				<div id = ""player_speeds"">";
						bool updatedHTML = false;

						StringBuilder pingsTextNames = new StringBuilder();
						StringBuilder pingsTextPings = new StringBuilder();
						StringBuilder[] teamNames = new StringBuilder[]
						{
							new StringBuilder(),
							new StringBuilder(),
							new StringBuilder()
						};

						// loop through all the players and set their speed progress bars and pings
						int i = 0;
						for (int t = 0; t < 3; t++)
						{
							foreach (var player in Program.lastFrame.teams[t].players)
							{
								if (t < 2)
								{
									if (playerSpeedBars.Count > i)
									{
										playerSpeedBars[i].Visibility = Visibility.Visible;
										double speed = (player.velocity.ToVector3().Length() * 10);
										if (speed > playerSpeedBars[i].Maximum) speed = playerSpeedBars[i].Maximum;
										playerSpeedBars[i].Value = speed;
										Color color = t == 0 ? Color.DodgerBlue : Color.Orange;

										// TODO convert to WPF
										//playerSpeedBars[i].Foreground = color;
										//playerSpeedBars[i].Background = speedsLayout.BackColor;
										i++;

										updatedHTML = true;
										playerSpeedHTML += "<div style=\"width:" + speed + "px;\" class=\"speed_bar " + (g_Team.TeamColor)t + "\"></div>\n";
									}

									pingsTextNames.AppendLine(player.name + ":");
									pingsTextPings.AppendLine(player.ping.ToString());
								}
								teamNames[t].AppendLine(player.name);
							}
						}

						playerPingsNames.Content = pingsTextNames.ToString();
						playerPingsPings.Content = pingsTextPings.ToString();

						blueTeamPlayersLabel.Content = teamNames[0].ToString().Trim();
						orangeTeamPlayersLabel.Content = teamNames[1].ToString().Trim();
						spectatorsLabel.Content = teamNames[2].ToString().Trim();

						if (updatedHTML && Program.writeToOBSHTMLFile)
						{
							playerSpeedHTML += "</div></body></html>";

							File.WriteAllText("html_output/player_speeds.html", playerSpeedHTML);
						}

						for (; i < playerSpeedBars.Count; i++)
						{
							playerSpeedBars[i].Visibility = Visibility.Visible;
							// TODO convert to WPF
							//playerSpeedBars[i].Background = speedsHovering ? Color.FromArgb(60, 60, 60) : Color.FromArgb(45, 45, 45);
						}
					}
					else
					{
						sessionIdTextBox.Text = "---";
						discSpeedLabel.Content = "--- m/s";
						//discSpeedProgressBar.Value = 0;
						//discSpeedProgressBar.ForeColor = Color.Gray;
						foreach (ProgressBar bar in playerSpeedBars)
						{
							bar.Value = 0;
							// TODO convert to WPF
							//bar.BackColor = speedsHovering ? Color.FromArgb(60, 60, 60) : Color.FromArgb(45, 45, 45);
						}
					}

					connectedLabel.Content = Program.inGame ? "Connected" : "Not Connected";

					// TODO convert to WPF
					//speedsLayout.BackColor = speedsHovering ? Color.FromArgb(60, 60, 60) : Color.FromArgb(45, 45, 45);



					#region Rejoiner

					// show the button once the player hasn't been getting data for some time
					float secondsUntilRejoiner = 1f;
					if (Program.lastFrame != null &&
						Program.lastFrame.private_match &&
						DateTime.Compare(Program.lastDataTime.AddSeconds(secondsUntilRejoiner), DateTime.Now) < 0)
					{
						rejoinButton.Visibility = Visibility.Visible;
					}
					else
					{
						rejoinButton.Visibility = Visibility.Collapsed;
					}

					#endregion

					RefreshDiscordLogin();

					if (!Program.running)
					{
						outputUpdateTimer.Stop();
					}
				});
			}
		}

		public void RefreshDiscordLogin()
		{
			string username = DiscordOAuth.DiscordUsername;
			if (username != lastDiscordUsername)
			{
				if (string.IsNullOrEmpty(username))
				{
					discordUsernameLabel.Content = "Discord Login";
					discordUsernameLabel.Width = 200;
					discordPFPImage.Source = null;
					discordPFPImage.Visibility = Visibility.Collapsed;
				}
				else
				{
					discordUsernameLabel.Content = username;
					string imgUrl = DiscordOAuth.DiscordPFPURL;
					if (!string.IsNullOrEmpty(imgUrl))
					{
						discordUsernameLabel.Width = 160;
						discordPFPImage.Source = new BitmapImage(new Uri(imgUrl));
						discordPFPImage.Visibility = Visibility.Visible;
					}
				}

				lastDiscordUsername = username;
			}
		}

		private void AddSpeedBar()
		{
			// TODO convert to WPF
			//ColoredProgressBar bar = new ColoredProgressBar();
			//playerSpeedBars.Add(bar);
			//bar.Height = 10;
			//Padding margins = bar.Margin;
			//margins.Top = 0;
			//margins.Bottom = 0;
			//margins.Left = 0;
			//margins.Right = 0;
			//bar.Margin = margins;
			//bar.Width = 200;
			//bar.Maximum = 200;

			//bar.Click += new EventHandler(openSpeedometer);
			//bar.MouseLeave += new EventHandler(speedsUnHover);
			//bar.MouseEnter += new EventHandler(speedsHover);
			//bar.Cursor = Cursors.Hand;

			//speedsFlowLayout.Controls.Add(bar);

		}


		private void LiveWindow_Load(object sender, EventArgs e)
		{
			lock (Program.logOutputWriteLock)
			{
				mainOutputTextBox.Text = string.Join('\n', fullFileCache);
			}

			_ = CheckForAppUpdate();
		}

		private async Task CheckForAppUpdate()
		{
			try
			{
				HttpClient updateClient = new HttpClient
				{
					BaseAddress = new Uri(Program.UpdateURL)
				};
				HttpResponseMessage response = await updateClient.GetAsync("get_ignitebot_update");
				JObject respObj = JObject.Parse(response.Content.ReadAsStringAsync().Result);
				string version = (string)respObj["version"];

				// if we need a new version
				if (version != Program.AppVersion())
				{
					updateFilename = (string)respObj["filename"];
					updateButton.Visibility = Visibility.Visible;
				}
			}
			catch (HttpRequestException)
			{
				LogRow(LogType.Error, "Couldn't check for update.");
			}
		}

		private async Task GetServerLocation(string ip)
		{
			if (ip != "")
			{
				try
				{
					HttpClient updateClient = new HttpClient
					{
						BaseAddress = new Uri("http://ip-api.com/json/")
					};
					HttpResponseMessage response = await updateClient.GetAsync(ip);
					JObject respObj = JObject.Parse(response.Content.ReadAsStringAsync().Result);
					string loc = (string)respObj["city"] + ", " + (string)respObj["regionName"];
					serverLocationLabel.Content = "Server Location:\n" + loc;

					if (Settings.Default.serverLocationTTS)
					{
						Program.synth.SpeakAsync(loc);
					}
				}
				catch (HttpRequestException)
				{
					LogRow(LogType.Error, "Couldn't get city of ip address.");
				}
			}
		}

		private void CloseButtonClicked(object sender, RoutedEventArgs e)
		{
			outputUpdateTimer.Stop();
			Close();
		}

		private void SettingsButtonClicked(object sender, RoutedEventArgs e)
		{
			if (Program.settingsWindow == null)
			{
				Program.settingsWindow = new SettingsWindow();
				Program.settingsWindow.Closed += (sender, args) => Program.settingsWindow = null;
				Program.settingsWindow.Show();
			}
			else
			{
				Program.settingsWindow.Close();
				// TODO maybe add Program.settingsWindow = null here. Check if it's necessary
			}
		}

		private void QuitButtonClicked(object sender, RoutedEventArgs e)
		{
			Program.Quit();
		}

		private void pauseButton_Click(object sender, RoutedEventArgs e)
		{
			Program.paused = !Program.paused;
			((Button)sender).Content = Program.paused ? "Continue" : "Pause";
		}

		private string FilterLines(string input)
		{
			string output = input;
			IEnumerable<string> lines = output
				.Split(new[] { '\r', '\n' })
				.Select(l => l.Trim())
				//.Where(l =>
				//{
				//	if (
				//	(!showHideLinesBox.Visible && l.Length > 0) || (
				//	(Settings.Default.outputGameStateEvents && l.Contains("Entered state:")) ||
				//	(Settings.Default.outputScoreEvents && l.Contains("scored")) ||
				//	(Settings.Default.outputStunEvents && l.Contains("just stunned")) ||
				//	(Settings.Default.outputDiscThrownEvents && l.Contains("threw the disk")) ||
				//	(Settings.Default.outputDiscCaughtEvents && l.Contains("caught the disk")) ||
				//	(Settings.Default.outputDiscStolenEvents && l.Contains("stole the disk")) ||
				//	(Settings.Default.outputSaveEvents && l.Contains("save"))
				//	))
				//	{
				//		return true;
				//	}
				//	else
				//	{
				//		return false;
				//	}
				//})
				;

			output = string.Join(Environment.NewLine, lines) + ((output != string.Empty) ? Environment.NewLine : string.Empty);

			//return output;
			return input;
		}

		private string FilterLines(List<string> input)
		{
			IEnumerable<string> lines = input
				.Select(l => l.Trim())
				//.Where(l =>
				//{
				//	if (
				//	(!showHideLinesBox.Visible && l.Length > 0) || (
				//	(Settings.Default.outputGameStateEvents && l.Contains("Entered state:")) ||
				//	(Settings.Default.outputScoreEvents && l.Contains("scored")) ||
				//	(Settings.Default.outputStunEvents && l.Contains("just stunned")) ||
				//	(Settings.Default.outputDiscThrownEvents && l.Contains("threw the disk")) ||
				//	(Settings.Default.outputDiscCaughtEvents && l.Contains("caught the disk")) ||
				//	(Settings.Default.outputDiscStolenEvents && l.Contains("stole the disk")) ||
				//	(Settings.Default.outputSaveEvents && l.Contains("save"))
				//	))
				//	{
				//		// Show this line
				//		return true;
				//	}
				//	else
				//	{
				//		// hide this line
				//		return false;
				//	}
				//})
				;

			string output = string.Join(Environment.NewLine, lines) + ((input.Count != 0 && input[0] != string.Empty) ? Environment.NewLine : string.Empty);

			//return output;
			return string.Join(Environment.NewLine, input);
		}

		private void accessCodeLabel_Click(object sender, RoutedEventArgs e)
		{
			LoginWindow login = new LoginWindow();

			login.Show();

			accessCodeLabel.Content = "Mode: " + Program.currentAccessCodeUsername;

			login.Close();
		}

		private void SessionIDFocused(object sender, RoutedEventArgs e)
		{
			sessionIdTextBox.SelectAll();
		}

		private void clearButton_Click(object sender, RoutedEventArgs e)
		{
			lock (Program.logOutputWriteLock)
			{
				fullFileCache.Clear();
				mainOutputTextBox.Text = FilterLines(fullFileCache);
			}
		}

		private void customIdChanged(object sender, RoutedEventArgs e)
		{
			Program.customId = ((TextBox)sender).Text;
		}

		private void splitStatsButtonClick(object sender, RoutedEventArgs e)
		{
			GenerateNewStatsId();
		}

		private void GenerateNewStatsId()
		{
			using (SHA256 sha = SHA256.Create())
			{
				byte[] hash = sha.ComputeHash(BitConverter.GetBytes(DateTime.Now.Ticks));
				// Convert the byte array to hexadecimal string
				StringBuilder sb = new StringBuilder();
				for (int i = 0; i < hash.Length; i++)
				{
					sb.Append(hash[i].ToString("X2"));
				}
				Program.customId = sb.ToString();
				customIdTextbox.Text = Program.customId;
			}
		}

		private void updateButton_Click(object sender, RoutedEventArgs e)
		{
			WebClient webClient = new WebClient();
			webClient.DownloadFileCompleted += new AsyncCompletedEventHandler(Completed);
			webClient.DownloadProgressChanged += new DownloadProgressChangedEventHandler(ProgressChanged);
			webClient.DownloadFileAsync(new Uri("https://ignitevr.gg/ignitebot_installers/" + updateFilename), Path.GetTempPath() + updateFilename);
		}

		private void ProgressChanged(object sender, DownloadProgressChangedEventArgs e)
		{
			updateProgressBar.Visibility = Visibility.Visible;
			updateProgressBar.Value = e.ProgressPercentage;
		}

		private void Completed(object sender, AsyncCompletedEventArgs e)
		{
			updateProgressBar.Visibility = Visibility.Collapsed;

			// TODO add a confirmation? 
			//DialogResult result = MessageBox.Show("Download Finished. Install?", "", MessageBoxButtons.OKCancel);

			//if (result == DialogResult.OK)
			//{

			// Install the update
			Process.Start(new ProcessStartInfo
			{
				FileName = Path.Combine(Path.GetTempPath(), updateFilename),
				UseShellExecute = true
			});

			Program.Quit();
			//}
			//else
			//{
			//	// just do nothing
			//}
		}

		private void UploadStatsManual(object sender, RoutedEventArgs e)
		{
			Program.UpdateStatsIngame(Program.lastFrame, manual: true);
		}

		private void RejoinClicked(object sender, RoutedEventArgs e)
		{
			Program.KillEchoVR();
			Program.StartEchoVR("player");
		}

		private void RestartAsSpectatorClick(object sender, RoutedEventArgs e)
		{
			Program.KillEchoVR();
			Program.StartEchoVR("spectator");
		}

		private void showEventLogFileButton_Click(object sender, RoutedEventArgs e)
		{
			string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IgniteBot\\" + logFolder);
			Process.Start(new ProcessStartInfo
			{
				FileName = folder,
				UseShellExecute = true
			});
		}

		private void openSpeedometer(object sender, RoutedEventArgs e)
		{
			if (Program.speedometerWindow == null)
			{
				Program.speedometerWindow = new Speedometer();
				Program.speedometerWindow.Closed += (sender, args) => Program.speedometerWindow = null;
				Program.speedometerWindow.Show();
			}
			else
			{
				Program.speedometerWindow.Close();
			}
		}

		private void speedsHover(object sender, RoutedEventArgs e)
		{
			speedsHoveringDict[(Control)sender] = true;
		}

		private void speedsUnHover(object sender, RoutedEventArgs e)
		{
			speedsHoveringDict[(Control)sender] = false;
		}

		private void hostLiveReplayButton_CheckedChanged(object sender, RoutedEventArgs e)
		{
			Program.hostingLiveReplay = ((CheckBox)sender).IsChecked == true;
		}

		private void enableAPIButton_Click(object sender, RoutedEventArgs e)
		{
			JToken settings = Program.ReadEchoVRSettings();
			if (settings != null)
			{
				new MessageBox("Enabled API access in the game settings.\nCLOSE ECHOVR BEFORE PRESSING OK!").Show();

				settings["game"]["EnableAPIAccess"] = true;
				Program.WriteEchoVRSettings(settings);
				enableAPIButton.Visibility = Visibility.Collapsed;

			}
			else
			{
				new MessageBox("Could not read EchoVR settings. \n How are you even here?").Show();
			}
		}

		private void showAtlasLinks_Click(object sender, RoutedEventArgs e)
		{
			if (Program.atlasLinksWindow == null)
			{
				Program.atlasLinksWindow = new AtlasLinks();
				Program.atlasLinksWindow.Closed += (sender, args) => Program.atlasLinksWindow = null;
				Program.atlasLinksWindow.Show();
			}
			else
			{
				Program.atlasLinksWindow.Close();
			}
		}

		private void playspaceButton_Click(object sender, RoutedEventArgs e)
		{
			if (Program.playspaceWindow == null)
			{
				Program.playspaceWindow = new Playspace();
				Program.playspaceWindow.Closed += (sender, args) => Program.playspaceWindow = null;
				Program.playspaceWindow.Show();
			}
			else
			{
				Program.playspaceWindow.Close();
			}
		}

		private void ttsSettings_Click(object sender, RoutedEventArgs e)
		{
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

		private void discordLoginHover(object sender, RoutedEventArgs e)
		{
			discordLoginHoveringDict[(Control)sender] = false;
		}

		private void discordLoginUnHover(object sender, RoutedEventArgs e)
		{
			discordLoginHoveringDict[(Control)sender] = false;
		}

		private void LoginWindowButtonClicked(object sender, RoutedEventArgs e)
		{
			if (Program.loginWindow == null)
			{
				Program.loginWindow = new LoginWindow();
				Program.loginWindow.Closed += (sender, args) => Program.loginWindow = null;
				Program.loginWindow.Show();
			}
			else
			{
				Program.loginWindow.Close();
			}
		}

		private void startSpectatorStream_Click(object sender, RoutedEventArgs e)
		{
			if (!string.IsNullOrEmpty(Settings.Default.echoVRPath))
			{
				Process.Start(Settings.Default.echoVRPath, "-spectatorstream");
			}
		}
	}
}
