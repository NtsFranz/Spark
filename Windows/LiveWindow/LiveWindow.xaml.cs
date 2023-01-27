using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using EchoVRAPI;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static Logger;
using Frame = EchoVRAPI.Frame;

namespace Spark
{
	/// <summary>
	/// Interaction logic for LiveWindow.xaml
	/// </summary>
	/// 
	public partial class LiveWindow
	{
		private readonly System.Timers.Timer outputUpdateTimer = new System.Timers.Timer();

		private string updateFilename = "";

		public static readonly object lastSnapshotLock = new object();

		private string lastDiscordUsername = string.Empty;
		private bool accessCodeDropdownListenerActive;
		public bool hidden;
		private bool isExplicitClose;

		string blueLogo = "";
		string orangeLogo = "";

		private bool tryingToShowGameOverlay;

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


		[StructLayout(LayoutKind.Sequential)]
		public struct RECT
		{
			public int Left; // x position of upper-left corner
			public int Top; // y position of upper-left corner
			public int Right; // x position of lower-right corner
			public int Bottom; // y position of lower-right corner
		}


		public Process SpeakerSystemProcess;
		private IntPtr unityHWND = IntPtr.Zero;

		const int UNITY_READY = 0x00000003;
		private const int WM_ACTIVATE = 0x0006;
		private readonly IntPtr WA_ACTIVE = new IntPtr(1);
		private const int GWL_STYLE = (-16);
		private const int WS_VISIBLE = 0x10000000;
		private const int GWL_USERDATA = (-21);

		[DllImport("user32.dll")]
		public static extern IntPtr GetWindowThreadProcessId(IntPtr hWnd, out uint ProcessId);

		[DllImport("user32.dll")]
		private static extern IntPtr GetForegroundWindow();

		private Process GetActiveProcessFileName()
		{
			IntPtr hwnd = GetForegroundWindow();
			uint pid;
			GetWindowThreadProcessId(hwnd, out pid);
			return Process.GetProcessById((int)pid);
		}


		private bool initialized;

