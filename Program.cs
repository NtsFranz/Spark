using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Spark.Properties;
using Microsoft.Win32;
using Newtonsoft.Json;
using static Logger;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Management;
using EchoVRAPI;
using Fleck;
using NetMQ;
using Newtonsoft.Json.Linq;

//using System.Windows.Forms;

namespace Spark
{
	/// <summary>
	/// Main
	/// </summary>
	internal class Program
	{
		/// <summary>
		/// Set this to false to finish up and close
		/// </summary>
		public static bool running = true;

		public static bool inGame;
		public static bool connectedToGame;
		public static bool apiSettingDisabled;
		private static bool wasInGame;

		public const string APIURL = "https://ignitevr.gg/cgi-bin/EchoStats.cgi/";
		// public const string APIURL = "http://127.0.0.1:5005/";
		public const string API_URL_2 = "https://api.ignitevr.workers.dev/";
		// public const string API_URL_2 = "http://127.0.0.1:5000/";
		public const string WRITE_API_URL = "http://127.0.0.1:6723/";


		public static readonly HttpClient client = new HttpClient();

		// public static string currentAccessCodeUsername = "";
		public static string InstalledSpeakerSystemVersion = "";
		public static bool IsSpeakerSystemUpdateAvailable;

		public static MatchData matchData;
		static MatchData lastMatchData;

		/// <summary>
		/// Contains the last state so that we can do a diff to determine state changes
		/// This acts like a set of flags.
		/// </summary>
		public static Frame lastFrame;

		public static Frame lastLastFrame;
		public static Frame lastLastLastFrame;

		private static int lastFrameSumOfStats;
		private static Frame lastValidStatsFrame;
		private static int lastValidSumOfStatsAge = 0;

		public static ConcurrentQueue<GoalData> lastGoals = new ConcurrentQueue<GoalData>();
		public static ConcurrentQueue<MatchData> lastMatches = new ConcurrentQueue<MatchData>();
		public static ConcurrentQueue<EventData> lastJousts = new ConcurrentQueue<EventData>();


		private class UserAtTime
		{
			public float gameClock;
			public Player player;
		}

		// { [stunner, stunnee], [stunner, stunnee] }
		static List<UserAtTime[]> stunningMatchedPairs = new List<UserAtTime[]>();
		private const float stunMatchingTimeout = 4f;

		public static string lastDateTimeString;
		public static string lastJSON;
		public static string lastBonesJSON;
		public static ulong fetchFrameIndex = 0;


		private static readonly object lastJSONLock = new object();
		private static readonly object lastFrameLock = new object();
		public static readonly object logOutputWriteLock = new object();
		public static readonly object gameStateLock = new object();

		public static DateTime lastDataTime;
		static float minTillAutorestart = 3;

		static bool wasThrown;
		static int lastThrowPlayerId = -1;
		static bool inPostMatch = false;
		public static float serverScoreSmoothingFactor = .95f;


		/// <summary>
		/// Not actually Hz. 1/Hz.
		/// </summary>
		public static float StatsHz => statsDeltaTimes[SparkSettings.instance.lowFrequencyMode ? 1 : 0];
		private static bool? lastLowFreqMode = null;

		// 60 or 30 hz main fetch speed
		private static readonly List<float> statsDeltaTimes = new List<float> { 16.6666666f, 33.3333333f };


		public static LiveWindow liveWindow;
		private static ClosingDialog closingWindow;

		private static readonly Dictionary<string, Window> popupWindows = new();

		private static float smoothDeltaTime = -1;

		private static string customId;
		public static string CustomId {
			get => customId;
			set {
				customId = value;
				if (matchData != null) matchData.customId = value;
			}
		}

		public static bool hostingLiveReplay = false;

		public static string echoVRIP = "";
		public static int echoVRPort = 6721;
		public const int SPECTATEME_PORT = 6720; 
		public static bool overrideEchoVRPort;

		public static bool spectateMe;
		private static string lastSpectatedSessionId;

		public static string hostedAtlasSessionId;
		public static LiveWindow.AtlasWhitelist atlasWhitelist = new LiveWindow.AtlasWhitelist();

		public static TTS synth;
		public static ReplayClips replayClips;
		public static ReplayFilesManager replayFilesManager;
		public static CameraWriteController cameraWriteController;
		public static CameraWrite cameraWriteWindow;
		public static EchoGPController echoGPController;
		public static WebSocketServerManager webSocketMan;

		private static CancellationTokenSource autorestartCancellation;
		private static CancellationTokenSource fetchThreadCancellation;
		private static CancellationTokenSource liveReplayCancel;
		public static Thread atlasHostingThread;
		private static Thread IPSearchthread1;
		private static Thread IPSearchthread2;
		public static OBS obs;
		private static OverlayServer overlayServer;
		public static NetMQEvents netMQEvents;
		private static readonly HttpClient fetchClient = new HttpClient();
		private static readonly System.Timers.Timer fetchTimer = new System.Timers.Timer();
		private static readonly Stopwatch fetchSw = new Stopwatch();


		#region Event Callbacks

		public static Action SparkClosing;
		/// <summary>
		/// Called by the fetch thread when a frame is successfully fetched
		/// Subscribe to this for raw json data. Only use this in a context that doesn't care about the deserialzed frames.
		/// params: timestamp, session string, bones string (or null)
		/// </summary>
		public static Action<DateTime, string, string> FrameFetched;
		/// <summary>
		/// Called when a frame is finished with conversion.
		/// Subscribe to this for Frame objects
		/// </summary>
		public static Action<Frame> NewFrame;
		/// <summary>
		/// Called when a frame is finished with conversion and it is an Echo Arena frame
		/// </summary>
		public static Action<Frame> NewArenaFrame;
		/// <summary>
		/// Called when connectedToGame state changes.
		/// This could be on loading screen or lobby
		/// string is the raw /session
		/// </summary>
		public static Action<DateTime, string> ConnectedToGame;
		/// <summary>
		/// Called when connectedToGame state changes.
		/// </summary>
		public static Action DisconnectedFromGame;
		public static Action<Frame> JoinedGame;
		public static Action<Frame> LeftGame;

		public static Action<Frame> JoinedLobby;
		public static Action<Frame> LeftLobby;

		public static Action<Frame, Team, Player> PlayerJoined;
		public static Action<Frame, Team, Player> PlayerLeft;
		/// <summary>
		/// frame, fromteam, toteam, player
		/// </summary>
		public static Action<Frame, Team, Team, Player> PlayerSwitchedTeams;
		public static Action<Frame> MatchReset;
		public static Action<Frame> PauseRequest;
		public static Action<Frame> GamePaused;
		public static Action<Frame> GameUnpaused;
		public static Action<Frame> RoundOver;
		public static Action<Frame> LocalThrow;
		/// <summary>
		/// frame, team, player, speed, howlongago
		/// </summary>
		public static Action<Frame, Team, Player, float, float> BigBoost;
		/// <summary>
		/// frame, team, player, playspacelocation (compare to head.Pos)
		/// </summary>
		public static Action<Frame, Team, Player, Vector3> PlayspaceAbuse;
		public static Action<Frame, EventData> Save;
		public static Action<Frame, EventData> Steal;
		/// <summary>
		/// frame, stunner_team, stunner_player, stunee_player
		/// </summary>
		public static Action<Frame, EventData> Stun;
		/// <summary>
		/// Catch by other team from throw within 7 seconds
		/// frame, team, throwplayer, catchplayer
		/// </summary>
		public static Action<Frame, Team, Player, Player> Interception;
		/// <summary>
		/// Catch by same team as throw within 7 seconds
		/// frame, team, throwplayer, catchplayer
		/// </summary>
		public static Action<Frame, Team, Player, Player> Pass;
		/// <summary>
		/// Any catch, including interceptions and passes
		/// frame, team, player
		/// </summary>
		public static Action<Frame, Team, Player> Catch;
		/// <summary>
		/// frame, team, player, lefthanded, underhandedness
		/// </summary>
		public static Action<Frame, Team, Player, bool, float> Throw;
		public static Action<Frame, Team, Player> ShotTaken;
		public static Action<Frame, Team.TeamColor> RestartRequest;
		/// <summary>
		/// frame, team, player, neutral joust?, time, maxSpeed, tubeExitSpeed
		/// </summary>
		public static Action<Frame, Team, Player, bool, float, float, float> Joust;
		public static Action<Frame, EventData> JoustEvent;
		public static Action<Frame, GoalData> Goal;
		/// <summary>
		/// This is called on the first frame that the goal happens rather than waiting for stable data
		/// </summary>
		public static Action<Frame> GoalImmediate;
		public static Action<Frame, GoalData> Assist;
		public static Action<Frame, Team, Player> LargePing;

		#region Spark Settings Changed

		public static Action OverlayConfigChanged;
		public static Action<string> EventLog;

		#endregion

		#endregion


		private static App app;

		public static void Main(string[] args, App app)
		{
			try
			{
				Logger.Init();

				Program.app = app;
				if (args.Contains("-port"))
				{
					int index = args.ToList().IndexOf("-port");
					if (index > -1)
					{
						if (int.TryParse(args[index + 1], out echoVRPort))
						{
							overrideEchoVRPort = true;
						}
					}
					else
					{
						LogRow(LogType.Error, "ERROR 3984. This shouldn't happen");
					}
				}


				if (CheckIfLaunchedWithCustomURLHandlerParam(args))
				{
					return; // wait for the dialog to quit the program
				}


				// allow multiple instances if the port is overriden
				if (IsSparkOpen() && !overrideEchoVRPort)
				{
					MessageBox box = new MessageBox(Resources.instance_already_running_message, Resources.Error);
					box.Show();
					//while(box!= null)
					{
						Thread.Sleep(10);
					}


					//return; // wait for the dialog to quit the program
				}



				netMQEvents = new NetMQEvents();

				InstalledSpeakerSystemVersion = FindEchoSpeakerSystemInstallVersion();
				if (InstalledSpeakerSystemVersion.Length > 0)
				{
					string[] latestSpeakerSystemVer = GetLatestSpeakerSystemURLVer();
					IsSpeakerSystemUpdateAvailable = latestSpeakerSystemVer[1] != InstalledSpeakerSystemVersion;
				}


				SparkSettings.instance.sparkExeLocation = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Spark.exe");
				SparkSettings.instance.Save();


				RegisterUriScheme("atlas", "ATLAS Protocol");
				RegisterUriScheme("spark", "Spark Protocol");

				obs = new OBS();


				// if logged in with discord
				if (!string.IsNullOrEmpty(SparkSettings.instance.discordOAuthRefreshToken))
				{
					DiscordOAuth.OAuthLoginRefresh(SparkSettings.instance.discordOAuthRefreshToken);
				}
				else
				{
					DiscordOAuth.RevertToPersonal();
				}

				liveWindow = new LiveWindow();
				liveWindow.Closed += (_, _) => liveWindow = null;
				liveWindow.Show();


				if (!SparkSettings.instance.firstTimeSetupShown)
				{
					ToggleWindow(typeof(FirstTimeSetupWindow));
					SparkSettings.instance.firstTimeSetupShown = true;
				}

				// Check for command-line flags
				if (args.Contains("-slowmode"))
				{
					SparkSettings.instance.lowFrequencyMode = true;
				}

				if (args.Contains("-autorestart"))
				{
					SparkSettings.instance.autoRestart = true;
				}

				if (args.Contains("-showdatabaselog"))
				{
					SparkSettings.instance.showDatabaseLog = true;
				}


				// make an exception for certain users
				// Note that these usernames are not the access codes. Don't even try.
				if (DiscordOAuth.AccessCode.series_name == "ignitevr")
				{
					ENABLE_LOGGER = false;
				}

				ReadSettings();

				client.DefaultRequestHeaders.Add("version", AppVersionString());
				client.DefaultRequestHeaders.Add("User-Agent", "Spark/" + AppVersionString());

				client.BaseAddress = new Uri(APIURL);


				if (SparkSettings.instance.onlyActivateHighlightsWhenGameIsOpen &&
					HighlightsHelper.isNVHighlightsEnabled)
				{
					HighlightsHelper.SetupNVHighlights();
				}
				else
				{
					HighlightsHelper.InitHighlightsSDK(true);
				}

				// only enable Highlights when game is open
				ConnectedToGame += (_, _) =>
				{
					if (HighlightsHelper.isNVHighlightsEnabled)
					{
						HighlightsHelper.SetupNVHighlights();
					}
				};
				DisconnectedFromGame += () =>
				{
					if (SparkSettings.instance.onlyActivateHighlightsWhenGameIsOpen)
					{
						HighlightsHelper.CloseNVHighlights();
					}
				};
				

				// TODO don't initialize twice and get this to work without discord login maybe
				synth = new TTS();

				DiscordOAuth.Authenticated += () =>
				{
					synth = new TTS();
					// Configure the audio output.
					synth.SetOutputToDefaultAudioDevice();
					synth.SetRate(SparkSettings.instance.TTSSpeed);
				};


				// this sets up the event listeners for replay clips
				replayClips = new ReplayClips();

				// Set up listeners for camera-related events
				cameraWriteController = new CameraWriteController();

				// sets up listeners for replay file saving
				replayFilesManager = new ReplayFilesManager();

				// sets up listeners for Echo GP timer
				echoGPController = new EchoGPController();

				webSocketMan = new WebSocketServerManager();

				// web server asp.net
				try
				{
					overlayServer = new OverlayServer();
				}
				catch (Exception e)
				{
					Logger.LogRow(LogType.Error, e.ToString());
				}


				UpdateEchoExeLocation();

				DiscordRichPresence.Start();

				spectateMe = SparkSettings.instance.spectateMeOnByDefault;

				autorestartCancellation = new CancellationTokenSource();
				Task.Run(AutorestartTask, autorestartCancellation.Token);
				// fetchThreadCancellation= new CancellationTokenSource();
				// Task.Run(FetchThreadNew, fetchThreadCancellation.Token);


				fetchTimer.Interval = 2000;
				fetchTimer.Elapsed += FetchAPI;
				fetchTimer.Start();

				ConnectedToGame += (_,_) =>
				{
					fetchTimer.Interval = StatsHz;
				};
				DisconnectedFromGame += () =>
				{
					fetchTimer.Interval = 2000;
				};

				liveReplayCancel = new CancellationTokenSource();
				Task.Run(LiveReplayHostingTask, liveReplayCancel.Token);


				_ = Task.Run(() =>
				{
					try
					{
						EchoVRSettingsManager.ReloadLoadingTips();
						JToken toolsAll = EchoVRSettingsManager.loadingTips["tools-all"];
						JArray tips = (JArray) EchoVRSettingsManager.loadingTips["tools-all"]["tips"];

						// keep only those without SPARK in the title
						tips = new JArray(tips.Where(t => (string) t[1] != "SPARK"));

						foreach (string tip in LoadingTips.newTips)
						{
							if (!tips.Any(existingTip => (string) existingTip[2] == tip))
							{
								tips.Add(new JArray("", "SPARK", tip));
							}
						}

						toolsAll["tips"] = tips;

						EchoVRSettingsManager.WriteEchoVRLoadingTips(EchoVRSettingsManager.loadingTips);
					}
					catch (Exception e)
					{
						Logger.LogRow(LogType.Error, e.ToString());
					}
				});

				//HighlightsHelper.CloseNVHighlights();

				AutoUploadTabletStats();


				#region Add Listeners

				LeftGame += (f) =>
				{
					if (spectateMe)
					{
						try
						{
							KillEchoVR($"-httpport {SPECTATEME_PORT}");
							lastSpectatedSessionId = string.Empty;
						}
						catch (Exception e)
						{
							LogRow(LogType.Error, $"Broke something in the spectator follow system.\n{e}");
						}
					}
				};

				#endregion

			}
			catch (Exception e)
			{
				Logger.LogRow(LogType.Error, e.ToString());
				Quit();
			}
		}


