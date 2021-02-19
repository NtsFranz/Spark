using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using IgniteBot.Properties;
using NetMQ;
using Newtonsoft.Json.Linq;
using static Logger;

namespace IgniteBot
{
	/// <summary>
	/// Interaction logic for LiveWindow.xaml
	/// </summary>
	/// 
	public partial class LiveWindow : Window
	{
		private readonly System.Timers.Timer outputUpdateTimer = new System.Timers.Timer();

		List<ProgressBar> playerSpeedBars = new List<ProgressBar>();
		private string updateFilename = "";

		public static readonly object lastSnapshotLock = new object();
		private string lastIP;

		private string lastDiscordUsername = string.Empty;
		private bool hidden;

		private bool isExplicitClose = false;

		private float smoothedServerScore = 100;
		private float serverScoreSmoothingFactor = .99f;

		[DllImport("User32.dll")]
		static extern bool MoveWindow(IntPtr handle, int x, int y, int width, int height, bool redraw);

		[DllImport("user32.dll")]
		static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

		internal delegate int WindowEnumProc(IntPtr hwnd, IntPtr lparam);
		[DllImport("user32.dll")]
		internal static extern bool EnumChildWindows(IntPtr hwnd, WindowEnumProc func, IntPtr lParam);

		[DllImport("user32.dll")]
		static extern int SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

		[DllImport("user32.dll")]
		static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

		[DllImport("user32.dll", EntryPoint = "SetWindowLongA", SetLastError = true)]
		private static extern long SetWindowLong(IntPtr hwnd, int nIndex, long dwNewLong);

		[DllImport("user32.dll")]
		private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		static extern bool GetWindowRect(HandleRef hWnd, out RECT lpRect);

		[StructLayout(LayoutKind.Sequential)]
		public struct RECT
		{
			public int Left;        // x position of upper-left corner
			public int Top;         // y position of upper-left corner
			public int Right;       // x position of lower-right corner
			public int Bottom;      // y position of lower-right corner
		}


		public Process SpeakerSystemProcess;
		private IntPtr unityHWND = IntPtr.Zero;

		const int UNITY_READY = 0x00000003;
		private const int WM_ACTIVATE = 0x0006;
		private readonly IntPtr WA_ACTIVE = new IntPtr(1);
		private readonly IntPtr WA_INACTIVE = new IntPtr(0);
		private const int GWL_STYLE = (-16);
		private const int WS_VISIBLE = 0x10000000;
		private const int GWL_USERDATA = (-21);

		public LiveWindow()
		{
			InitializeComponent();



			System.Windows.Application.Current.ShutdownMode = ShutdownMode.OnMainWindowClose;

			outputUpdateTimer.Interval = 100;
			outputUpdateTimer.Elapsed += Update;
			outputUpdateTimer.Enabled = true;

			JToken gameSettings = Program.ReadEchoVRSettings();
			if (gameSettings != null)
			{
				try
				{
					if (gameSettings["game"] != null && gameSettings["game"]["EnableAPIAccess"] != null)
					{
						enableAPIButton.Visibility = !(bool)gameSettings["game"]["EnableAPIAccess"] ? Visibility.Visible : Visibility.Collapsed;
					}
				}
				catch (Exception)
				{
					LogRow(LogType.Error, "Can't read EchoVR settings file. It exists, but something went wrong.");
					enableAPIButton.Visibility = Visibility.Collapsed;
				}
			}
			else
			{
				enableAPIButton.Visibility = Visibility.Collapsed;
			}
			//hostLiveReplayButton.Visible = !Program.Personal;

			versionLabel.Text = "v" + Program.AppVersion();

			GenerateNewStatsId();

			for (int i = 0; i < 10; i++)
			{
				AddSpeedBar();
			}

			RefreshDiscordLogin();

			casterToolsBox.Visibility = !Program.Personal ? Visibility.Visible : Visibility.Collapsed;
			showHighlights.IsEnabled = HighlightsHelper.DoNVClipsExist();
			showHighlights.Visibility = (HighlightsHelper.didHighlightsInit && HighlightsHelper.isNVHighlightsEnabled) ? Visibility.Visible : Visibility.Collapsed;
			showHighlights.Content = HighlightsHelper.DoNVClipsExist() ? "Show " + HighlightsHelper.nvHighlightClipCount + " Highlights" : "No clips available";


			tabControl.SelectionChanged += TabControl_SelectionChanged;
		}