		public LiveWindow()
		{
			InitializeComponent();

			Application.Current.ShutdownMode = ShutdownMode.OnMainWindowClose;

			outputUpdateTimer.Interval = 150;
			outputUpdateTimer.Elapsed += Update;
			outputUpdateTimer.Enabled = true;

			Loaded += (_, _) =>
			{
				if (SparkSettings.instance.startMinimized)
				{
					Hide();
					showHideMenuItem.Header = Properties.Resources.Show_Main_Window;
					hidden = true;
				}
			};

			DiscordOAuth.AccessCodeChanged += _ =>
			{
				Dispatcher.Invoke(() =>
				{
					RefreshAccessCodeList();
					RefreshDiscordLogin();
				});
			};

			DiscordOAuth.Authenticated += () =>
			{
				Dispatcher.Invoke(() =>
				{
					RefreshAccessCodeList();
					RefreshDiscordLogin();
				});
			};
			RefreshAccessCodeList();
			RefreshDiscordLogin();

			Program.NewMatch += frame =>
			{
				Dispatcher.Invoke(() =>
				{
					// ip stuff
					serverLocationLabel.Content = "Server IP: " + frame.sessionip;
					_ = GetServerLocation(frame.sessionip);
					RefreshPlayerList(frame);
				});
			};

			Program.PlayerJoined += (frame, team, arg3) => { Dispatcher.Invoke(() => { RefreshPlayerList(frame); }); };

			Program.PlayerLeft += (frame, team, arg3) => { Dispatcher.Invoke(() => { RefreshPlayerList(frame); }); };
			Program.PlayerSwitchedTeams += (frame, team, arg3, arg4) => { Dispatcher.Invoke(() => { RefreshPlayerList(frame); }); };
			Program.LeftGame += frame =>
			{
				Dispatcher.Invoke(() =>
				{
					RefreshLastRoundsList();
					RefreshPlayerList(frame);
				});
			};
			Program.JoinedGame += frame =>
			{
				Dispatcher.Invoke(() =>
				{
					RefreshLastRoundsList();
					RefreshPlayerList(frame);
				});
			};
			Program.Goal += (frame, data) =>
			{
				Dispatcher.Invoke(() =>
				{
					RefreshLastRoundsList();
					RefreshLastGoalsList();
				});
			};
			Program.NewRound += (frame) => { Dispatcher.Invoke(() => { RefreshLastRoundsList(); }); };
			Program.RoundOver += (frame, reason) => { Dispatcher.Invoke(() => { RefreshLastRoundsList(); }); };

			RefreshLastRoundsList();
			RefreshLastGoalsList();

			RefreshPlayerList(Program.lastFrame);

			JToken gameSettings = EchoVRSettingsManager.ReadEchoVRSettings();
			if (gameSettings != null)
			{
				try
				{
					if (gameSettings["game"]?["EnableAPIAccess"] != null)
					{
						// TODO re-enable this feature once game setting saving works again
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

			showHighlights.IsEnabled = HighlightsHelper.DoNVClipsExist();
			showHighlights.Visibility = (HighlightsHelper.didHighlightsInit && HighlightsHelper.isNVHighlightsEnabled) ? Visibility.Visible : Visibility.Collapsed;
			showHighlights.Content = HighlightsHelper.DoNVClipsExist() ? Properties.Resources.Show + " " + HighlightsHelper.nvHighlightClipCount + " " + Properties.Resources.Highlights : Properties.Resources.No_clips_available;

#if DEBUG
			EchoGPTab.Visibility = Visibility.Visible;
			ShowClickableOverlayButton.Visibility = Visibility.Visible;
#endif


			tabControl.SelectionChanged += TabControl_SelectionChanged;

			SetDashboardItem1Visibility(SparkSettings.instance.dashboardItem1);

			_ = CheckForAppUpdate();

			initialized = true;
		}

		private async void LiveWindow_Load(object sender, EventArgs e)
		{
			lock (Program.logOutputWriteLock)
			{
				mainOutputTextBox.Text = string.Join('\n', fullFileCache);
			}

			if (SparkSettings.instance.spectateMeOnByDefault)
			{
				spectateMeSubtitle.Text = Properties.Resources.Waiting_until_you_join_a_game;
				spectateMeLabel.Content = Properties.Resources.Stop_Spectating_Me;
			}

			try
			{
				string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
					"IgniteVR", "Spark", "WebView");
				CoreWebView2Environment webView2Environment = await CoreWebView2Environment.CreateAsync(null, path);
				//await PlayercardWebView.EnsureCoreWebView2Async(webView2Environment);
				//PlayercardWebView.Source = new UriBuilder("https://metrics.ignitevr.gg/playercard_embed").Uri;
			}
			catch (FileNotFoundException ex)
			{
				LogRow(LogType.Error, "4538: Failed to load WebView.\n" + ex);
				new MessageBox("Failed to load. Please report this to NtsFranz or else ┗|｀O′|┛ (4538)").Show();
			}
			catch (WebView2RuntimeNotFoundException ex)
			{
				Error("Error setting up webview: " + ex);
				string sparkFolder = Path.GetDirectoryName(SparkSettings.instance.sparkExeLocation) ?? "";
				string exePath = Path.Combine(sparkFolder, "resources", "MicrosoftEdgeWebview2Setup.exe");

				Process.Start(new ProcessStartInfo
				{
					FileName = exePath,
				});
			}
			catch (Exception ex)
			{
				LogRow(LogType.Error, "3645: Failed to load WebView for an unknown reason.\n" + ex);
				new MessageBox("Failed to load. Please report this to NtsFranz ( ╯□╰ ) (3645)").Show();
			}

			//_ = CheckForAppUpdate();
		}

		public void SetSpectateMeSubtitle(string text)
		{
			Dispatcher.Invoke(() => { spectateMeSubtitle.Text = text; });
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

		public static string AppVersionLabelText => $"v{Program.AppVersionString()}  {(Program.IsWindowsStore() ? Properties.Resources.Microsoft_Store : "")}";
		public static Visibility PlayercardsTabVisibility => Visibility.Visible; //Program.IsWindowsStore() ? Visibility.Visible : Visibility.Collapsed;

		private void ActivateUnityWindow()
		{
			SendMessage(unityHWND, WM_ACTIVATE, WA_ACTIVE, IntPtr.Zero);
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

			Point relativePoint = speakerSystemPanel.TransformToAncestor(this).Transform(new Point(0, 0));
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
			Point relativePoint = speakerSystemPanel.TransformToAncestor(this)
				.Transform(new Point(0, 0));

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
					showHighlights.Content = HighlightsHelper.DoNVClipsExist() ? "Show " + HighlightsHelper.nvHighlightClipCount + " Highlights" : Properties.Resources.No_clips_available;

					DiscordNotLoggedInHosting.Visibility = !DiscordOAuth.IsLoggedIn ? Visibility.Visible : Visibility.Collapsed;

					switch (Program.connectionState)
					{
						case Program.ConnectionState.NotConnected:
							statusLabel.Content = Properties.Resources.Not_Connected;
							statusCircle.Fill = new SolidColorBrush(Colors.Red);
							NotConnectedHelp.Visibility = Visibility.Visible;
							break;
						case Program.ConnectionState.Menu:
							statusLabel.Content = Properties.Resources.In_Loading_Screen;
							statusCircle.Fill = new SolidColorBrush(Colors.Yellow);
							NotConnectedHelp.Visibility = Visibility.Collapsed;
							break;
						case Program.ConnectionState.NoAPI:
							statusLabel.Content = Properties.Resources.API_Setting_Disabled;
							statusCircle.Fill = new SolidColorBrush(Colors.Yellow);
							NotConnectedHelp.Visibility = Visibility.Collapsed;
							break;
						case Program.ConnectionState.InLobby:
							statusLabel.Content = Properties.Resources.In_Lobby;
							statusCircle.Fill = new SolidColorBrush(Colors.Yellow);
							NotConnectedHelp.Visibility = Visibility.Collapsed;
							break;
						case Program.ConnectionState.InGame:
							statusLabel.Content = Properties.Resources.Connected;
							statusCircle.Fill = new SolidColorBrush(Colors.Green);
							NotConnectedHelp.Visibility = Visibility.Collapsed;
							break;
					}


					// update the other labels in the stats box
					if (Program.lastFrame != null) // 'mpl_lobby_b2' may change in the future
					{
						// session ID
						sessionIdTextBox.Text = Program.CurrentSparkLink(Program.lastFrame.sessionid);


						// last throw stuff
						LastThrow lt = Program.lastFrame.last_throw;
						if (lt != null)
						{
							string stats = $"Total Speed:\t{lt.total_speed:N2} m/s\n Arm:\t\t{lt.speed_from_arm:N2} m/s\n Wrist:\t\t{lt.speed_from_wrist:N2} m/s\n Movement:\t{lt.speed_from_movement:N2} m/s\n\nTouch Data\n Arm Speed:\t{lt.arm_speed:N2} m/s\n Rots/second:\t{lt.rot_per_sec:N2} r/s\n Pot spd from rot:\t{lt.pot_speed_from_rot:N2} m/s\n\nAlignment Analysis\n Off Axis Spin:\t{lt.off_axis_spin_deg:N1} deg\n Wrist align:\t{lt.wrist_align_to_throw_deg:N1} deg\n Movement align:\t{lt.throw_align_to_movement_deg:N1} deg";
							lastThrowStats.Text = stats;
						}


						StringBuilder blueTextNames = new StringBuilder();
						StringBuilder orangeTextNames = new StringBuilder();
						StringBuilder bluePingsTextPings = new StringBuilder();
						StringBuilder orangePingsTextPings = new StringBuilder();
						StringBuilder blueSpeedsTextSpeeds = new StringBuilder();
						StringBuilder orangeSpeedsTextSpeeds = new StringBuilder();
						StringBuilder[] teamNames =
						{
							new StringBuilder(),
							new StringBuilder(),
							new StringBuilder()
						};
						List<List<int>> pings = new List<List<int>> { new List<int>(), new List<int>() };

						// loop through all the players and set their speed progress bars and pings
						for (int t = 0; t < 3; t++)
						{
							foreach (Player player in Program.lastFrame.teams[t].players)
							{
								switch (t)
								{
									case 0:
										blueTextNames.AppendLine(player.name);
										// bluePingsTextPings.AppendLine($"{player.ping}\t{player.packetlossratio:P1}");
										bluePingsTextPings.AppendLine($"{player.ping}");
										blueSpeedsTextSpeeds.AppendLine(player.velocity.ToVector3().Length().ToString("N1"));
										pings[t].Add(player.ping);
										break;
									case 1:
										orangeTextNames.AppendLine(player.name);
										// orangePingsTextPings.AppendLine($"{player.ping}\t{player.packetlossratio:P1}");
										orangePingsTextPings.AppendLine($"{player.ping}");
										orangeSpeedsTextSpeeds.AppendLine(player.velocity.ToVector3().Length().ToString("N1"));
										pings[t].Add(player.ping);
										break;
								}

								teamNames[t].AppendLine(player.name);
							}
						}

						bluePlayerPingsNames.Text = blueTextNames.ToString();
						bluePlayerPingsPings.Text = bluePingsTextPings.ToString();
						orangePlayerPingsNames.Text = orangeTextNames.ToString();
						orangePlayerPingsPings.Text = orangePingsTextPings.ToString();


						string playerPingsHeader;

						if (Program.CurrentRound.serverScore > 0)
						{
							playerPingsHeader = $"{Properties.Resources.Player_Pings}   {Properties.Resources.Score_} {Program.CurrentRound.smoothedServerScore:N1}";
						}
						// if == -1
						else if (Math.Abs(Program.CurrentRound.serverScore - -1) < .1f)
						{
							playerPingsHeader = $"{Properties.Resources.Player_Pings}     >150";
						}
						// if <= -2
						else if (Program.CurrentRound.serverScore < -1.5f)
						{
							string wrongPlayerCount = "Wrong Player Count";
							playerPingsHeader = $"{Properties.Resources.Player_Pings}     {wrongPlayerCount}";
						}
						else
						{
							playerPingsHeader = $"{Properties.Resources.Player_Pings}   {Properties.Resources.Score_} --";
						}

						playerPingsGroupbox.Header = playerPingsHeader;

						if (blueLogo != Program.CurrentRound.teams[Team.TeamColor.blue].vrmlTeamLogo)
						{
							blueLogo = Program.CurrentRound.teams[Team.TeamColor.blue].vrmlTeamLogo;
							blueTeamLogo.Source = string.IsNullOrEmpty(blueLogo) ? null : new BitmapImage(new Uri(blueLogo));
							blueTeamLogo.ToolTip = Program.CurrentRound.teams[Team.TeamColor.blue].vrmlTeamName;
						}

						if (orangeLogo != Program.CurrentRound.teams[Team.TeamColor.orange].vrmlTeamLogo)
						{
							orangeLogo = Program.CurrentRound.teams[Team.TeamColor.orange].vrmlTeamLogo;
							orangeTeamLogo.Source = string.IsNullOrEmpty(orangeLogo) ? null : new BitmapImage(new Uri(orangeLogo));
							orangeTeamLogo.ToolTip = Program.CurrentRound.teams[Team.TeamColor.orange].vrmlTeamName;
						}


						bluePlayersSpeedsNames.Text = blueTextNames.ToString();
						bluePlayerSpeedsSpeeds.Text = blueSpeedsTextSpeeds.ToString();
						orangePlayersSpeedsNames.Text = orangeTextNames.ToString();
						orangePlayerSpeedsSpeeds.Text = orangeSpeedsTextSpeeds.ToString();


						#region Rejoiner

						// show the button once the player hasn't been getting data for some time
						float secondsUntilRejoiner = 1f;
						if (!Program.InGame &&
						    Program.lastFrame != null &&
						    Program.lastFrame.private_match &&
						    Program.lastFrame.GetAllPlayers(true).Count > 1 && // if we weren't the last
						    DateTime.Compare(Program.lastDataTime.AddSeconds(secondsUntilRejoiner), DateTime.UtcNow) < 0 &&
						    SparkSettings.instance.echoVRIP == "127.0.0.1")
						{
							rejoinButton.Visibility = Visibility.Visible;
						}
						else
						{
							rejoinButton.Visibility = Visibility.Collapsed;
						}

						#endregion
					}

					bool blueReadyVisible = false;
					bool orangeReadyVisible = false;
					bool bluePauseVisible = false;
					bool orangePauseVisible = false;
					bool blueRestartVisible = false;
					bool orangeRestartVisible = false;
					bool bluePauseEnabled = false;
					bool orangePauseEnabled = false;
					string bluePauseText = "Pause";
					string orangePauseText = "Pause";
					if (Program.InGame && Program.lastFrame != null && Program.lastFrame.private_match && Program.lastFrame.client_name != "anonymous")
					{
						if (!DiscordOAuth.Personal || Program.lastFrame.ClientTeam.color == Team.TeamColor.blue)
						{
							blueReadyVisible = true;
							bluePauseVisible = true;
							blueRestartVisible = true;
							bluePauseEnabled = true;
						}

						if (!DiscordOAuth.Personal || Program.lastFrame.ClientTeam.color == Team.TeamColor.orange)
						{
							orangeReadyVisible = true;
							orangePauseVisible = true;
							orangeRestartVisible = true;
							orangePauseEnabled = true;
						}

						if (Program.lastFrame.pause.paused_state == "paused_requested" || Program.lastFrame.pause.paused_state == "paused")
						{
							if (Program.lastFrame.pause.paused_requested_team == "blue")
							{
								bluePauseText = "Unpause";
								orangePauseText = "Unpause";
							}

							if (Program.lastFrame.pause.paused_requested_team == "orange")
							{
								bluePauseText = "Unpause";
								orangePauseText = "Unpause";
							}
						}
					}

					BlueTeamReadyUp.Visibility = blueReadyVisible ? Visibility.Visible : Visibility.Collapsed;
					OrangeTeamReadyUp.Visibility = orangeReadyVisible ? Visibility.Visible : Visibility.Collapsed;
					BlueTeamPause.Visibility = bluePauseVisible ? Visibility.Visible : Visibility.Collapsed;
					OrangeTeamPause.Visibility = orangePauseVisible ? Visibility.Visible : Visibility.Collapsed;
					BlueTeamRestart.Visibility = blueRestartVisible ? Visibility.Visible : Visibility.Collapsed;
					OrangeTeamRestart.Visibility = orangeRestartVisible ? Visibility.Visible : Visibility.Collapsed;
					BlueTeamPause.IsEnabled = bluePauseEnabled;
					OrangeTeamPause.IsEnabled = orangePauseEnabled;
					BlueTeamPause.Content = bluePauseText;
					OrangeTeamPause.Content = orangePauseText;

					if (Program.lastFrame?.InArena == true) // only the arena has a disc
					{
						discSpeedLabel.Text = $"{Program.lastFrame.disc.velocity.ToVector3().Length():N2}";
						// discSpeedLabel.Text = $"{Program.lastFrame.disc.velocity.ToVector3().Length():N2} m/s\t{Program.lastFrame.disc.Position.X:N2}, {Program.lastFrame.disc.Position.Y:N2}, {Program.lastFrame.disc.Position.Z:N2}";
						discSpeedLabel.Foreground = Program.lastFrame.possession[0] switch
						{
							0 => Brushes.CornflowerBlue,
							1 => Brushes.Orange,
							_ => Brushes.White
						};
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


						OrangePoints.Text = Program.lastFrame.orange_points.ToString();
						BluePoints.Text = Program.lastFrame.blue_points.ToString();
						GameClock.Text = Program.lastFrame.game_clock_display[..^3];

						StringBuilder lastJoustsString = new StringBuilder();
						List<EventData> lastJousts = Program.LastJousts.ToList(); // TODO list was modified
						if (lastJousts.Count > 0)
						{
							if (SparkSettings.instance.dashboardJoustTimeOrder == 1)
							{
								lastJousts.Sort((j1, j2) => j2.joustTimeMillis.CompareTo(j1.joustTimeMillis));
							}

							for (int j = lastJousts.Count - 1; j >= 0; j--)
							{
								EventData joust = lastJousts[j];
								lastJoustsString.AppendLine(joust.player.name + "  " + (joust.joustTimeMillis / 1000f).ToString("N2") + " s" + (joust.eventType == EventContainer.EventType.joust_speed ? " N" : ""));
							}
						}

						lastJoustsTextBlock.Text = lastJoustsString.ToString();
					}
					else
					{
						discSpeedLabel.Text = "---";
						discSpeedLabel.Foreground = Brushes.LightGray;
					}


					RefreshDiscordLogin();

					if (SparkSettings.instance.echoVRIP != "127.0.0.1" || SparkSettings.instance.allowSpectateMeOnLocalPC)
					{
						spectateMeButton.Visibility = Visibility.Visible;
					}
					else
					{
						spectateMeButton.Visibility = Visibility.Collapsed;
					}


					hostMatchButton.IsEnabled = Program.lastFrame != null && Program.lastFrame.private_match;

					if (Program.lastFrame != null)
					{
						joinLink.Text = Program.CurrentSparkLink(Program.lastFrame.sessionid);
					}

					// if we're trying to show the window
					if (tryingToShowGameOverlay)
					{
						// if the window is closed
						if (Program.GetWindowIfOpen(typeof(GameOverlay)) == null)
						{
							// if echovr is focused
							if (GetActiveProcessFileName().ProcessName == "echovr")
							{
								Program.ToggleWindow(typeof(GameOverlay));
							}
							else
							{
								ClickableOverlaySubtitle.Text = Properties.Resources.Echo_VR_not_active;
							}
						}
						else
						{
							// close the overlay
							if (GetActiveProcessFileName().ProcessName != "echovr")
							{
								Program.ToggleWindow(typeof(GameOverlay));
							}

							ClickableOverlaySubtitle.Text = Properties.Resources.Active;
						}
					}
					else
					{
						ClickableOverlaySubtitle.Text = Properties.Resources.Not_active;
					}

					DownloadingOverlaysBar.Visibility = OverlaysCustom.downloading ? Visibility.Visible : Visibility.Hidden;
					DownloadingOverlaysText.Visibility = OverlaysCustom.downloading ? Visibility.Visible : Visibility.Hidden;


					if (!Program.running)
					{
						outputUpdateTimer.Stop();
					}
				});
			}
		}