		public static Version AppVersion()
		{
			return Application.Current.GetType().Assembly.GetName().Version;
		}

		public static string AppVersionString()
		{
			Version version = AppVersion();
			return $"{version.Major}.{version.Minor}.{version.Build}";
		}
		
		public static bool IsWindowsStore()
		{
			#if WINDOWS_STORE_RELEASE
				return true;
			#else
				return false;
			#endif
		}

		/// <summary>
		/// This is just a failsafe so that the program doesn't leave a dangling thread.
		/// </summary>
		static void KillAll()
		{
			if (liveWindow != null)
			{
				liveWindow.Close();
				liveWindow = null;
			}
			overlayServer?.Stop();
		}

		private static async Task GentleClose()
		{
			running = false;
			
			closingWindow.label.Content = Resources.Closing___;
			
			netMQEvents.CloseApp();
			await Task.Delay(50);

			overlayServer?.Stop();

			while (atlasHostingThread != null && atlasHostingThread.IsAlive)
			{
				closingWindow.label.Content = Resources.Shutting_down_Atlas___;
				await Task.Delay(10);
			}

			fetchTimer?.Stop();
			autorestartCancellation.Cancel();
			fetchThreadCancellation?.Cancel();
			liveReplayCancel.Cancel();

			while (replayFilesManager.zipping)
			{
				closingWindow.label.Content = Resources.Compressing_Replay_File___;
				await Task.Delay(10);
			}

			closingWindow.label.Content = Resources.Closing_NVIDIA_Highlights___;
			HighlightsHelper.CloseNVHighlights();

			closingWindow.label.Content = Resources.Closing_Speaker_System___;
			liveWindow?.KillSpeakerSystem();

			closingWindow.label.Content = "Closing PubSub System...";
			
			AsyncIO.ForceDotNet.Force();
			NetMQConfig.Cleanup(false);
			closingWindow.label.Content = Resources.Closing___;
			
			app.ExitApplication();

			await Task.Delay(100);

			closingWindow.label.Content = "Failed to close gracefully. Using an axe instead...";
			LogRow(LogType.Error, "Failed to close gracefully. Using an axe instead...");
			KillAll();
		}

		public static bool IsSparkOpen()
		{
			try
			{
				Process[] process = Process.GetProcessesByName("IgniteBot");
				Process[] processesSpark = Process.GetProcessesByName("Spark");
				return process?.Length > 1 || processesSpark?.Length > 1;
			}
			catch (Exception e)
			{
				LogRow(LogType.Error, "Error getting other Spark windows\n" + e.ToString());
			}
			return false;
		}

		public static void KillAllOtherSparkInstances()
		{
			try
			{
				Process[] processes = Process.GetProcessesByName("IgniteBot");
				Process[] processesSpark = Process.GetProcessesByName("Spark");
				foreach (Process process in processes)
				{
					process.Kill();

				}
				foreach (Process process in processesSpark)
				{
					process.Kill();
				}
			}
			catch (Exception e)
			{
				LogRow(LogType.Error, "Error killing other Spark windows\n" + e.ToString());
			}
		}

		
		public static async Task FetchThreadNew()
		{
			while (running)
			{	
				DateTime fetchTime = DateTime.UtcNow;
				
				_ = Task.Run(() => { FetchAPI(null,null); });

				// set up timing for next fetch
				DateTime next = fetchTime.AddMilliseconds(StatsHz);
				if (DateTime.UtcNow < next)
				{
					TimeSpan delay = next - DateTime.UtcNow;
					await Task.Delay(delay);
				}
				else
				{
					LogRow(LogType.Error, $"Fetch rate too slow. Skipped {(DateTime.UtcNow - next).TotalSeconds:N} seconds");
				}

				// don't spam when not in a game
				if (!connectedToGame)
				{
					await Task.Delay(2000);
				}
				
				
				// Debug.WriteLine(fetchSw.Elapsed.TotalSeconds.ToString("N5"));
				// fetchSw.Restart();
			}
		}

		/// <summary>
		/// Fetches the API once
		/// </summary>
		/// <param name="sender">unused</param>
		/// <param name="e">unused</param>
		private static void FetchAPI(object sender, EventArgs e)
		{
			try
			{
				// fetch the session or bones data
				List<Task<HttpResponseMessage>> tasks = new List<Task<HttpResponseMessage>>
				{
					fetchClient.GetAsync($"http://{echoVRIP}:{echoVRPort}/session")
				};
				if (SparkSettings.instance.fetchBones)
				{
					tasks.Add(fetchClient.GetAsync($"http://{echoVRIP}:{echoVRPort}/player_bones"));
				}

				_ = Task.Run(async () =>
				{
					try
					{
						HttpResponseMessage[] results = await Task.WhenAll(tasks);

						if (results[0].IsSuccessStatusCode)
						{
							string session = await results[0].Content.ReadAsStringAsync();

							string bones = null;
							if (results.Length > 1 && results[1].IsSuccessStatusCode)
							{
								bones = await results[1].Content.ReadAsStringAsync();
							}

							// add this data to the public variable
							lock (lastJSONLock)
							{
								lastJSON = session;
								lastBonesJSON = bones;
							}

							DateTime frameTime = DateTime.UtcNow;

							if (connectedToGame == false)
							{
								_ = Task.Run(() => { ConnectedToGame?.Invoke(frameTime, session); });
							}

							connectedToGame = true;
							apiSettingDisabled = false;

							// early quit if the program was quit while fetching
							if (!running) return;

							// tell the processing methods that stuff is available
							_ = Task.Run(() => { FrameFetched?.Invoke(frameTime, session, bones); });

							// tell the processing methods that stuff is available
							_ = Task.Run(() =>
							{
								lock (gameStateLock)
								{
									Frame f = Frame.FromJSON(frameTime, session, bones);

									if (f != null)
									{
										if (f.err_code == -3)
										{
											inGame = false;
											apiSettingDisabled = true;
										}
										else
										{
											inGame = true;

											ProcessFrame(f);

											NewFrame?.Invoke(f);
										}
									}
									else
									{
										LeftGame?.Invoke(lastFrame);
									}

									lock (lastFrameLock)
									{
										// for the very first frame, copy it to the other previous frames
										lastFrame ??= f;

										lastLastLastFrame = lastLastFrame;
										lastLastFrame = lastFrame;
										lastFrame = f;
									}
								}
							});
						}
						else // not a success status code
						{
							await FetchFail(results);
						}

						if (lastLowFreqMode != null && lastLowFreqMode != SparkSettings.instance.lowFrequencyMode)
						{
							fetchTimer.Interval = StatsHz;
							Debug.WriteLine("Changed fetch interval");
						}

						lastLowFreqMode = SparkSettings.instance.lowFrequencyMode;
					}
					catch (HttpRequestException)
					{
						await FetchFail(null);
					}
					catch (Exception ex)
					{
						LogRow(LogType.Error, $"Error in fetch request.\n{ex}");
					}
				});
			}
			catch (Exception ex)
			{
				LogRow(LogType.Error, $"Error in fetch request.\n{ex}");
			}
			
			fetchSw.Restart();

			wasInGame = inGame;
		}

		private static async Task FetchFail(HttpResponseMessage[] results)
		{
			// Not connected to game

			if (connectedToGame)
			{
				_ = Task.Run(() => { DisconnectedFromGame?.Invoke(); });
			}

			connectedToGame = false;
			inGame = false;

			if (results != null)
			{
				string session = await results[0].Content.ReadAsStringAsync();

				if (session.Length > 0 && session[0] == '{')
				{
					Frame f = Frame.FromJSON(DateTime.UtcNow, session, null);
					apiSettingDisabled = f.err_code == -2;
				}
				else
				{
					// in loading screen, where the response is not json
				}
			}

			// left game
			if (!inGame && wasInGame)
			{
				try
				{
					if (lastFrame != null)
					{
						if (lastFrame.InLobby)
						{
							_ = Task.Run(() => { LeftLobby?.Invoke(lastFrame); });
						}
						else
						{
							_ = Task.Run(() => { LeftGame?.Invoke(lastFrame); });
						}
					}
				}
				catch (Exception exp)
				{
					LogRow(LogType.Error, "Error processing action", exp.ToString());
				}
			}

			// add this data to the public variable
			lock (lastJSONLock)
			{
				lastJSON = null;
				lastBonesJSON = null;
			}
		}

		private static void ProcessFrame(Frame frame)
		{
			try
			{

				if (matchData == null)
				{
					matchData = new MatchData(frame, CustomId);
					UpdateStatsIngame(frame);
				}

				try
				{
					GenerateEvents(frame);
				}
				catch (Exception ex)
				{
					LogRow(LogType.Error, $"Error in ProcessFrame. Please catch inside.\n{ex}");
				}
			}
			catch (Exception ex)
			{
				LogRow(LogType.Error, "Big oopsie. Please catch inside. " + ex);
			}
		}

		

		/// <summary>
		/// Thread to detect crashes and restart EchoVR
		/// </summary>
		private static async Task AutorestartTask()
		{
			lastDataTime = DateTime.UtcNow;

			// Session pull loop.
			while (running)
			{
				if (SparkSettings.instance.autoRestart)
				{
					// If `minTillAutorestart` minutes have passed, restart EchoVR
					if (DateTime.Compare(lastDataTime.AddMinutes(minTillAutorestart), DateTime.UtcNow) < 0)
					{
						KillEchoVR();
						StartEchoVR(JoinType.Spectator, noovr: SparkSettings.instance.spectatorStreamNoOVR, combat: SparkSettings.instance.spectatorStreamCombat);

						// reset timer
						lastDataTime = DateTime.UtcNow;
					}
				}

				await Task.Delay(1000);
			}
		}

		private static async Task LiveReplayHostingTask()
		{
			while (running)
			{
				if (hostingLiveReplay)
				{
					StringContent content = null;
					lock (lastJSONLock)
					{
						if (lastJSON != null)
							content = new StringContent(lastJSON, Encoding.UTF8, "application/json");
					}

					if (content != null)
					{
						_ = DoLiveReplayUpload(content);
					}
				}

				await Task.Delay(1000);
			}
		}

		private static async Task DoLiveReplayUpload(StringContent content)
		{
			try
			{
				// client_name is just for visibility in the log
				HttpResponseMessage response = await client.PostAsync(
					"live_replay/" + lastFrame.sessionid + "?caprate=1&default=true&client_name=" +
					lastFrame.client_name, content);
			}
			catch
			{
				LogRow(LogType.Error, "Can't connect to the DB server");
			}
		}

		/// <summary>
		/// Saves the current process path
		/// </summary>
		/// <returns>The actual process</returns>
		public static Process[] GetEchoVRProcess()
		{
			try
			{
				Process[] process = Process.GetProcessesByName("echovr");
				if (process.Length > 0)
				{
					// Get process path
					var newEchoPath = process[0].MainModule.FileName;
					if (!string.IsNullOrEmpty(newEchoPath))
					{
						SparkSettings.instance.echoVRPath = newEchoPath;
					}
				}

				return process;
			}
			catch (Exception e)
			{
				LogRow(LogType.Error, $"Error getting process\n{e}");
				return null;
			}
		}

		/// <summary>
		/// Kills EchoVR if it is open
		/// </summary>
		/// <param name="findInArgs"></param>
		/// <returns>True if successfully killed something</returns>
		public static bool KillEchoVR(string findInArgs = null)
		{
			bool killed = false;
			if (lastFrame != null)
			{
				LogRow(LogType.File, Program.lastFrame.sessionid, "Killing EchoVR...");
			}
			LogRow(LogType.Error, "Killing EchoVR...");
			Process[] process = Process.GetProcessesByName("echovr");
			foreach (Process p in process)
			{
				if (findInArgs == null || GetCommandLine(p).Contains(findInArgs))
				{
					try
					{
						p.Kill();
						killed = true;
					}
					catch (Exception ex)
					{
						LogRow(LogType.Error, "Failed to kill process\n" + ex);
					}
				}
			}
			return killed;
		}
		
		private static string GetCommandLine(Process process)
		{
			using ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT CommandLine FROM Win32_Process WHERE ProcessId = " + process.Id);
			using ManagementObjectCollection objects = searcher.Get();
			return objects.Cast<ManagementBaseObject>().SingleOrDefault()?["CommandLine"]?.ToString();
		}


		public enum JoinType
		{
			Choose,
			Player,
			Spectator
		}
		public static void StartEchoVR(JoinType joinType, int port = 6721, bool noovr = false, string session_id = null, string level = null, string region = null, bool combat=false)
		{
			if (joinType == JoinType.Choose)
			{
				new MessageBox("Can't launch with join type Choose", Resources.Error).Show();
				return;
			}
			string echoPath = SparkSettings.instance.echoVRPath;
			if (!string.IsNullOrEmpty(echoPath))
			{
				bool spectating = joinType == JoinType.Spectator;
				Process.Start(echoPath, 
					(spectating && SparkSettings.instance.capturevp2 ? "-capturevp2 " : " ") + 
					(spectating ? "-spectatorstream " : " ") +
					(combat ? "echo_combat " : "") + 
					(session_id == null ? "" : $"-lobbyid {session_id} ") +  
					(noovr ? "-noovr " : "") +
					(port != 6721 ? $"-httpport {port} " : "") +
					(level == null ? "" : $"-level {level} ") +
					(region == null ? "" : $"-region {region} ")
				);
			}
			else
			{
				new MessageBox(Resources.echovr_path_not_set, Resources.Error, Quit).Show();
			}
		}