		private void ActivateUnityWindow()
		{
			SendMessage(unityHWND, WM_ACTIVATE, WA_ACTIVE, IntPtr.Zero);
		}

		private void DeactivateUnityWindow()
		{
			SendMessage(unityHWND, WM_ACTIVATE, WA_INACTIVE, IntPtr.Zero);
		}

		private int WindowEnum(IntPtr hwnd, IntPtr lparam)
		{
			unityHWND = hwnd;
			//ActivateUnityWindow();
			MoveSpeakerSystemWindow();
			return 0;
		}

		private void speakerSystemPanel_Resize(object sender, EventArgs e)
		{
			if (speakerSystemPanel.IsVisible && SpeakerSystemProcess != null && SpeakerSystemProcess.Handle.ToInt32() > 0)
			{
				System.Windows.Point relativePoint = speakerSystemPanel.TransformToAncestor(this)
						  .Transform(new System.Windows.Point(0, 0));
				MoveWindow(unityHWND, (int)relativePoint.X, (int)relativePoint.Y, (int)speakerSystemPanel.ActualWidth, (int)speakerSystemPanel.ActualHeight, true);
				ActivateUnityWindow();
			}
		}

		public void MoveSpeakerSystemWindow()
		{
			//Wait until unity app is ready to be resized
			int count = 0;
			while (((int)GetWindowLongPtr(unityHWND, GWL_USERDATA) & UNITY_READY) != 1 && count < 40)
			{
				count++;
				Thread.Sleep(150);
			}
			ActivateUnityWindow();
			startStopEchoSpeakerSystem.IsEnabled = true;
			System.Windows.Point relativePoint = speakerSystemPanel.TransformToAncestor(this)
						  .Transform(new System.Windows.Point(0, 0));

			MoveWindow(unityHWND, Convert.ToInt32(relativePoint.X), Convert.ToInt32(relativePoint.Y), Convert.ToInt32(speakerSystemPanel.ActualWidth), Convert.ToInt32(speakerSystemPanel.ActualHeight), true);
		}
		private void liveWindow_FormClosed(object sender, EventArgs e)
		{
			try
			{
				KillSpeakerSystem();
				SpeakerSystemProcess?.CloseMainWindow();

			}
			catch (Exception)
			{

			}
		}

		public void KillSpeakerSystem()
		{
			try
			{

				if (SpeakerSystemProcess != null)
				{
					while (!SpeakerSystemProcess.HasExited)
						SpeakerSystemProcess.Kill();

					unityHWND = IntPtr.Zero;
					Thread.Sleep(100);
				}
			}
			catch (Exception)
			{

			}
		}
		private void Form1_Activated(object sender, EventArgs e)
		{
			ActivateUnityWindow();
		}

		private void Form1_Deactivate(object sender, EventArgs e)
		{
			DeactivateUnityWindow();
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
								mainOutputTextBox.ScrollToEnd();

								//	if (Program.writeToOBSHTMLFile) // TODO this file path won't work
								//	{
								//		// write to html file for overlay as well
								//		File.WriteAllText("html_output/events.html", @"
								//	<html>
								//	<head>
								//	<meta http-equiv=""refresh"" content=""1"">
								//	<link rel=""stylesheet"" type=""text/css"" href=""styles.css"">
								//	</head>
								//	<body>

								//	<div id=""info""> " +
								//					newText
								//					+ @"
								//	</div>

								//	</body>
								//	</html>
								//");
								//	}
							}
							catch (Exception) { }