		private void RefreshLastRoundsList()
		{
			LastRoundScoresBox.Children.Clear();

			AccumulatedFrame[] lastMatches = Program.rounds.ToArray();
			if (lastMatches.Length > 0)
			{
				for (int i = lastMatches.Length - 1; i >= 0; i--)
				{
					AccumulatedFrame match = lastMatches[i];
					TextBlock label = new TextBlock()
					{
						Background = (i % 2 != 0)
							? new SolidColorBrush(Color.FromArgb(0xff, 0x2f, 0x2f, 0x2f))
							: new SolidColorBrush(Color.FromArgb(0xff, 0x23, 0x23, 0x23)),
						Padding = new Thickness(5, 5, 5, 5),
					};

					switch (match.finishReason)
					{
						case AccumulatedFrame.FinishReason.not_finished:
							label.Inlines.Add(new Run("not finished"));
							break;
						case AccumulatedFrame.FinishReason.game_time:
							label.Inlines.Add(new Run(match.matchTime.ToLocalTime().ToString("t")));
							break;
						default:
							label.Inlines.Add(new Run($"{match.matchTime.ToLocalTime():t}  {match.finishReason}"));
							break;
					}

					label.Inlines.Add(new Run(match.finishReason == AccumulatedFrame.FinishReason.reset ? $"  {match.endTime}" : ""));
					label.Inlines.Add(new Run($"  {(match.teams[Team.TeamColor.orange].vrmlTeamName != "" ? match.teams[Team.TeamColor.orange].vrmlTeamName : "ORANGE")}: {match.frame.orange_points}") { Foreground = Brushes.Peru });
					label.Inlines.Add(new Run($"  {(match.teams[Team.TeamColor.blue].vrmlTeamName != "" ? match.teams[Team.TeamColor.blue].vrmlTeamName : "BLUE")}: {match.frame.blue_points}") { Foreground = Brushes.CornflowerBlue });
					if (match.frame.total_round_count > 0)
					{
						label.Inlines.Add(match.finishReason == AccumulatedFrame.FinishReason.not_finished ? new Run($"  ROUND: {(match.frame.blue_round_score + match.frame.orange_round_score + 1) / match.frame.total_round_count}") : new Run($"\t  ROUND: {(match.frame.blue_round_score + match.frame.orange_round_score) / match.frame.total_round_count}"));
					}

					LastRoundScoresBox.Children.Add(label);
				}
			}
		}

