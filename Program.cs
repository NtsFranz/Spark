using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Numerics;
using System.Runtime.InteropServices;
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
using System.Resources;
using EchoVRAPI;
using NetMQ;
using Newtonsoft.Json.Linq;
using Microsoft.Web.WebView2.Core;

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

		public enum ConnectionState {
			NotConnected,
			Menu,		// loading screen or menu - the response is not json
			NoAPI,		// user has not enabled API access
			InLobby,	// error code for lobby -6
			InGame		// in an arena or combat match
		}
		public static ConnectionState connectionState;
		public static ConnectionState lastConnectionState;
		public static ConnectionState lastConnectionStateEvent;
		public static bool InGame => connectionState == ConnectionState.InGame;

		public const string APIURL = "https://api.ignitevr.gg";
		// public const string APIURL = "http://127.0.0.1:8000";
		public const string WRITE_API_URL = "http://127.0.0.1:6723/";

		// public static string currentAccessCodeUsername = "";
		public static string InstalledSpeakerSystemVersion = "";
		public static bool IsSpeakerSystemUpdateAvailable;

		public static ConcurrentQueue<AccumulatedFrame> rounds = new ConcurrentQueue<AccumulatedFrame>();
		public static AccumulatedFrame CurrentRound => rounds.LastOrDefault() ?? emptyRound;
		public static AccumulatedFrame LastRound => rounds.TakeLast(2).FirstOrDefault() ?? emptyRound;
		private static readonly AccumulatedFrame emptyRound = new AccumulatedFrame(Frame.CreateEmpty());

		public static IEnumerable<GoalData> LastGoals => rounds.SelectMany(r => r.goals);
		public static IEnumerable<EventData> LastJousts => rounds.SelectMany(r => r.events.Where(e=>e.eventType.IsJoust()));

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

		private static long frameIndex = 0;
		private static long lastProcessedFrameIndex = 0;


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
		public static float serverScoreSmoothingFactor = .95f;


		/// <summary>
		/// Not actually Hz. 1/Hz.
		/// </summary>
		public static float StatsIntervalMs => statsDeltaTimes[SparkSettings.instance.lowFrequencyMode ? 1 : 0];
		private static bool? lastLowFreqMode = null;

		// 30 or 15 hz main fetch speed
		private static readonly List<float> statsDeltaTimes = new List<float> { 33.3333333f, 66.6666666f };


		public static LiveWindow liveWindow;
		private static ClosingDialog closingWindow;

		private static readonly Dictionary<string, Window> popupWindows = new Dictionary<string, Window>();

		private static float smoothDeltaTime = -1;

		public static bool hostingLiveReplay = false;

		public static string echoVRIP = "";
		public static int echoVRPort = 6721; 
		public static bool overrideEchoVRPort;


		public static string hostedAtlasSessionId;
		public static LiveWindow.AtlasWhitelist atlasWhitelist = new LiveWindow.AtlasWhitelist();

		public static TTSController synth;
		public static ReplayClips replayClips;
		public static ReplayFilesManager replayFilesManager;
		public static CameraWriteController cameraWriteController;
		public static CameraWrite cameraWriteWindow;
		public static EchoGPController echoGPController;
		public static WebSocketServerManager webSocketMan;
		public static SpeechRecognition speechRecognizer;
		public static LoggerEvents loggerEvents;
		private static CancellationTokenSource autorestartCancellation;
		private static CancellationTokenSource fetchThreadCancellation;
		private static CancellationTokenSource liveReplayCancel;
		public static Thread atlasHostingThread;
		private static Thread IPSearchThread1;
		private static Thread IPSearchThread2;
		public static OBS obs;
		public static Medal medal;
		private static OverlayServer overlayServer;
		public static SpectateMeController spectateMeController;
		public static UploadController uploadController;
		public static LocalDatabase localDatabase;
		public static NetMQEvents netMQEvents;
		private static readonly HttpClient fetchClient = new HttpClient();
		// private static readonly System.Timers.Timer fetchTimer = new System.Timers.Timer();
		private static readonly Stopwatch fetchSw = new Stopwatch();
		private static Timer ccuCounter;
		public static CameraController cameraController;

		public static CoreWebView2Environment webView2Environment;


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
		/// Called when a frame is finished with conversion and it is an Echo Combat frame
		/// </summary>
		public static Action<Frame> NewCombatFrame;
		/// <summary>
		/// Called when connectedToGame state changes.
		/// This could be on loading screen or lobby
		/// string is the raw /session, but this may be from html from a menu
		/// </summary>
		public static Action<DateTime, string> ConnectedToGame;
		/// <summary>
		/// Called when connectedToGame state changes to Not_Connected.
		/// </summary>
		public static Action DisconnectedFromGame;
		
		/// <summary>
		/// When the connectedToGame state changes to InGame 
		/// </summary>
		public static Action<Frame> JoinedGame;
		/// <summary>
		/// When the connectedToGame state changes to Not_Connected and the previous state was InGame 
		/// </summary>
		public static Action<Frame> LeftGame;

		/// <summary>
		/// We changed session ids. This happens for first join and switching matches
		/// </summary>
		public static Action<Frame> NewMatch;
		public static Action<Frame, AccumulatedFrame.FinishReason> RoundOver;
		/// <summary>
		/// A new round started. This only happens for secondary rounds in private matches.
		/// Frame is the first frame of the new round with scores at 0-0 
		/// </summary>
		public static Action<Frame> NewRound;
		

		public static Action<Frame> JoinedLobby;
		public static Action<Frame> LeftLobby;

		public static Action<Frame, Team, Player> PlayerJoined;
		public static Action<Frame, Team, Player> PlayerLeft;
		/// <summary>
		/// frame, fromteam, toteam, player
		/// </summary>
		public static Action<Frame, Team, Team, Player> PlayerSwitchedTeams;
		
		// TODO add player/team who requested the reset
		/// <summary>
		/// Frame is the last frame of the last match
		/// </summary>
		public static Action<Frame> MatchReset;
		/// <summary>
		/// Frame, nearest player, distance to podium
		/// </summary>
		public static Action<Frame, Player, float> PauseRequest;
		/// <summary>
		/// Frame, nearest player, distance to podium
		/// </summary>
		public static Action<Frame, Player, float> GamePaused;
		/// <summary>
		/// Frame, nearest player, distance to podium
		/// </summary>
		public static Action<Frame, Player, float> GameUnpaused;
		
		/// <summary>
		/// lastFrame, newFrame
		/// </summary>
		public static Action<Frame, Frame> GameStatusChanged;
		public static Action<Frame> LocalThrow;
		/// <summary>
		/// frame, team, player, speed, howlongago
		/// </summary>
		public static Action<Frame, Team, Player, float, float> BigBoost;
		/// <summary>
		/// bool is true for left, false for right
		/// </summary>
		public static Action<Frame, Team, Player, bool> EmoteActivated;
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
		/// Catch by the other team
		/// frame, team, throwplayer, catchplayer
		/// </summary>
		public static Action<Frame, Team, Player, Player> Turnover;
		/// <summary>
		/// Any catch, including interceptions, passes, and turnovers
		/// frame, team, player
		/// </summary>
		public static Action<Frame, Team, Player> Catch;
		/// <summary>
		/// frame, team, player, lefthanded, underhandedness
		/// </summary>
		public static Action<Frame, Team, Player, bool, float> Throw;
		public static Action<Frame, Team, Player> ShotTaken;
		/// <summary>
		/// Frame, teamcolor, nearest player, distance to podium
		/// </summary>
		public static Action<Frame, Team.TeamColor, Player, float> RestartRequest;
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
		public static Action<Frame> RulesChanged;
		
		public static Action ManualClip;
		public static Action BadWordDetected;

		/// <summary>
		/// For any event type that has EventData
		/// </summary>
		public static Action<EventData> OnEvent;

		#region Spark Settings Changed

		public static Action OverlayConfigChanged;
		public static Action<string> EventLog;
		public static Action<string> IPGeolocated;

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
				
				
				FetchUtils.client.DefaultRequestHeaders.Add("version", AppVersionString());
				FetchUtils.client.DefaultRequestHeaders.Add("User-Agent", "Spark/" + AppVersionString());

				FetchUtils.client.BaseAddress = new Uri(APIURL);


				if (CheckIfLaunchedWithCustomURLHandlerParam(args))
				{
					return; // wait for the dialog to quit the program
				}


				// allow multiple instances if the port is overriden
				if (IsSparkOpen() && !overrideEchoVRPort)
				{
					Task.Run(async () =>
					{
						HttpClient localClient = new HttpClient();
						localClient.Timeout = TimeSpan.FromSeconds(1);
						try
						{
							string responseBody = await localClient.GetStringAsync("http://localhost:6724/api/focus_spark");

							Console.WriteLine(responseBody);
						}
						catch (Exception)
						{
							// ignored
						}
						
						Quit();
					});

					return;

					// MessageBox box = new MessageBox(Resources.instance_already_running_message, Resources.Error);
					// box.Show();
					// //while(box!= null)
					// {
					// 	Thread.Sleep(10);
					// }


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
				medal = new Medal();


				// if logged in with discord
				if (!string.IsNullOrEmpty(SparkSettings.instance.discordOAuthRefreshToken))
				{
					DiscordOAuth.OAuthLoginRefresh(SparkSettings.instance.discordOAuthRefreshToken);
				}
				else
				{
					DiscordOAuth.RevertToPersonal();
				}

				_ = Task.Run(() =>
				{
					string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IgniteVR", "Spark", "WebView");
					if (!Directory.Exists(path))
					{
						Directory.CreateDirectory(path);
					}
					//webView2Environment = await CoreWebView2Environment.CreateAsync(null, path);
					Debug.WriteLine(Directory.Exists(path));
				});

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



				if (!SparkSettings.instance.onlyActivateHighlightsWhenGameIsOpen &&
					SparkSettings.instance.isNVHighlightsEnabled)
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
					if (!HighlightsHelper.isNVHighlightsEnabled)
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
				synth = new TTSController();

				DiscordOAuth.Authenticated += () =>
				{
					synth = new TTSController();
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
				speechRecognizer = new SpeechRecognition();
				loggerEvents = new LoggerEvents();
				spectateMeController = new SpectateMeController();
				uploadController = new UploadController();
				localDatabase = new LocalDatabase();
				cameraController = new CameraController();
				
				

				// web server asp.net
				try
				{
					overlayServer = new OverlayServer();
				}
				catch (Exception e)
				{
					LogRow(LogType.Error, e.ToString());
				}


				UpdateEchoExeLocation();

				DiscordRichPresence.Start();

				spectateMeController.spectateMe = SparkSettings.instance.spectateMeOnByDefault;

				autorestartCancellation = new CancellationTokenSource();
				Task.Run(AutorestartTask, autorestartCancellation.Token);

				fetchThreadCancellation = new CancellationTokenSource();
				Task.Run(MainLoop, fetchThreadCancellation.Token);

				liveReplayCancel = new CancellationTokenSource();
				_ = Task.Run(LiveReplayHostingTask, liveReplayCancel.Token);


				_ = Task.Run(async () =>
				{
					try
					{
						EchoVRSettingsManager.ReloadLoadingTips();
						if (EchoVRSettingsManager.loadingTips != null)
						{
							JToken toolsAll = EchoVRSettingsManager.loadingTips["tools-all"];
							JArray tips = (JArray)EchoVRSettingsManager.loadingTips["tools-all"]?["tips"];

							if (tips != null)
							{
								// keep only those without SPARK in the title
								tips = new JArray(tips.Where(t => (string)t[1] != "SPARK"));

								ResourceSet resourceSet =
									LoadingTips.ResourceManager.GetResourceSet(CultureInfo.CurrentUICulture, true,
										true);
								if (resourceSet != null)
								{
									// loop through the resource strings
									foreach (DictionaryEntry entry in resourceSet)
									{
										string tip = entry.Value?.ToString();
										if (tip != null)
										{
											if (tips.All(existingTip => (string)existingTip[2] != tip))
											{
												tips.Add(new JArray("", "SPARK", tip));
											}
										}
										else
										{
											LogRow(LogType.Error, "Loading tip was null.");
										}
									}
								}

								if (toolsAll != null) toolsAll["tips"] = tips;
							}

							EchoVRSettingsManager.WriteEchoVRLoadingTips(EchoVRSettingsManager.loadingTips);
						}
					}
					catch (Exception e)
					{
						Logger.Error(e.ToString());
					}

					// wait 5 seconds for login to happen
					await Task.Delay(5000);
					AutoUploadTabletStats();
				});

				//HighlightsHelper.CloseNVHighlights();

				ccuCounter = new Timer(CCUCounter, null, 0, 60000);
				

				#region Add Listeners

				JoinedGame += OnJoinedGame;
				LeftGame += OnLeftGame;

				#endregion

			}
			catch (Exception e)
			{
				Logger.Error(e.ToString());
				Quit();
			}
		}

		private static void OnLeftGame(Frame obj)
		{
			//
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

		private static void CCUCounter(object state)
		{
			_ = FetchUtils.client.PostAsync($"/spark_is_open?hw_id={DeviceId}&client_name={SparkSettings.instance.client_name}", null);
		}

		/// <summary>
		/// This is just a failsafe so that the program doesn't leave a dangling thread.
		/// </summary>
		private static void KillAll()
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

			if (closingWindow != null)
			{
				closingWindow.label.Content = Resources.Closing___;
			}

			netMQEvents?.CloseApp();
			await Task.Delay(50);

			overlayServer?.Stop();

			while (atlasHostingThread != null && atlasHostingThread.IsAlive)
			{
				if (closingWindow != null) closingWindow.label.Content = Resources.Shutting_down_Atlas___;
				await Task.Delay(10);
			}

			autorestartCancellation?.Cancel();
			fetchThreadCancellation?.Cancel();
			liveReplayCancel?.Cancel();
			ccuCounter?.Dispose();

			if (replayFilesManager != null)
			{
				while (replayFilesManager.zipping || 
				       replayFilesManager.replayThreadActive || 
				       replayFilesManager.splitting)
				{
					if (closingWindow != null) closingWindow.label.Content = Resources.Compressing_Replay_File___;
					await Task.Delay(10);
				}
			}

			if (closingWindow != null) closingWindow.label.Content = Resources.Closing_NVIDIA_Highlights___;
			HighlightsHelper.CloseNVHighlights();

			if (closingWindow != null) closingWindow.label.Content = Resources.Closing_Speaker_System___;
			liveWindow?.KillSpeakerSystem();

			if (closingWindow != null) closingWindow.label.Content = "Closing PubSub System...";

			AsyncIO.ForceDotNet.Force();
			NetMQConfig.Cleanup(false);
			if (closingWindow != null) closingWindow.label.Content = Resources.Closing___;

			app.ExitApplication();

			await Task.Delay(100);

			if (closingWindow != null) closingWindow.label.Content = "Failed to close gracefully. Using an axe instead...";


			LogRow(LogType.Error, "Failed to close gracefully. Using an axe instead...");
			KillAll();
		}

		/// <summary>
		/// Checks if another instance of Spark is open
		/// </summary>
		/// <returns>True if another is open, false if not.</returns>
		private static bool IsSparkOpen()
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


		private static async Task MainLoop()
		{
			fetchClient.Timeout = TimeSpan.FromSeconds(5);

			DateTime lastFetch = DateTime.UtcNow;
			while (running)
			{
				fetchSw.Restart();

				frameIndex++;
				long localFrameIndex = frameIndex;

				// fetch the session or bones data
				List<Task<HttpResponseMessage>> tasks = new List<Task<HttpResponseMessage>>
				{
					fetchClient.GetAsync($"http://{echoVRIP}:{echoVRPort}/session")
				};
				
				if (SparkSettings.instance.fetchBones)
				{
					tasks.Add(fetchClient.GetAsync($"http://{echoVRIP}:{echoVRPort}/player_bones"));
				}

				lastFetch = DateTime.UtcNow;
				

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
						lastDataTime = DateTime.UtcNow;

						if (connectionState == ConnectionState.NotConnected)
						{
							_ = Task.Run(() =>
							{
								ConnectedToGame?.Invoke(frameTime, session);
							});
						}

						connectionState = ConnectionState.InGame;

						// early quit if the program was quit while fetching
						if (!running) return;


						// tell the processing methods that stuff is available
						_ = Task.Run(() =>
						{
							FrameFetched?.Invoke(frameTime, session, bones);
						});

						// parse the API data
						Frame f = Frame.FromJSON(frameTime, session, bones);

						if (f != null)
						{
							// tell the processing methods that stuff is available
							ProcessFrame(f, lastFrame, localFrameIndex);

							NewFrame?.Invoke(f);
						}
						else
						{
							LogRow(LogType.Error, "Converting to Frame failed. Investigate 🕵");
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
					else // not a success status code
					{
						await FetchFail(results);
					}
				}
				catch (TaskCanceledException)
				{
					await FetchFail(null);
				}
				catch (HttpRequestException)
				{
					await FetchFail(null);
				}
				catch (Exception ex)
				{
					LogRow(LogType.Error, $"Error in fetch request.\n{ex}");
				}

				lastConnectionState = connectionState;


				// wait for next frame time based on time last frame was actually fetched
				int delay = Math.Clamp((int)(StatsIntervalMs - fetchSw.ElapsedMilliseconds - 3), 0, 1000);
				if (delay > 0)
				{
					Thread.Sleep(delay);
				}
			}
		}


		private static async Task FetchFail(HttpResponseMessage[] results)
		{
			// just revert to not connected to be sure. This will be set properly lower down
			connectionState = ConnectionState.NotConnected;

			if (results != null)
			{
				string session = await results[0].Content.ReadAsStringAsync();

				if (session.Length > 0 && session[0] == '{')
				{
					Frame f = Frame.FromJSON(DateTime.UtcNow, session, null);

					if (f == null)
					{
						LogRow(LogType.Error, "Error parsing error frame");
						return;
					}
					if (lastConnectionState == ConnectionState.NotConnected)
					{
						ConnectedToGame?.Invoke(f.recorded_time, session);
					}
					
					// check error codes
					switch (f.err_code)
					{
						case -2: // api disabled
							connectionState = ConnectionState.NoAPI;
							break;
						case -6: // lobby
							if (lastConnectionState != ConnectionState.InLobby)
							{
								try
								{
									JoinedLobby?.Invoke(f);
								}
								catch (Exception exp)
								{
									LogRow(LogType.Error, "Error processing action", exp.ToString());
								}
							}

							connectionState = ConnectionState.InLobby;
							break;
					}
				}
				else
				{
					connectionState = ConnectionState.Menu;
					// in loading screen, where the response is not json
					
					if (lastConnectionState == ConnectionState.NotConnected)
					{
						ConnectedToGame?.Invoke(DateTime.UtcNow, session);
					}
				}
			}
			else
			{
				// Not connected to game
				if (lastConnectionState != ConnectionState.NotConnected)
				{
					_ = Task.Run(() => { DisconnectedFromGame?.Invoke(); });
				}
			}

			// left game
			if (lastConnectionState == ConnectionState.InGame && lastFrame != null)
			{
				try
				{
					_ = Task.Run(() => { LeftGame?.Invoke(lastFrame); });
				}
				catch (Exception exp)
				{
					LogRow(LogType.Error, "Error processing action", exp.ToString());
				}
			}

			// make sure this gets set back to nonconnected to send rejoin events
			lastConnectionStateEvent = connectionState;

			// add this data to the public variable
			lock (lastJSONLock)
			{
				lastJSON = null;
				lastBonesJSON = null;
			}

			// add additional delay to prevent spamming the request when idle
			await Task.Delay(1000);
		}

		private static void ProcessFrame(Frame frame, Frame lastFrame, long localFrameIndex)
		{
			try
			{
				// process events. This may include splitting to a new round
				try
				{
					GenerateEvents(frame, lastFrame);
				}
				catch (Exception ex)
				{
					LogRow(LogType.Error, $"Error in GenerateEvents. Please catch inside.\n{ex}");
				}

				if (frame.InArena)
				{
					// add the stats to the current round data
					CurrentRound.Accumulate(frame, lastFrame);
				}
			}
			catch (Exception ex)
			{
				LogRow(LogType.Error, "Big oopsie. Please catch inside. " + ex);
			}

			lastProcessedFrameIndex = localFrameIndex;
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
					double time = (lastDataTime.AddMinutes(minTillAutorestart) - DateTime.UtcNow).TotalSeconds;
					if (time < 0)
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
				HttpResponseMessage response = await FetchUtils.client.PostAsync(
					"/live_replay/" + lastFrame.sessionid + "?caprate=1&default=true&client_name=" +
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
					ProcessModule processModule = process[0].MainModule;
					if (processModule != null)
					{
						string newEchoPath = processModule.FileName;
						if (!string.IsNullOrEmpty(newEchoPath))
						{
							SparkSettings.instance.echoVRPath = newEchoPath;
						}
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
		
		/// <returns>True if successfully launched EchoVR, false if not</returns>
		public static bool StartEchoVR(JoinType joinType, int port = 6721, bool noovr = false, string session_id = null, string level = null, string region = null, bool combat=false, int teamIndex = -1, bool quitIfError = false, string gameType = null)
		{
			if (joinType == JoinType.Choose)
			{
				new MessageBox("Can't launch with join type Choose", Resources.Error, quitIfError ? Quit: () => { }).Show();
				return false;
			}
			string echoPath = SparkSettings.instance.echoVRPath;
			if (!string.IsNullOrEmpty(echoPath))
			{
				bool spectating = joinType == JoinType.Spectator;
				Process.Start(echoPath, 
					((spectating && SparkSettings.instance.capturevp2) || SparkSettings.instance.capturevp2VR ? "-capturevp2 " : " ") + 
					(spectating ? "-spectatorstream " : " ") +
					(combat ? "echo_combat " : "") + 
					(session_id == null ? "" : $"-lobbyid {session_id} ") +  
					(noovr ? "-noovr " : "") +
					($"-httpport {port} ") +
					(level == null ? "" : $"-level {level} ") +
					(region == null ? "" : $"-region {region} ") + 
					(string.IsNullOrEmpty(gameType) ? "" : $"-gametype {gameType} ") + 
					(teamIndex == -1 ? "" : $"-lobbyteam {teamIndex} ")
				);
			}
			else
			{
				new MessageBox(Resources.echovr_path_not_set, Resources.Error, quitIfError ? Quit: () => { }).Show();
				return false;
			}

			return true;
		}

		public static async Task<bool> APIJoin(string session_id, int teamIndex = -1, string overrideIP = null, int overridePort = 0)
		{
			try
			{
				Dictionary<string, object> body = new Dictionary<string, object>()
				{
					{ "session_id", session_id },
					{ "team_idx", teamIndex },
				};

				string ip = overrideIP ?? SparkSettings.instance.echoVRIP;
				int port = overridePort == 0 ? SparkSettings.instance.echoVRPort : overridePort;
				string resp = await FetchUtils.PostRequestAsync($"http://{ip}:{port}/join_session", null, JsonConvert.SerializeObject(body));
				return !string.IsNullOrEmpty(resp) && resp.StartsWith("{");
			}
			catch (Exception)
			{
				return false;
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
		private static void GenerateEvents(Frame frame, Frame lastFrame)
		{
			// add a new round for the first frame
			if (lastFrame == null || frame.sessionid != lastFrame.sessionid)
			{
				rounds.Enqueue(new AccumulatedFrame(frame, null));
			}
			
			if (lastConnectionStateEvent != ConnectionState.InGame)
			{
				if (connectionState != ConnectionState.InGame)
				{
					LogRow(LogType.Error, "1982: Can't be in this state.");
				}
				try
				{
					JoinedGame?.Invoke(frame);

				}
				catch (Exception exp)
				{
					LogRow(LogType.Error, "Error processing action", exp.ToString());
				}
			}
			lastConnectionStateEvent = connectionState;

			if (frame == null)
			{
				LogRow(LogType.Error, "7193: Frame is null when generating events.");
				return;
			}

			// lobby stuff

			if (frame.game_status == null) return;
			if (frame.InLobby) return;
			
			
			// this frame is an Arena frame
			if (frame.InArena)
			{
				try
				{
					NewArenaFrame?.Invoke(frame);
				}
				catch (Exception exp)
				{
					LogRow(LogType.Error, "Error processing action", exp.ToString());
				}
			}
			else if (frame.InCombat)
			{
				try
				{
					NewCombatFrame?.Invoke(frame);
				}
				catch (Exception exp)
				{
					LogRow(LogType.Error, "Error processing action", exp.ToString());
				}
			}
			else
			{
				LogRow(LogType.Error, "2698: Can't be in this state.");
			}
			
			// if we entered a different match
			if (lastFrame == null || frame.sessionid != lastFrame.sessionid)
			{
				lastFrame = frame; // don't detect stats changes across matches
				
				try
				{
					NewMatch?.Invoke(frame);
				}
				catch (Exception exp)
				{
					LogRow(LogType.Error, "Error processing action", exp.ToString());
				}
			}

			// The time between the current frame and last frame in seconds based on the game clock
			float deltaTime = lastFrame.game_clock - frame.game_clock;
			if (deltaTime != 0)
			{
				if (Math.Abs(smoothDeltaTime - (-1)) < .001f) smoothDeltaTime = deltaTime;
				const float smoothingFactor = .99f;
				smoothDeltaTime = smoothDeltaTime * smoothingFactor + deltaTime * (1 - smoothingFactor);
			}
			

			int currentFrameStats = 0;
			foreach (Team team in frame.teams)
			{
				// Loop through players on team.
				foreach (Player player in team.players)
				{
					currentFrameStats += player.stats.stuns + player.stats.points;
				}
			}

			if (currentFrameStats < lastFrameSumOfStats)
			{
				lastValidStatsFrame = lastFrame;
				lastValidSumOfStatsAge = 0;
				// LogRow(LogType.File, frame.sessionid, $"{frame.game_clock_display} - Stats reset by game - {lastFrame.game_status}->{frame.game_status}");
			}

			lastValidSumOfStatsAge++;
			lastFrameSumOfStats = currentFrameStats;


			// Did the game state change?
			if (frame.game_status != lastFrame.game_status)
			{
				try
				{
					GameStatusChanged?.Invoke(lastFrame, frame);
				}
				catch (Exception exp)
				{
					LogRow(LogType.Error, "Error processing action", exp.ToString());
				}
				
				ProcessGameStateChange(frame, lastFrame, deltaTime);
				
				// Autofocus
				if (SparkSettings.instance.isAutofocusEnabled)
				{
					FocusEchoVR();
				}
			}

			if (frame.orange_round_score != lastFrame.orange_round_score)
			{
				LogRow(LogType.File, frame.sessionid, $"{frame.game_clock_display} - Orange round score: {lastFrame.orange_round_score} -> {frame.orange_round_score}");
				
			}
			if (frame.blue_round_score != lastFrame.blue_round_score)
			{
				LogRow(LogType.File, frame.sessionid, $"{frame.game_clock_display} - Blue round score: {lastFrame.blue_round_score} -> {frame.blue_round_score}");
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
						EventData joinEvent = new EventData(
							CurrentRound,
							EventContainer.EventType.player_joined,
							frame.game_clock,
							team,
							player,
							null,
							player.head.Position,
							Vector3.Zero);
						CurrentRound.events.Enqueue(joinEvent);
						
						// cache this players stats so they aren't overwritten
						CurrentRound.GetPlayerData(player)?.CacheStats(player.stats);

						if (team.color != Team.TeamColor.spectator)
						{
							// find the vrml team names
							CurrentRound.teams[team.color].FindTeamNamesFromPlayerList(team);
						}

						try
						{
							PlayerJoined?.Invoke(frame, team, player);
							OnEvent?.Invoke(joinEvent);
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

					EventData leaveEvent = new EventData(
						CurrentRound,
						EventContainer.EventType.player_left,
						frame.game_clock,
						team,
						player,
						null,
						player.head.Position,
						player.velocity.ToVector3());
					
					CurrentRound.events.Enqueue(leaveEvent);

					try
					{
						PlayerLeft?.Invoke(frame, team, player);
						OnEvent?.Invoke(leaveEvent);
					}
					catch (Exception exp)
					{
						LogRow(LogType.Error, "Error processing action", exp.ToString());
					}
					
					// find the vrml team names
					CurrentRound.teams[team.color].FindTeamNamesFromPlayerList(team);
				}
			}

			// Did a player switch teams? (Player Switch 🔁)
			// Loop through current frame teams.
			foreach (Team team in frame.teams)
			{
				// Loop through players on team.
				foreach (Player player in team.players)
				{
					Team lastTeam = lastFrame.GetTeam(player.userid);
					if (lastTeam == null) continue;
					if (lastTeam.color == team.color) continue;

					EventData switchedTeamsEvent = new EventData(
						CurrentRound,
						EventContainer.EventType.player_switched_teams,
						frame.game_clock,
						team,
						player,
						null,
						player.head.Position,
						player.velocity.ToVector3());
					CurrentRound.events.Enqueue(switchedTeamsEvent
					);

					try
					{
						PlayerSwitchedTeams?.Invoke(frame, lastTeam, team, player);
						OnEvent?.Invoke(switchedTeamsEvent);
					}
					catch (Exception exp)
					{
						LogRow(LogType.Error, "Error processing action", exp.ToString());
					}
				}
			}


			


			// pause state changed
			try
			{
				if (frame.pause.paused_state != lastFrame.pause.paused_state)
				{
					if (frame.pause.paused_state == "paused")
					{
						(Player minPlayer, float minDistance) = ClosestPlayerToPodium(frame, frame.pause.paused_requested_team);

						EventData pauseEvent = new EventData(
							CurrentRound,
							EventContainer.EventType.pause_request,
							frame.game_clock,
							frame.teams[frame.pause.paused_requested_team == "blue" ? (int)Team.TeamColor.blue : (int)Team.TeamColor.orange],
							minPlayer,
							null,
							minPlayer?.head.Position ?? Vector3.Zero,
							Vector3.Zero);
						
						CurrentRound.events.Enqueue(pauseEvent);

						try
						{
							GamePaused?.Invoke(frame, minPlayer, minDistance);
							OnEvent?.Invoke(pauseEvent);
						}
						catch (Exception exp)
						{
							LogRow(LogType.Error, "Error processing action", exp.ToString());
						}
					}

					if (lastFrame.pause.paused_state == "unpaused" &&
						frame.pause.paused_state == "paused_requested")
					{
						(Player minPlayer, float minDistance) = ClosestPlayerToPodium(frame, frame.pause.paused_requested_team);
						
						try
						{
							PauseRequest?.Invoke(frame, minPlayer, minDistance);
						}
						catch (Exception exp)
						{
							LogRow(LogType.Error, "Error processing action", exp.ToString());
						}
					}

					if (lastFrame.pause.paused_state == "paused" &&
						frame.pause.paused_state == "unpausing")
					{
						(Player minPlayer, float minDistance) = ClosestPlayerToPodium(frame, frame.pause.unpaused_team);
						
						try
						{
							GameUnpaused?.Invoke(frame, minPlayer, minDistance);
						}
						catch (Exception exp)
						{
							LogRow(LogType.Error, "Error processing action", exp.ToString());
						}
					}
				}
			}
			catch (Exception e)
			{
				LogRow(LogType.Error, $"Error with pause request parsing\n{e}");
			}


			// calculate a smoothed server score
			List<int>[] pings =
			{
				frame.teams[0].players.Select(p => p.ping).ToList(),
				frame.teams[1].players.Select(p => p.ping).ToList()
			};
			float newServerScore = CalculateServerScore(pings[0], pings[1]);

			// reset the smoothing every time it switches to being valid
			if (CurrentRound.serverScore < 0)
			{
				CurrentRound.serverScore = newServerScore;
				CurrentRound.smoothedServerScore = CurrentRound.serverScore;
			}
			else
			{
				CurrentRound.serverScore = newServerScore;
				float t = 1f - MathF.Pow(1 - serverScoreSmoothingFactor, deltaTime);
				CurrentRound.smoothedServerScore = Math2.Lerp(CurrentRound.smoothedServerScore, CurrentRound.serverScore, t);
			}


			// Score
			if (lastFrame.game_status == "playing" &&
			    (lastFrame.orange_points < frame.orange_points ||
			    lastFrame.blue_points < frame.blue_points))
			{
				_ = ProcessScore(CurrentRound);
			}


			if (lastFrame.rules_changed_at != frame.rules_changed_at)
			{
				try
				{
					RulesChanged?.Invoke(frame);
				}
				catch (Exception exp)
				{
					LogRow(LogType.Error, "Error processing action", exp.ToString());
				}
			}
			
			
			// Generate events for each player even when not playing
			foreach (Team team in frame.teams)
			{
				foreach (Player player in team.players)
				{
					Player lastPlayer = lastFrame.GetPlayer(player.userid);
					if (lastPlayer == null) continue;

					MatchPlayer playerData = CurrentRound.GetPlayerData(player);
					if (playerData != null)
					{
						// check emote activation
						if (player.is_emote_playing && !lastPlayer.is_emote_playing)
						{
							EventData eventData = new EventData(
								CurrentRound,
								EventContainer.EventType.emote,
								frame.game_clock,
								team,
								player,
								null,
								player.head.Position,
								player.velocity.ToVector3()
							);

							float leftHandDistance = Vector3.Distance(player.lhand.Position,player.head.Position); 
							float rightHandDistance = Vector3.Distance(player.rhand.Position,player.head.Position);
							float leftHandHorizontalDistance = Vector3.Dot(player.head.left.ToVector3(), player.head.Position - player.lhand.Position);
							float rightHandHorizontalDistance = Vector3.Dot(player.head.left.ToVector3(), player.head.Position - player.rhand.Position);

							bool isLeft;
							if (leftHandDistance < rightHandDistance)
							{
								isLeft = leftHandHorizontalDistance < 0;
							}
							else
							{
								isLeft = rightHandHorizontalDistance < 0;
							}
							try
							{
								EmoteActivated?.Invoke(frame, team, player, isLeft);
								OnEvent?.Invoke(eventData);
							}
							catch (Exception exp)
							{
								LogRow(LogType.Error, "Error processing action", exp.ToString());
							}

							CurrentRound.events.Enqueue(eventData);
							
							// emotes are always local-only
							if (player.name == frame.client_name)
							{
								HighlightsHelper.SaveHighlightMaybe(player.name, frame, "EMOTE");
							}
						}
					}
				}
			}
			
			
			// check for local throws
			if (!lastFrame.last_throw.Equals(frame.last_throw))
			{
				Team clientPlayerTeam = frame?.GetTeam(frame.client_name);
				Player clientPlayer = frame?.GetPlayer(frame.client_name);

				CurrentRound.events.Enqueue(new EventData(CurrentRound, EventContainer.EventType.local_throw,
					frame.game_clock, clientPlayerTeam, clientPlayer, frame.last_throw));

				try
				{
					LocalThrow?.Invoke(frame);
				}
				catch (Exception exp)
				{
					LogRow(LogType.Error, "Error processing action", exp.ToString());
				}
			}

			// while playing and frames aren't identical
			if (frame.game_status == "playing" && deltaTime != 0)
			{
				if (SparkSettings.instance.isAutofocusEnabled && (Math.Round(frame.game_clock, 2, MidpointRounding.AwayFromZero) % 10 == 0))
				{
					FocusEchoVR();
				}

				CurrentRound.currentDiskTrajectory.Add(frame.disc.position.ToVector3());

				if (frame.disc.velocity.ToVector3().Equals(Vector3.Zero))
				{
					wasThrown = false;
				}



				// Generate "playing" events
				foreach (Team team in frame.teams)
				{
					foreach (Player player in team.players)
					{
						Player lastPlayer = lastFrame.GetPlayer(player.userid);
						if (lastPlayer == null) continue;

						MatchPlayer playerData = CurrentRound.GetPlayerData(player);
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

								(float boostSpeed, float howLongAgoBoost) = playerData.GetMaxRecentVelocity(reset: true);


								EventData bigBoostEvent = new EventData(
									CurrentRound,
									EventContainer.EventType.big_boost,
									frame.game_clock + howLongAgoBoost,
									team,
									player,
									null,
									player.head.Position,
									new Vector3(boostSpeed, 0, 0)
								);
								CurrentRound.events.Enqueue(bigBoostEvent);
								
								
								try
								{
									BigBoost?.Invoke(frame, team, player, boostSpeed, howLongAgoBoost);
									OnEvent?.Invoke(bigBoostEvent);
								}
								catch (Exception exp)
								{
									LogRow(LogType.Error, "Error processing action", exp.ToString());
								}

								HighlightsHelper.SaveHighlightMaybe(player.name, frame, "BIG_BOOST");

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

							// conditions to allow playspace abuse checking
							if (team.color != Team.TeamColor.spectator && Math.Abs(smoothDeltaTime) < .1f &&
								Math.Abs(deltaTime) < .1f &&
								Vector3.Distance(player.head.Position, playerData.playspaceLocation) > 1.7f &&
								DateTime.UtcNow - playerData.lastAbuse > TimeSpan.FromSeconds(3) &&    // create a 3 second buffer between detections
								playerData.playspaceInvincibility <= TimeSpan.Zero
								)
							{
								// playspace abuse happened
							

								EventData eventData = new EventData(
									CurrentRound,
									EventContainer.EventType.playspace_abuse,
									frame.game_clock,
									team,
									player,
									null,
									player.head.Position,
									player.head.Position - playerData.playspaceLocation);
								
								CurrentRound.events.Enqueue(eventData);
								try
								{
									PlayspaceAbuse?.Invoke(frame, team, player, playerData.playspaceLocation);
									OnEvent?.Invoke(eventData);
								}
								catch (Exception exp)
								{
									LogRow(LogType.Error, "Error processing action", exp.ToString());
								}
								
								playerData.PlayspaceAbuses++;

								// reset the playspace so we don't get extra events
								playerData.playspaceLocation = player.head.Position;
							}
							else if (Math.Abs(smoothDeltaTime) > .2f)
							{
								Debug.WriteLine("Update rate too slow to calculate playspace abuses.");
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
							EventData eventData = new EventData(
								CurrentRound,
								EventContainer.EventType.save,
								frame.game_clock,
								team,
								player,
								null,
								player.head.Position,
								Vector3.Zero
							);
							try
							{
								Save?.Invoke(frame, eventData);
								OnEvent?.Invoke(eventData);
							}
							catch (Exception exp)
							{
								LogRow(LogType.Error, "Error processing action", exp.ToString());
							}

							CurrentRound.events.Enqueue(eventData);
							HighlightsHelper.SaveHighlightMaybe(player.name, frame, "SAVE");
						}

						// check steals 🕵️‍
						if (lastPlayer.stats.steals != player.stats.steals)
						{
							EventData eventData = new EventData(CurrentRound, EventContainer.EventType.steal, frame.game_clock,
								team, player, null, player.head.Position, Vector3.Zero);
							try
							{
								Steal?.Invoke(frame, eventData);
								OnEvent?.Invoke(eventData);
							}
							catch (Exception exp)
							{
								LogRow(LogType.Error, "Error processing action", exp.ToString());
							}

							CurrentRound.events.Enqueue(eventData);

							if (WasStealNearGoal(frame.disc.position.ToVector3(), team.color, frame))
							{
								HighlightsHelper.SaveHighlightMaybe(player.name, frame, "STEAL_SAVE");
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

										EventData stunEventData = new EventData(CurrentRound, EventContainer.EventType.stun,
											frame.game_clock, team, stunner, stunnee, stunnee.head.Position,
											Vector3.Zero);
										CurrentRound.events.Enqueue(stunEventData);
										added = true;

										try
										{
											Stun?.Invoke(frame, stunEventData);
											OnEvent?.Invoke(stunEventData);
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

										EventData stunEventData = new EventData(CurrentRound, EventContainer.EventType.stun,
											frame.game_clock, team, stunner, stunnee, stunnee.head.Position,
											Vector3.Zero);
										CurrentRound.events.Enqueue(stunEventData);
										added = true;

										try
										{
											Stun?.Invoke(frame, stunEventData);
											OnEvent?.Invoke(stunEventData);
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
							EventData eventData = new EventData(CurrentRound, EventContainer.EventType.@catch, frame.game_clock,
								team, player, null, player.head.Position, Vector3.Zero);
							CurrentRound.events.Enqueue(eventData);
							playerData.Catches++;
							bool caughtThrow = false;
							Team throwTeam = null;
							Player throwPlayer = null;
							bool wasTurnoverCatch = false;
							
							try
							{
								Catch?.Invoke(frame, team, player);
								OnEvent?.Invoke(eventData);
							}
							catch (Exception exp)
							{
								LogRow(LogType.Error, "Error processing action", exp.ToString());
							}

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
									
									EventData turnoverEvent = new EventData(CurrentRound, EventContainer.EventType.turnover, frame.game_clock, team, throwPlayer, player, throwPlayer.head.Position, player.head.Position);
									
									try
									{
										Turnover?.Invoke(frame, team, throwPlayer, player);
										OnEvent?.Invoke(turnoverEvent);
									}
									catch (Exception exp)
									{
										LogRow(LogType.Error, "Error processing action", exp.ToString());
									}
									
									// TODO enable once the db can handle it
									CurrentRound.events.Enqueue(turnoverEvent);
									CurrentRound.GetPlayerData(throwPlayer).Turnovers++;
								}
								else
								{
									EventData passEvent = new EventData(CurrentRound, EventContainer.EventType.pass,
										frame.game_clock, team, throwPlayer, player, throwPlayer.head.Position,
										player.head.Position);
									try
									{
										Pass?.Invoke(frame, team, throwPlayer, player);
										OnEvent?.Invoke(passEvent);
									}
									catch (Exception exp)
									{
										LogRow(LogType.Error, "Error processing action", exp.ToString());
									}

									CurrentRound.events.Enqueue(passEvent);
									CurrentRound.GetPlayerData(throwPlayer).Passes++;
								}
							}
						}

						// check shots taken 🧺
						if (lastPlayer.stats.shots_taken != player.stats.shots_taken)
						{

							EventData eventData = new EventData(CurrentRound, EventContainer.EventType.shot_taken,
								frame.game_clock, team, player, null, player.head.Position, Vector3.Zero);
							CurrentRound.events.Enqueue(eventData);
							
							
							try
							{
								ShotTaken?.Invoke(frame, team, player);
								OnEvent?.Invoke(eventData);
							}
							catch (Exception exp)
							{
								LogRow(LogType.Error, "Error processing action", exp.ToString());
							}
							
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
							Vector3 leftHandVelocity = (lastPlayer.lhand.Position - player.lhand.Position) / deltaTime;
							Vector3 rightHandVelocity = (lastPlayer.rhand.Position - player.rhand.Position) / deltaTime;

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
							CurrentRound.events.Enqueue(new EventData(CurrentRound, EventContainer.EventType.pass, frame.game_clock,
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
						(Player minPlayer, float minDistance) = ClosestPlayerToPodium(frame, "blue");
						

						EventData eventData = new EventData(CurrentRound, EventContainer.EventType.restart_request,
							lastFrame.game_clock, frame.teams[(int)Team.TeamColor.blue], null, null, Vector3.Zero,
							Vector3.Zero);
						CurrentRound.events.Enqueue(eventData);
						
						try
						{
							RestartRequest?.Invoke(frame, Team.TeamColor.blue, minPlayer, minDistance);
							OnEvent?.Invoke(eventData);
						}
						catch (Exception exp)
						{
							LogRow(LogType.Error, "Error processing action", exp.ToString());
						}
					}

					// check orange restart request ↩
					if (!lastFrame.orange_team_restart_request && frame.orange_team_restart_request)
					{
						(Player minPlayer, float minDistance) = ClosestPlayerToPodium(frame, "orange");


						EventData eventData = new EventData(CurrentRound, EventContainer.EventType.restart_request,
							lastFrame.game_clock, frame.teams[(int)Team.TeamColor.orange], null, null, Vector3.Zero,
							Vector3.Zero);
						CurrentRound.events.Enqueue(eventData);
						try
						{
							RestartRequest?.Invoke(frame, Team.TeamColor.orange, minPlayer, minDistance);
							OnEvent?.Invoke(eventData);
						}
						catch (Exception exp)
						{
							LogRow(LogType.Error, "Error processing action", exp.ToString());
						}
					}
				}
				catch (Exception)
				{
					LogRow(LogType.Error, "Error with restart request parsing");
				}
			}
		}

		private static void OnJoinedGame(Frame frame)
		{
			if (!string.IsNullOrWhiteSpace(frame.client_name) && frame.client_name != "anonymous")
			{
				SparkSettings.instance.client_name = frame.client_name;
			}


			// make sure there is a valid echovr path saved
			if (SparkSettings.instance.echoVRPath == "" || SparkSettings.instance.echoVRPath.Contains("win7"))
			{
				UpdateEchoExeLocation();
			}
		}

		private static (Player, float) ClosestPlayerToPodium(Frame frame, string teamColor)
		{
			float minDistance = float.MaxValue;
			Player minPlayer = null;

			foreach (Player p in frame.GetAllPlayers())
			{
				Vector3 terminalPos = teamColor == "blue"
					? new Vector3(0, -3.5f, -73.46f)
					: new Vector3(0, -3.5f, 73.46f);

				float leftDist = Vector3.Distance(p.lhand.Position, terminalPos);
				if (leftDist < minDistance)
				{
					minPlayer = p;
					minDistance = leftDist;
				}

				float rightDist = Vector3.Distance(p.rhand.Position, terminalPos);
				if (rightDist < minDistance)
				{
					minPlayer = p;
					minDistance = rightDist;
				}
			}

			return (minPlayer, minDistance);
		}


		


		// 💨
		private static async Task JoustDetection(Frame firstFrame, EventContainer.EventType eventType, Team.TeamColor side)
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
							CurrentRound,
							eventType,
							frame.game_clock,
							team,
							player,
							(long)((startGameClock - frame.game_clock) * 1000),
							player.head.Position,
							new Vector3(
								maxSpeed,
								maxTubeExitSpeed,
								startGameClock - frame.game_clock)
						);


						CurrentRound.events.Enqueue(joustEvent);

						try
						{
							Joust?.Invoke(frame, team, player, eventType == EventContainer.EventType.joust_speed, startGameClock - frame.game_clock, maxSpeed, maxTubeExitSpeed);
							JoustEvent?.Invoke(frame, joustEvent);
							OnEvent?.Invoke(joustEvent);
						}
						catch (Exception exp)
						{
							LogRow(LogType.Error, "Error processing action", exp.ToString());
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
			if (CurrentRound == null)
			{
				LogRow(LogType.Error, "6859: Past end of the match?");
				return;
			}

			foreach (Team team in frame.teams)
			{
				foreach (Player player in team.players)
				{
					if (player.playerid != originalPlayer.playerid) continue;
					if (player.stats.saves != originalPlayer.stats.saves) continue;

					EventData eventData = new EventData(
						CurrentRound, 
						EventContainer.EventType.interception, 
						frame.game_clock, 
						team, player, null, 
						player.head.Position, 
						Vector3.Zero
						);
					try
					{
						Interception?.Invoke(originalFrame, originalTeam, throwPlayer, originalPlayer);
						OnEvent?.Invoke(eventData);
					}
					catch (Exception exp)
					{
						LogRow(LogType.Error, "Error processing action", exp.ToString());
					}

					// TODO enable this once the db supports it
					CurrentRound.events.Enqueue(eventData);
					MatchPlayer otherMatchPlayer = CurrentRound.GetPlayerData(player);
					if (otherMatchPlayer != null) otherMatchPlayer.Interceptions++;
					else LogRow(LogType.Error, "Can't find player by name from other team: " + player.name);

					HighlightsHelper.SaveHighlightMaybe(player.name, frame, "INTERCEPTION");
				}
			}
		}

		private static async Task DelayedThrowEvent(Player originalPlayer, bool leftHanded, float underhandedness, float origSpeed)
		{
			// wait some time before re-checking the throw velocity
			await Task.Delay(150);

			// throw expired
			if (CurrentRound == null)
			{
				LogRow(LogType.Error, "8593: Past end of the match?");
				return;
			}

			Frame frame = lastFrame;

			foreach (Team team in frame.teams)
			{
				foreach (Player player in team.players)
				{
					if (player.playerid == originalPlayer.playerid)
					{
						if (player.possession && !frame.disc.velocity.ToVector3().Equals(Vector3.Zero))
						{

							EventData eventData = new EventData(CurrentRound, EventContainer.EventType.@throw, frame.game_clock,
								team, player, null, player.head.Position, frame.disc.velocity.ToVector3());
							CurrentRound.events.Enqueue(eventData);
							
							try
							{
								Throw?.Invoke(frame, team, player, leftHanded, underhandedness);
								OnEvent?.Invoke(eventData);
							}
							catch (Exception exp)
							{
								LogRow(LogType.Error, "Error processing action", exp.ToString());
							}

							CurrentRound.currentDiskTrajectory.Clear();

							// add throw data type
							CurrentRound.throws.Enqueue(
								new ThrowData(
									CurrentRound,
									frame.game_clock,
									player,
									frame.disc.position.ToVector3(),
									frame.disc.velocity.ToVector3(),
									leftHanded,
									underhandedness
								)
							);
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
		/// Function used to execute certain behavior based on frame given and previous frame(s).
		/// </summary>
		/// <param name="frame"></param>
		/// <param name="deltaTime"></param>
		private static void ProcessGameStateChange(Frame frame, Frame lastFrame, float deltaTime)
		{
			// LogRow(LogType.File, frame.sessionid, $"{lastFrame.game_clock_display} - Left state: {lastFrame.game_status} ({lastFrame.orange_round_score}+{lastFrame.blue_round_score})/{lastFrame.total_round_count}");
			// LogRow(LogType.File, frame.sessionid, $"{frame.game_clock_display} - Entered state: {frame.game_status} ({frame.orange_round_score}+{frame.blue_round_score})/{frame.total_round_count}");

			switch (frame.game_status)
			{
				case "pre_match":
					// if we just came from a playing state, this was a reset - requires a high enough polling rate
					if (lastFrame.game_status == "playing" || lastFrame.game_status == "round_start")
					{
						EventMatchFinished(lastFrame, frame, AccumulatedFrame.FinishReason.reset);
						
						rounds.Enqueue(new AccumulatedFrame(frame, null));
						while (rounds.Count > 50)
						{
							rounds.TryDequeue(out AccumulatedFrame _);
						}

						try
						{
							MatchReset?.Invoke(lastFrame);
						}
						catch (Exception exp)
						{
							LogRow(LogType.Error, "Error processing action", exp.ToString());
						}
					}
					break;

				// round began
				case "round_start":

					// new round of a private match started.
					// this happens when the scores are reset and players teleport back to the start
					if (lastFrame.game_status == "" && CurrentRound.finishReason != AccumulatedFrame.FinishReason.not_finished)
					{
						rounds.Enqueue(new AccumulatedFrame(frame, rounds.Last()));
						while (rounds.Count > 50)
						{
							rounds.TryDequeue(out AccumulatedFrame _);
						}
						
						try
						{
							NewRound?.Invoke(frame);
						}
						catch (Exception exp)
						{
							LogRow(LogType.Error, "Error processing action", exp.ToString());
						}
					}
					
					// if we just started a new 'round' (so stats haven't been reset)
					if (lastFrame.game_status == "round_over")
					{
						// foreach (MatchPlayer player in CurrentRound.players.Values)
						// {
						// 	// TODO isn't this just a shallow copy anyway and won't do anything? How is this working?
						// 	MatchPlayer lastPlayer = LastRound.GetPlayerData(player.Id);
						//
						// 	if (lastPlayer != null)
						// 	{
						// 		player.StoreLastRoundStats(lastPlayer);
						// 	}
						// 	else
						// 	{
						// 		LogRow(LogType.Error, "Player exists in this round but not in last. Y");
						// 	}
						// }
					}

					break;

				// round really began
				case "playing":

					#region Started Playing
					
					// Loop through teams.
					foreach (Team team in frame.teams)
					{
						// Loop through players on team.
						foreach (Player player in team.players)
						{
							// reset playspace
							MatchPlayer playerData = CurrentRound.GetPlayerData(player);
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
							_ = JoustDetection(frame, EventContainer.EventType.joust_speed, Team.TeamColor.blue);
							_ = JoustDetection(frame, EventContainer.EventType.joust_speed, Team.TeamColor.orange);
						}
						// if the disc is on the orange nest
						else if (Math.Abs(zDiscPos + 27.5f) < 1)
						{
							_ = JoustDetection(frame, EventContainer.EventType.defensive_joust, Team.TeamColor.orange);
						}
						// if the disc is on the blue nest
						else if (Math.Abs(zDiscPos - 27.5f) < 1)
						{
							_ = JoustDetection(frame, EventContainer.EventType.defensive_joust, Team.TeamColor.blue);
						}
					}

					#endregion

					break;

				// just scored
				case "score":
					break;

				case "round_over":
					if ((int)frame.blue_points == (int)frame.orange_points)
					{
						// OVERTIME
						LogRow(LogType.Info, "overtime");
					}
					// mercy win
					else if (!frame.last_score.Equals(lastLastLastFrame.last_score))
					{
						EventMatchFinished(lastFrame, frame, AccumulatedFrame.FinishReason.mercy);
					}
					else if (frame.game_clock == 0 || lastFrame.game_clock < deltaTime * 10 || deltaTime < 0)
					{
						EventMatchFinished(lastFrame, frame, AccumulatedFrame.FinishReason.game_time);
					}
					else if (lastFrame.game_clock < deltaTime * 10 || lastFrame.game_status == "post_sudden_death" ||
							 deltaTime < 0)
					{
						// TODO find why finished and set reason
						EventMatchFinished(lastFrame, frame, AccumulatedFrame.FinishReason.not_finished);
						Error("Match finished for unknown reason");
					}
					else
					{
						EventMatchFinished(lastFrame, frame, AccumulatedFrame.FinishReason.not_finished);
						Error($"Match finished for unknown reason 2. {frame.game_clock} {frame.orange_points} {frame.blue_points} {frame.private_match}");
					}

					break;

				// Game finished and showing scoreboard
				case "post_match":
					// if (frame.private_match)
					// {
					// 	RoundOver?.Invoke(frame);
					// }

					//EventMatchFinished(frame, MatchData.FinishReason.not_finished);
					break;

				case "pre_sudden_death":
					LogRow(LogType.Error, "pre_sudden_death");
					break;
				case "sudden_death":
					// this happens right as the match finishes in a tie
					CurrentRound.overtimeCount++;
					break;
				case "post_sudden_death":
					LogRow(LogType.Error, "post_sudden_death");
					break;
			}
			
		}

		private static bool WasStealNearGoal(Vector3 disckPos, Team.TeamColor playerTeamColor, Frame frame)
		{
			const float stealSaveRadius = 2.2f;

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
		private static async Task ProcessScore(AccumulatedFrame matchData)
		{
			Frame initialFrame = lastLastFrame;
			GoalImmediate?.Invoke(lastFrame);

			// wait some time before re-checking the throw velocity
			await Task.Delay(150);

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

			Player scorer = frame.GetPlayer(frame.last_score.person_scored);
			if (scorer != null)
			{
				MatchPlayer scorerPlayerData = matchData.GetPlayerData(scorer);
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
			if (matchData.throws.Count > 0)
			{
				ThrowData lastThrow = matchData.throws.Last();
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
			matchData.goals.Enqueue(goalEvent);
			
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
		}

		/// <summary>
		/// Function to wrap up the match once we've entered post_match, restarted, or left spectate unexpectedly (crash)
		/// </summary>
		/// <param name="lastRoundFrame"></param>
		/// <param name="nextRoundFrame"></param>
		/// <param name="reason"></param>
		private static void EventMatchFinished(Frame lastRoundFrame, Frame nextRoundFrame, AccumulatedFrame.FinishReason reason)
		{
			CurrentRound.finishReason = reason;


			if (lastRoundFrame == null)
			{
				// this happened on a restart right in the beginning once
				LogRow(LogType.Error, "frame is null on match finished event. INVESTIGATE");
				return;
			}
			
			CurrentRound.frame.blue_round_score = lastRoundFrame.blue_round_score;
			CurrentRound.frame.orange_round_score = lastRoundFrame.orange_round_score;

			LogRow(LogType.File, lastRoundFrame.sessionid, "Match Finished: " + reason);

			// show the scores in the log
			LogRow(LogType.File, lastRoundFrame.sessionid, 
				lastRoundFrame.game_clock_display + " - ORANGE: " + lastRoundFrame.orange_points + "  BLUE: " + lastRoundFrame.blue_points);

			RoundOver?.Invoke(lastRoundFrame, reason);
			
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

			JoinType joinType;
			switch (parts[2])
			{
				case "spectate":
				case "spectator":
				case "s":
					joinType = JoinType.Spectator;
					break;
				case "join":
				case "player":
				case "j":
				case "p":
					joinType = JoinType.Player;
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


			_ = Task.Run(async () =>
			{
				try
				{
					FetchUtils.client.Timeout = TimeSpan.FromSeconds(1);
					HttpResponseMessage response = await FetchUtils.client.GetAsync($"http://{SparkSettings.instance.echoVRIP}:{SparkSettings.instance.echoVRPort}/session");
					if (response.StatusCode == HttpStatusCode.OK)
					{
						await APIJoin(parts[3], joinType == JoinType.Spectator ? 2 : -1);
					}
				}
				catch (Exception e)
				{
					new MessageBox(Resources.Failed_to_send_join_data_to_the_game__Maybe_you_left_the_game_, Resources.Error, Quit).Show();
				}
			});
			


			// start client
			// quit immediately if successful, otherwise quit from error messages
			if (StartEchoVR(joinType, session_id:parts[3], quitIfError:true)) Quit();

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

		private static async void PingIPList(IEnumerable<IPAddress> IPs, int threadID)
		{
			IEnumerable<Task<PingReply>> tasks = IPs.Select(ip => new Ping().SendPingAsync(ip, 4000));
			PingReply[] results = await Task.WhenAll(tasks);
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

		private static void PingNetworkIPs(IPAddress address, IPAddress mask)
		{
			uint ipAddress = BitConverter.ToUInt32(address.GetAddressBytes(), 0);
			uint ipMaskV4 = BitConverter.ToUInt32(mask.GetAddressBytes(), 0);
			uint broadCastIpAddress = ipAddress | ~ipMaskV4;

			IPAddress start = new IPAddress(BitConverter.GetBytes(broadCastIpAddress));

			byte[] bytes = start.GetAddressBytes();
			byte leastSigByte = address.GetAddressBytes().Last();
			int range = 255 - leastSigByte;

			List<IPAddress> pingReplyTasks = Enumerable.Range(leastSigByte, range)
				.Select(x =>
				{
					byte[] bb = start.GetAddressBytes();
					bb[3] = (byte)x;
					IPAddress destIp = new IPAddress(bb);
					return destIp;
				})
				.ToList();
			List<IPAddress> pingReplyTasks2 = Enumerable.Range(0, leastSigByte - 1)
				.Select(x =>
				{

					byte[] bb = start.GetAddressBytes();
					bb[3] = (byte)x;
					IPAddress destIp = new IPAddress(bb);
					return destIp;
				})
				.ToList();
			IPSearchThread1 = new Thread(() => PingIPList(pingReplyTasks, 1));
			IPSearchThread2 = new Thread(() => PingIPList(pingReplyTasks2, 2));
			IPPingThread1Done = false;
			IPPingThread2Done = false;
			IPSearchThread1.Start();
			IPSearchThread2.Start();
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
				table[index] = (MIB_IPNETROW)Marshal.PtrToStructure(new IntPtr(currentBuffer.ToInt64() + (index * Marshal.SizeOf(typeof(MIB_IPNETROW)))), typeof(MIB_IPNETROW));
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
				if (spectateMeController.spectateMe)
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
							if (!spectateMeController.spectateMe) return;

							// TODO this crashes on local pc
							result = await FetchUtils.GetRequestAsync($"http://{ip}:{port}/session", null);
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
			}
			catch (Exception e)
			{
				LogRow(LogType.Error, e.ToString());
				new MessageBox($"Failed to open window: {type}.\nPlease report this to NtsFranz.").Show();
				return false;
			}
		}

		public static Window GetWindowIfOpen(Type type, string windowName = null)
		{
			windowName ??= type.ToString();
			return popupWindows.ContainsKey(windowName) ? popupWindows[windowName] : null;
		}

		public static void AutoUploadTabletStats()
		{
			_ = Task.Run(async () =>
			{
				// wait 5 seconds for write to happen
				await Task.Delay(1000);
				
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
				profiles.Sort((p1, p2) => p2.update_time.CompareTo(p1.update_time));
				profiles = profiles.DistinctBy(x => x.player_name).ToList();

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


					StringContent content = new StringContent(dataString, Encoding.UTF8, "application/json");

					try
					{
						HttpResponseMessage response = await FetchUtils.client.PostAsync("/update_tablet_stats?hashkey=" + hash + "&player_name=" + p.player_name, content);
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
		/// <returns>
		/// The server score
		/// -1: Ping over 150
		/// -2: Too few players
		/// -3: Too many players
		/// -4: Teams have different number of players
		/// </returns>
		public static float CalculateServerScore(List<int> bluePings, List<int> orangePings)
		{
			
			if (bluePings == null || orangePings == null)
			{
				return -100;
			}
			
			// configurable parameters for tuning
			int ppt = bluePings.Count; // players per team - can be set to 5 for NEPA
			int min_ping = 10; // you don't lose points for being higher than this value
			int max_ping = 150; // won't compute if someone is over this number
			int ping_threshold = 100; // you lose extra points for being higher than this

			// points_distribution dictates how many points come from each area:
			//   0 - difference in sum of pings between teams
			//   1 - within-team variance
			//   2 - overall server variance
			//   3 - overall high/low pings for server
			int[] points_distribution = { 30, 30, 30, 10 };



			// sanity check for ping values
			switch (bluePings.Count)
			{
				case < 4:
					return -2;
				case > 5:
					return -3;
			}

			if (bluePings.Count != orangePings.Count)
			{
				return -4;
			}

			if (bluePings.Max() > max_ping || orangePings.Max() > max_ping)
			{
				// Console.WriteLine("No player's ping can be over 150.");
				return -1;
			}
			
			
			// determine max possible server/team variance and max possible sum diff,
			// given the min/max allowable ping
			float max_server_var = Variance(Enumerable.Repeat(min_ping, ppt).Concat(Enumerable.Repeat(max_ping, ppt)).ToArray());
			float max_team_var = Variance(Enumerable.Repeat(min_ping, (int)Math.Floor(ppt / 2f)).Concat(Enumerable.Repeat(max_ping, (int)Math.Ceiling(ppt / 2f))).ToArray());
			float max_sum_diff = (ppt * max_ping) - (ppt * min_ping);

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
		
			List<int> bothPings = new List<int>(bluePings);
			bothPings.AddRange(orangePings);

			// calculate points for server variance
			float server_var = Variance(bothPings);
			float server_points = (1 - (server_var / max_server_var)) * points_distribution[2];

			// calculate points for high/low ping across server
			float hilo = ((blueSum + orangeSum) - (min_ping * ppt * 2)) / ((ping_threshold * ppt * 2) - (min_ping * ppt * 2));
			float hilo_points = (1 - hilo) * points_distribution[3];

			// add up points
			float final = sum_points + team_points + server_points + hilo_points;

			return final;
		}


		public static float Variance(IEnumerable<float> values)
		{
			IEnumerable<float> enumerable = values as float[] ?? values.ToArray();
			float avg = enumerable.Average();
			return enumerable.Average(v => MathF.Pow(v - avg, 2));
		}

		public static float Variance(IEnumerable<int> values)
		{
			IEnumerable<int> enumerable = values as int[] ?? values.ToArray();
			float avg = (float)enumerable.Average();
			return enumerable.Average(v => MathF.Pow(v - avg, 2));
		}

		// TODO
		public void ShowToast(string text, float duration = 3)
		{

		}

		
		public static string CurrentSparkLink(string sessionid)
		{
			if (string.IsNullOrEmpty(sessionid)) return "---";
			
			string link = "";
			if (SparkSettings.instance.atlasLinkUseAngleBrackets)
			{
				link = SparkSettings.instance.atlasLinkStyle switch
				{
					0 => $"<spark://c/{sessionid}>",
					1 => $"<spark://j/{sessionid}>",
					2 => $"<spark://s/{sessionid}>",
					_ => link
				};
			}
			else
			{
				link = SparkSettings.instance.atlasLinkStyle switch
				{
					0 => $"spark://c/{sessionid}",
					1 => $"spark://j/{sessionid}",
					2 => $"spark://s/{sessionid}",
					_ => link
				};
			}

			if (SparkSettings.instance.atlasLinkAppendTeamNames)
			{
				if (CurrentRound?.teams[Team.TeamColor.blue] != null && 
				    CurrentRound?.teams[Team.TeamColor.orange] != null && 
				    !string.IsNullOrEmpty(CurrentRound.teams[Team.TeamColor.blue].vrmlTeamName) && 
				    !string.IsNullOrEmpty(CurrentRound.teams[Team.TeamColor.orange].vrmlTeamName))
				{
					link += $" {CurrentRound.teams[Team.TeamColor.orange].vrmlTeamName} vs {CurrentRound.teams[Team.TeamColor.blue].vrmlTeamName}";
				}
			}

			return link;
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

			if (liveWindow != null)
			{
				liveWindow.trayIcon.Visibility = Visibility.Collapsed;

				liveWindow.Dispatcher.Invoke(() =>
				{
					closingWindow = new ClosingDialog();
					closingWindow.Show();
				});
			}


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