							//ColorizeOutput("Entered state:", gameStateChangedCheckBox.ForeColor, mainOutputTextBox.Text.Length - newText.Length);
						}
						unusedFileCache.Clear();
					}
					showHighlights.IsEnabled = HighlightsHelper.DoNVClipsExist();
					showHighlights.Visibility = (HighlightsHelper.didHighlightsInit && HighlightsHelper.isNVHighlightsEnabled) ? Visibility.Visible : Visibility.Collapsed;
					showHighlights.Content = HighlightsHelper.DoNVClipsExist() ? "Show " + HighlightsHelper.nvHighlightClipCount + " Highlights" : "No clips available";


					// update the other labels in the stats box
					if (Program.lastFrame != null)  // 'mpl_lobby_b2' may change in the future
					{
						// session ID
						sessionIdTextBox.Text = "<ignitebot://choose/" + Program.lastFrame.sessionid + ">";

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
						discSpeedLabel.Text = Program.lastFrame.disc.velocity.ToVector3().Length().ToString("N2");
						switch (Program.lastFrame.possession[0])
						{
							case 0:
								discSpeedLabel.Foreground = System.Windows.Media.Brushes.CornflowerBlue;
								break;
							case 1:
								discSpeedLabel.Foreground = System.Windows.Media.Brushes.Orange;
								break;
							default:
								discSpeedLabel.Foreground = System.Windows.Media.Brushes.White;
								break;
						}
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

						StringBuilder blueTextNames = new StringBuilder();
						StringBuilder orangeTextNames = new StringBuilder();
						StringBuilder bluePingsTextPings = new StringBuilder();
						StringBuilder orangePingsTextPings = new StringBuilder();
						StringBuilder blueSpeedsTextSpeeds = new StringBuilder();
						StringBuilder orangeSpeedsTextSpeeds = new StringBuilder();
						StringBuilder[] teamNames = {
							new StringBuilder(),
							new StringBuilder(),
							new StringBuilder()
						};
						List<List<int>> pings = new List<List<int>> { new List<int>(), new List<int>() };

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

									if (t == 0)
									{
										blueTextNames.AppendLine(player.name);
										bluePingsTextPings.AppendLine(player.ping.ToString());
										blueSpeedsTextSpeeds.AppendLine(player.velocity.ToVector3().Length().ToString("N1"));
									}

									if (t == 1)
									{
										orangeTextNames.AppendLine(player.name);
										orangePingsTextPings.AppendLine(player.ping.ToString());
										orangeSpeedsTextSpeeds.AppendLine(player.velocity.ToVector3().Length().ToString("N1"));
									}

									pings[t].Add(player.ping);

								}
								teamNames[t].AppendLine(player.name);
							}
						}

						bluePlayerPingsNames.Text = blueTextNames.ToString();
						bluePlayerPingsPings.Text = bluePingsTextPings.ToString();
						orangePlayerPingsNames.Text = orangeTextNames.ToString();
						orangePlayerPingsPings.Text = orangePingsTextPings.ToString();

						float serverScore = Program.CalculateServerScore(pings[0], pings[1]);
						if (serverScore < 0)
						{
							playerPingsGroupbox.Header = $"Player Pings     >150";
						}
						else
						{
							smoothedServerScore = smoothedServerScore * serverScoreSmoothingFactor + (1 - serverScoreSmoothingFactor) * serverScore;
							playerPingsGroupbox.Header = $"Player Pings   Score: {smoothedServerScore:N1}";
						}
						if (Program.matchData != null)
						{
							Program.matchData.ServerScore = smoothedServerScore;
						}


						bluePlayersSpeedsNames.Text = blueTextNames.ToString();
						bluePlayerSpeedsSpeeds.Text = blueSpeedsTextSpeeds.ToString();
						orangePlayersSpeedsNames.Text = orangeTextNames.ToString();
						orangePlayerSpeedsSpeeds.Text = orangeSpeedsTextSpeeds.ToString();

						blueTeamPlayersLabel.Content = teamNames[0].ToString().Trim();
						orangeTeamPlayersLabel.Content = teamNames[1].ToString().Trim();
						spectatorsLabel.Content = teamNames[2].ToString().Trim();


						// last goals and last matches
						StringBuilder lastGoalsString = new StringBuilder();
						var lastGoals = Program.lastGoals.ToArray();
						if (lastGoals.Length > 0)
						{
							for (int j = lastGoals.Length - 1; j >= 0; j--)
							{
								var goal = lastGoals[j];
								lastGoalsString.AppendLine(goal.GameClock.ToString("N0") + "s  " + goal.LastScore.point_amount + " pts  " + goal.LastScore.person_scored + "  " + goal.LastScore.disc_speed.ToString("N1") + " m/s  " + goal.LastScore.distance_thrown.ToString("N1") + " m");
							}
						}
						lastGoalsTextBlock.Text = lastGoalsString.ToString();

						StringBuilder lastMatchesString = new StringBuilder();
						var lastMatches = Program.lastMatches.ToArray();
						if (lastMatches.Length > 0)
						{
							for (int j = lastMatches.Length - 1; j >= 0; j--)
							{
								var match = lastMatches[j];
								lastMatchesString.AppendLine(match.finishReason + (match.finishReason == MatchData.FinishReason.reset ? "  " + match.endTime : "") + "  ORANGE: " + match.teams[g_Team.TeamColor.orange].points + "  BLUE: " + match.teams[g_Team.TeamColor.blue].points);
							}
						}
						lastRoundScoresTextBlock.Text = lastMatchesString.ToString();

						StringBuilder lastJoustsString = new StringBuilder();
						var lastJousts = Program.lastJousts.ToArray();
						if (lastJousts.Length > 0)
						{
							for (int j = lastJousts.Length - 1; j >= 0; j--)
							{
								var joust = lastJousts[j];
								lastJoustsString.AppendLine(joust.player.name + "  " + (joust.joustTimeMillis / 1000f).ToString("N2") + " s" + (joust.eventType == EventData.EventType.joust_speed ? " N" : ""));
							}
						}
						lastJoustsTextBlock.Text = lastJoustsString.ToString();


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
						discSpeedLabel.Text = "---";
						discSpeedLabel.Foreground = System.Windows.Media.Brushes.LightGray;
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
						DateTime.Compare(Program.lastDataTime.AddSeconds(secondsUntilRejoiner), DateTime.Now) < 0 &&
						Settings.Default.echoVRIP == "127.0.0.1")
					{
						rejoinButton.Visibility = Visibility.Visible;
					}
					else
					{
						rejoinButton.Visibility = Visibility.Collapsed;
					}

					#endregion

					RefreshDiscordLogin();

					RefreshAccessCode();

					if (Settings.Default.echoVRIP != "127.0.0.1")
					{
						spectateMeButton.Visibility = Visibility.Visible;
					}
					else
					{
						spectateMeButton.Visibility = Visibility.Collapsed;
					}

					if (!Program.running)
					{
						outputUpdateTimer.Stop();
					}
				});
			}
		}


		protected override void OnClosing(CancelEventArgs e)
		{
			base.OnClosing(e);

			// if not specifically a exit button press, hide
			if (isExplicitClose == false)
			{
				e.Cancel = true;
				Hide();
				showHideMenuItem.Header = "Show Main Window";
				hidden = true;
			}

		}

		private void RefreshAccessCode()
		{
			accessCodeLabel.Text = "Mode: " + Program.currentAccessCodeUsername;
			casterToolsBox.Visibility = !Program.Personal ? Visibility.Visible : Visibility.Collapsed;
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
				else
				{
					updateButton.Visibility = Visibility.Collapsed;
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
					Program.matchData.ServerLocation = loc;
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
			Hide();
			showHideMenuItem.Header = "Show Main Window";
			hidden = true;
		}

		private void SettingsButtonClicked(object sender, RoutedEventArgs e)
		{
			if (Program.settingsWindow == null)
			{
				Program.settingsWindow = new UnifiedSettingsWindow();
				Program.settingsWindow.Owner = this;
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
			isExplicitClose = true;
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
				.Split('\r', '\n')
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

			accessCodeLabel.Text = "Mode: " + Program.currentAccessCodeUsername;

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
			webClient.DownloadFileCompleted += Completed;
			webClient.DownloadProgressChanged += ProgressChanged;
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
			Program.StartEchoVR("j");
		}

		private void RestartAsSpectatorClick(object sender, RoutedEventArgs e)
		{
			Program.KillEchoVR();
			Program.StartEchoVR("s");
		}

		private void showEventLogFileButton_Click(object sender, RoutedEventArgs e)
		{
			string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IgniteBot\\" + logFolder);
			if (Directory.Exists(folder))
			{
				Process.Start(new ProcessStartInfo
				{
					FileName = folder,
					UseShellExecute = true
				});
			}
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

		private void hostLiveReplayButton_CheckedChanged(object sender, RoutedEventArgs e)
		{
			Program.hostingLiveReplay = ((CheckBox)sender).IsChecked == true;
		}

		private void enableAPIButton_Click(object sender, RoutedEventArgs e)
		{
			try
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
			catch (Exception)
			{
				LogRow(LogType.Error, "Can't write to EchoVR settings file.");
			}
		}

		private void showAtlasLinks_Click(object sender, RoutedEventArgs e)
		{
			if (Program.atlasLinksWindow == null)
			{
				Program.atlasLinksWindow = new AtlasLinks();
				Program.atlasLinksWindow.Owner = this;
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

		private void showHighlights_Click(object sender, RoutedEventArgs e)
		{
			HighlightsHelper.ShowNVHighlights();
		}

		private void showNVHighlightsSettings_Click(object sender, RoutedEventArgs e)
		{
			if (Program.nvhWindow == null)
			{
				Program.nvhWindow = new NVHighlightsSettingsWindow();
				Program.nvhWindow.Closed += (sender, args) => Program.nvhWindow = null;
				Program.nvhWindow.Show();
			}
			else
			{
				Program.nvhWindow.Close();
			}
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
				Process.Start(Settings.Default.echoVRPath, "-spectatorstream" + (Settings.Default.capturevp2 ? " -capturevp2" : ""));
			}
		}

		private void ToggleHidden(object sender, RoutedEventArgs e)
		{
			if (hidden)
			{
				Show();
				showHideMenuItem.Header = "Hide Main Window";
			}
			else
			{
				Hide();
				showHideMenuItem.Header = "Show Main Window";
			}
			hidden = !hidden;
		}

		private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			// switched to event log tab
			if (((TabControl)sender).SelectedIndex == 1)
			{
				mainOutputTextBox.ScrollToEnd();
			}
			if (((TabControl)sender).SelectedIndex != 2 && SpeakerSystemProcess != null)
			{

				ShowWindow(unityHWND, 0);
			}
			else if (SpeakerSystemProcess != null)
			{
				ShowWindow(unityHWND, 1);
			}
		}

		private void SpectateMeClicked(object sender, RoutedEventArgs e)
		{
			Program.spectateMe = !Program.spectateMe;
			try
			{
				if (Program.spectateMe)
				{
					if (Program.inGame)
					{
						if (Program.lastFrame != null && !Program.lastFrame.inLobby)
						{
							Program.KillEchoVR();
							Program.StartEchoVR("spectate");
						}
					}
					spectateMeButton.Content = "Stop Spectating Me";
				}
				else
				{
					Program.KillEchoVR();
					spectateMeButton.Content = "Spectate Me";
				}
			}
			catch (Exception ex)
			{
				LogRow(LogType.Error, "Broke something in the spectator follow system.\n" + ex.ToString());
			}
		}

		private void EventLogTabClicked(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			mainOutputTextBox.ScrollToEnd();
		}

		private void EventLogTabClicked(object sender, System.Windows.Input.TouchEventArgs e)
		{
			mainOutputTextBox.ScrollToEnd();
		}

		private void CopyIgniteJoinLink(object sender, RoutedEventArgs e)
		{
			var link = sessionIdTextBox.Text;
			System.Windows.Clipboard.SetText(link);
			Task.Run(() => ShowCopiedText());
		}
		async Task ShowCopiedText()
		{
			Dispatcher.Invoke(() =>
			{
				copySessionIdButton.Content = "Copied!";
			});
			await Task.Delay(3000);

			Dispatcher.Invoke(() =>
			{
				copySessionIdButton.Content = "Copy";
			});
		}

		private void speakerSystemPanel_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
		{
			if (speakerSystemPanel.IsVisible)
			{
				if (SpeakerSystemProcess == null || SpeakerSystemProcess.Handle.ToInt32() == 0)
				{
					try
					{

						LogRow(LogType.Info, AppContext.BaseDirectory);
						if (Program.InstalledSpeakerSystemVersion.Length > 0)
						{
							installEchoSpeakerSystem.Visibility = Visibility.Hidden;
							startStopEchoSpeakerSystem.Visibility = Visibility.Visible;
							speakerSystemInstallLabel.Visibility = Visibility.Hidden;
						}
						else
						{
							installEchoSpeakerSystem.Visibility = Visibility.Visible;
							startStopEchoSpeakerSystem.Visibility = Visibility.Hidden;
						}
						if (Program.IsSpeakerSystemUpdateAvailable)
						{
							updateEchoSpeakerSystem.Visibility = Visibility.Visible;
						}
						else
						{
							updateEchoSpeakerSystem.Visibility = Visibility.Hidden;
						}
					}
					catch
					{
					}
				}
			}
		}

		private async void installEchoSpeakerSystem_Click(object sender, RoutedEventArgs e)
		{
			speakerSystemInstallLabel.Visibility = Visibility.Hidden;
			Program.pubSocket.SendMoreFrame("CloseApp").SendFrame("");
			Thread.Sleep(800);
			KillSpeakerSystem();
			startStopEchoSpeakerSystem.Content = "Start Echo Speaker System";

			speakerSystemInstallLabel.Content = "Installing Echo Speaker System";
			speakerSystemInstallLabel.Visibility = Visibility.Visible;
			installEchoSpeakerSystem.IsEnabled = false;
			startStopEchoSpeakerSystem.IsEnabled = false;
			var progress = new Progress<string>(s => speakerSystemInstallLabel.Content = s);
			await Task.Factory.StartNew(() => Program.InstallSpeakerSystem(progress),
										TaskCreationOptions.None);

			if (Program.InstalledSpeakerSystemVersion.Length > 0)
			{
				installEchoSpeakerSystem.Visibility = Visibility.Hidden;
				startStopEchoSpeakerSystem.Visibility = Visibility.Visible;
			}
			else
			{
				installEchoSpeakerSystem.Visibility = Visibility.Visible;
				startStopEchoSpeakerSystem.Visibility = Visibility.Hidden;
			}
			if (Program.IsSpeakerSystemUpdateAvailable)
			{
				updateEchoSpeakerSystem.Visibility = Visibility.Visible;
			}
			else
			{
				updateEchoSpeakerSystem.Visibility = Visibility.Hidden;
			}
		}
		public void SpeakerSystemStart(IntPtr unityHandle)
		{
			this.Dispatcher.Invoke(() =>
			{
				EnumChildWindows(unityHandle, WindowEnum, IntPtr.Zero);
				speakerSystemInstallLabel.Visibility = Visibility.Hidden;
				startStopEchoSpeakerSystem.Content = "Stop Echo Speaker System";
			});
		}

		public IntPtr GetUnityHandler()
		{
			IntPtr unityHandle = IntPtr.Zero;
			this.Dispatcher.Invoke(() =>
			{

				HwndSource source = (HwndSource)PresentationSource.FromVisual(speakerSystemPanel);

				var helper = new WindowInteropHelper(this);
				var hwndSource = HwndSource.FromHwnd(helper.EnsureHandle());
				unityHandle = hwndSource.Handle;
				return unityHandle;
			});
			return unityHandle;
		}

		private void startStopEchoSpeakerSystem_Click(object sender, RoutedEventArgs e)
		{
			if (speakerSystemPanel.IsVisible)
			{
				if (SpeakerSystemProcess == null || SpeakerSystemProcess.HasExited)
				{
					try
					{
						speakerSystemInstallLabel.Visibility = Visibility.Hidden;
						startStopEchoSpeakerSystem.IsEnabled = false;
						startStopEchoSpeakerSystem.Content = "Stop Echo Speaker System";
						SpeakerSystemProcess = new Process();
						HwndSource source = (HwndSource)PresentationSource.FromVisual(speakerSystemPanel);

						var helper = new WindowInteropHelper(this);
						var hwndSource = HwndSource.FromHwnd(helper.EnsureHandle());
						IntPtr unityHandle = hwndSource.Handle;
						SpeakerSystemProcess.StartInfo.FileName = "C:\\Program Files (x86)\\Echo Speaker System\\Echo Speaker System.exe";
						SpeakerSystemProcess.StartInfo.Arguments = "ignitebot -parentHWND " + unityHandle.ToInt32() + " " + Environment.CommandLine;
						SpeakerSystemProcess.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
						SpeakerSystemProcess.StartInfo.CreateNoWindow = true;

						SpeakerSystemProcess.Start();
						SpeakerSystemProcess.WaitForInputIdle();
						SpeakerSystemProcess.Refresh();
						SetParent(unityHWND, unityHandle);
						SetWindowLong(SpeakerSystemProcess.MainWindowHandle, GWL_STYLE, WS_VISIBLE);
						SpeakerSystemStart(unityHandle);
					}
					catch (Exception ex)
					{
						startStopEchoSpeakerSystem.Content = "Start Echo Speaker System";
						startStopEchoSpeakerSystem.IsEnabled = true;
					}
				}
				else
				{
					speakerSystemInstallLabel.Visibility = Visibility.Hidden;
					Program.pubSocket.SendMoreFrame("CloseApp").SendFrame("");
					Thread.Sleep(800);
					KillSpeakerSystem();
					startStopEchoSpeakerSystem.Content = "Start Echo Speaker System";
					startStopEchoSpeakerSystem.IsEnabled = true;
				}
			}
		}
	}
}