		private void RefreshLastGoalsList()
		{
			LastGoalsBox.Children.Clear();

			GoalData[] lastGoals = Program.LastGoals.ToArray();
			if (lastGoals.Length > 0)
			{
				for (int i = lastGoals.Length - 1; i >= 0; i--)
				{
					GoalData goal = lastGoals[i];
					TextBlock label = new TextBlock()
					{
						Text = $"{goal.GameClock:N0}s\t  {goal.LastScore.point_amount} pts\t  {goal.LastScore.person_scored}   {goal.LastScore.disc_speed:N1} m/s  {goal.LastScore.distance_thrown:N1} m",
						Background = (i % 2 != 0)
							? new SolidColorBrush(Color.FromArgb(0xff, 0x2f, 0x2f, 0x2f))
							: new SolidColorBrush(Color.FromArgb(0xff, 0x23, 0x23, 0x23)),
						Padding = new Thickness(5, 5, 5, 5),
					};
					LastGoalsBox.Children.Add(label);
				}
			}
		}

		private void RefreshPlayerList(Frame frame)
		{
			if (frame == null) return;

			BlueTeamPlayersBox.Children.Clear();
			for (int i = 0; i < frame.teams[0].players.Count; i++)
			{
				Player player = frame.teams[0].players[i];
				StackPanel panel = new StackPanel
				{
					Orientation = Orientation.Horizontal,
					Background = (i % 2 != 0)
						? new SolidColorBrush(Color.FromArgb(0xff, 0x2f, 0x2f, 0x2f))
						: new SolidColorBrush(Color.FromArgb(0xff, 0x23, 0x23, 0x23))
				};
				panel.Children.Add(new TextBlock()
				{
					Text = player.name,
					Padding = new Thickness(5, 5, 5, 5)
				});
				if (player.name != "anonymous")
				{
					panel.Cursor = Cursors.Hand;

					int i1 = i;
					panel.MouseEnter += (sender, args) => { panel.Background = new SolidColorBrush(Color.FromArgb(0xff, 0x1a, 0x1a, 0x1a)); };
					panel.MouseLeave += (sender, args) =>
					{
						panel.Background = (i1 % 2 != 0)
							? new SolidColorBrush(Color.FromArgb(0xff, 0x2f, 0x2f, 0x2f))
							: new SolidColorBrush(Color.FromArgb(0xff, 0x23, 0x23, 0x23));
					};
					panel.MouseLeftButtonUp += (sender, args) => { ClickedOnPlayer(player.name); };
				}

				BlueTeamPlayersBox.Children.Add(panel);
			}

			OrangeTeamPlayersBox.Children.Clear();
			for (int i = 0; i < frame.teams[1].players.Count; i++)
			{
				Player player = frame.teams[1].players[i];
				StackPanel panel = new StackPanel
				{
					Orientation = Orientation.Horizontal,
					Background = (i % 2 != 0)
						? new SolidColorBrush(Color.FromArgb(0xff, 0x2f, 0x2f, 0x2f))
						: new SolidColorBrush(Color.FromArgb(0xff, 0x20, 0x20, 0x20))
				};
				panel.Children.Add(new TextBlock()
				{
					Text = player.name,
					Padding = new Thickness(5, 5, 5, 5)
				});
				if (player.name != "anonymous")
				{
					panel.Cursor = Cursors.Hand;

					int i1 = i;
					panel.MouseEnter += (sender, args) => { panel.Background = new SolidColorBrush(Color.FromArgb(0xff, 0x1a, 0x1a, 0x1a)); };
					panel.MouseLeave += (sender, args) =>
					{
						panel.Background = (i1 % 2 != 0)
							? new SolidColorBrush(Color.FromArgb(0xff, 0x2f, 0x2f, 0x2f))
							: new SolidColorBrush(Color.FromArgb(0xff, 0x20, 0x20, 0x20));
					};
					panel.MouseLeftButtonUp += (sender, args) => { ClickedOnPlayer(player.name); };
				}

				OrangeTeamPlayersBox.Children.Add(panel);
			}

			SpectatorsPlayersBox.Children.Clear();
			for (int i = 0; i < frame.teams[2].players.Count; i++)
			{
				Player player = frame.teams[2].players[i];
				StackPanel panel = new StackPanel
				{
					Orientation = Orientation.Horizontal,
					Background = (i % 2 != 0)
						? new SolidColorBrush(Color.FromArgb(0xff, 0x2f, 0x2f, 0x2f))
						: new SolidColorBrush(Color.FromArgb(0xff, 0x20, 0x20, 0x20))
				};
				panel.Children.Add(new TextBlock()
				{
					Text = player.name,
					Padding = new Thickness(5, 5, 5, 5)
				});
				if (player.name != "anonymous")
				{
					panel.Cursor = Cursors.Hand;

					int i1 = i;
					panel.MouseEnter += (sender, args) => { panel.Background = new SolidColorBrush(Color.FromArgb(0xff, 0x1a, 0x1a, 0x1a)); };
					panel.MouseLeave += (sender, args) =>
					{
						panel.Background = (i1 % 2 != 0)
							? new SolidColorBrush(Color.FromArgb(0xff, 0x2f, 0x2f, 0x2f))
							: new SolidColorBrush(Color.FromArgb(0xff, 0x20, 0x20, 0x20));
					};
					panel.MouseLeftButtonUp += (sender, args) => { ClickedOnPlayer(player.name); };
				}

				SpectatorsPlayersBox.Children.Add(panel);
			}
		}


		protected override void OnClosing(CancelEventArgs e)
		{
			base.OnClosing(e);

			// if not specifically a exit button press, hide
			if (isExplicitClose == false)
			{
				e.Cancel = true;
				Program.ToggleWindow(typeof(YouSureAboutClosing), null, this);
			}
		}

		private void ClickedOnPlayer(string playerName)
		{
			Process.Start(new ProcessStartInfo("https://metrics.ignitevr.gg/stats/" + playerName)
			{
				UseShellExecute = true
			});
		}


		/// <summary>
		/// Enables or disables parts of the UI to match the current access code
		/// </summary>
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
			}