		// Functions required to force focus change, couldn't make them all local because GetWindowThreadProcessId would throw an error (though it likely shouldn't)
		[DllImport("User32.dll")]
		static extern bool SetForegroundWindow(IntPtr hWnd);
		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		static extern bool ShowWindow(IntPtr hWnd, ShowWindowEnum flags);
		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		static extern bool GetWindowPlacement(IntPtr hWnd, ref Windowplacement lpwndpl);
		[DllImport("user32.dll")]
		static extern IntPtr GetForegroundWindow();
		[DllImport("user32.dll")]
		static extern bool AttachThreadInput(IntPtr idAttach, IntPtr idAttachTo, bool fAttach);
		[DllImport("user32.dll")]
		static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		public static extern IntPtr FindWindow(String lpClassName, String lpWindowName);
		private enum ShowWindowEnum
		{
			Hide = 0,
			ShowNormal = 1, ShowMinimized = 2, ShowMaximized = 3,
			Maximize = 3, ShowNormalNoActivate = 4, Show = 5,
			Minimize = 6, ShowMinNoActivate = 7, ShowNoActivate = 8,
			Restore = 9, ShowDefault = 10, ForceMinimized = 11
		};
		private struct Windowplacement
		{
			public int length;
			public int flags;
			public int showCmd;
			public System.Drawing.Point ptMinPosition;
			public System.Drawing.Point ptMaxPosition;
			public System.Drawing.Rectangle rcNormalPosition;
		}

		public static void FocusEchoVR()
		{
			IntPtr EchoHandle = FindWindow(null, "Echo VR");
			IntPtr echoThread = new IntPtr(GetWindowThreadProcessId(EchoHandle, out _));

			if (EchoHandle != GetForegroundWindow())
			{
				IntPtr foregroundThread = new IntPtr(GetWindowThreadProcessId(GetForegroundWindow(), out _));
				AttachThreadInput(
					foregroundThread,
					echoThread, true
				);

				//get the hWnd of the process
				Windowplacement placement = new Windowplacement();
				GetWindowPlacement(EchoHandle, ref placement);

				// Check if window is minimized
				if (placement.showCmd == 2)
				{
					//the window is hidden so we restore it
					ShowWindow(EchoHandle, ShowWindowEnum.Restore);
				}
				else
				{
					ShowWindow(EchoHandle, ShowWindowEnum.Hide);
					ShowWindow(EchoHandle, ShowWindowEnum.Show);
				}
				SetForegroundWindow(EchoHandle);
				AttachThreadInput(foregroundThread, echoThread, false);
			}
		}

		public static string FindEchoSpeakerSystemInstallVersion()
		{
			string ret = "";
			try
			{
				string filePath = Path.Combine("C:\\Program Files (x86)\\Echo Speaker System", "latestversion.txt");

				string[] lines = File.ReadAllLines(filePath);
				if (lines != null && lines.Length > 0)
				{
					ret = "v" + lines[0];
				}
			}
			catch { }
			return ret;
		}

		private static void UpdateEchoExeLocation()
		{
			Task.Run(() => { 
			// skip if we already have a valid path
			if (File.Exists(SparkSettings.instance.echoVRPath)) return;

			try
			{
				if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				{
					const string key = "Software\\Oculus VR, LLC\\Oculus\\Libraries";
					RegistryKey oculusReg = Registry.CurrentUser.OpenSubKey(key);
					if (oculusReg == null)
					{
						// Oculus not installed
						return;
					}

					List<string> paths = new List<string>();
					foreach (string subkey in oculusReg.GetSubKeyNames())
					{
						paths.Add((string)oculusReg.OpenSubKey(subkey).GetValue("OriginalPath"));
					}

					const string echoDir = "Software\\ready-at-dawn-echo-arena\\bin\\win10\\echovr.exe";
					foreach (string path in paths)
					{
						string file = Path.Combine(path, echoDir);
						if (File.Exists(file))
						{
							SparkSettings.instance.echoVRPath = file;
							return;
						}
					}
				}
			}
			catch (Exception e)
			{
				LogRow(LogType.Error, $"Can't get EchoVR path from registry\n{e}");
			}});
		}

		private static void ReadSettings()
		{
			echoVRIP = SparkSettings.instance.echoVRIP;
			HighlightsHelper.isNVHighlightsEnabled = SparkSettings.instance.isNVHighlightsEnabled;
			if (!overrideEchoVRPort) echoVRPort = SparkSettings.instance.echoVRPort;

			try
			{
				if (SparkSettings.instance.saveFolder == "none" || !Directory.Exists(SparkSettings.instance.saveFolder))
				{
					SparkSettings.instance.saveFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Spark", "replays");
					Directory.CreateDirectory(SparkSettings.instance.saveFolder);
					SparkSettings.instance.Save();
				}
			}
			catch (Exception)
			{
				new MessageBox($"Error accessing replay folder path:\n{SparkSettings.instance.saveFolder}").Show();
				LogRow(LogType.Error, $"Error accessing replay folder path:\n{SparkSettings.instance.saveFolder}");
			}
		}

		

