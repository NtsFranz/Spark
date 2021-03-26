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
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Spark.Properties;
using NetMQ;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static Logger;

namespace Spark
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
		private bool lastFrameWasValidSmoothedScore = false;
		private float serverScoreSmoothingFactor = .95f;

		string blueLogo = "";
		string orangeLogo = "";

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

			Application.Current.ShutdownMode = ShutdownMode.OnMainWindowClose;

			Loaded += (_, _) =>
			{
				if (Settings.Default.startMinimized)
				{
					Hide();
					showHideMenuItem.Header = Properties.Resources.Show_Main_Window;
					hidden = true;
				}
			};

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
						// TODO re-enable this feature once game setting saving works again
						//enableAPIButton.Visibility = !(bool)gameSettings["game"]["EnableAPIAccess"] ? Visibility.Visible : Visibility.Collapsed;
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

			GenerateNewStatsId();

			for (int i = 0; i < 10; i++)
			{
				AddSpeedBar();
			}

			RefreshDiscordLogin();

			casterToolsBox.Visibility = !Program.Personal ? Visibility.Visible : Visibility.Collapsed;
			showHighlights.IsEnabled = HighlightsHelper.DoNVClipsExist();
			showHighlights.Visibility = (HighlightsHelper.didHighlightsInit && HighlightsHelper.isNVHighlightsEnabled) ? Visibility.Visible : Visibility.Collapsed;
			showHighlights.Content = HighlightsHelper.DoNVClipsExist() ? Properties.Resources.Show + " " + HighlightsHelper.nvHighlightClipCount + " " + Properties.Resources.Highlights : Properties.Resources.No_clips_available;


			tabControl.SelectionChanged += TabControl_SelectionChanged;
		}
		public void FocusSpark()
		{
			//WPF focus the Spark Window 
			Dispatcher.Invoke(() =>
			{
				if (!IsVisible)
				{
					Show();
				}

				if (WindowState == WindowState.Minimized)
				{
					WindowState = WindowState.Normal;
				}

				Activate();
				Topmost = true;
				Topmost = false;
				Focus();
			});
		}

		public static string AppVersionLabelText => $"v{Program.AppVersion()}";

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
			if (!speakerSystemPanel.IsVisible || SpeakerSystemProcess == null || SpeakerSystemProcess.Handle.ToInt32() <= 0) return;

			System.Windows.Point relativePoint = speakerSystemPanel.TransformToAncestor(this).Transform(new System.Windows.Point(0, 0));
			MoveWindow(unityHWND, (int)relativePoint.X, (int)relativePoint.Y, (int)speakerSystemPanel.ActualWidth, (int)speakerSystemPanel.ActualHeight, true);
			ActivateUnityWindow();
		}

		private void MoveSpeakerSystemWindow()
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
			catch (Exception ex)
			{
				LogRow(LogType.Error, $"Error closing live window\n{ex}");
			}
		}

		public void KillSpeakerSystem()
		{
			try
			{
				if (SpeakerSystemProcess == null) return;

				while (!SpeakerSystemProcess.HasExited)
				{
					SpeakerSystemProcess.Kill();
				}

				unityHWND = IntPtr.Zero;
				Thread.Sleep(100);
			}
			catch (Exception e)
			{
				LogRow(LogType.Error, $"Error killing speaker system\n{e}");
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
							catch (Exception ex)
							{
								LogRow(LogType.Error, $"Error writing to output log.\n{ex}");
							}

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
						serverLocationLabel.Content = $"{Properties.Resources.Server_IP_} ---";
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
										System.Drawing.Color color = t == 0 ? System.Drawing.Color.DodgerBlue : System.Drawing.Color.Orange;

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

						if (pings[0].Count != 4 || pings[1].Count != 4)
						{
							playerPingsGroupbox.Header = $"{Properties.Resources.Player_Pings}   {Properties.Resources.Score_} --";
						}
						else if (serverScore < 0)
						{
							playerPingsGroupbox.Header = $"{Properties.Resources.Player_Pings}     >150";
						}
						else
						{
							// reset the smoothing every time it switches to being valid
							if (!lastFrameWasValidSmoothedScore)
							{
								smoothedServerScore = serverScore;
								lastFrameWasValidSmoothedScore = true;
							}
							else
							{
								smoothedServerScore = smoothedServerScore * serverScoreSmoothingFactor + (1 - serverScoreSmoothingFactor) * serverScore;
							}
							playerPingsGroupbox.Header = $"{Properties.Resources.Player_Pings}   {Properties.Resources.Score_} {smoothedServerScore:N1}";
						}
						if (Program.matchData != null)
						{
							Program.matchData.ServerScore = smoothedServerScore;

							if (blueLogo != Program.matchData.teams[g_Team.TeamColor.blue].vrmlTeamLogo)
							{
								blueLogo = Program.matchData.teams[g_Team.TeamColor.blue].vrmlTeamLogo;
								blueTeamLogo.Source = string.IsNullOrEmpty(blueLogo) ? null : new BitmapImage(new Uri(blueLogo));
								blueTeamLogo.ToolTip = Program.matchData.teams[g_Team.TeamColor.blue].vrmlTeamName;
							}
							if (orangeLogo != Program.matchData.teams[g_Team.TeamColor.orange].vrmlTeamLogo)
							{
								orangeLogo = Program.matchData.teams[g_Team.TeamColor.orange].vrmlTeamLogo;
								orangeTeamLogo.Source = string.IsNullOrEmpty(orangeLogo) ? null : new BitmapImage(new Uri(orangeLogo));
								orangeTeamLogo.ToolTip = Program.matchData.teams[g_Team.TeamColor.orange].vrmlTeamName;
							}
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
						GoalData[] lastGoals = Program.lastGoals.ToArray();
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


					hostMatchButton.IsEnabled = Program.lastFrame != null && Program.lastFrame.private_match == true;

					if (Program.lastFrame != null)
					{
						joinLink.Text = CurrentLink(Program.lastFrame.sessionid);
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
				showHideMenuItem.Header = Properties.Resources.Show_Main_Window;
				hidden = true;
			}

		}

		private void RefreshAccessCode()
		{
			string accessCodeLocal = Program.currentAccessCodeUsername == "Personal" ? Properties.Resources.Personal : Program.currentAccessCodeUsername;
			accessCodeLabel.Text = Properties.Resources.Mode + accessCodeLocal;
			casterToolsBox.Visibility = !Program.Personal ? Visibility.Visible : Visibility.Collapsed;
		}

		public void RefreshDiscordLogin()
		{
			string username = DiscordOAuth.DiscordUsername;
			if (username != lastDiscordUsername)
			{
				if (string.IsNullOrEmpty(username))
				{
					discordUsernameLabel.Content = Properties.Resources.Discord_Login;
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
					serverLocationLabel.Content = Properties.Resources.Server_Location_ + "\n" + loc;

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
			showHideMenuItem.Header = Properties.Resources.Show_Main_Window;
			hidden = true;
		}

		private void SettingsButtonClicked(object sender, RoutedEventArgs e)
		{
			Program.ToggleWindow(typeof(UnifiedSettingsWindow), "Settings");
		}

		private void QuitButtonClicked(object sender, RoutedEventArgs e)
		{
			isExplicitClose = true;
			Program.Quit();
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
			using SHA256 sha = SHA256.Create();

			byte[] hash = sha.ComputeHash(BitConverter.GetBytes(DateTime.Now.Ticks));
			// Convert the byte array to hexadecimal string
			StringBuilder sb = new StringBuilder();
			foreach (byte b in hash)
			{
				sb.Append(b.ToString("X2"));
			}
			Program.customId = sb.ToString();
			customIdTextbox.Text = Program.customId;
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
			string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Spark\\" + logFolder);
			if (Directory.Exists(folder))
			{
				Process.Start(new ProcessStartInfo
				{
					FileName = folder,
					UseShellExecute = true
				});
			} else
			{
				Directory.CreateDirectory(folder);
			}
		}

		private void openSpeedometer(object sender, RoutedEventArgs e)
		{
			Program.ToggleWindow(typeof(Speedometer), ownedBy: this);
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
					new MessageBox(Properties.Resources.Enabled_API_access_in_the_game_settings__nCLOSE_ECHOVR_BEFORE_PRESSING_OK_).Show();

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

		private void playspaceButton_Click(object sender, RoutedEventArgs e)
		{
			Program.ToggleWindow(typeof(Playspace));
		}

		private void showHighlights_Click(object sender, RoutedEventArgs e)
		{
			HighlightsHelper.ShowNVHighlights();
		}

		private void LoginWindowButtonClicked(object sender, RoutedEventArgs e)
		{
			Program.ToggleWindow(typeof(LoginWindow), ownedBy: this);
		}

		private void startSpectatorStream_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				if (!string.IsNullOrEmpty(Settings.Default.echoVRPath))
				{
					Process.Start(Settings.Default.echoVRPath, "-spectatorstream" + (Settings.Default.capturevp2 ? " -capturevp2" : ""));
				}
			}
			catch (Exception ex)
			{
				// TODO show message about path not set
			}
		}

		private void ToggleHidden(object sender, RoutedEventArgs e)
		{
			if (hidden)
			{
				Show();
				showHideMenuItem.Header = Properties.Resources.Hide_Main_Window;
			}
			else
			{
				Hide();
				showHideMenuItem.Header = Properties.Resources.Show_Main_Window;
			}
			hidden = !hidden;
		}

		private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			// if switched to atlas tab
			if (((TabControl)sender).SelectedIndex == 1)
			{
				RefreshCurrentLink();
				GetAtlasMatches();
			}
			// switched to event log tab
			else if (((TabControl)sender).SelectedIndex == 2)
			{
				mainOutputTextBox.ScrollToEnd();
			}
			if (((TabControl)sender).SelectedIndex != 3 && SpeakerSystemProcess != null)
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
					spectateMeButton.Content = Properties.Resources.Stop_Spectating_Me;
				}
				else
				{
					Program.KillEchoVR();
					spectateMeButton.Content = Properties.Resources.Spectate_Me;
				}
			}
			catch (Exception ex)
			{
				LogRow(LogType.Error, $"Broke something in the spectator follow system.\n{ex}");
			}
		}

		private void EventLogTabClicked(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			lock (Program.logOutputWriteLock)
			{
				mainOutputTextBox.ScrollToEnd();
			}
		}

		private void EventLogTabClicked(object sender, System.Windows.Input.TouchEventArgs e)
		{
			lock (Program.logOutputWriteLock)
			{
				mainOutputTextBox.ScrollToEnd();
			}
		}

		private void CopyIgniteJoinLink(object sender, RoutedEventArgs e)
		{
			string link = sessionIdTextBox.Text;
			Clipboard.SetText(link);
			Task.Run(ShowCopiedText);
		}

		private async Task ShowCopiedText()
		{
			Dispatcher.Invoke(() =>
			{
				copySessionIdButton.Content = Properties.Resources.Copied_;
			});
			await Task.Delay(3000);

			Dispatcher.Invoke(() =>
			{
				copySessionIdButton.Content = Properties.Resources.Copy;
			});
		}

		private void speakerSystemPanel_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
		{
			if (!speakerSystemPanel.IsVisible) return;
			if (SpeakerSystemProcess != null && SpeakerSystemProcess.Handle.ToInt32() != 0) return;

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
			catch (Exception ex)
			{
				LogRow(LogType.Error, $"Error showing or hiding speaker system.\n{ex}");
			}
		}

		private async void installEchoSpeakerSystem_Click(object sender, RoutedEventArgs e)
		{
			speakerSystemInstallLabel.Visibility = Visibility.Hidden;
			Program.pubSocket.SendMoreFrame("CloseApp").SendFrame("");
			Thread.Sleep(800);
			KillSpeakerSystem();
			startStopEchoSpeakerSystem.Content = Properties.Resources.Start_Echo_Speaker_System;

			speakerSystemInstallLabel.Content = Properties.Resources.Installing_Echo_Speaker_System;
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
			Dispatcher.Invoke(() =>
			{
				SpeakerSystemProcess.Refresh();
				SetParent(unityHWND, unityHandle);
				SetWindowLong(SpeakerSystemProcess.MainWindowHandle, GWL_STYLE, WS_VISIBLE);
				EnumChildWindows(unityHandle, WindowEnum, IntPtr.Zero);
				speakerSystemInstallLabel.Visibility = Visibility.Hidden;
				startStopEchoSpeakerSystem.Content = Properties.Resources.Stop_Echo_Speaker_System;
			});
		}

		public IntPtr GetUnityHandler()
		{
			IntPtr unityHandle = IntPtr.Zero;
			Dispatcher.Invoke(() =>
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
			if (!speakerSystemPanel.IsVisible) return;

			if (SpeakerSystemProcess == null || SpeakerSystemProcess.HasExited)
			{
				try
				{
					speakerSystemInstallLabel.Visibility = Visibility.Hidden;
					startStopEchoSpeakerSystem.IsEnabled = false;
					startStopEchoSpeakerSystem.Content = Properties.Resources.Stop_Echo_Speaker_System;
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
					SpeakerSystemStart(unityHandle);
				}
				catch (Exception ex)
				{
					startStopEchoSpeakerSystem.Content = Properties.Resources.Start_Echo_Speaker_System;
					startStopEchoSpeakerSystem.IsEnabled = true;
				}
			}
			else
			{
				speakerSystemInstallLabel.Visibility = Visibility.Hidden;
				Program.pubSocket.SendMoreFrame("CloseApp").SendFrame("");
				Thread.Sleep(800);
				KillSpeakerSystem();
				startStopEchoSpeakerSystem.Content = Properties.Resources.Start_Echo_Speaker_System;
				startStopEchoSpeakerSystem.IsEnabled = true;
			}
		}

		private void LoneEchoSubtitlesClick(object sender, RoutedEventArgs e)
		{
			Program.ToggleWindow(typeof(LoneEchoSubtitles));
		}

		private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
		{
			try
			{
				Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
				e.Handled = true;
			}
			catch (Exception ex)
			{
				LogRow(LogType.Error, ex.ToString());
			}
		}

		#region Atlas Links Tab

		private static string CurrentLink(string sessionid)
		{
			string link = "";
			if (Settings.Default.atlasLinkUseAngleBrackets)
			{
				switch (Settings.Default.atlasLinkStyle)
				{
					case 0:
						link = "<ignitebot://choose/" + sessionid + ">";
						break;
					case 1:
						link = "<atlas://j/" + sessionid + ">";
						break;
					case 2:
						link = "<atlas://s/" + sessionid + ">";
						break;
				}
			}
			else
			{
				switch (Settings.Default.atlasLinkStyle)
				{
					case 0:
						link = "ignitebot://choose/" + sessionid;
						break;
					case 1:
						link = "atlas://j/" + sessionid;
						break;
					case 2:
						link = "atlas://s/" + sessionid;
						break;
				}
			}

			if (Settings.Default.atlasLinkAppendTeamNames)
			{
				if (Program.matchData != null &&
					Program.matchData.teams[g_Team.TeamColor.blue] != null &&
					Program.matchData.teams[g_Team.TeamColor.orange] != null &&
					!string.IsNullOrEmpty(Program.matchData.teams[g_Team.TeamColor.blue].vrmlTeamName) &&
					!string.IsNullOrEmpty(Program.matchData.teams[g_Team.TeamColor.orange].vrmlTeamName))
				{
					link += $" {Program.matchData.teams[g_Team.TeamColor.orange].vrmlTeamName} vs {Program.matchData.teams[g_Team.TeamColor.blue].vrmlTeamName}";
				}
			}

			return link;
		}

		private void GetLinks(object sender, RoutedEventArgs e)
		{
			string ip = alternateIPTextBox.Text;
			Task.Run(() => Program.GetAsync($"http://{ip}:6721/session", null, (responseJSON) =>
			{
				try
				{
					g_InstanceSimple obj = JsonConvert.DeserializeObject<g_InstanceSimple>(responseJSON);

					if (obj != null && !string.IsNullOrEmpty(obj.sessionid))
					{
						Dispatcher.Invoke(() =>
						{
							joinLink.Text = CurrentLink(obj.sessionid);

							Settings.Default.alternateEchoVRIP = alternateIPTextBox.Text;
							Settings.Default.Save();
						});
					}

				}
				catch (Exception ex)
				{
					Logger.LogRow(Logger.LogType.Error, $"Can't parse response\n{ex}");
				}
			}));
		}

		//public int HostingVisibilityDropdown {
		//	get => Settings.Default.atlasHostingVisibility;
		//	set {
		//		Settings.Default.atlasHostingVisibility = value;
		//		Settings.Default.Save();
		//	}
		//}

		private void CloseButton_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}

		private void HostMatchClicked(object sender, RoutedEventArgs e)
		{
			if (string.IsNullOrEmpty(Program.hostedAtlasSessionId))
			{
				Program.atlasHostingThread = new Thread(AtlasHostingThread);
				Program.atlasHostingThread.IsBackground = true;
				Program.atlasHostingThread.Start();
				hostingMatchCheckbox.IsChecked = true;
				hostingMatchLabel.Content = "Stop Hosting";
			}
			else
			{
				Program.hostedAtlasSessionId = "";
				hostingMatchCheckbox.IsChecked = false;
				hostingMatchLabel.Content = "Host Match";
			}
		}

		public class AtlasMatchResponse
		{
			public List<AtlasMatch> matches;
			public string player;
			public string qtype;
			public string datetime;
		}
		public class AtlasMatch
		{
			public class AtlasTeamInfo
			{
				public int count;
				public float percentage;
				public string team_logo;
				public string team_name;
			}
			[Obsolete("Use matchid instead")]
			public string session_id;
			/// <summary>
			/// Session id. This could be empty if the match isn't available to join
			/// </summary>
			public string matchid;
			/// <summary>
			/// Who hosted this match?
			/// </summary>
			public string username;
			public AtlasTeamInfo blue_team_info;
			public AtlasTeamInfo orange_team_info;
			/// <summary>
			/// List of player names
			/// </summary>
			public string[] blue_team;
			/// <summary>
			/// List of player names
			/// </summary>
			public string[] orange_team;
			/// <summary>
			/// If this is true, users with the caster login in Spark can see this match
			/// </summary>
			public bool visible_to_casters;
			/// <summary>
			/// Hides the match from public view. Can still be viewed by whitelist or casters if visible_for_casters is true
			/// </summary>
			public bool is_protected;
			/// <summary>
			/// Resolved location of the server (e.g. Chicago, Illinois)
			/// </summary>
			public string server_location;
			public float server_score;
			/// <summary>
			/// arena
			/// </summary>
			public string match_type;
			public string description;
			public bool is_lfg;
			public string[] whitelist;
			/// <summary>
			/// Currently used-up slots
			/// </summary>
			public int slots;
			/// <summary>
			/// Maximum allowed people in the match
			/// </summary>
			public int max_slots;
			public int blue_points;
			public int orange_points;
			public string title;
			public string map_name;
			public string game_type;
			public bool tournament_match;
			public string game_status;
			public bool allow_spectators;
			public bool private_match;
			public float game_clock;
			public string game_clock_display;

			public Dictionary<string, object> ToDict()
			{
				try
				{
					Dictionary<string, object> values = new()
					{
						{ "matchid", matchid },
						{ "username", username },
						{ "blue_team", blue_team },
						{ "orange_team", orange_team },
						{ "is_protected", is_protected },
						{ "visible_to_casters", visible_to_casters },
						{ "server_location", server_location },
						{ "server_score", server_score },
						{ "private_match", private_match },
						{ "whitelist", whitelist },
						{ "blue_points", blue_points },
						{ "orange_points", orange_points },
						{ "slots", slots },
						{ "allow_spectators", allow_spectators },
						{ "game_status", game_status },
						{ "game_clock", game_clock },
					};
					return values;
				}
				catch (Exception e)
				{
					Logger.LogRow(Logger.LogType.Error, $"Can't serialize atlas match data.\n{e.Message}\n{e.StackTrace}");
					return new Dictionary<string, object>
					{
						{"none", 0}
					};
				}
			}
		}

		public class AtlasWhitelist
		{
			public class AtlasTeam
			{
				public string teamName;
				public List<string> players = new();

				public AtlasTeam(string teamName)
				{
					this.teamName = teamName;
				}
			}

			public List<AtlasTeam> teams = new();
			public List<string> players = new();

			public List<string> TeamNames => teams.Select(t => t.teamName).ToList();
			public List<string> AllPlayers {
				get {
					List<string> allPlayers = new List<string>(players);
					foreach (AtlasTeam team in teams)
					{
						allPlayers.AddRange(team.players);
					}

					return allPlayers;
				}
			}
		}

		private void UpdateUIWithAtlasMatches(IEnumerable<AtlasMatch> matches)
		{
			try
			{
				Dispatcher.Invoke(() =>
				{
					// remove all the old children
					MatchesBox.Children.RemoveRange(0, MatchesBox.Children.Count);

					foreach (AtlasMatch match in matches)
					{
						Grid content = new Grid();
						StackPanel header = new StackPanel
						{
							Orientation = Orientation.Horizontal,
							VerticalAlignment = VerticalAlignment.Top,
							HorizontalAlignment = HorizontalAlignment.Right,
							Margin = new Thickness(0, 0, 10, 0)
						};
						header.Children.Add(new Label
						{
							Content = match.is_protected ? (match.visible_to_casters ? "Casters Only" : "Private") : "Public"
						});

						byte buttonColor = 70;
						Button copyLinkButton = new Button
						{
							Content = "Copy Atlas Link",
							Margin = new Thickness(50, 0, 0, 0),
							Padding = new Thickness(10, 0, 10, 0),
							Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(buttonColor, buttonColor, buttonColor)),
						};
						copyLinkButton.Click += (_, _) =>
						{
							Clipboard.SetText(CurrentLink(match.matchid));
						};
						header.Children.Add(copyLinkButton);
						Button joinButton = new Button
						{
							Content = "Join",
							Margin = new Thickness(20, 0, 0, 0),
							Padding = new Thickness(10, 0, 10, 0),
							Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(buttonColor, buttonColor, buttonColor)),
						};
						joinButton.Click += (_, _) =>
						{
							Process.Start(new ProcessStartInfo
							{
								FileName = "ignitebot://choose/" + match.matchid,
								UseShellExecute = true
							});
						};
						header.Children.Add(joinButton);

						if (!string.IsNullOrEmpty(match.title) && match.title != "Default Lobby Name")
						{
							header.Children.Add(new Label
							{
								Content = match.title
							});
						}
						else if (!string.IsNullOrEmpty(match.server_location))
						{
							header.Children.Add(new Label
							{
								Content = match.server_location
							});
						}

						content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
						content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
						content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
						content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });

						content.ShowGridLines = true;

						System.Windows.Controls.Image blueLogo = new System.Windows.Controls.Image
						{
							Width = 100,
							Height = 100
						};
						if (match.blue_team_info?.team_logo != string.Empty)
						{
							blueLogo.Source = string.IsNullOrEmpty(match.blue_team_info?.team_logo) ? null : (new BitmapImage(new Uri(match.blue_team_info.team_logo)));
						}
						StackPanel blueLogoBox = new StackPanel
						{
							Orientation = Orientation.Vertical,
							Margin = new Thickness(5, 10, 5, 10)
						};
						blueLogoBox.SetValue(Grid.ColumnProperty, 0);
						blueLogoBox.Children.Add(blueLogo);
						blueLogoBox.Children.Add(new Label
						{
							Content = match.blue_team_info?.team_name,
							HorizontalAlignment = HorizontalAlignment.Center

						});


						System.Windows.Controls.Image orangeLogo = new System.Windows.Controls.Image
						{
							Width = 100,
							Height = 100
						};
						if (match.orange_team_info?.team_logo != string.Empty)
						{
							orangeLogo.Source = string.IsNullOrEmpty(match.orange_team_info?.team_logo) ? null : (new BitmapImage(new Uri(match.orange_team_info.team_logo)));
						}
						StackPanel orangeLogoBox = new StackPanel
						{
							Orientation = Orientation.Vertical,
							Margin = new Thickness(5, 10, 5, 10)
						};
						orangeLogoBox.SetValue(Grid.ColumnProperty, 3);
						orangeLogoBox.Children.Add(orangeLogo);
						orangeLogoBox.Children.Add(new Label
						{
							Content = match.orange_team_info?.team_name,
							HorizontalAlignment = HorizontalAlignment.Center
						});

						TextBlock bluePlayers = new TextBlock
						{
							Text = string.Join('\n', match.blue_team),
							Margin = new Thickness(10, 10, 10, 10),
							TextAlignment = TextAlignment.Right
						};
						bluePlayers.SetValue(Grid.ColumnProperty, 1);
						TextBlock orangePlayers = new TextBlock
						{
							Text = string.Join('\n', match.orange_team),
							Margin = new Thickness(10, 10, 10, 10)
						};
						orangePlayers.SetValue(Grid.ColumnProperty, 2);
						Label sessionIdTextBox = new Label
						{
							Content = match.matchid
						};
						//content.Children.Add(sessionIdTextBox);
						content.Children.Add(blueLogoBox);
						content.Children.Add(orangeLogoBox);
						content.Children.Add(bluePlayers);
						content.Children.Add(orangePlayers);
						MatchesBox.Children.Add(new GroupBox
						{
							Content = content,
							Margin = new Thickness(10, 10, 10, 10),
							Header = header
						});
					}

				});
			}
			catch (Exception e)
			{
				Logger.LogRow(Logger.LogType.Error, $"Error showing matches in UI\n{e}");
			}
		}

		private void AtlasHostingThread()
		{
			const string hostURL = Program.APIURL + "host_atlas_match_v2";
			const string unhostURL = Program.APIURL + "unhost_atlas_match_v2";

			// TODO show error message instead of just quitting
			if (Program.lastFrame == null || Program.lastFrame.teams == null) return;

			Program.hostedAtlasSessionId = Program.lastFrame.sessionid;

			AtlasMatch match = new AtlasMatch
			{
				matchid = Program.lastFrame.sessionid,
				blue_team = Program.lastFrame.teams[0].player_names.ToArray(),
				orange_team = Program.lastFrame.teams[1].player_names.ToArray(),
				is_protected = (Settings.Default.atlasHostingVisibility > 0),
				visible_to_casters = (Settings.Default.atlasHostingVisibility == 1),
				server_location = Program.matchData.ServerLocation,
				server_score = Program.matchData.ServerScore,
				private_match = Program.lastFrame.private_match,
				username = Program.lastFrame.client_name,
				whitelist = Program.atlasWhitelist.AllPlayers.ToArray(),
			};
			bool firstHost = true;

			while (Program.running &&
				   Program.inGame &&
				   Program.lastFrame != null &&
				   Program.lastFrame.teams != null &&
				   Program.hostedAtlasSessionId == Program.lastFrame.sessionid)
			{
				bool diff =
					firstHost ||
					match.blue_team.Length != Program.lastFrame.teams[0].players.Count ||
					match.orange_team.Length != Program.lastFrame.teams[1].players.Count ||
					(Program.lastFrame.teams[0].stats != null && match.blue_points != Program.lastFrame.teams[0].stats.points) ||
					(Program.lastFrame.teams[1].stats != null && match.orange_points != Program.lastFrame.teams[1].stats.points) ||
					match.is_protected != (Settings.Default.atlasHostingVisibility > 0) ||
					match.visible_to_casters != (Settings.Default.atlasHostingVisibility == 1) ||
					match.whitelist.Length != Program.atlasWhitelist.AllPlayers.Count;

				if (diff)
				{
					// actually update values
					match.blue_team = Program.lastFrame.teams[0].player_names.ToArray();
					match.orange_team = Program.lastFrame.teams[1].player_names.ToArray();
					match.blue_points = Program.lastFrame.teams[0].stats != null ? Program.lastFrame.teams[0].stats.points : 0;
					match.orange_points = Program.lastFrame.teams[1].stats != null ? Program.lastFrame.teams[1].stats.points : 0;
					match.is_protected = (Settings.Default.atlasHostingVisibility > 0);
					match.visible_to_casters = (Settings.Default.atlasHostingVisibility == 1);
					match.server_score = Program.matchData.ServerScore;
					match.username = Program.lastFrame.client_name;
					match.whitelist = Program.atlasWhitelist.AllPlayers.ToArray();
					match.slots = Program.lastFrame.GetAllPlayers().Count;

					string data = JsonConvert.SerializeObject(match.ToDict());
					firstHost = false;

					// post new data, then fetch the updated list
					Task.Run(() => Program.PostAsync(hostURL, new Dictionary<string, string>() { { "x-api-key", DiscordOAuth.igniteUploadKey } }, data, (responseJSON) =>
					{
						GetAtlasMatches();
					}));
				}

				Thread.Sleep(100);
			}

			// post new data, then fetch the updated list
			string matchInfo = JsonConvert.SerializeObject(match.ToDict());
			Task.Run(() => Program.PostAsync(unhostURL, new Dictionary<string, string>() { { "x-api-key", DiscordOAuth.igniteUploadKey } }, matchInfo, (responseJSON) =>
			{
				Program.hostedAtlasSessionId = string.Empty;
				Dispatcher.Invoke(() =>
				{
					hostingMatchCheckbox.IsChecked = false;
					hostingMatchLabel.Content = "Host Match";
				});
				Thread.Sleep(10);
				GetAtlasMatches();
			}));
		}

		private void GetAtlasMatches()
		{
			AtlasMatchResponse oldAtlasResponse = null;
			AtlasMatchResponse igniteAtlasResponse = null;

			bool oldAtlasDone = false;
			bool igniteAtlasDone = false;

			Task.Run(() => Program.PostAsync("https://echovrconnect.appspot.com/api/v1/player/" + Settings.Default.client_name, new Dictionary<string, string> { { "User-Agent", "Atlas/0.5.8" } }, "", (responseJSON) =>
			{
				try
				{
					oldAtlasResponse = JsonConvert.DeserializeObject<AtlasMatchResponse>(responseJSON);
					oldAtlasDone = true;
				}
				catch (Exception e)
				{
					oldAtlasDone = true;
					Logger.LogRow(Logger.LogType.Error, $"Can't parse Atlas matches response\n{e}");
				}
			}));


			string matchesAPIURL = Program.APIURL + "atlas_matches_v2/" + Settings.Default.client_name;
			Task.Run(() => Program.GetAsync(
				matchesAPIURL,
				new Dictionary<string, string>() {
					{ "x-api-key", DiscordOAuth.igniteUploadKey },
					{ "access_code", DiscordOAuth.AccessCode }
				},
				(responseJSON) =>
				{
					try
					{
						igniteAtlasResponse = JsonConvert.DeserializeObject<AtlasMatchResponse>(responseJSON);
						igniteAtlasDone = true;

					}
					catch (Exception e)
					{
						igniteAtlasDone = true;
						Logger.LogRow(Logger.LogType.Error, $"Can't parse Atlas matches response\n{e}");
					}
				}
			 ));

			// once both requests are done....
			Task.Run(() =>
			{
				// wait until both requests are done
				while (!oldAtlasDone || !igniteAtlasDone) Task.Delay(100);

				// if the old atlas request worked
				if (oldAtlasResponse != null && oldAtlasResponse.matches != null)
				{
					// if both worked, add the ignite matches to the old list
					if (igniteAtlasResponse != null && igniteAtlasResponse.matches != null)
					{
						oldAtlasResponse.matches.AddRange(igniteAtlasResponse.matches);
					}
					UpdateUIWithAtlasMatches(oldAtlasResponse.matches);
				}
				// if only the ignite atlas request worked
				else if (igniteAtlasResponse != null && igniteAtlasResponse.matches != null)
				{
					UpdateUIWithAtlasMatches(igniteAtlasResponse.matches);
				}
				// if none worked
				else
				{
					UpdateUIWithAtlasMatches(Array.Empty<AtlasMatch>());
				}
			});
		}

		private void RefreshMatchesClicked(object sender, RoutedEventArgs e)
		{
			GetAtlasMatches();
		}

		private void RefreshCurrentLink()
		{
			if (Program.lastFrame != null)
			{
				joinLink.Text = CurrentLink(Program.lastFrame.sessionid);
			}
		}

		private void CopyMainLinkToClipboard(object sender, RoutedEventArgs e)
		{
			Clipboard.SetText(joinLink.Text);
		}

		private void FollowMainLink(object sender, RoutedEventArgs e)
		{
			try
			{
				if (joinLink.Text.Length > 10)
				{
					string text = joinLink.Text;
					if (joinLink.Text.StartsWith('<'))
					{
						text = text[1..^1];
					}
					text = text.Split(' ')[0];
					Process.Start(new ProcessStartInfo
					{
						FileName = text,
						UseShellExecute = true
					});
				}
			}
			catch (Exception ex)
			{
				Logger.LogRow(Logger.LogType.Error, ex.ToString());
			}
		}

		private void IPSourceDropdownChanged(object sender, SelectionChangedEventArgs e)
		{
			// TODO
		}

		private void WhitelistButtonClicked(object sender, RoutedEventArgs e)
		{
			Program.ToggleWindow(typeof(AtlasWhitelistWindow), "Atlas Whitelist", this);
		}

		public int LinkType {
			get => Settings.Default.atlasLinkStyle;
			set {
				Settings.Default.atlasLinkStyle = value;
				Settings.Default.Save();
				RefreshCurrentLink();
			}
		}

		private async void FindQuestIP(object sender, RoutedEventArgs e)
		{
			findQuestStatusLabel.Content = "Searching for Quest on network";
			findQuestStatusLabel.Visibility = Visibility.Visible;
			alternateIPTextBox.IsEnabled = false;
			findQuest.IsEnabled = false;
			resetIP.IsEnabled = false;
			var progress = new Progress<string>(s => findQuestStatusLabel.Content = s);
			await Task.Factory.StartNew(() => Program.echoVRIP = Program.FindQuestIP(progress),
										TaskCreationOptions.None);
			alternateIPTextBox.IsEnabled = true;
			findQuest.IsEnabled = true;
			resetIP.IsEnabled = true;
			if (!Program.overrideEchoVRPort) Program.echoVRPort = 6721;
			alternateIPTextBox.Text = Program.echoVRIP;
			Settings.Default.echoVRIP = Program.echoVRIP;
			if (!Program.overrideEchoVRPort) Settings.Default.echoVRPort = Program.echoVRPort;
			Settings.Default.Save();
		}

		private void SetToLocalIP(object sender, RoutedEventArgs e)
		{
			Program.echoVRIP = "127.0.0.1";
			alternateIPTextBox.Text = Program.echoVRIP;
			Settings.Default.echoVRIP = Program.echoVRIP;
			Settings.Default.Save();
		}

		private void EchoVRIPChanged(object sender, TextChangedEventArgs e)
		{
			if (!initialized) return;
			Program.echoVRIP = ((TextBox)sender).Text;
			Settings.Default.echoVRIP = Program.echoVRIP;
			Settings.Default.Save();
		}

		#endregion
	}
}