			lastDiscordUsername = username;
		}

		/// <summary>
		/// Regenerates the options in the dropdown for access codes.
		/// </summary>
		private void RefreshAccessCodeList()
		{
			accessCodeDropdownListenerActive = false;
			string accessCodeLocalized = DiscordOAuth.Personal ? Properties.Resources.Personal : DiscordOAuth.AccessCode.username;
			if (DiscordOAuth.availableAccessCodes.Count < 2)
			{
				accessCodeLabel.Text = Properties.Resources.Mode + accessCodeLocalized;
			}
			else
			{
				accessCodeLabel.Text = Properties.Resources.Mode;
			}


			AccessCodesComboboxLiveWindow.Items.Clear();
			foreach (DiscordOAuth.AccessCodeKey code in DiscordOAuth.availableAccessCodes)
			{
				AccessCodesComboboxLiveWindow.Items.Add(code.username);
			}

			// if not logged in with discord
			if (!AccessCodesComboboxLiveWindow.Items.Contains("Personal")) AccessCodesComboboxLiveWindow.Items.Add("Personal");

			// set the dropdown value
			AccessCodesComboboxLiveWindow.SelectedIndex = DiscordOAuth.GetAccessCodeIndexByHash(SparkSettings.instance.accessCode);

			// show or hide the dropdown entirely
			AccessCodesComboboxLiveWindow.Visibility = DiscordOAuth.availableAccessCodes.Count < 2 ? Visibility.Collapsed : Visibility.Visible;

			casterToolsBox.Visibility = !DiscordOAuth.Personal ? Visibility.Visible : Visibility.Collapsed;
			PasteLinkInLiveButton.Visibility = DiscordOAuth.AccessCode?.series_name.Contains("vrml") ?? false ? Visibility.Visible : Visibility.Collapsed;
			MatchSetupButton.Visibility = DiscordOAuth.AccessCode?.series_name.Contains("vrml") ?? false ? Visibility.Visible : Visibility.Collapsed;

			accessCodeDropdownListenerActive = true;
		}


		private async Task CheckForAppUpdate()
		{
#if WINDOWS_STORE_RELEASE
			return;
#endif
			try
			{
				string respString = await FetchUtils.GetRequestAsync("https://api.github.com/repos/NtsFranz/Spark/releases", null);

				List<VersionJson> versions = JsonConvert.DeserializeObject<List<VersionJson>>(respString);

				// find the appropriate version
				VersionJson chosenVersion = versions?.First(v => !v.prerelease || v.prerelease == SparkSettings.instance.betaUpdates);

				// get the details from the version
				if (chosenVersion != null)
				{
					string downloadUrl = chosenVersion.assets.First(url => url.browser_download_url.EndsWith(".msi")).browser_download_url;
					string version = chosenVersion.tag_name.TrimStart('v');
					string changelog = chosenVersion.body;

					Version remoteVersion = new Version(version);

					// if we need a new version
					if (remoteVersion > Program.AppVersion())
					{
						updateFilename = downloadUrl;
						updateButton.Visibility = Visibility.Visible;

						MessageBox box = new MessageBox(changelog, Properties.Resources.Update_Available);
						box.Topmost = true;
						box.Show();
					}
					else
					{
						updateButton.Visibility = Visibility.Collapsed;
					}
				}
			}
			catch (Exception e)
			{
				LogRow(LogType.Error, $"Couldn't check for update.\n{e}");
			}
		}

		private async Task GetServerLocation(string ip)
		{
			if (!string.IsNullOrEmpty(ip))
			{
				try
				{
					// string resp = await FetchUtils.client.GetStringAsync(new Uri($"{Program.APIURL}/ip_geolocation/{ip}"));
					string resp = await FetchUtils.client.GetStringAsync(new Uri($"{Program.APIURL}/ip_geolocation/{ip}"));
					Program.CurrentRound.serverLocationResponse = resp;
					Dictionary<string, dynamic> obj = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(resp);
					if (obj == null) return;
					string loc = (string)obj["ip-api"]["city"] + ", " + (string)obj["ip-api"]["regionName"];

					// if an aws server, use ipdata.co instead
					if ((string)obj["ip-api"]["org"] == "AWS EC2 (us-east-1)")
					{
						loc = (string)obj["ipdata"]["city"] + ", " + (string)obj["ipdata"]["region"];
					}

					Program.CurrentRound.serverLocation = loc;
					serverLocationLabel.Content = Properties.Resources.Server_Location_ + "\n" + loc;

					serverLocationLabel.ToolTip = $"{obj["ip-api"]["query"]}\n{obj["ip-api"]["org"]}\n{obj["ip-api"]["as"]}";

					try
					{
						Program.IPGeolocated?.Invoke(resp);
					}
					catch (Exception)
					{
						LogRow(LogType.Error, "Error processing event for IP Geolocation");
					}

					if (SparkSettings.instance.serverLocationTTS)
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
				//	(SparkSettings.instance.outputGameStateEvents && l.Contains("Entered state:")) ||
				//	(SparkSettings.instance.outputScoreEvents && l.Contains("scored")) ||
				//	(SparkSettings.instance.outputStunEvents && l.Contains("just stunned")) ||
				//	(SparkSettings.instance.outputDiscThrownEvents && l.Contains("threw the disk")) ||
				//	(SparkSettings.instance.outputDiscCaughtEvents && l.Contains("caught the disk")) ||
				//	(SparkSettings.instance.outputDiscStolenEvents && l.Contains("stole the disk")) ||
				//	(SparkSettings.instance.outputSaveEvents && l.Contains("save"))
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
				//	(SparkSettings.instance.outputGameStateEvents && l.Contains("Entered state:")) ||
				//	(SparkSettings.instance.outputScoreEvents && l.Contains("scored")) ||
				//	(SparkSettings.instance.outputStunEvents && l.Contains("just stunned")) ||
				//	(SparkSettings.instance.outputDiscThrownEvents && l.Contains("threw the disk")) ||
				//	(SparkSettings.instance.outputDiscCaughtEvents && l.Contains("caught the disk")) ||
				//	(SparkSettings.instance.outputDiscStolenEvents && l.Contains("stole the disk")) ||
				//	(SparkSettings.instance.outputSaveEvents && l.Contains("save"))
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

		private void updateButton_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				WebClient webClient = new WebClient();
				webClient.DownloadFileCompleted += Completed;
				webClient.DownloadProgressChanged += ProgressChanged;
				webClient.DownloadFileAsync(new Uri(updateFilename), Path.GetTempPath() + Path.GetFileName(updateFilename));
			}
			catch (Exception)
			{
				new MessageBox(Properties.Resources.Something_broke_while_trying_to_download_update_, Properties.Resources.Error).Show();
			}
		}

		private void ProgressChanged(object sender, DownloadProgressChangedEventArgs e)
		{
			updateProgressBar.Visibility = Visibility.Visible;
			updateProgressBar.Value = e.ProgressPercentage;
		}

		private void Completed(object sender, AsyncCompletedEventArgs e)
		{
			updateProgressBar.Visibility = Visibility.Collapsed;

			try
			{
				// Install the update
				Process.Start(new ProcessStartInfo
				{
					FileName = Path.Combine(Path.GetTempPath(), Path.GetFileName(updateFilename) ?? throw new InvalidOperationException()),
					UseShellExecute = true
				});

				Program.Quit();
			}
			catch (Exception)
			{
				new MessageBox(Properties.Resources.Something_broke_while_trying_to_launch_update_installer, Properties.Resources.Error).Show();
			}
		}

		private void RejoinClicked(object sender, RoutedEventArgs e)
		{
			if (Program.lastFrame == null)
			{
				LogRow(LogType.Error, "Last frame null when trying to use rejoiner.");
				return;
			}

			Program.KillEchoVR();

			// join in spectator if we were in spectator before
			Team team = Program.lastFrame.GetTeam(Program.lastFrame.client_name);
			if (team != null && team.color == Team.TeamColor.spectator)
			{
				Program.StartEchoVR(Program.JoinType.Spectator, session_id: Program.lastFrame.sessionid);
			}

			Program.StartEchoVR(Program.JoinType.Player, session_id: Program.lastFrame.sessionid);
		}

		private void RestartAsSpectatorClick(object sender, RoutedEventArgs e)
		{
			Program.KillEchoVR();
			if (Program.lastFrame != null)
			{
				Program.StartEchoVR(Program.JoinType.Spectator, session_id: Program.lastFrame.sessionid);
			}
		}

		private void showEventLogFileButton_Click(object sender, RoutedEventArgs e)
		{
			string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Spark", logFolder);
			if (Directory.Exists(folder))
			{
				Process.Start(new ProcessStartInfo
				{
					FileName = folder,
					UseShellExecute = true
				});
			}
			else
			{
				Directory.CreateDirectory(folder);
			}
		}

		private void OpenSpeedometer(object sender, RoutedEventArgs e)
		{
			Program.ToggleWindow(typeof(Speedometer), ownedBy: this);
		}

		private void enableAPIButton_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				JToken settings = EchoVRSettingsManager.ReadEchoVRSettings();
				if (settings != null)
				{
					new MessageBox(Properties.Resources.Enabled_API_access_in_the_game_settings__CLOSE_ECHOVR_BEFORE_PRESSING_OK_, callback: () =>
					{
						settings["game"]!["EnableAPIAccess"] = true;
						EchoVRSettingsManager.WriteEchoVRSettings(settings);
					}).Show();

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

		private void StartSpectatorStreamClick(object sender, RoutedEventArgs e)
		{
			Program.StartEchoVR(Program.JoinType.Spectator, noovr: SparkSettings.instance.spectatorStreamNoOVR, combat: false);
		}

		private void CombatSpectatorstreamClick(object sender, RoutedEventArgs e)
		{
			Program.StartEchoVR(Program.JoinType.Spectator, noovr: SparkSettings.instance.spectatorStreamNoOVR, combat: true);
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
			if (e.Source is not TabControl) return;

			// if switched to atlas tab
			if (Equals(((TabControl)sender).SelectedItem, LinksTab))
			{
				RefreshCurrentLink();
				GetAtlasMatches();
			}
			// switched to event log tab
			else if (Equals(((TabControl)sender).SelectedItem, EventLogTab))
			{
				mainOutputTextBox.ScrollToEnd();
			}

			if (SpeakerSystemProcess != null)
			{
				if (!Equals(((TabControl)sender).SelectedItem, SpeakerSystemTab))
				{
					ShowWindow(unityHWND, 0);
				}
				else
				{
					ShowWindow(unityHWND, 1);
				}
			}

			e.Handled = true;
		}

		private void SpectateMeClicked(object sender, RoutedEventArgs e)
		{
			(string labelText, string subtitleText) = Program.spectateMeController.ToggleSpectateMe();

			Program.liveWindow.spectateMeLabel.Content = labelText;
			Program.liveWindow.spectateMeSubtitle.Text = subtitleText;
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
			try
			{
				Clipboard.SetText(link);
				Task.Run(ShowCopiedText);
			}
			catch (COMException ex)
			{
				LogRow(LogType.Error, "Failed to copy text.\n" + ex);
			}
		}

		private async Task ShowCopiedText()
		{
			Dispatcher.Invoke(() => { copySessionIdButton.Content = Properties.Resources.Copied_; });
			await Task.Delay(3000);

			Dispatcher.Invoke(() => { copySessionIdButton.Content = Properties.Resources.Copy; });
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
			Program.netMQEvents.CloseApp();
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
				WindowInteropHelper helper = new WindowInteropHelper(this);
				HwndSource hwndSource = HwndSource.FromHwnd(helper.EnsureHandle());
				if (hwndSource != null) unityHandle = hwndSource.Handle;
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

					WindowInteropHelper helper = new WindowInteropHelper(this);
					HwndSource hwndSource = HwndSource.FromHwnd(helper.EnsureHandle());
					if (hwndSource != null)
					{
						IntPtr unityHandle = hwndSource.Handle;
						SpeakerSystemProcess.StartInfo.FileName = "C:\\Program Files (x86)\\Echo Speaker System\\Echo Speaker System.exe";
						SpeakerSystemProcess.StartInfo.Arguments = "ignitebot -parentHWND " + unityHandle.ToInt32() + " " + Environment.CommandLine;
						SpeakerSystemProcess.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
						SpeakerSystemProcess.StartInfo.CreateNoWindow = true;

						SpeakerSystemProcess.Start();
						SpeakerSystemProcess.WaitForInputIdle();
						SpeakerSystemStart(unityHandle);
					}
				}
				catch (Exception)
				{
					startStopEchoSpeakerSystem.Content = Properties.Resources.Start_Echo_Speaker_System;
					startStopEchoSpeakerSystem.IsEnabled = true;
				}
			}
			else
			{
				speakerSystemInstallLabel.Visibility = Visibility.Hidden;
				Program.netMQEvents.CloseApp();
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

		private void HostMatchClicked(object sender, RoutedEventArgs e)
		{
			if (string.IsNullOrEmpty(Program.hostedAtlasSessionId))
			{
				Program.atlasHostingThread = new Thread(AtlasHostingThread);
				Program.atlasHostingThread.IsBackground = true;
				Program.atlasHostingThread.Start();
				hostingMatchCheckbox.IsChecked = true;
				hostingMatchLabel.Content = Properties.Resources.Stop_Hosting;
			}
			else
			{
				Program.hostedAtlasSessionId = "";
				hostingMatchCheckbox.IsChecked = false;
				hostingMatchLabel.Content = Properties.Resources.Host_Match;
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

			[Obsolete("Use matchid instead")] public string session_id;

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
					LogRow(LogType.Error, $"Can't serialize atlas match data.\n{e.Message}\n{e.StackTrace}");
					return new Dictionary<string, object>
					{
						{ "none", 0 }
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

			public List<string> AllPlayers
			{
				get
				{
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
							Content = match.is_protected ? (match.visible_to_casters ? Properties.Resources.Casters_Only : Properties.Resources.Private) : Properties.Resources.Public
						});

						byte buttonColor = 70;
						Button copyLinkButton = new Button
						{
							Content = Properties.Resources.Copy_Spark_Link,
							Margin = new Thickness(50, 0, 0, 0),
							Padding = new Thickness(10, 0, 10, 0),
							Background = new SolidColorBrush(Color.FromRgb(buttonColor, buttonColor, buttonColor)),
						};
						copyLinkButton.Click += (_, _) => { Clipboard.SetText(Program.CurrentSparkLink(match.matchid)); };
						header.Children.Add(copyLinkButton);
						Button joinButton = new Button
						{
							Content = Properties.Resources.Join,
							Margin = new Thickness(20, 0, 0, 0),
							Padding = new Thickness(10, 0, 10, 0),
							Background = new SolidColorBrush(Color.FromRgb(buttonColor, buttonColor, buttonColor)),
						};
						joinButton.Click += (_, _) =>
						{
							Process.Start(new ProcessStartInfo
							{
								FileName = "spark://c/" + match.matchid,
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

						Image blueLogo2 = new Image
						{
							Width = 100,
							Height = 100
						};
						if (match.blue_team_info?.team_logo != string.Empty)
						{
							blueLogo2.Source = string.IsNullOrEmpty(match.blue_team_info?.team_logo) ? null : (new BitmapImage(new Uri(match.blue_team_info.team_logo)));
						}

						StackPanel blueLogoBox = new StackPanel
						{
							Orientation = Orientation.Vertical,
							Margin = new Thickness(5, 10, 5, 10)
						};
						blueLogoBox.SetValue(Grid.ColumnProperty, 0);
						blueLogoBox.Children.Add(blueLogo2);
						blueLogoBox.Children.Add(new Label
						{
							Content = match.blue_team_info?.team_name,
							HorizontalAlignment = HorizontalAlignment.Center
						});


						Image orangeLogo2 = new Image
						{
							Width = 100,
							Height = 100
						};
						if (match.orange_team_info?.team_logo != string.Empty)
						{
							orangeLogo2.Source = string.IsNullOrEmpty(match.orange_team_info?.team_logo) ? null : (new BitmapImage(new Uri(match.orange_team_info.team_logo)));
						}

						StackPanel orangeLogoBox = new StackPanel
						{
							Orientation = Orientation.Vertical,
							Margin = new Thickness(5, 10, 5, 10)
						};
						orangeLogoBox.SetValue(Grid.ColumnProperty, 3);
						orangeLogoBox.Children.Add(orangeLogo2);
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
						// Label sessionIdTextBox = new Label
						// {
						// 	Content = match.matchid
						// };
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
				LogRow(LogType.Error, $"Error showing matches in UI\n{e}");
			}
		}

		private void AtlasHostingThread()
		{
			const string hostURL = Program.APIURL + "/host_match";
			const string unhostURL = Program.APIURL + "/unhost_match";

			// TODO show error message instead of just quitting
			if (Program.lastFrame == null || Program.lastFrame.teams == null) return;

			Program.hostedAtlasSessionId = Program.lastFrame.sessionid;

			AtlasMatch match = new AtlasMatch
			{
				matchid = Program.lastFrame.sessionid,
				blue_team = Program.lastFrame.teams[0].player_names.ToArray(),
				orange_team = Program.lastFrame.teams[1].player_names.ToArray(),
				is_protected = (SparkSettings.instance.atlasHostingVisibility > 0),
				visible_to_casters = (SparkSettings.instance.atlasHostingVisibility == 1),
				server_location = Program.CurrentRound.serverLocation,
				server_score = Program.CurrentRound.serverScore,
				private_match = Program.lastFrame.private_match,
				username = Program.lastFrame.client_name,
				whitelist = Program.atlasWhitelist.AllPlayers.ToArray(),
			};
			bool firstHost = true;

			while (Program.running &&
			       Program.InGame &&
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
					match.is_protected != (SparkSettings.instance.atlasHostingVisibility > 0) ||
					// match.visible_to_casters != (SparkSettings.instance.atlasHostingVisibility == 1) ||
					match.whitelist.Length != Program.atlasWhitelist.AllPlayers.Count;

				if (diff)
				{
					// actually update values
					match.blue_team = Program.lastFrame.teams[0].player_names.ToArray();
					match.orange_team = Program.lastFrame.teams[1].player_names.ToArray();
					match.blue_points = Program.lastFrame.teams[0].stats != null ? Program.lastFrame.teams[0].stats.points : 0;
					match.orange_points = Program.lastFrame.teams[1].stats != null ? Program.lastFrame.teams[1].stats.points : 0;
					match.is_protected = (SparkSettings.instance.atlasHostingVisibility > 0);
					// match.visible_to_casters = (SparkSettings.instance.atlasHostingVisibility == 1);
					match.server_score = Program.CurrentRound.serverScore;
					match.username = Program.lastFrame.client_name;
					match.whitelist = Program.atlasWhitelist.AllPlayers.ToArray();
					match.slots = Program.lastFrame.GetAllPlayers().Count;

					string data = JsonConvert.SerializeObject(match.ToDict());
					firstHost = false;

					// post new data, then fetch the updated list
					FetchUtils.PostRequestCallback(
						hostURL,
						new Dictionary<string, string> { { "x-api-key", DiscordOAuth.igniteUploadKey } },
						data,
						_ => { GetAtlasMatches(); });
				}

				Thread.Sleep(100);
			}

			// post new data, then fetch the updated list
			string matchInfo = JsonConvert.SerializeObject(match.ToDict());
			FetchUtils.PostRequestCallback(
				unhostURL,
				new Dictionary<string, string> { { "x-api-key", DiscordOAuth.igniteUploadKey } },
				matchInfo,
				_ =>
				{
					Program.hostedAtlasSessionId = string.Empty;
					Dispatcher.Invoke(() =>
					{
						hostingMatchCheckbox.IsChecked = false;
						hostingMatchLabel.Content = Properties.Resources.Host_Match;
					});
					Thread.Sleep(10);
					GetAtlasMatches();
				});
		}

		private void GetAtlasMatches()
		{
			string matchesAPIURL = $"{Program.APIURL}/hosted_matches/{(SparkSettings.instance.client_name == string.Empty ? "_" : SparkSettings.instance.client_name)}";
			FetchUtils.GetRequestCallback(
				matchesAPIURL,
				new Dictionary<string, string>()
				{
					{ "x-api-key", DiscordOAuth.igniteUploadKey },
					{ "access_code", DiscordOAuth.AccessCode.series_name }
				},
				responseJSON =>
				{
					try
					{
						AtlasMatchResponse igniteAtlasResponse = JsonConvert.DeserializeObject<AtlasMatchResponse>(responseJSON);
						if (igniteAtlasResponse != null) UpdateUIWithAtlasMatches(igniteAtlasResponse.matches);
					}
					catch (Exception e)
					{
						LogRow(LogType.Error, $"Can't parse Atlas matches response\n{e}");
					}
				}
			);
		}

		private void RefreshMatchesClicked(object sender, RoutedEventArgs e)
		{
			GetAtlasMatches();
		}

		private void RefreshCurrentLink()
		{
			if (Program.lastFrame != null)
			{
				joinLink.Text = Program.CurrentSparkLink(Program.lastFrame.sessionid);
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
				LogRow(LogType.Error, ex.ToString());
			}
		}

		private void WhitelistButtonClicked(object sender, RoutedEventArgs e)
		{
			Program.ToggleWindow(typeof(AtlasWhitelistWindow), "Atlas Whitelist", this);
		}

		public int LinkType
		{
			get => SparkSettings.instance.atlasLinkStyle;
			set
			{
				SparkSettings.instance.atlasLinkStyle = value;
				RefreshCurrentLink();
			}
		}

		#endregion

		private void DashboardItem1Changed(object sender, SelectionChangedEventArgs e)
		{
			if (!initialized) return;
			int index = ((ComboBox)sender).SelectedIndex;
			SetDashboardItem1Visibility(index);
		}

		private void SetDashboardItem1Visibility(int index)
		{
			switch (index)
			{
				case 0:
					playerSpeedsBox.Visibility = Visibility.Collapsed;
					lastThrowStats.Visibility = Visibility.Visible;
					break;
				case 1:
					playerSpeedsBox.Visibility = Visibility.Visible;
					lastThrowStats.Visibility = Visibility.Collapsed;
					break;
			}
		}

		private void chooseServerRegion_Click(object sender, RoutedEventArgs e)
		{
			Program.ToggleWindow(typeof(CreateServer), ownedBy: this);
		}

		private void showOverlay_Click(object sender, RoutedEventArgs e)
		{
			tryingToShowGameOverlay = !tryingToShowGameOverlay;

			// close the overlay if it's open
			if (Program.GetWindowIfOpen(typeof(GameOverlay)) != null)
			{
				Program.ToggleWindow(typeof(GameOverlay));
			}
		}


		private void AccessCodeChangedLiveWindow(object sender, SelectionChangedEventArgs e)
		{
			if (accessCodeDropdownListenerActive)
			{
				string username = AccessCodesComboboxLiveWindow.SelectedValue.ToString();
				DiscordOAuth.SetAccessCodeByUsername(username);
			}
		}


		private void FindAllQuests(object sender, RoutedEventArgs e)
		{
			Program.ToggleWindow(typeof(QuestIPs));
		}

		private void OpenOverlays(object sender, RoutedEventArgs e)
		{
			OpenWebpage("http://localhost:6724/");
		}

		private static void OpenWebpage(string url)
		{
			try
			{
				Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
			}
			catch (Exception ex)
			{
				LogRow(LogType.Error, ex.ToString());
			}
		}

		private async void PasteLinkInLive(object sender, RoutedEventArgs e)
		{
			if (Program.lastFrame == null) return;
			try
			{
				Process.Start(new ProcessStartInfo("discord://discordapp.com/channels/776209623857889361/794763716645355560") { UseShellExecute = true });
				Clipboard.SetText(Program.CurrentSparkLink(Program.lastFrame.sessionid));
				await Task.Delay(1000);
				Keyboard.SendKey(Keyboard.DirectXKeyStrokes.DIK_LCONTROL, false, Keyboard.InputType.Keyboard);
				await Task.Delay(10);
				Keyboard.SendKey(Keyboard.DirectXKeyStrokes.DIK_V, false, Keyboard.InputType.Keyboard);
				await Task.Delay(10);
				Keyboard.SendKey(Keyboard.DirectXKeyStrokes.DIK_V, true, Keyboard.InputType.Keyboard);
				await Task.Delay(10);
				Keyboard.SendKey(Keyboard.DirectXKeyStrokes.DIK_LCONTROL, true, Keyboard.InputType.Keyboard);
			}
			catch (Exception ex)
			{
				LogRow(LogType.Error, ex.ToString());
			}
		}

		private void DefaultMatchSetupClick(object sender, RoutedEventArgs e)
		{
			OpenWebpage("http://localhost:6724/overlays/match_setup");
		}

		private void MatchSetupClick(object sender, RoutedEventArgs e)
		{
			OpenWebpage("http://localhost:6724/" + DiscordOAuth.AccessCode.series_name.Split('_')[0] + "/match_setup");
		}

		private void LeagueOverlaysClick(object sender, RoutedEventArgs e)
		{
			OpenWebpage("http://localhost:6724/" + DiscordOAuth.AccessCode.series_name.Split('_')[0]);
		}

		private void ServerLocationButtonClicked(object sender, RoutedEventArgs e)
		{
			tabControl.SelectedItem = ServerInfoTab;
		}

		private void TabletStatsUploadClick(object sender, RoutedEventArgs e)
		{
			List<TabletStats> stats = Program.FindTabletStats();

			if (stats != null)
			{
				new UploadTabletStatsMenu(stats) { Owner = this }.Show();
			}
		}

		private void ACIIECHO_Click(object sender, RoutedEventArgs e)
		{
			Task.Run(async () =>
			{
				// try
				// {
				// 	string sparkFolder = Path.GetDirectoryName(SparkSettings.instance.sparkExeLocation) ?? "";
				// 	string exePath = Path.Combine(sparkFolder, "resources", "asciiecho.exe");
				// 	
				// 	//Declare and instantiate a new process component.
				// 	Process process = new Process();
				// 	process.StartInfo.UseShellExecute = false;
				// 	process.StartInfo.RedirectStandardOutput = true;
				// 	process.StartInfo.RedirectStandardError = true;
				// 	process.StartInfo.RedirectStandardInput = true; // Is a MUST!
				// 	process.EnableRaisingEvents = true;
				// 	process.StartInfo.FileName = "cmd.exe";
				// 	process.StartInfo.Arguments = "C:\\Users\\Anton\\Desktop\\test.bat";
				// 	process.StartInfo.CreateNoWindow = true;
				// 	process.OutputDataReceived += (s, e) =>
				// 	{
				// 		Debug.WriteLine(e.Data);
				// 	};
				// 	process.ErrorDataReceived += (s, e) =>
				// 	{
				// 		Debug.WriteLine(e.Data);
				// 	};
				// 	process.Start();
				// 	process.BeginOutputReadLine();
				// 	process.BeginErrorReadLine();
				// 	await process.WaitForExitAsync();
				// }
				// catch (Exception ex)
				// {
				// 	Error(ex.ToString());
				// }

				try
				{
					string sparkFolder = Path.GetDirectoryName(SparkSettings.instance.sparkExeLocation) ?? "";
					string exePath = Path.Combine(sparkFolder, "resources", "asciiecho.exe");

					Process p = Process.Start(new ProcessStartInfo
					{
						FileName = exePath,
						UseShellExecute = true,
						WindowStyle = ProcessWindowStyle.Maximized,
					});


					//if (p != null)
					//{
					//	for (int i = 0; i < 50; i++)
					//	{
					//		await Task.Delay(100);
					//		// Point relativePoint = speakerSystemPanel.TransformToAncestor(this).Transform(new Point(0, 0));
					//		// MoveWindow(p.MainWindowHandle, (int)relativePoint.X, (int)relativePoint.Y, (int)speakerSystemPanel.ActualWidth, (int)speakerSystemPanel.ActualHeight, true);
					//		MoveWindow(p.MainWindowHandle, 200, 200, 1024, 768, true);
					//	}
					//}
				}
				catch (Exception ex)
				{
					Error(ex.ToString());
				}
			});
		}

		private void BlueTeamPauseClick(object sender, RoutedEventArgs e)
		{
			Dictionary<string, object> data = new Dictionary<string, object>()
			{
				{ "team_idx", 0 }
			};
			FetchUtils.PostRequestCallback($"http://{Program.echoVRIP}:{Program.echoVRPort}/set_pause", null, JsonConvert.SerializeObject(data), null);
		}

		private void OrangeTeamPauseClick(object sender, RoutedEventArgs e)
		{
			Dictionary<string, object> data = new Dictionary<string, object>()
			{
				{ "team_idx", 1 }
			};
			FetchUtils.PostRequestCallback($"http://{Program.echoVRIP}:{Program.echoVRPort}/set_pause", null, JsonConvert.SerializeObject(data), null);
		}

		private void BlueTeamReadyClick(object sender, RoutedEventArgs e)
		{
			Dictionary<string, object> data = new Dictionary<string, object>()
			{
				{ "team_idx", 0 }
			};
			FetchUtils.PostRequestCallback($"http://{Program.echoVRIP}:{Program.echoVRPort}/set_ready", null, JsonConvert.SerializeObject(data), null);
		}

		private void OrangeTeamReadyClick(object sender, RoutedEventArgs e)
		{
			Dictionary<string, object> data = new Dictionary<string, object>()
			{
				{ "team_idx", 1 }
			};
			FetchUtils.PostRequestCallback($"http://{Program.echoVRIP}:{Program.echoVRPort}/set_ready", null, JsonConvert.SerializeObject(data), null);
		}

		private void BlueTeamRestartClick(object sender, RoutedEventArgs e)
		{
			Dictionary<string, object> data = new Dictionary<string, object>()
			{
				{ "team_idx", 0 }
			};
			FetchUtils.PostRequestCallback($"http://{Program.echoVRIP}:{Program.echoVRPort}/restart_request", null, JsonConvert.SerializeObject(data), null);
		}

		private void OrangeTeamRestartClick(object sender, RoutedEventArgs e)
		{
			Dictionary<string, object> data = new Dictionary<string, object>()
			{
				{ "team_idx", 1 }
			};
			FetchUtils.PostRequestCallback($"http://{Program.echoVRIP}:{Program.echoVRPort}/restart_request", null, JsonConvert.SerializeObject(data), null);
		}

		private void OpenLocalDatabaseBrowser(object sender, RoutedEventArgs e)
		{
			Process.Start(new ProcessStartInfo
			{
				FileName = "http://localhost:6724/local_database",
				UseShellExecute = true
			});
		}

		private async void ReplayViewer_Click(object sender, RoutedEventArgs e)
		{
			await LaunchInstallReplayViewer(false);
		}

		private async Task LaunchInstallReplayViewer(bool vrMode)
		{
			string versionFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
				"Replay Viewer", "version.txt");

			VersionJson remoteVersion = await GetGitHubVersion("robidasdavid", "Demo-Viewer");
			if (remoteVersion == null)
			{
				Error("Failed to get Replay Viewer version from GitHub.");
				return;
			}

			if (File.Exists(versionFile))
			{
				string localVersion = await File.ReadAllTextAsync(versionFile);
				if (localVersion != remoteVersion.tag_name)
				{
					await InstallReplayViewer(versionFile, remoteVersion);
				}
			}
			else
			{
				await InstallReplayViewer(versionFile, remoteVersion);
			}

			Process.Start(new ProcessStartInfo
			{
				FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Replay Viewer", "Replay Viewer.exe"),
				Arguments = vrMode ? " -useVR" : "",
				UseShellExecute = true
			});
		}

		private async Task InstallReplayViewer(string versionFile, VersionJson remoteVersion)
		{
			ReplayViewerProgressBar.Visibility = Visibility.Visible;
			string zipUrl = remoteVersion.assets.First(url => url.browser_download_url.EndsWith("zip")).browser_download_url;
			HttpResponseMessage response = await FetchUtils.client.GetAsync(zipUrl);
			string fileName = Path.Combine(Path.GetTempPath(), "replay_viewer.zip");
			await using (FileStream fs = new FileStream(fileName, FileMode.Create))
			{
				await response.Content.CopyToAsync(fs);
			}

			string replayViewerFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Replay Viewer");
			if (!Directory.Exists(replayViewerFolder))
			{
				Directory.CreateDirectory(replayViewerFolder);
			}

			await Task.Run(() => Directory.Delete(replayViewerFolder, true));
			await Task.Run(() => ZipFile.ExtractToDirectory(fileName, replayViewerFolder));
			await File.WriteAllTextAsync(versionFile, remoteVersion.tag_name);
			ReplayViewerProgressBar.Visibility = Visibility.Collapsed;
		}

		private static async Task<VersionJson> GetGitHubVersion(string authorName, string repoName)
		{
			try
			{
				string resp = await FetchUtils.client.GetStringAsync($"https://api.github.com/repos/{authorName}/{repoName}/releases/latest");
				return JsonConvert.DeserializeObject<VersionJson>(resp);
			}
			catch (Exception e)
			{
				LogRow(LogType.Error, e.Message);
				return null;
			}
		}

		private async void ReplayViewerVR_Click(object sender, RoutedEventArgs e)
		{
			await LaunchInstallReplayViewer(true);
		}
	}
}