		/// <summary>
		/// Goes through a "frame" (single JSON object) and generates the relevant events
		/// </summary>
		private static void GenerateEvents(Frame frame)
		{
			if (!wasInGame)
			{
				try
				{
					if (frame.InLobby)
					{
						JoinedLobby?.Invoke(frame);
					}
					else
					{
						JoinedGame?.Invoke(frame);
						
						// make sure there is a valid echovr path saved
						if (SparkSettings.instance.echoVRPath == "" || SparkSettings.instance.echoVRPath.Contains("win7"))
						{
							UpdateEchoExeLocation();
						}
					}
				}
				catch (Exception exp)
				{
					LogRow(LogType.Error, "Error processing action", exp.ToString());
				}
			}

			// 'mpl_lobby_b2' may change in the future
			if (frame == null) return;

			if (frame.client_name != "anonymous")
			{
				SparkSettings.instance.client_name = frame.client_name;
			}

			// lobby stuff

			// last throw state changed
			try
			{
				if (frame.last_throw != null && lastFrame != null && frame.last_throw.total_speed != 0 && Math.Abs(frame.last_throw.total_speed - lastFrame.last_throw.total_speed) > .001f)
				{
					LogRow(LogType.File, frame.sessionid, $"{frame.game_clock_display} - Total speed: {frame.last_throw.total_speed}  Arm: {frame.last_throw.speed_from_arm}  Wrist: {frame.last_throw.speed_from_wrist}  Movement: {frame.last_throw.speed_from_movement}");
					//matchData.Events.Add(
					//	new EventData(
					//		matchData,
					//		EventData.EventType.@throw,
					//		frame.game_clock,
					//		frame.teams[frame.pause.paused_requested_team == "blue" ? (int)TeamColor.blue : (int)TeamColor.orange],
					//		null,
					//		null,
					//		Vector3.Zero,
					//		Vector3.Zero)
					//	);

					try
					{
						LocalThrow?.Invoke(frame);
					}
					catch (Exception exp)
					{
						LogRow(LogType.Error, "Error processing action", exp.ToString());
					}
				}
			}
			catch (Exception e)
			{
				LogRow(LogType.Error, $"Error with last throw parsing\n{e}");
			}


			if (string.IsNullOrWhiteSpace(frame.game_status)) return;
			if (frame.InLobby) return;
			
			// this frame is an Arena frame
			if (frame.InArena) NewArenaFrame?.Invoke(frame);
			
			// if we entered a different match
			if (lastFrame == null || frame.sessionid != lastFrame.sessionid)
			{
				
				// We just discard the old match and hope it was already submitted

				lastFrame = frame; // don't detect stats changes across matches
								   // TODO discard old players

				inPostMatch = false;
				matchData = new MatchData(frame, CustomId);
				UpdateStatsIngame(frame);

				if (string.IsNullOrEmpty(SparkSettings.instance.echoVRPath))
				{
					GetEchoVRProcess();
				}

				if (spectateMe)
				{
					try
					{
						KillEchoVR($"-httpport {SPECTATEME_PORT}");
						StartEchoVR(JoinType.Spectator, SPECTATEME_PORT, SparkSettings.instance.useAnonymousSpectateMe, lastFrame.sessionid);
						lastSpectatedSessionId = lastFrame.sessionid;

						liveWindow.SetSpectateMeSubtitle(Resources.Waiting_for_EchoVR_to_start);
					}
					catch (Exception e)
					{
						LogRow(LogType.Error, $"Broke something in the spectator follow system.\n{e}");
					}
				}

				WaitUntilLocalGameLaunched(CameraWriteController.UseCameraControlKeys, port:SPECTATEME_PORT);
			}

			// The time between the current frame and last frame in seconds based on the game clock
			float deltaTime = lastFrame.game_clock - frame.game_clock;
			if (deltaTime != 0)
			{
				if (smoothDeltaTime == -1) smoothDeltaTime = deltaTime;
				const float smoothingFactor = .99f;
				smoothDeltaTime = smoothDeltaTime * smoothingFactor + deltaTime * (1 - smoothingFactor);
			}
			


			// Did a player join or leave?

			// is a player from the current frame not in the last frame? (Player Join 🤝)
			// Loop through teams.
			foreach (Team team in frame.teams)
			{
				// Loop through players on team.
				foreach (Player player in team.players)
				{
					// make sure the player wasn't in the last frame
					if (lastFrame.GetAllPlayers(true).Any(p => p.userid == player.userid)) continue;

					// TODO find why this is crashing
					try
					{
						matchData.Events.Add(new EventData(matchData, EventData.EventType.player_joined,
							frame.game_clock, team, player, null, player.head.Position, Vector3.Zero));
						LogRow(LogType.File, frame.sessionid, $"{frame.game_clock_display} - Player Joined: {player.name}");

						if (team.color != Team.TeamColor.spectator)
						{
							// cache this players stats so they aren't overridden if they join again
							MatchPlayer playerData = matchData.GetPlayerData(player);
							// if player was in this match before
							playerData?.CacheStats(player.stats);

							// find the vrml team names
							FindTeamNamesFromPlayerList(matchData, team);
						}

						UpdateStatsIngame(frame);

						try
						{
							PlayerJoined?.Invoke(frame, team, player);
						}
						catch (Exception exp)
						{
							LogRow(LogType.Error, "Error processing action", exp.ToString());
						}

					}
					catch (Exception ex)
					{
						LogRow(LogType.Error, ex.ToString());
					}
				}
			}

			// Is a player from the last frame not in the current frame? (Player Leave 🚪)
			// Loop through teams.
			foreach (Team team in lastFrame.teams)
			{
				// Loop through players on team.
				foreach (Player player in team.players)
				{
					if (frame.GetAllPlayers(true).Any(p => p.userid == player.userid)) continue;

					matchData.Events.Add(new EventData(
						matchData,
						EventData.EventType.player_left,
						frame.game_clock,
						team,
						player,
						null,
						player.head.Position,
						player.velocity.ToVector3())
					);

					LogRow(LogType.File, frame.sessionid, frame.game_clock_display + " - Player Left: " + player.name);

					UpdateStatsIngame(frame);

					try
					{
						PlayerLeft?.Invoke(frame, team, player);
					}
					catch (Exception exp)
					{
						LogRow(LogType.Error, "Error processing action", exp.ToString());
					}
				}
			}

			// Did a player switch teams? (Player Switch 🔁)
			// Loop through current frame teams.
			foreach (Team team in frame.teams)
			{
				// Loop through players on team.
				foreach (Player player in team.players)
				{
					Team.TeamColor lastTeamColor = lastFrame.GetTeamColor(player.userid);
					if (lastTeamColor == team.color) continue;

					matchData.Events.Add(new EventData(
						matchData,
						EventData.EventType.player_switched_teams,
						frame.game_clock,
						team,
						player,
						null,
						player.head.Position,
						player.velocity.ToVector3())
					);

					LogRow(LogType.File, frame.sessionid, $"{frame.game_clock_display} - Player switched to {team.color} team: {player.name}");

					UpdateStatsIngame(frame);

					Team lastTeam = lastFrame.GetTeam(player.userid);
					try
					{
						PlayerSwitchedTeams?.Invoke(frame, lastTeam, team, player);
					}
					catch (Exception exp)
					{
						LogRow(LogType.Error, "Error processing action", exp.ToString());
					}
				}
			}


			int currentFrameStats = 0;
			foreach (var team in frame.teams)
			{
				// Loop through players on team.
				foreach (var player in team.players)
				{
					currentFrameStats += player.stats.stuns + player.stats.points;
				}
			}

			if (currentFrameStats < lastFrameSumOfStats)
			{
				lastValidStatsFrame = lastFrame;
				lastValidSumOfStatsAge = 0;
			}

			lastValidSumOfStatsAge++;
			lastFrameSumOfStats = currentFrameStats;


			// Did the game state change?
			if (frame.game_status != lastFrame.game_status)
			{
				ProcessGameStateChange(frame, deltaTime);
			}


			// pause state changed
			try
			{
				if (frame.pause.paused_state != lastFrame.pause.paused_state)
				{
					if (frame.pause.paused_state == "paused")
					{
						LogRow(LogType.File, frame.sessionid, $"{frame.game_clock_display} - {frame.pause.paused_requested_team} team paused the game");
						
						EventData pauseEvent = new EventData(
							matchData,
							EventData.EventType.pause_request,
							frame.game_clock,
							frame.teams[frame.pause.paused_requested_team == "blue" ? (int) Team.TeamColor.blue : (int) Team.TeamColor.orange],
							null,
							null,
							Vector3.Zero,
							Vector3.Zero);
						
						matchData.Events.Add(pauseEvent);

						try
						{
							GamePaused?.Invoke(frame);
						}
						catch (Exception exp)
						{
							LogRow(LogType.Error, "Error processing action", exp.ToString());
						}
					}

					if (lastFrame.pause.paused_state == "unpaused" &&
						frame.pause.paused_state == "paused_requested")
					{
						try
						{
							PauseRequest?.Invoke(frame);
						}
						catch (Exception exp)
						{
							LogRow(LogType.Error, "Error processing action", exp.ToString());
						}

						LogRow(LogType.File, frame.sessionid, $"{frame.game_clock_display} - {frame.pause.paused_requested_team} team requested a pause");
					}

					if (lastFrame.pause.paused_state == "paused" &&
						frame.pause.paused_state == "unpausing")
					{
						try
						{
							GameUnpaused?.Invoke(frame);
						}
						catch (Exception exp)
						{
							LogRow(LogType.Error, "Error processing action", exp.ToString());
						}

						LogRow(LogType.File, frame.sessionid, $"{frame.game_clock_display} - {frame.pause.paused_requested_team} team unpaused the game");
					}
				}
			}
			catch (Exception e)
			{
				LogRow(LogType.Error, $"Error with pause request parsing\n{e}");
			}


			if (matchData != null)
			{
				// calculate a smoothed server score
				List<int>[] pings =
				{
					frame.teams[0].players.Select(p => p.ping).ToList(),
					frame.teams[0].players.Select(p => p.ping).ToList()
				};
				float newServerScore = CalculateServerScore(pings[0], pings[1]);

				if (pings[0].Count != 4 || pings[1].Count != 4)
				{
					matchData.ServerScore = -2;
					matchData.SmoothedServerScore = matchData.ServerScore;
				}
				else if (newServerScore > 0)
				{
					// reset the smoothing every time it switches to being valid
					if (matchData.ServerScore < 0)
					{
						matchData.ServerScore = newServerScore;
						matchData.SmoothedServerScore = matchData.ServerScore;
					}
					else
					{
						matchData.ServerScore = newServerScore;
						float t = 1f - MathF.Pow(1 - serverScoreSmoothingFactor, deltaTime);
						matchData.SmoothedServerScore = Math2.Lerp(matchData.SmoothedServerScore, matchData.ServerScore, t);
					}
				}
			} else
			{
				LogRow(LogType.Error, "MatchData is null in event generator.");
			}
			


			// while playing and frames aren't identical
			if (frame.game_status == "playing" && deltaTime != 0)
			{
				inPostMatch = false;
				if (SparkSettings.instance.isAutofocusEnabled && (Math.Round(frame.game_clock, 2, MidpointRounding.AwayFromZero) % 10 == 0))
				{
					FocusEchoVR();
				}

				matchData.currentDiskTrajectory.Add(frame.disc.position.ToVector3());

				if (frame.disc.velocity.ToVector3().Equals(Vector3.Zero))
				{
					wasThrown = false;
				}

				// Generate "playing" events
				foreach (Team team in frame.teams)
				{
					foreach (Player player in team.players)
					{
						var lastPlayer = lastFrame.GetPlayer(player.userid);
						if (lastPlayer == null) continue;

						MatchPlayer playerData = matchData.GetPlayerData(player);
						if (playerData != null)
						{
							// update player velocity
							//Vector3 playerSpeed = (player.head.Position - lastPlayer.head.Position) / deltaTime;
							//float speed = playerSpeed.Length();
							Vector3 playerSpeed = player.velocity.ToVector3();
							float speed = playerSpeed.Length();
							playerData.UpdateAverageSpeed(speed);
							playerData.AddRecentVelocity(speed);

							// starting a boost
							float smoothedVel = playerData.GetSmoothedVelocity();
							if (smoothedVel > MatchPlayer.boostVelCutoff)
							{
								// if not already boosting and there are enough values to get a stable reading
								if (!playerData.boosting && playerData.recentVelocities.Count > 10)
								{
									playerData.boosting = true;
								}
							}

							// finished a boost
							if (smoothedVel < MatchPlayer.boostVelStopCutoff && playerData.boosting)
							{
								playerData.boosting = false;

								(float, float) boost = playerData.GetMaxRecentVelocity(reset: true);
								float boostSpeed = boost.Item1;
								float howLongAgoBoost = boost.Item2;

								try
								{
									BigBoost?.Invoke(frame, team, player, boostSpeed, howLongAgoBoost);
								}
								catch (Exception exp)
								{
									LogRow(LogType.Error, "Error processing action", exp.ToString());
								}

								HighlightsHelper.SaveHighlightMaybe(player, frame, "BIG_BOOST");

								matchData.Events.Add(
									new EventData(
										matchData,
										EventData.EventType.big_boost,
										frame.game_clock + howLongAgoBoost,
										team,
										player,
										null,
										player.head.Position,
										new Vector3(boostSpeed, 0, 0)
									)
								);

								LogRow(LogType.File, frame.sessionid,
									frame.game_clock_display + " - " + player.name + " boosted to " +
									boostSpeed.ToString("N1") + " m/s");
							}

							// update hand velocities
							playerData.UpdateAverageSpeedLHand(
								((player.lhand.Position - lastPlayer.lhand.Position) - playerSpeed).Length() /
								deltaTime);
							playerData.UpdateAverageSpeedRHand(
								((player.rhand.Position - lastPlayer.rhand.Position) - playerSpeed).Length() /
								deltaTime);

							// update distance between hands
							//playerData.distanceBetweenHands.Add(Vector3.Distance(player.lhand.ToVector3(), player.rhand.ToVector3()));

							// update distance from hand to head
							float leftHandDistance = Vector3.Distance(player.head.Position, player.lhand.Position);
							float rightHandDistance = Vector3.Distance(player.head.Position, player.rhand.Position);
							playerData.distanceBetweenHands.Add(Math.Max(leftHandDistance, rightHandDistance));


							#region play space abuse

							if (Math.Abs(deltaTime) < .1f)
							{
								// move the playspace based on reported game velocity
								playerData.playspaceLocation += player.velocity.ToVector3() * deltaTime;
							}
							else
							{
								// reset playspace
								playerData.playspaceLocation = player.head.Position;
								LogRow(LogType.Info, "Reset playspace due to framerate");
							}
							// move the playspace towards the current player position
							Vector3 offset = (player.head.Position - playerData.playspaceLocation).Normalized() * .05f * deltaTime;
							// if there is no difference, so normalization doesn't work
							if (double.IsNaN(offset.X))
							{
								offset = Vector3.Zero;
							}
							playerData.playspaceLocation += offset;

							if (team.color != Team.TeamColor.spectator && Math.Abs(smoothDeltaTime) < .1f &&
								Math.Abs(deltaTime) < .1f &&
								Vector3.Distance(player.head.Position, playerData.playspaceLocation) > 1.7f &&
								DateTime.UtcNow - playerData.lastAbuse > TimeSpan.FromSeconds(3) &&    // create a 3 second buffer between detections
								playerData.playspaceInvincibility <= TimeSpan.Zero
								)
							{
								// playspace abuse happened
								try
								{
									PlayspaceAbuse?.Invoke(frame, team, player, playerData.playspaceLocation);
								}
								catch (Exception exp)
								{
									LogRow(LogType.Error, "Error processing action", exp.ToString());
								}

								matchData.Events.Add(
									new EventData(
										matchData,
										EventData.EventType.playspace_abuse,
										frame.game_clock,
										team,
										player,
										null,
										player.head.Position,
										player.head.Position - playerData.playspaceLocation));
								LogRow(LogType.File, frame.sessionid,
									frame.game_clock_display + " - " + player.name + " abused their playspace");
								playerData.PlayspaceAbuses++;

								// reset the playspace so we don't get extra events
								playerData.playspaceLocation = player.head.Position;
							}
							else if (Math.Abs(smoothDeltaTime) > .1f)
							{
								if (ENABLE_LOGGER)
								{
									//Console.WriteLine("Update rate too slow to calculate playspace abuses.");
								}
							}
							playerData.playspaceInvincibility -= TimeSpan.FromSeconds(deltaTime);

							#endregion

							// add time if upside down
							if (Vector3.Dot(player.head.up.ToVector3(), Vector3.UnitY) < 0)
							{
								playerData.InvertedTime += deltaTime;
							}


							playerData.PlayTime += deltaTime;
						}
						else
						{
							LogRow(LogType.Error, "PlayerData is null");
						}


						// check saves 
						if (lastPlayer.stats.saves != player.stats.saves)
						{
							EventData eventData = new EventData(matchData, EventContainer.EventType.save, frame.game_clock,
								team, player, null, player.head.Position, Vector3.Zero);
							try
							{
								Save?.Invoke(frame, eventData);
							}
							catch (Exception exp)
							{
								LogRow(LogType.Error, "Error processing action", exp.ToString());
							}

							matchData.Events.Add(eventData);
							LogRow(LogType.File, frame.sessionid,
								frame.game_clock_display + " - " + player.name + " made a save");
							HighlightsHelper.SaveHighlightMaybe(player, frame, "SAVE");
						}

						// check steals 🕵️‍
						if (lastPlayer.stats.steals != player.stats.steals)
						{
							EventData eventData = new EventData(matchData, EventContainer.EventType.steal, frame.game_clock,
								team, player, null, player.head.Position, Vector3.Zero);
							try
							{
								Steal?.Invoke(frame, eventData);
							}
							catch (Exception exp)
							{
								LogRow(LogType.Error, "Error processing action", exp.ToString());
							}

							matchData.Events.Add(eventData);

							if (WasStealNearGoal(frame.disc.position.ToVector3(), team.color, frame))
							{
								HighlightsHelper.SaveHighlightMaybe(player, frame, "STEAL_SAVE");
								LogRow(LogType.File, frame.sessionid,
									frame.game_clock_display + " - " + player.name + " stole the disk near goal!");
							}
							else
							{
								LogRow(LogType.File, frame.sessionid,
									frame.game_clock_display + " - " + player.name + " stole the disk");
							}
						}

						// check stuns
						if (lastPlayer.stats.stuns != player.stats.stuns)
						{
							// try to match it to an existing stunnee

							// clean up the stun match list
							stunningMatchedPairs.RemoveAll(uat =>
							{
								if (uat[0] != null && uat[0].gameClock - frame.game_clock > stunMatchingTimeout)
									return true;
								else if (uat[1] != null && uat[1].gameClock - frame.game_clock > stunMatchingTimeout)
									return true;
								else return false;
							});

							bool added = false;
							foreach (UserAtTime[] stunEvent in stunningMatchedPairs)
							{
								if (stunEvent[0] == null)
								{
									// if (stunEvent[1].player position is close to the stunner)
									if (stunEvent[1].player.name != player.name)
									{
										stunningMatchedPairs.Remove(stunEvent);

										Player stunner = player;
										Player stunnee = stunEvent[1].player;

										EventData stunEventData = new EventData(matchData, EventData.EventType.stun,
											frame.game_clock, team, stunner, stunnee, stunnee.head.Position,
											Vector3.Zero);
										matchData.Events.Add(stunEventData);
										LogRow(LogType.File, frame.sessionid,
											frame.game_clock_display + " - " + stunner.name + " stunned " +
											stunnee.name);
										added = true;

										try
										{
											Stun?.Invoke(frame, stunEventData);
										}
										catch (Exception exp)
										{
											LogRow(LogType.Error, $"Error processing action\n{exp}");
										}


										break;
									}
								}
							}

							if (!added)
							{
								stunningMatchedPairs.Add(new UserAtTime[]
									{new UserAtTime {gameClock = frame.game_clock, player = player}, null});
							}
						}

						// check getting stunned 
						if (!lastPlayer.stunned && player.stunned)
						{
							// try to match it to an existing stun

							// clean up the stun match list
							stunningMatchedPairs.RemoveAll(uat =>
							{
								if (uat[0] != null && uat[0].gameClock - frame.game_clock > stunMatchingTimeout)
									return true;
								else if (uat[1] != null && uat[1].gameClock - frame.game_clock > stunMatchingTimeout)
									return true;
								else return false;
							});
							bool added = false;
							foreach (UserAtTime[] stunEvent in stunningMatchedPairs)
							{
								if (stunEvent[1] == null)
								{
									// if (stunEvent[0].player position is close to the stunee)
									if (stunEvent[0].player.name != player.name)
									{
										stunningMatchedPairs.Remove(stunEvent);

										Player stunner = stunEvent[0].player;
										Player stunnee = player;

										EventData stunEventData = new EventData(matchData, EventData.EventType.stun,
											frame.game_clock, team, stunner, stunnee, stunnee.head.Position,
											Vector3.Zero);
										matchData.Events.Add(stunEventData);
										LogRow(LogType.File, frame.sessionid,
											frame.game_clock_display + " - " + stunner.name + " stunned " +
											stunnee.name);
										added = true;

										try
										{
											Stun?.Invoke(frame, stunEventData);
										}
										catch (Exception exp)
										{
											LogRow(LogType.Error, "Error processing action", exp.ToString());
										}

										break;
									}
								}
							}

							if (!added)
							{
								stunningMatchedPairs.Add(new UserAtTime[]
									{null, new UserAtTime {gameClock = frame.game_clock, player = player}});
							}
						}

						// check disk was caught 🥊
						if (!lastPlayer.possession && player.possession)
						{
							EventData eventData = new EventData(matchData, EventData.EventType.@catch, frame.game_clock,
								team, player, null, player.head.Position, Vector3.Zero);
							matchData.Events.Add(eventData);
							playerData.Catches++;
							bool caughtThrow = false;
							Team throwTeam = null;
							Player throwPlayer = null;
							bool wasTurnoverCatch = false;

							if (lastThrowPlayerId > 0)
							{
								Frame lframe = lastFrame;

								foreach (Team lteam in lframe.teams)
								{
									foreach (Player lplayer in lteam.players)
									{
										if (lplayer.playerid == lastThrowPlayerId && lplayer.possession)
										{
											caughtThrow = true;
											throwPlayer = lplayer;
											throwTeam = lteam;
											if (lteam.color != team.color)
											{
												wasTurnoverCatch = true;
											}
										}
									}
								}
							}

							if (caughtThrow)
							{
								if (wasTurnoverCatch && lastPlayer.stats.saves == player.stats.saves)
								{
									_ = DelayedCatchEvent(frame, team, player, throwPlayer);
									LogRow(LogType.File, frame.sessionid,
										frame.game_clock_display + " - " + throwPlayer.name +
										" turned over the disk to " + player.name);
									// TODO enable once the db can handle it
									// matchData.Events.Add(new EventData(matchData, EventData.EventType.turnover, frame.game_clock, team, throwPlayer, player, throwPlayer.head.Position, player.head.Position));
									matchData.GetPlayerData(throwPlayer).Turnovers++;
								}
								else
								{
									try
									{
										Pass?.Invoke(frame, team, throwPlayer, player);
									}
									catch (Exception exp)
									{
										LogRow(LogType.Error, "Error processing action", exp.ToString());
									}

									LogRow(LogType.File, frame.sessionid,
										frame.game_clock_display + " - " + player.name + " received a pass from " +
										throwPlayer.name);
									matchData.Events.Add(new EventData(matchData, EventData.EventType.pass,
										frame.game_clock, team, throwPlayer, player, throwPlayer.head.Position,
										player.head.Position));
									matchData.GetPlayerData(throwPlayer).Passes++;
								}
							}
							else
							{
								try
								{
									Catch?.Invoke(frame, team, player);
								}
								catch (Exception exp)
								{
									LogRow(LogType.Error, "Error processing action", exp.ToString());
								}

								LogRow(LogType.File, frame.sessionid,
									frame.game_clock_display + " - " + player.name + " made a catch");
							}
						}

						// check shots taken 🧺
						if (lastPlayer.stats.shots_taken != player.stats.shots_taken)
						{
							try
							{
								ShotTaken?.Invoke(frame, team, player);
							}
							catch (Exception exp)
							{
								LogRow(LogType.Error, "Error processing action", exp.ToString());
							}

							matchData.Events.Add(new EventData(matchData, EventData.EventType.shot_taken,
								frame.game_clock, team, player, null, player.head.Position, Vector3.Zero));
							LogRow(LogType.File, frame.sessionid,
								frame.game_clock_display + " - " + player.name + " took a shot");
							if (lastThrowPlayerId == player.playerid)
							{
								lastThrowPlayerId = -1;
							}
						}



						// check ping went over 150 📶
						if (lastPlayer.ping <= 150 && player.ping > 150)
						{
							try
							{
								LargePing?.Invoke(frame, team, player);
							}
							catch (Exception exp)
							{
								LogRow(LogType.Error, "Error processing action", exp.ToString());
							}

							LogRow(LogType.File, frame.sessionid, frame.game_clock_display + " - " + player.name + " ping went above 150");
						}


						// check disk was thrown ⚾
						if (!wasThrown && player.possession &&
						    (
							    // local throw
							    (
								    frame.client_name == player.name && 
								    frame.last_throw != null && 
								    frame.last_throw.total_speed != 0 && 
								    Math.Abs(frame.last_throw.total_speed - lastFrame.last_throw.total_speed) > .001f
							    )
							    ||
							    // any other player throw
							    (
								    // last and current frame disc vel is nonzero
								    !lastFrame.disc.velocity.ToVector3().Equals(Vector3.Zero) &&
								    !frame.disc.velocity.ToVector3().Equals(Vector3.Zero) &&
								    // disc relative velocity is > 3
								    (frame.disc.velocity.ToVector3() - player.velocity.ToVector3()).Length() > 3
							    )
						    ))
						{
							wasThrown = true;
							lastThrowPlayerId = player.playerid;

							// find out which hand it was thrown by
							bool leftHanded = false;
							var leftHandVelocity = (lastPlayer.lhand.Position - player.lhand.Position) / deltaTime;
							var rightHandVelocity = (lastPlayer.rhand.Position - player.rhand.Position) / deltaTime;

							// based on position of hands
							if (Vector3.Distance(lastPlayer.lhand.Position, lastFrame.disc.position.ToVector3()) <
								Vector3.Distance(lastPlayer.rhand.Position, lastFrame.disc.position.ToVector3()))
							{
								leftHanded = true;
							}

							// find out underhandedness
							float underhandedness =
								Vector3.Distance(lastPlayer.lhand.Position, lastFrame.disc.position.ToVector3()) <
								Vector3.Distance(lastPlayer.rhand.Position, lastFrame.disc.position.ToVector3())
									? Vector3.Dot(lastPlayer.head.up.ToVector3(),
										lastPlayer.lhand.Position - lastPlayer.head.Position)
									: Vector3.Dot(lastPlayer.head.up.ToVector3(),
										lastPlayer.rhand.Position - lastPlayer.head.Position);

							// wait to actually log this throw to get more accurate velocity
							_ = DelayedThrowEvent(player, leftHanded, underhandedness,
								frame.disc.velocity.ToVector3().Length());
						}

						// TODO check if a pass was made
						if (false)
						{
							matchData.Events.Add(new EventData(matchData, EventData.EventType.pass, frame.game_clock,
								team, player, null, player.head.Position, Vector3.Zero));
							LogRow(LogType.File, frame.sessionid,
								frame.game_clock_display + " - " + player.name + " made a pass");
						}
					}
				}


				// generate general playing events (not player-specific)

				try
				{
					// check blue restart request ↩
					if (!lastFrame.blue_team_restart_request && frame.blue_team_restart_request)
					{
						try
						{
							RestartRequest?.Invoke(frame, Team.TeamColor.blue);
						}
						catch (Exception exp)
						{
							LogRow(LogType.Error, "Error processing action", exp.ToString());
						}

						matchData.Events.Add(new EventData(matchData, EventData.EventType.restart_request,
							lastFrame.game_clock, frame.teams[(int)Team.TeamColor.blue], null, null, Vector3.Zero,
							Vector3.Zero));
						LogRow(LogType.File, frame.sessionid,
							frame.game_clock_display + " - " + "blue team restart request");
					}

					// check orange restart request ↩
					if (!lastFrame.orange_team_restart_request && frame.orange_team_restart_request)
					{
						try
						{
							RestartRequest?.Invoke(frame, Team.TeamColor.orange);
						}
						catch (Exception exp)
						{
							LogRow(LogType.Error, "Error processing action", exp.ToString());
						}

						matchData.Events.Add(new EventData(matchData, EventData.EventType.restart_request,
							lastFrame.game_clock, frame.teams[(int)Team.TeamColor.orange], null, null, Vector3.Zero,
							Vector3.Zero));
						LogRow(LogType.File, frame.sessionid,
							frame.game_clock_display + " - " + "orange team restart request");
					}
				}
				catch (Exception)
				{
					LogRow(LogType.Error, "Error with restart request parsing");
				}
			}
		}


		public static void FindTeamNamesFromPlayerList(MatchData matchDataLocal, Team team)
		{
			//if (frame.private_match)
			{
				if (team.players.Count > 0 && matchDataLocal != null)
				{
					GetRequestCallback(
						$"{API_URL_2}get_team_name_from_list?player_list=[{string.Join(',', team.player_names.Select(name => $"\"{name}\""))}]",
						new Dictionary<string, string> { { "x-api-key", DiscordOAuth.igniteUploadKey } },
						returnJSON =>
						{
							try
							{
								if (!string.IsNullOrEmpty(returnJSON))
								{
									Dictionary<string, string> data = JsonConvert.DeserializeObject<Dictionary<string, string>>(returnJSON);
									if (data != null)
									{
										// if there are at least 2 players from that team
										if (data.ContainsKey("count") && int.Parse(data["count"]) >= 2)
										{
											matchDataLocal.teams[team.color].vrmlTeamName = data["team_name"];
											matchDataLocal.teams[team.color].vrmlTeamLogo = data["team_logo"];
										}
										else // reset the names if people leave
										{
											matchDataLocal.teams[team.color].vrmlTeamName = string.Empty;
											matchDataLocal.teams[team.color].vrmlTeamLogo = string.Empty;
										}
									}
								}
							}
							catch (Exception ex)
							{
								LogRow(LogType.Error, $"Can't parse get_team_name_from_list response: {ex}");
							}
						}
					);
				}
			}
		}


		// 💨
		private static async Task JoustDetection(Frame firstFrame, EventData.EventType eventType, Team.TeamColor side)
		{
			float startGameClock = firstFrame.game_clock;
			float maxTubeExitSpeed = 0;
			float maxSpeed = 0;
			List<string> playersWhoExitedTube = new List<string>();

			int interval = 8; // time between checks - ms. This is faster than cap rate just to be sure.
			float maxJoustTimeTimeout = 10000; // time before giving up calculating joust time - ms
			for (int i = 0; i < maxJoustTimeTimeout / interval; i++)
			{
				Frame frame = lastFrame;

				if (i > 0 && frame.game_status != "playing") return;

				Team team = frame.teams[(int)side];
				foreach (Player player in team.players)
				{
					float speed = player.velocity.ToVector3().Length();
					// if the player exited the tube for the first time
					if (player.head.Position.Z * ((int)side * 2 - 1) < 40 && !playersWhoExitedTube.Contains(player.name))
					{
						playersWhoExitedTube.Add(player.name);
						if (speed > maxTubeExitSpeed)
						{
							maxTubeExitSpeed = speed;
						}
					}

					if (speed > maxSpeed)
					{
						maxSpeed = speed;
					}

					// if the player crossed the centerline - joust finished 🏁
					if (player.head.Position.Z * ((int)side * 2 - 1) < 0)
					{

						EventData joustEvent = new EventData(
							matchData,
							eventType,
							frame.game_clock,
							team,
							player,
							(long)((startGameClock - frame.game_clock) * 1000),
							Vector3.Zero,
							new Vector3(
								maxSpeed,
								maxTubeExitSpeed,
								startGameClock - frame.game_clock)
						);


						matchData.Events.Add(joustEvent);
						LogRow(LogType.File, frame.sessionid, frame.game_clock_display + " - " +
															  team.color.ToString() +
															  " team joust time" +
															  (eventType == EventData.EventType.defensive_joust ? " (defensive)" : "") +
															  ": " +
															  (startGameClock - frame.game_clock)
															  .ToString("N2") +
															  " s, Max speed: " +
															  maxSpeed.ToString("N2") +
															  " m/s, Tube Exit Speed: " +
															  maxTubeExitSpeed.ToString("N2") + " m/s");

						try
						{
							Joust?.Invoke(frame, team, player, eventType == EventContainer.EventType.joust_speed, startGameClock - frame.game_clock, maxSpeed, maxTubeExitSpeed);
							JoustEvent?.Invoke(frame, joustEvent);
						}
						catch (Exception exp)
						{
							LogRow(LogType.Error, "Error processing action", exp.ToString());
						}

						lastJousts.Enqueue(joustEvent);
						if (lastJousts.Count > 100)
						{
							lastJousts.TryDequeue(out EventData joust);
						}

						return;
					}
				}
				await Task.Delay(interval);
			}
		}


		private static async Task DelayedCatchEvent(Frame originalFrame, Team originalTeam, Player originalPlayer, Player throwPlayer)
		{
			// TODO look through again
			// wait some time before checking if a save happened (then it wouldn't be an interception)
			await Task.Delay(2000);

			Frame frame = lastFrame;
			
			// we may have gone past the end of the match
			if (matchData == null) return;

			foreach (Team team in frame.teams)
			{
				foreach (Player player in team.players)
				{
					if (player.playerid != originalPlayer.playerid) continue;
					if (player.stats.saves != originalPlayer.stats.saves) continue;

					EventData eventData = new EventData(
						matchData, 
						EventContainer.EventType.interception, 
						frame.game_clock, 
						team, player, null, 
						player.head.Position, 
						Vector3.Zero
						);
					try
					{
						Interception?.Invoke(originalFrame, originalTeam, throwPlayer, originalPlayer);
					}
					catch (Exception exp)
					{
						LogRow(LogType.Error, "Error processing action", exp.ToString());
					}

					LogRow(LogType.File, frame.sessionid,
						frame.game_clock_display + " - " + player.name + " intercepted a throw from " +
						throwPlayer.name);
					// TODO enable this once the db supports it
					// matchData.Events.Add(eventData);
					MatchPlayer otherMatchPlayer = matchData.GetPlayerData(player);
					if (otherMatchPlayer != null) otherMatchPlayer.Interceptions++;
					else LogRow(LogType.Error, "Can't find player by name from other team: " + player.name);

					HighlightsHelper.SaveHighlightMaybe(player, frame, "INTERCEPTION");
				}
			}
		}

		private static async Task DelayedThrowEvent(Player originalPlayer, bool leftHanded, float underhandedness, float origSpeed)
		{
			// wait some time before re-checking the throw velocity
			await Task.Delay(150);

			// throw expired
			if (matchData == null) return;

			Frame frame = lastFrame;

			foreach (var team in frame.teams)
			{
				foreach (var player in team.players)
				{
					if (player.playerid == originalPlayer.playerid)
					{
						if (player.possession && !frame.disc.velocity.ToVector3().Equals(Vector3.Zero))
						{
							try
							{
								Throw?.Invoke(frame, team, player, leftHanded, underhandedness);
							}
							catch (Exception exp)
							{
								LogRow(LogType.Error, "Error processing action", exp.ToString());
							}

							matchData.Events.Add(new EventData(matchData, EventData.EventType.@throw, frame.game_clock,
								team, player, null, player.head.Position, frame.disc.velocity.ToVector3()));
							LogRow(LogType.File, frame.sessionid, frame.game_clock_display + " - " + player.name +
																  " threw the disk at " +
																  frame.disc.velocity.ToVector3().Length()
																	  .ToString("N2") + " m/s with their " +
																  (leftHanded ? "left" : "right") + " hand");
							matchData.currentDiskTrajectory.Clear();

							// add throw data type
							matchData.Throws.Add(new ThrowData(matchData, frame.game_clock, player,
								frame.disc.position.ToVector3(), frame.disc.velocity.ToVector3(), leftHanded,
								underhandedness));

							//if (SparkSettings.instance.throwSpeedTTS)
							//{
							//	if (player.name == frame.client_name)
							//	{
							//		synth.SpeakAsync("" + player.velocity.ToVector3().Length().ToString("N1"));
							//	}
							//}
						}
						else
						{
							Console.WriteLine("Disc already caught");
						}
					}
				}
			}
		}

		/// <summary>
		/// Function used to excute certain behavior based on frame given and previous frame(s).
		/// </summary>
		/// <param name="frame"></param>
		/// <param name="deltaTime"></param>
		private static void ProcessGameStateChange(Frame frame, float deltaTime)
		{
			LogRow(LogType.File, frame.sessionid, frame.game_clock_display + " - Entered state: " + frame.game_status);

			switch (frame.game_status)
			{
				case "pre_match":
					// if we just came from a playing state, this was a reset - requires a high enough polling rate
					if (lastFrame.game_status == "playing" || lastFrame.game_status == "round_start")
					{
						Frame frameToUse = lastLastFrame;
						if (lastValidSumOfStatsAge < 30)
						{
							frameToUse = lastValidStatsFrame;
						}

						EventMatchFinished(frameToUse, MatchData.FinishReason.reset, lastFrame.game_clock);

						try
						{
							MatchReset?.Invoke(frameToUse);
						}
						catch (Exception exp)
						{
							LogRow(LogType.Error, "Error processing action", exp.ToString());
						}
					}

					// Autofocus
					if (SparkSettings.instance.isAutofocusEnabled)
					{
						FocusEchoVR();
					}
					break;

				// round began
				case "round_start":
					inPostMatch = false;

					// if we just started a new 'round' (so stats haven't been reset)
					if (lastFrame.game_status == "round_over")
					{
						UpdateStatsIngame(frame, false, false);

						foreach (MatchPlayer player in matchData.players.Values)
						{
							Player p = new Player
							{
								userid = player.Id,
								name = player.Name
							};

							// TODO isn't this just a shallow copy anyway and won't do anything? How is this working?
							MatchPlayer lastPlayer = lastMatchData.GetPlayerData(p);

							if (lastPlayer != null)
							{
								player.StoreLastRoundStats(lastPlayer);
							}
							else
							{
								LogRow(LogType.Error, "Player exists in this round but not in last. Y");
							}
						}
						matchData.round++;
					}
					// Autofocus
					if (SparkSettings.instance.isAutofocusEnabled)
					{
						FocusEchoVR();
					}

					UpdateStatsIngame(frame);

					break;

				// round really began
				case "playing":

					#region Started Playing

					UpdateStatsIngame(frame);

					// Autofocus
					if (SparkSettings.instance.isAutofocusEnabled)
					{
						FocusEchoVR();
					}

					// Loop through teams.
					foreach (var team in frame.teams)
					{
						// Loop through players on team.
						foreach (var player in team.players)
						{
							// reset playspace
							MatchPlayer playerData = matchData.GetPlayerData(player);
							if (playerData != null)
							{
								playerData.playspaceLocation = player.head.Position;
							}
						}
					}

					// start a joust detection
					if (lastFrame.game_status == "round_start")
					{
						float zDiscPos = frame.disc.position.ToVector3().Z;
						// if the disc is in the center of the arena, neutral joust
						if (Math.Abs(zDiscPos) < .1f)
						{
							_ = JoustDetection(frame, EventData.EventType.joust_speed, Team.TeamColor.blue);
							_ = JoustDetection(frame, EventData.EventType.joust_speed, Team.TeamColor.orange);
						}

						// if the disc is on the orange nest
						else if (Math.Abs(zDiscPos + 27.5f) < 1)
						{
							_ = JoustDetection(frame, EventData.EventType.defensive_joust, Team.TeamColor.orange);
						}


						// if the disc is on the blue nest
						else if (Math.Abs(zDiscPos - 27.5f) < 1)
						{
							_ = JoustDetection(frame, EventData.EventType.defensive_joust, Team.TeamColor.blue);
						}
					}

					#endregion

					break;

				// just scored
				case "score":

					#region Process Score

					_ = ProcessScore(matchData);
					#endregion

					break;

				case "round_over":
					if (frame.blue_points == frame.orange_points)
					{
						// OVERTIME
						LogRow(LogType.Info, "overtime");
					}
					// mercy win
					else if (!frame.last_score.Equals(lastFrame.last_score))
					{
						// TODO check if the score actually changes the same frame the game ends
						_ = ProcessScore(matchData);

						EventMatchFinished(frame, MatchData.FinishReason.mercy, lastFrame.game_clock);
					}
					else if (frame.game_clock == 0 || lastFrame.game_clock < deltaTime * 10 || deltaTime < 0)
					{
						EventMatchFinished(frame, MatchData.FinishReason.game_time, 0);
					}
					else if (lastFrame.game_clock < deltaTime * 10 || lastFrame.game_status == "post_sudden_death" ||
							 deltaTime < 0)
					{
						// TODO add the score that ends an overtime
						// ProcessScore(frame); 

						// TODO find why finished and set reason
						EventMatchFinished(frame, MatchData.FinishReason.not_finished, lastFrame.game_clock);
					}
					else
					{
						EventMatchFinished(frame, MatchData.FinishReason.not_finished, lastFrame.game_clock);
					}

					break;

				// Game finished and showing scoreboard
				case "post_match":
					if (frame.private_match)
					{
						RoundOver?.Invoke(frame);
					}

					//EventMatchFinished(frame, MatchData.FinishReason.not_finished);
					break;

				case "pre_sudden_death":
					// Autofocus
					if (SparkSettings.instance.isAutofocusEnabled)
					{
						FocusEchoVR();
					}
					LogRow(LogType.Error, "pre_sudden_death");
					break;
				case "sudden_death":
					// Autofocus
					if (SparkSettings.instance.isAutofocusEnabled)
					{
						FocusEchoVR();
					}
					// this happens right as the match finishes in a tie
					matchData.overtimeCount++;
					break;
				case "post_sudden_death":
					LogRow(LogType.Error, "post_sudden_death");
					break;
			}
		}

		private static bool WasStealNearGoal(Vector3 disckPos, Team.TeamColor playerTeamColor, Frame frame)
		{
			float stealSaveRadius = 2.2f;

			float goalXPos = 0f;
			float goalYPos = 0f;
			float goalZPos = 36f;
			if (playerTeamColor == Team.TeamColor.blue)
			{
				goalZPos *= -1f;
			}

			int x1 = (int)Math.Pow((disckPos.X - goalXPos), 2);
			int y1 = (int)Math.Pow((disckPos.Y - goalYPos), 2);
			int z1 = (int)Math.Pow((disckPos.Z - goalZPos), 2);

			// distance between the 
			// centre and given point 
			float distanceFromGoalCenter = x1 + y1 + z1;
			//LogRow(LogType.File, frame.sessionid, "Steal distance from goal: " + distanceFromGoalCenter + " X" + disckPos.X + " Y" + disckPos.Y + " Z" + disckPos.Z + " " + playerTeamColor);
			if (distanceFromGoalCenter > (stealSaveRadius * stealSaveRadius))
			{
				return false;
			}
			else
			{
				return true;
			}
		}

		/// <summary>
		/// Function used to extracted data from frame given
		/// Delays a bit to avoid disk extrapolation
		/// </summary>
		private static async Task ProcessScore(MatchData matchData)
		{
			Frame initialFrame = lastLastFrame;
			GoalImmediate?.Invoke(lastFrame);

			// wait some time before re-checking the throw velocity
			await Task.Delay(100);

			Frame frame = lastFrame;

			Vector3 discVel = initialFrame.disc.velocity.ToVector3();
			Vector3 discPos = initialFrame.disc.position.ToVector3();
			Vector2 goalPos;
			bool backboard = false;
			float angleIntoGoal = 0;
			if (discVel != Vector3.Zero)
			{
				float angleIntoGoalRad =
					(float)(Math.Acos(Vector3.Dot(discVel, new Vector3(0, 0, 1) * (discPos.Z < 0 ? -1 : 1)) /
									   discVel.Length()));
				angleIntoGoal = (float)(angleIntoGoalRad * (180 / Math.PI));

				// make the angle negative if backboard
				if (angleIntoGoal > 90)
				{
					angleIntoGoal = 180 - angleIntoGoal;
					backboard = true;
				}
			}

			goalPos = new Vector2(initialFrame.disc.position.ToVector3().X, initialFrame.disc.position.ToVector3().Y);

			// nvidia highlights
			if (!HighlightsHelper.SaveHighlightMaybe(frame.last_score.person_scored, frame, "SCORE"))
			{
				HighlightsHelper.SaveHighlightMaybe(frame.last_score.assist_scored, frame, "ASSIST");
			}

			// Call the Score event
			LogRow(LogType.File, frame.sessionid,
				frame.game_clock_display + " - " + frame.last_score.person_scored + " scored at " +
				frame.last_score.disc_speed.ToString("N2") + " m/s from " +
				frame.last_score.distance_thrown.ToString("N2") + " m away" +
				(frame.last_score.assist_scored == "[INVALID]"
					? "!"
					: (", assisted by " + frame.last_score.assist_scored + "!")));
			LogRow(LogType.File, frame.sessionid,
				frame.game_clock_display + " - Goal angle: " + angleIntoGoal.ToString("N2") + "deg, from " +
				(backboard ? "behind" : "the front"));

			// show the scores in the log
			LogRow(LogType.File, frame.sessionid,
				frame.game_clock_display + " - ORANGE: " + frame.orange_points + "  BLUE: " + frame.blue_points);

			Player scorer = frame.GetPlayer(frame.last_score.person_scored);
			MatchPlayer scorerPlayerData = matchData.GetPlayerData(scorer);
			if (scorer != null)
			{
				if (scorerPlayerData != null)
				{
					if (frame.last_score.point_amount == 2)
					{
						scorerPlayerData.TwoPointers++;
					}
					else
					{
						scorerPlayerData.ThreePointers++;
					}

					scorerPlayerData.GoalsNum++;
				}
			}

			// these are nullable types
			bool? leftHanded = null;
			float? underhandedness = null;
			if (matchData.Throws.Count > 0)
			{
				var lastThrow = matchData.Throws.Last();
				if (lastThrow != null)
				{
					leftHanded = lastThrow.isLeftHanded;
					underhandedness = lastThrow.underhandedness;
				}
			}

			GoalData goalEvent = new GoalData(
				matchData,
				scorer,
				frame.last_score,
				frame.game_clock,
				goalPos,
				angleIntoGoal,
				backboard,
				discPos.Z < 0 ? Team.TeamColor.blue : Team.TeamColor.orange,
				leftHanded,
				underhandedness,
				matchData.currentDiskTrajectory
			);
			matchData.Goals.Add(goalEvent);
			lastGoals.Enqueue(goalEvent);
			if (lastGoals.Count > 30)
			{
				lastGoals.TryDequeue(out GoalData goal);
			}

			try
			{
				Goal?.Invoke(frame, goalEvent);
			}
			catch (Exception exp)
			{
				LogRow(LogType.Error, "Error processing action", exp.ToString());
			}


			try
			{
				if (frame.GetPlayer(frame.last_score.assist_scored) != null)
				{
					Assist?.Invoke(frame, goalEvent);
				}
			}
			catch (Exception exp)
			{
				LogRow(LogType.Error, "Error processing action", exp.ToString());
			}

			UpdateStatsIngame(frame, allowUpload: false);
		}

		/// <summary>
		/// Can be called often to update the ingame player stats
		/// </summary>
		/// <param name="frame">The current frame</param>
		/// <param name="endOfMatch"></param>
		/// <param name="allowUpload"></param>
		/// <param name="manual"></param>
		public static void UpdateStatsIngame(Frame frame, bool endOfMatch = false, bool allowUpload = true, bool manual = false)
		{
			if (inPostMatch || matchData == null)
			{
				return;
			}

			// team names may have changed
			matchData.teams[Team.TeamColor.blue].teamName = frame.teams[0].team;
			matchData.teams[Team.TeamColor.orange].teamName = frame.teams[1].team;
			matchData.teams[Team.TeamColor.spectator].teamName = frame.teams[2].team;

			if (frame.teams[0].stats != null)
			{
				matchData.teams[Team.TeamColor.blue].points = frame.blue_points;
			}

			if (frame.teams[1].stats != null)
			{
				matchData.teams[Team.TeamColor.orange].points = frame.orange_points;
			}

			UpdateAllPlayers(frame);

			// if end of match upload
			if (endOfMatch)
			{
				UploadMatchBatch(true);
			}
			// if during-match upload
			else if (manual || (!DiscordOAuth.Personal && DiscordOAuth.AccessCode.series_name != "ignitevr"))
			{
				UploadMatchBatch(false);
			}
		}

		/// <summary>
		/// Function to wrap up the match once we've entered post_match, restarted, or left spectate unexpectedly (crash)
		/// </summary>
		/// <param name="frame"></param>
		/// <param name="reason"></param>
		/// <param name="endTime"></param>
		private static void EventMatchFinished(Frame frame, MatchData.FinishReason reason, float endTime = 0)
		{
			matchData.endTime = endTime;
			matchData.finishReason = reason;

			if (frame == null)
			{
				// this happened on a restart right in the beginning once
				LogRow(LogType.Error, "frame is null on match finished event. INVESTIGATE");
				return;
			}

			LogRow(LogType.File, frame.sessionid, "Match Finished: " + reason);
			UpdateStatsIngame(frame, true);

			lastMatches.Enqueue(matchData);
			if (lastMatches.Count > 50)
			{
				lastMatches.TryDequeue(out MatchData match);
			}

			lastMatchData = matchData;
			matchData = null;

			inPostMatch = true;

			// show the scores in the log
			LogRow(LogType.File, frame.sessionid,
				frame.game_clock_display + " - ORANGE: " + frame.orange_points + "  BLUE: " + frame.blue_points);
		}

		public static void UploadMatchBatch(bool final = false)
		{
			if (!SparkSettings.instance.uploadToIgniteDB)
			{
				Console.WriteLine("Won't upload right now.");
			}

			BatchOutputFormat data = new BatchOutputFormat
			{
				final = final,
				match_data = matchData.ToDict()
			};
			matchData.players.Values.ToList().ForEach(e =>
			{
				if (e.Name != "anonymous") data.match_players.Add(e.ToDict());
			});

			matchData.Events.ForEach(e =>
			{
				if (!e.inDB) data.events.Add(e.ToDict());
				e.inDB = true;
			});
			matchData.Goals.ForEach(e =>
			{
				if (!e.inDB) data.goals.Add(e.ToDict());
				e.inDB = true;
			});
			matchData.Throws.ForEach(e =>
			{
				if (!e.inDB) data.throws.Add(e.ToDict());
				e.inDB = true;
			});

			string dataString = JsonConvert.SerializeObject(data);
			string hash;
			using (SHA256 sha = SHA256.Create())
			{
				byte[] rawHash = sha.ComputeHash(Encoding.ASCII.GetBytes(dataString + matchData.firstFrame.client_name));

				// Convert the byte array to hexadecimal string
				StringBuilder sb = new();
				foreach (byte b in rawHash)
				{
					sb.Append(b.ToString("X2"));
				}

				hash = sb.ToString().ToLower();
			}

			if (SparkSettings.instance.uploadToIgniteDB || DiscordOAuth.AccessCode.series_name.Contains("vrml"))
			{
				_ = DoUploadMatchBatchIgniteDB(dataString, hash, matchData.firstFrame.client_name);
			}
			
			// upload tablet stats as well
			AutoUploadTabletStats();
		}

		static async Task DoUploadMatchBatchIgniteDB(string data, string hash, string client_name)
		{
			client.DefaultRequestHeaders.Remove("x-api-key");
			client.DefaultRequestHeaders.Add("x-api-key", DiscordOAuth.igniteUploadKey);

			client.DefaultRequestHeaders.Remove("access-code");
			client.DefaultRequestHeaders.Add("access-code", DiscordOAuth.AccessCode.series_name);

			StringContent content = new StringContent(data, Encoding.UTF8, "application/json");

			try
			{
				HttpResponseMessage response =
					await client.PostAsync("add_data?hashkey=" + hash + "&client_name=" + client_name, content);
				LogRow(LogType.Info, "[DB][Response] " + response.Content.ReadAsStringAsync().Result);
			}
			catch
			{
				LogRow(LogType.Error, "Can't connect to the DB server");
			}
		}


		// Update existing player with stats from game.
		private static void UpdateSinglePlayer(Frame frame, Team team, Player player, int won)
		{
			if (!matchData.teams.ContainsKey(team.color))
			{
				LogRow(LogType.Error, "No team. Wat.");
				return;
			}

			if (player.stats == null)
			{
				LogRow(LogType.Error, "Player stats are null. Maybe in lobby?");
				return;
			}

			TeamData teamData = matchData.teams[team.color];

			// add a new match player if they didn't exist before
			if (!matchData.players.ContainsKey(player.name))
			{
				matchData.players.Add(player.name, new MatchPlayer(matchData, teamData, player));
			}

			if (player.name != "anonymous")
			{
				MatchPlayer playerData = matchData.players[player.name];
				playerData.teamData = teamData;

				playerData.Level = player.level;
				playerData.Number = player.number;
				playerData.PossessionTime = player.stats.possession_time;
				playerData.Points = player.stats.points;
				playerData.ShotsTaken = player.stats.shots_taken;
				playerData.Saves = player.stats.saves;
				// playerData.GoalsNum = player.stats.goals;	// disabled in favor of manual increment because the api is broken here
				// playerData.Passes = player.stats.passes;		// api reports 0
				// playerData.Catches = player.stats.catches;  	// api reports 0
				playerData.Steals = player.stats.steals;
				playerData.Stuns = player.stats.stuns;
				playerData.Blocks = player.stats.blocks; // api reports 0
														 // playerData.Interceptions = player.stats.interceptions;	// api reports 0
				playerData.Assists = player.stats.assists;
				playerData.Won = won;
			}
		}

		static void UpdateAllPlayers(Frame frame)
		{
			// Loop through teams.
			foreach (Team team in frame.teams)
			{
				// Loop through players on team.
				foreach (Player player in team.players)
				{
					Team.TeamColor winningTeam = frame.blue_points > frame.orange_points ? Team.TeamColor.blue : Team.TeamColor.orange;
					int won = team.color == winningTeam ? 1 : 0;

					UpdateSinglePlayer(frame, team, player, won);
				}
			}
		}


		/// <summary>
		/// Generic method for getting data from a web url
		/// </summary>
		/// <param name="headers">Key-value pairs for headers. Leave null if none.</param>
		public static void GetRequestCallback(string uri, Dictionary<string, string> headers, Action<string> callback)
		{
			Task.Run(async () =>
			{
				string resp = await GetRequestAsync(uri, headers);
				callback(resp);
			});
		}

		/// <summary>
		/// Generic method for getting data from a web url
		/// </summary>
		/// <param name="uri">The URL to GET</param>
		/// <param name="headers">Key-value pairs for headers. Leave null if none.</param>
		public static async Task<string> GetRequestAsync(string uri, Dictionary<string, string> headers)
		{
			try
			{
				HttpWebRequest request = (HttpWebRequest) WebRequest.Create(uri);
				if (headers != null)
				{
					foreach ((string key, string value) in headers)
					{
						request.Headers[key] = value;
					}
				}

				request.UserAgent = $"Spark/{AppVersionString()}";
				using WebResponse response = await request.GetResponseAsync();
				await using Stream stream = response.GetResponseStream();
				if (stream != null)
				{
					using StreamReader reader = new StreamReader(stream);
					return await reader.ReadToEndAsync();
				}
			}
			catch (Exception e)
			{
				Console.WriteLine($"Can't get data\n{e}");
			}
			
			return string.Empty;
		}
		
		/// <summary>
		/// Generic method for getting data from a web url. Returns a Stream
		/// </summary>
		/// <param name="uri">The URL to GET</param>
		/// <param name="headers">Key-value pairs for headers. Leave null if none.</param>
		public static async Task<Stream> GetRequestAsyncStream(string uri, Dictionary<string, string> headers)
		{
			try
			{
				HttpWebRequest request = (HttpWebRequest) WebRequest.Create(uri);
				if (headers != null)
				{
					foreach ((string key, string value) in headers)
					{
						request.Headers[key] = value;
					}
				}

				request.UserAgent = $"Spark/{AppVersionString()}";
				using WebResponse response = await request.GetResponseAsync();
				return response.GetResponseStream();
			}
			catch (Exception e)
			{
				Console.WriteLine($"Can't get data\n{e}");
			}
			
			return null;
		}

		/// <summary>
		/// Generic method for posting data to a web url
		/// </summary>
		/// <param name="headers">Key-value pairs for headers. Leave null if none.</param>
		public static void PostRequestCallback(string uri, Dictionary<string, string> headers, string body, Action<string> callback)
		{
			Task.Run(async () =>
			{
				string resp = await PostRequestAsync(uri, headers, body);
				callback?.Invoke(resp);
			});
		}

		/// <summary>
		/// Generic method for posting data to a web url
		/// </summary>
		/// <param name="headers">Key-value pairs for headers. Leave null if none.</param>
		public static async Task<string> PostRequestAsync(string uri, Dictionary<string, string> headers, string body)
		{
			try
			{
				if (headers != null)
				{
					foreach (KeyValuePair<string, string> header in headers)
					{
						client.DefaultRequestHeaders.Remove(header.Key);
						client.DefaultRequestHeaders.Add(header.Key, header.Value);
					}
				}
				StringContent content = new StringContent(body, Encoding.UTF8, "application/json");
				HttpResponseMessage response = await client.PostAsync(uri, content);
				return await response.Content.ReadAsStringAsync();
			}
			catch (Exception e)
			{
				Console.WriteLine($"Can't get data\n{e}");
				return string.Empty;
			}
		}


		private static void RegisterUriScheme(string UriScheme, string FriendlyName)
		{
			try
			{
				string applicationLocation = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Spark.exe");
				// LogRow(LogType.Error, $"[URI ASSOC] Spark path: {applicationLocation}");

				GetKey(UriScheme, out RegistryKey key, out RegistryKey defaultIcon, out RegistryKey commandKey);

				key.SetValue("", "URL:" + FriendlyName);
				key.SetValue("URL Protocol", "");
				defaultIcon.SetValue("", applicationLocation + ",1");
				commandKey.SetValue("", "\"" + applicationLocation + "\" \"%1\"");

//				// refetch the key
//				GetKey(UriScheme, out key, out defaultIcon, out commandKey);
//				string actualValue = (string)commandKey.GetValue("");

//				LogRow(LogType.Error, $"[URI ASSOC] {UriScheme} path: {actualValue}");

//				if (!actualValue.Contains(applicationLocation))
//				{
//					Process.Start(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SparkLinkLauncher.exe"));
//				} else
//				{
//#if WINDOWS_STORE_RELEASE
//					Process.Start(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SparkLinkLauncher.exe"));
//#endif
//				}



			}
			catch (Exception e)
			{
				LogRow(LogType.Error, $"Failed to set URI scheme\n{e}");
			}

			static void GetKey(string UriScheme, out RegistryKey key, out RegistryKey defaultIcon, out RegistryKey commandKey)
			{
				key = Registry.CurrentUser.CreateSubKey("SOFTWARE\\Classes\\" + UriScheme);
				defaultIcon = key.CreateSubKey("DefaultIcon");
				commandKey = key.CreateSubKey(@"shell\open\command");
			}
		}

		private static bool CheckIfLaunchedWithCustomURLHandlerParam(string[] args)
		{
			if (args.Length <= 0 || (!args[0].Contains("ignitebot://") && !args[0].Contains("atlas://") && !args[0].Contains("spark://"))) return false;

			// join a match directly	
			string[] parts = args[0].Split('/');
			if (parts.Length != 4)
			{
				LogRow(LogType.Error, "ERROR 3452. Incorrectly formatted Spark or Atlas link");
				new MessageBox(
					$"{Resources.Incorrectly_formatted_Spark_or_Atlas_link_}\n{Resources.wrong_number_of_____characters_for_link_}\n{args[0]}\n{parts.Length}",
					Resources.Error, Quit).Show();
			}

			bool spectating = false;
			switch (parts[2])
			{
				case "spectate":
				case "spectator":
				case "s":
					spectating = true;
					break;
				case "join":
				case "player":
				case "j":
				case "p":
					spectating = false;
					break;
				case "choose":
				case "c":
					// hand the whole thing off to the popup window
					new ChooseJoinTypeDialog(parts[3]).Show();
					return true;
				default:
					LogRow(LogType.Error, "ERROR 8675. Incorrectly formatted Spark or Atlas link");
					new MessageBox($"{Resources.Incorrectly_formatted_Spark_or_Atlas_link_}\n{Resources.Incorrect_join_type_}", Resources.Error, Quit)
						.Show();
					return true;
			}


			// start client
			string echoPath = SparkSettings.instance.echoVRPath;
			if (!string.IsNullOrEmpty(echoPath))
			{
				Process.Start(echoPath, (SparkSettings.instance.capturevp2 ? "-capturevp2 " : " ") + (spectating ? "-spectatorstream " : " ") + "-lobbyid " + parts[3]);
				Quit();
			}
			else
			{
				new MessageBox(Resources.echovr_path_not_set, Resources.Error, Quit).Show();
			}

			return true;
		}
		

		private static string[] GetLatestSpeakerSystemURLVer()
		{
			string[] ret = new string[2];
			try
			{
				HttpWebRequest req = (HttpWebRequest)WebRequest.Create(@"https://api.github.com/repos/iblowatsports/Echo-VR-Speaker-System/releases/latest");
				req.Accept = "application/json";
				req.UserAgent = "Spark";

				WebResponse resp = req.GetResponse();
				Stream ds = resp.GetResponseStream();
				StreamReader sr = new StreamReader(ds);

				// Session Contents
				string textResp = sr.ReadToEnd();
				VersionJson versionJson = JsonConvert.DeserializeObject<VersionJson>(textResp);
				ret[0] = versionJson.assets.First(url => url.browser_download_url.EndsWith("exe")).browser_download_url;
				ret[1] = versionJson.tag_name;
			}
			catch (Exception e)
			{
				LogRow(LogType.Error, e.Message);
			}
			return ret;
		}
		
		#region IP
		
		// The max number of physical addresses.
		const int MAXLEN_PHYSADDR = 8;

		// Define the MIB_IPNETROW structure.
		[StructLayout(LayoutKind.Sequential)]
		struct MIB_IPNETROW
		{
			[MarshalAs(UnmanagedType.U4)]
			public int dwIndex;
			[MarshalAs(UnmanagedType.U4)]
			public int dwPhysAddrLen;
			[MarshalAs(UnmanagedType.U1)]
			public byte mac0;
			[MarshalAs(UnmanagedType.U1)]
			public byte mac1;
			[MarshalAs(UnmanagedType.U1)]
			public byte mac2;
			[MarshalAs(UnmanagedType.U1)]
			public byte mac3;
			[MarshalAs(UnmanagedType.U1)]
			public byte mac4;
			[MarshalAs(UnmanagedType.U1)]
			public byte mac5;
			[MarshalAs(UnmanagedType.U1)]
			public byte mac6;
			[MarshalAs(UnmanagedType.U1)]
			public byte mac7;
			[MarshalAs(UnmanagedType.U4)]
			public int dwAddr;
			[MarshalAs(UnmanagedType.U4)]
			public int dwType;
		}
		[DllImport("iphlpapi.dll", ExactSpelling = true)]
		public static extern int SendARP(int DestIP, int SrcIP, [Out] byte[] pMacAddr, ref int PhyAddrLen);

		public static IPAddress QuestIP = null;
		public static bool IPPingThread1Done = false;
		public static bool IPPingThread2Done = false;

		public static void GetCurrentIPAndPingNetwork()
		{
			foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces().Where(ni => ni.OperationalStatus == OperationalStatus.Up && (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 || ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet)))
			{
				var addr = adapter.GetIPProperties().GatewayAddresses.FirstOrDefault();
				if (addr != null && !addr.Address.ToString().Equals("0.0.0.0"))
				{
					foreach (UnicastIPAddressInformation unicastIPAddressInformation in adapter.GetIPProperties().UnicastAddresses)
					{
						if (unicastIPAddressInformation.Address.AddressFamily == AddressFamily.InterNetwork)
						{
							Console.WriteLine("PC IP Address: " + unicastIPAddressInformation.Address);
							Console.Write("PC Subnet Mask: " + unicastIPAddressInformation.IPv4Mask + "\n Searching for Quest on network...");
							PingNetworkIPs(unicastIPAddressInformation.Address, unicastIPAddressInformation.IPv4Mask);
						}
					}
				}
			}
		}
		
		public static async void PingIPList(List<IPAddress> IPs, int threadID)
		{
			var tasks = IPs.Select(ip => new Ping().SendPingAsync(ip, 4000));
			var results = await Task.WhenAll(tasks);
			switch (threadID)
			{
				case 1:
					IPPingThread1Done = true;
					break;
				case 2:
					IPPingThread2Done = true;
					break;
				default:
					break;
			}
		}
		public static void PingNetworkIPs(IPAddress address, IPAddress mask)
		{
			uint ipAddress = BitConverter.ToUInt32(address.GetAddressBytes(), 0);
			uint ipMaskV4 = BitConverter.ToUInt32(mask.GetAddressBytes(), 0);
			uint broadCastIpAddress = ipAddress | ~ipMaskV4;

			IPAddress start = new IPAddress(BitConverter.GetBytes(broadCastIpAddress));

			var bytes = start.GetAddressBytes();
			var leastSigByte = address.GetAddressBytes().Last();
			var range = 255 - leastSigByte;

			var pingReplyTasks = Enumerable.Range(leastSigByte, range)
				.Select(x =>
				{
					var bb = start.GetAddressBytes();
					bb[3] = (byte)x;
					var destIp = new IPAddress(bb);
					return destIp;
				})
				.ToList();
			var pingReplyTasks2 = Enumerable.Range(0, leastSigByte - 1)
				.Select(x =>
				{

					var bb = start.GetAddressBytes();
					bb[3] = (byte)x;
					var destIp = new IPAddress(bb);
					return destIp;
				})
				.ToList();
			IPSearchthread1 = new Thread(new ThreadStart(() => PingIPList(pingReplyTasks, 1)));
			IPSearchthread2 = new Thread(new ThreadStart(() => PingIPList(pingReplyTasks2, 2)));
			IPPingThread1Done = false;
			IPPingThread2Done = false;
			IPSearchthread1.Start();
			IPSearchthread2.Start();
		}

		// Declare the GetIpNetTable function.
		[DllImport("IpHlpApi.dll")]
		[return: MarshalAs(UnmanagedType.U4)]
		static extern int GetIpNetTable(
		   IntPtr pIpNetTable,
		   [MarshalAs(UnmanagedType.U4)]
		 ref int pdwSize,
		   bool bOrder);

		[DllImport("IpHlpApi.dll", SetLastError = true, CharSet = CharSet.Auto)]
		internal static extern int FreeMibTable(IntPtr plpNetTable);

		// The insufficient buffer error.
		const int ERROR_INSUFFICIENT_BUFFER = 122;
		static IntPtr buffer;

		static void CheckARPTable()
		{

			int bytesNeeded = 0;

			// The result from the API call.
			int result = GetIpNetTable(IntPtr.Zero, ref bytesNeeded, false);

			// Call the function, expecting an insufficient buffer.
			if (result != ERROR_INSUFFICIENT_BUFFER)
			{
				// Throw an exception.
				throw new Exception();
			}

			// Allocate the memory, do it in a try/finally block, to ensure
			// that it is released.
			buffer = IntPtr.Zero;
			// Allocate the memory.
			buffer = Marshal.AllocCoTaskMem(bytesNeeded);

			// Make the call again. If it did not succeed, then
			// raise an error.
			result = GetIpNetTable(buffer, ref bytesNeeded, false);

			// If the result is not 0 (no error), then throw an exception.
			if (result != 0)
			{
				// Throw an exception.
				throw new Exception();
			}

			// Now we have the buffer, we have to marshal it. We can read
			// the first 4 bytes to get the length of the buffer.
			int entries = Marshal.ReadInt32(buffer);

			// Increment the memory pointer by the size of the int.
			IntPtr currentBuffer = new IntPtr(buffer.ToInt64() +
				Marshal.SizeOf(typeof(int)));

			// Allocate an array of entries.
			MIB_IPNETROW[] table = new MIB_IPNETROW[entries];

			// Cycle through the entries.
			for (int index = 0; index < entries; index++)
			{
				// Call PtrToStructure, getting the structure information.
				table[index] = (MIB_IPNETROW)Marshal.PtrToStructure(new
					IntPtr(currentBuffer.ToInt64() + (index *
					Marshal.SizeOf(typeof(MIB_IPNETROW)))), typeof(MIB_IPNETROW));
			}

			for (int index = 0; index < entries; index++)
			{
				MIB_IPNETROW row = table[index];

				if (row.mac0 == 0x2C && row.mac1 == 0x26 && row.mac2 == 0x17)
				{
					QuestIP = new IPAddress(BitConverter.GetBytes(row.dwAddr));
					break;
				}

			}
		}
		#endregion
		

		public static void InstallSpeakerSystem(IProgress<string> progress)
		{
			try
			{
				IntPtr unityHandle = liveWindow.GetUnityHandler();
				string[] SpeakerSystemURLVer = GetLatestSpeakerSystemURLVer();
				string updateFileName = "EchoSpeakerSystemInstall_" + SpeakerSystemURLVer[1] + ".exe";
				WebClient webClient = new WebClient();
				//webClient.DownloadFileCompleted += Completed;
				//webClient.DownloadProgressChanged += ProgressChanged;
				webClient.DownloadFile(new Uri(SpeakerSystemURLVer[0]), Path.GetTempPath() + updateFileName);
				Process process = Process.Start(new ProcessStartInfo
				{
					FileName = Path.Combine(Path.GetTempPath(), updateFileName),
					UseShellExecute = true,
					Arguments = "/ignite=true /HWND=" + unityHandle.ToInt32() + " "
				});
				int count = 0;
				string SpeakerSystemInstallLabel = "Installing Echo Speaker System";
				string statusDots = "";
				while (!process.HasExited && count < 12000) //Time out after 10 mins
				{
					if (count % 16 == 0)
					{
						statusDots = "";
					}
					else if (count % 4 == 0)
					{
						statusDots += ".";
					}
					count++;

					progress.Report(SpeakerSystemInstallLabel + statusDots);
					Thread.Sleep(50);
				}
				if (!process.HasExited)
				{
					process.Kill();
					progress.Report("Echo Speaker System install failed!");
				}
				else if (process.ExitCode > -1)
				{
					Process[] speakerSystemProcs = Process.GetProcessesByName("Echo Speaker System");
					if (speakerSystemProcs.Length > 0)
					{
						liveWindow.SpeakerSystemProcess = speakerSystemProcs[0];
						liveWindow.SpeakerSystemStart(unityHandle);
					}
					progress.Report("Echo Speaker System installed successfully!");
				}
				int code = process.ExitCode;
				InstalledSpeakerSystemVersion = FindEchoSpeakerSystemInstallVersion();
				IsSpeakerSystemUpdateAvailable = false;
			}
			catch (Exception)
			{
				InstalledSpeakerSystemVersion = FindEchoSpeakerSystemInstallVersion();
				IsSpeakerSystemUpdateAvailable = false;
				progress.Report("Echo Speaker System install failed!");
			}
		}


		public static void WaitUntilLocalGameLaunched(Action callback, string ip = "127.0.0.1", int port = 6721)
		{
			try
			{
				if (spectateMe)
				{
					// Team clientPlayerTeam = lastFrame?.GetTeam(lastFrame.client_name);
					// if (clientPlayerTeam == null) return;
					// if (clientPlayerTeam.color == TeamColor.spectator) return;

					Task.Run(async () =>
					{
						// check if we're actually on quest and running pc spectator
						TimeSpan pcSpectatorStartupTime = TimeSpan.FromSeconds(90f);
						bool inPCSpectator = false;
						string result = string.Empty;

						while (!inPCSpectator && pcSpectatorStartupTime > TimeSpan.Zero)
						{
							// if we stopped trying to spectate, cancel
							if (!spectateMe) return;

							// TODO this crashes on local pc
							result = await GetRequestAsync($"http://{ip}:{port}/session", null);
							if (string.IsNullOrEmpty(result))
							{
								await Task.Delay(200);
								continue;
							}


							inPCSpectator = true;
						}

						if (!inPCSpectator)
						{
							new MessageBox("You have chosen to automatically set the camera to follow the player, but you don't have EchoVR running in spectator mode on this pc.").Show();
							return;
						}

						Frame frame = JsonConvert.DeserializeObject<Frame>(result);
						if (frame == null)
						{
							new MessageBox("Failed to process frame from the local PC").Show();
							return;
						}

						if (frame.sessionid != lastFrame.sessionid || lastFrame.GetPlayer(frame.client_name) == null)
						{
							new MessageBox("Local PC is not in the same match as your Quest. Can't follow player.").Show();
							return;
						}

						callback?.Invoke();
					});
				}
			}
			catch (Exception ex)
			{
				LogRow(LogType.Error, $"Error with waiting for spectate-me to launch.\n{ex}");
			}


		}
		

		/// <summary>
		/// Shows or hides the requested popup window
		/// </summary>
		/// <param name="type">The class of the window</param>
		/// <param name="windowName">The identifier of the window. This is used to hide if a window of that name was already shown</param>
		/// <param name="ownedBy">The window to be owned by. This makes the popup always on top of the parent</param>
		/// <returns>True if the window was opened, false if the window was closed</returns>
		public static bool ToggleWindow(Type type, string windowName = null, Window ownedBy = null)
		{
			try
			{
				windowName ??= type.ToString();

				if (!popupWindows.ContainsKey(windowName) || popupWindows[windowName] == null)
				{
					popupWindows[windowName] = (Window)Activator.CreateInstance(type);
					popupWindows[windowName].Owner = ownedBy;
					popupWindows[windowName].Closed += (_, _) => popupWindows[windowName] = null;
					popupWindows[windowName].Show();
					return true;
				}
				else
				{
					popupWindows[windowName].Close();
					return false;
				}
			}catch (Exception)
			{
				new MessageBox($"Failed to open window: {type}.\nPlease report this to NtsFranz.").Show();
				return false;
			}
		}

		public static Window GetWindowIfOpen(Type type, string windowName = null)
		{
			windowName ??= type.ToString();
			return popupWindows.ContainsKey(windowName) ? popupWindows[windowName] : null;
		}

		private static void AutoUploadTabletStats()
		{
			_ = Task.Run(() =>
			{
				List<TabletStats> stats = FindTabletStats();
				stats.ForEach(s =>
				{
					if (SparkSettings.instance.autoUploadProfiles.ContainsKey(s.player_name) &&
					    SparkSettings.instance.autoUploadProfiles[s.player_name])
					{
						UploadTabletStats(s);
					}
				});
			});
		}

		public static List<TabletStats> FindTabletStats()
		{
			try
			{
				string baseFolder = Path.Combine(
					Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
					"..", "Local", "rad", "echovr", "users", "ovr-org");
				if (!Directory.Exists(baseFolder))
				{
					LogRow(LogType.Error, "Can't find the EchoVR profile folder.");
					return new List<TabletStats>();
				}

				List<string> folders = Directory.GetDirectories(baseFolder).ToList();
				List<string> files = new List<string>();
				folders.ForEach(folder => files.AddRange(Directory.GetFiles(folder).ToList()));

				List<TabletStats> profiles = new List<TabletStats>();
				files.Where(f => f.EndsWith("serverprofile.json")).ToList().ForEach(file =>
				{
					TabletStats tabletStats = new TabletStats(File.ReadAllText(file));
					if (tabletStats.IsValid())
					{
						profiles.Add(tabletStats);
					}
				});
				profiles.Sort((p1, p2) => p1.update_time.CompareTo(p2.update_time));

				return profiles;
			}
			catch (Exception ex)
			{
				LogRow(LogType.Error, ex.ToString());
				new MessageBox($"Failed to find tablet stats.\nPlease report this to NtsFranz.").Show();
				return new List<TabletStats>();
			}
		}

		public static void UploadTabletStats(TabletStats p, Action<bool> finishedCallback = null)
		{
			try
			{
				Task.Run(async () =>
				{
					string dataString = JsonConvert.SerializeObject(p);
					string hash = SecretKeys.Hash(dataString + p.player_name);

					client.DefaultRequestHeaders.Remove("x-api-key");
					client.DefaultRequestHeaders.Add("x-api-key", DiscordOAuth.igniteUploadKey);

					StringContent content = new StringContent(dataString, Encoding.UTF8, "application/json");

					try
					{
						HttpResponseMessage response = await client.PostAsync("update_tablet_stats?hashkey=" + hash + "&player_name=" + p.player_name, content);
						LogRow(LogType.Info, "[DB][Response] " + response.Content.ReadAsStringAsync().Result);
						finishedCallback?.Invoke(response.IsSuccessStatusCode);
					}
					catch
					{
						LogRow(LogType.Error, "Can't connect to the DB server");
						finishedCallback?.Invoke(false);
					}
				});
			}
			catch (Exception ex)
			{
				LogRow(LogType.Error, ex.ToString());
				new MessageBox($"Failed to upload tablet stats.\nPlease report this to NtsFranz.").Show();
				finishedCallback?.Invoke(false);
			}
		}

		/// <summary>
		/// This method is based on the python code that is used in the VRML Discord bot for calculating server score.
		/// </summary>
		/// <returns>The server score</returns>
		public static float CalculateServerScore(List<int> bluePings, List<int> orangePings)
		{
			// configurable parameters for tuning
			int min_ping = 10; // you don't lose points for being higher than this value
			int max_ping = 150; // won't compute if someone is over this number
			int ping_threshold = 100; // you lose extra points for being higher than this

			// points_distribution dictates how many points come from each area:
			//   0 - difference in sum of pings between teams
			//   1 - within-team variance
			//   2 - overall server variance
			//   3 - overall high/low pings for server
			int[] points_distribution = new int[] { 30, 30, 30, 10 };

			// determine max possible server/team variance and max possible sum diff,
			// given the min/max allowable ping
			float max_server_var = Variance(new float[]
				{min_ping, min_ping, min_ping, min_ping, max_ping, max_ping, max_ping, max_ping});
			float max_team_var = Variance(new float[] { min_ping, min_ping, max_ping, max_ping });
			float max_sum_diff = (4 * max_ping) - (4 * min_ping);

			// sanity check for ping values
			if (bluePings == null || bluePings.Count == 0 || orangePings == null || orangePings.Count == 0)
			{
				// Console.WriteLine("No player's ping can be over 150.");
				return -1;
			}
			if (bluePings.Max() > max_ping || orangePings.Max() > max_ping)
			{
				// Console.WriteLine("No player's ping can be over 150.");
				return -1;
			}



			// calculate points for sum diff
			float blueSum = bluePings.Sum();
			float orangeSum = orangePings.Sum();
			float sum_diff = MathF.Abs(blueSum - orangeSum);

			float sum_points = (1 - (sum_diff / max_sum_diff)) * points_distribution[0];

			// calculate points for team variances
			float blueVariance = Variance(bluePings);
			float orangeVariance = Variance(orangePings);

			float mean_var = new[] { blueVariance, orangeVariance }.Average();
			float team_points = (1 - (mean_var / max_team_var)) * points_distribution[1];

			// calculate points for server variance
			List<int> bothPings = new List<int>(bluePings);
			bothPings.AddRange(orangePings);

			float server_var = Variance(bothPings);

			float server_points = (1 - (server_var / max_server_var)) * points_distribution[2];

			// calculate points for high/low ping across server
			float hilo = ((blueSum + orangeSum) - (min_ping * 8)) / ((ping_threshold * 8) - (min_ping * 8));

			float hilo_points = (1 - hilo) * points_distribution[3];

			// add up points
			float final = sum_points + team_points + server_points + hilo_points;

			return final;
		}


		public static float Variance(IEnumerable<float> values)
		{
			float avg = values.Average();
			return values.Average(v => MathF.Pow(v - avg, 2));
		}

		public static float Variance(IEnumerable<int> values)
		{
			float avg = (float)values.Average();
			return values.Average(v => MathF.Pow(v - avg, 2));
		}

		// TODO
		public void ShowToast(string text, float duration = 3)
		{

		}

		public static string GetLocalIP()
		{
			using Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
			socket.Connect("8.8.8.8", 65530);
			IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
			return endPoint != null ? endPoint.Address.ToString() : "";
		}

		internal static void Quit()
		{
			running = false;
			SparkClosing?.Invoke();
			SparkSettings.instance.Save();
			if (closingWindow != null)
			{
				// already trying to close
				return;
			}

			liveWindow.trayIcon.Visibility = Visibility.Collapsed;

			closingWindow = new ClosingDialog();
			closingWindow.Show();

			_ = GentleClose();
		}
	}

	/// <summary>
	/// Custom Vector3 class used to keep track of 3D coordinates.
	/// Works more like the Vector3 included with Unity now.
	/// </summary>
	public static class Vector3Extensions
	{
		public static Vector3 ToVector3(this List<float> input)
		{
			if (input.Count != 3)
			{
				throw new Exception("Can't convert List to Vector3");
			}

			return new Vector3(input[0], input[1], input[2]);
		}

		public static Vector3 ToVector3(this float[] input)
		{
			if (input.Length != 3)
			{
				throw new Exception("Can't convert array to Vector3");
			}

			return new Vector3(input[0], input[1], input[2]);
		}
		
		public static Vector3 ToVector3Backwards(this float[] input)
		{
			if (input.Length != 3)
			{
				throw new Exception("Can't convert array to Vector3");
			}

			return new Vector3(input[2], input[1], input[0]);
		}

		public static float DistanceTo(this Vector3 v1, Vector3 v2)
		{
			return (float)Math.Sqrt(Math.Pow(v1.X - v2.X, 2) + Math.Pow(v1.Y - v2.Y, 2) + Math.Pow(v1.Z - v2.Z, 2));
		}

		public static Vector3 Normalized(this Vector3 v1)
		{
			return v1 / v1.Length();
		}
	}
	
	
		
	public static class TeamColorExtensions
	{
		public static string ToLocalizedString(this Team.TeamColor color)
		{
			return color switch
			{
				Team.TeamColor.blue => Resources.blue,
				Team.TeamColor.orange => Resources.orange,
				Team.TeamColor.spectator => Resources.spectator,
				_ => ""
			};
		}
	}
	
}