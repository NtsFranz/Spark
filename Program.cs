#if INCLUDE_FIRESTORE
using Google.Cloud.Firestore;
using Google.Cloud.Firestore.V1;
#endif
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
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
using IgniteBot.Properties;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static IgniteBot.g_Team;
using static Logger;
using NVIDIA;

namespace IgniteBot
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

		/// <summary>
		/// Whether to continue reading input right now (for reading files)
		/// </summary>
		public static bool paused = false;

		// READ FROM FILE
		private const bool READ_FROM_FILE = false;

		/// <summary>
		/// whether to read slower when reading file 
		/// </summary>
		private const bool realtimeWhenReadingFile = false;

		// Should only use queue when reading from file
		private const bool readIntoQueue = true;
		private static bool fileFinishedReading = false;

		public static bool isNVHighlightsEnabled = false;
		public static bool didHighlightsInit = false;
		public static bool isNVHighlightsSupported = true;
		public static HighlightLevel ClientHighlightScope = HighlightLevel.CLIENT_ONLY;

		public static Highlights.EmptyCallbackDelegate videoCallback = NVSetVideoCallback;
		public static Highlights.EmptyCallbackDelegate openSummaryCallback = Highlights.DefaultOpenSummaryCallback;
		public static Highlights.EmptyCallbackDelegate closeGroupCallback = NVCloseGroupCallback;
		public static Highlights.GetNumberOfHighlightsCallbackDelegate getNumOfHighlightsCallback = NVGetNumberOfHighlightsCallback;
		public static Highlights.EmptyCallbackDelegate configStepCallback = NVConfigCallback;

		//private static string readFromFolder = "S:\\git_repo\\EchoVR-Session-Grabber\\bin\\Debug\\full_session_data\\example\\";
		private const string readFromFolder = "F:\\Documents\\EchoDataStorage\\TitanV Machine";
		private static List<string> filesInFolder;
		private static int readFromFolderIndex;

		public const bool uploadOnlyAtEndOfMatch = true;

		public static bool writeToOBSHTMLFile = false;

		public const string APIURL = "https://ignitevr.gg/cgi-bin/EchoStats.cgi/";
		//public const string APIURL = "http://127.0.0.1:5005/";

		public const string UpdateURL = "https://ignitevr.gg/cgi-bin/EchoStats.cgi/";

		public static bool enableStatsLogging = true;
		public static bool enableFullLogging = true;
		public static bool clearHighlightsOnExit = true;

		public static readonly HttpClient client = new HttpClient();

		public static string currentAccessCodeUsername = "";
		public static string currentSeasonName = "";

		public static bool autoRestart;
		public static bool showDatabaseLog;

		// declarations.
		public static MatchData matchData;
		static MatchData lastMatchData;

		/// <summary>
		/// Contains the last state so that we can do a diff to determine state changes
		/// This acts like a set of flags.
		/// </summary>
		public static g_Instance lastFrame;
		public static g_Instance lastLastFrame;
		public static g_Instance lastLastLastFrame;

		private static int lastFrameSumOfStats;
		private static g_Instance lastValidStatsFrame;
		private static int lastValidSumOfStatsAge = 0;
		public static int nvHighlightClipCount = 0;

		public static ConcurrentQueue<GoalData> lastGoals = new ConcurrentQueue<GoalData>();
		public static ConcurrentQueue<MatchData> lastMatches = new ConcurrentQueue<MatchData>();
		public static ConcurrentQueue<EventData> lastJousts = new ConcurrentQueue<EventData>();

		/// <summary>
		/// For replay file saving in batches
		/// </summary>
		private static List<string> dataCache = new List<string>();

		private class UserAtTime
		{
			public float gameClock;
			public g_Player player;
		}

		// { [stunner, stunnee], [stunner, stunnee] }
		static List<UserAtTime[]> stunningMatchedPairs = new List<UserAtTime[]>();
		private const float stunMatchingTimeout = 4f;

		public static string lastDateTimeString;
		public static string lastJSON;
		public static ConcurrentQueue<string> lastJSONQueue = new ConcurrentQueue<string>();
		public static ConcurrentQueue<string> lastDateTimeStringQueue = new ConcurrentQueue<string>();
		public static ConcurrentStack<g_Instance> milkFramesToSave = new ConcurrentStack<g_Instance>();
		public static Milk milkData;

		private static bool lastJSONUsed;

		private static readonly object lastJSONLock = new object();
		private static readonly object fileWritingLock = new object();

		public static readonly object logOutputWriteLock = new object();

		public static DateTime lastDataTime;
		static float minTillAutorestart = 3;

		static bool wasThrown;
		static int lastThrowPlayerId = -1;
		static bool inPostMatch = false;

		/// Joust stats
		static bool[] isJousting = new bool[2];
		static float[] joustMaxSpeed = new float[2];
		static float[] joustTubeExitSpeed = new float[2];
		static float joustStartTime = -1;

		public static int deltaTimeIndexStats;
		public static int deltaTimeIndexFull = 1;

		public static int StatsHz { get => statsDeltaTimes[deltaTimeIndexStats]; }
		public static List<int> statsDeltaTimes = new List<int> { 16, 100 };
		public static List<int> fullDeltaTimes = new List<int> { 16, 33, 1000 };

		/// <summary>
		/// The folder to save all the full data logs to
		/// </summary>
		public static string saveFolder;
		public static string fileName;
		public static bool useCompression;
		public static bool batchWrites;

		public static LiveWindow liveWindow;
		public static SettingsWindow settingsWindow;
		public static Speedometer speedometerWindow;
		public static AtlasLinks atlasLinksWindow;
		public static Playspace playspaceWindow;
		public static TTSSettingsWindow ttsWindow;
		public static NVHighlightsSettingsWindow nvhWindow;
		public static LoginWindow loginWindow;
		public static ClosingDialog closingWindow;

		private static float smoothDeltaTime = -1;

		public static string customId;

		public static bool hostingLiveReplay = false;

#if INCLUDE_FIRESTORE
		public static FirestoreDb db;
#endif

		public static string echoVRIP = "";
		public static int echoVRPort = 6721;
		public static bool overrideEchoVRPort;

		public static bool Personal {
			get {
				return currentAccessCodeUsername == "Personal";
			}
		}


		public static SpeechSynthesizer synth;


		static Thread statsThread;
		static Thread fullLogThread;
		static Thread autorestartThread;
		static Thread fetchThread;
		static Thread liveReplayThread;
		static Thread milkThread;

		public static App app;

		public static void Main(string[] args, App app)
		{
			Program.app = app;
			using (Mutex mutex = new Mutex(false, "Global\\ignitebot"))
			{

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
					return;  // wait for the dialog to quit the program
				}

				// allow multiple instances if the port is overriden
				if (!mutex.WaitOne(0, false) && !overrideEchoVRPort)
				{
					new MessageBox("Instance already running", "Error", Quit).Show();
					return;  // wait for the dialog to quit the program
				}

				// Reload old settings file
				if (Settings.Default.UpdateSettings)
				{
					Settings.Default.Upgrade();
					Settings.Default.UpdateSettings = false;
					Settings.Default.Save();
				}

				RegisterUriScheme("ignitebot", "IgniteBot Protocol");
				RegisterUriScheme("atlas", "ATLAS Protocol");   // TODO see how this would overwrite ATLAS URL opening

				// if logged in with discord
				if (!string.IsNullOrEmpty(Settings.Default.discordOAuthRefreshToken))
				{
					DiscordOAuth.OAuthLoginRefresh(Settings.Default.discordOAuthRefreshToken);
				}


				if (string.IsNullOrEmpty(Settings.Default.accessCode))
				{
					Settings.Default.accessCode = SecretKeys.Hash("personal");
				}
				else
				{
					// checkaccesscode checks and sets the access code as active
					switch (CheckAccessCode(Settings.Default.accessCode))
					{
						case AuthCode.network_error:
							// TODO show that there's a network error
							break;
						case AuthCode.denied:
							// TODO show that you were denied
							break;
					}
				}

				liveWindow = new LiveWindow();
				liveWindow.Closed += (sender, args) => liveWindow = null;
				liveWindow.Show();

				var argsList = new List<string>(args);

				// Check for command-line flags
				if (args.Contains("-slowmode"))
				{
					deltaTimeIndexStats = 1;
					Settings.Default.targetDeltaTimeIndexStats = deltaTimeIndexStats;
				}
				if (args.Contains("-autorestart"))
				{
					autoRestart = true;
					Settings.Default.autoRestart = true;
				}
				if (args.Contains("-showdatabaselog"))
				{
					showDatabaseLog = true;
					Settings.Default.showDatabaseLog = true;
				}


				// make an exception for vrml casters
				// Note that these usernames are not the access codes. Don't even try.
				if (currentAccessCodeUsername == "VRML_S2")
				{
					Settings.Default.whenToUploadLogs = 1;
				}

				if (currentAccessCodeUsername == "Personal")
				{
					if (Settings.Default.whenToUploadLogs == 1)
					{
						Settings.Default.whenToUploadLogs = 0;
					}
				}

				if (currentAccessCodeUsername == "ignitevr")
				{
					ENABLE_LOGGER = false;
				}

				Settings.Default.Save();

				ReadSettings();

				client.DefaultRequestHeaders.Add("x-api-key", SecretKeys.IgniteAPIKey);
				client.DefaultRequestHeaders.Add("version", AppVersion());
				client.DefaultRequestHeaders.Add("User-Agent", "IgniteBot/" + AppVersion());

				client.BaseAddress = new Uri(APIURL);

#if INCLUDE_FIRESTORE
				var builder = new FirestoreClientBuilder { JsonCredentials = SecretKeys.firebaseJSONCredentials };
				db = FirestoreDb.Create("ignitevr-echostats", builder.Build());
#endif

				if (isNVHighlightsEnabled)
				{
					SetupNVHighlights();
				}
				else
				{
					InitHighlightsSDK(true);
				}

				// Initialize a new instance of the SpeechSynthesizer.
				synth = new SpeechSynthesizer();

				// Configure the audio output.
				synth.SetOutputToDefaultAudioDevice();
				synth.SetRate(Settings.Default.TTSSpeed);


				UpdateEchoExeLocation();

				DiscordRichPresence.Start();


				statsThread = new Thread(StatsThread);
				statsThread.IsBackground = true;
				statsThread.Start();

				fullLogThread = new Thread(FullLogThread);
				fullLogThread.IsBackground = true;
				fullLogThread.Start();

				autorestartThread = new Thread(AutorestartThread);
				autorestartThread.IsBackground = true;
				autorestartThread.Start();

				fetchThread = new Thread(FetchThread);
				fetchThread.IsBackground = true;
				fetchThread.Start();

				liveReplayThread = new Thread(LiveReplayHostingThread);
				liveReplayThread.IsBackground = true;
				liveReplayThread.Start();

				milkThread = new Thread(MilkThread);
				milkThread.IsBackground = true;
				//milkThread.Start();

				Init();

				CloseNVHighlights();
			}
		}

		public static string AppVersion()
		{
			var version = Application.Current.GetType().Assembly.GetName().Version;
			return $"{version.Major}.{version.Minor}.{version.Build}";
		}

		public static void CloseNVHighlights(bool wasDisableNVHCall = false)
		{
			if (didHighlightsInit)
			{
				if (clearHighlightsOnExit && !wasDisableNVHCall)
				{
					ClearUnsavedNVHighlights(false);

				}
				Highlights.ReleaseHighlightsSDK();
			}
		}
		public static int InitHighlightsSDK(bool isCheck)
		{
			Highlights.HighlightScope[] RequiredScopes = new
				Highlights.HighlightScope[3] {
					Highlights.HighlightScope.Highlights,
					Highlights.HighlightScope.HighlightsRecordVideo,
					Highlights.HighlightScope.HighlightsRecordScreenshot
				};
			if (Highlights.CreateHighlightsSDK("EchoVR", RequiredScopes) != Highlights.ReturnCode.SUCCESS)
			{
				Console.WriteLine("Failed to initialize Highlights");
				didHighlightsInit = false;
				isNVHighlightsSupported = false;
				return -1;
			}
			else if (isCheck)
			{
				Highlights.ReleaseHighlightsSDK();
				isNVHighlightsSupported = true;
				return 1;
			}
			didHighlightsInit = true;
			return 1;
		}

		public static int SetupNVHighlights()
		{
			if (InitHighlightsSDK(false) < 0)
			{
				return -1;
			}
			Highlights.RequestPermissions(configStepCallback);
			// Configure Highlights
			Highlights.HighlightDefinition[] highlightDefinitions = new Highlights.HighlightDefinition[5];

			highlightDefinitions[0].Id = "SAVE";
			highlightDefinitions[0].HighlightTags = Highlights.HighlightType.Achievement;
			highlightDefinitions[0].Significance = Highlights.HighlightSignificance.Good;
			highlightDefinitions[0].UserDefaultInterest = true;
			highlightDefinitions[0].NameTranslationTable = new Highlights.TranslationEntry[] { new Highlights.TranslationEntry("en-US", "Save!"), };

			highlightDefinitions[1].Id = "SCORE";
			highlightDefinitions[1].HighlightTags = Highlights.HighlightType.Achievement;
			highlightDefinitions[1].Significance = Highlights.HighlightSignificance.Good;
			highlightDefinitions[1].UserDefaultInterest = true;
			highlightDefinitions[1].NameTranslationTable = new Highlights.TranslationEntry[] { new Highlights.TranslationEntry("en-US", "Goal!"), };

			highlightDefinitions[2].Id = "INTERCEPTION";
			highlightDefinitions[2].HighlightTags = Highlights.HighlightType.Achievement;
			highlightDefinitions[2].Significance = Highlights.HighlightSignificance.Good;
			highlightDefinitions[2].UserDefaultInterest = true;
			highlightDefinitions[2].NameTranslationTable = new Highlights.TranslationEntry[] { new Highlights.TranslationEntry("en-US", "Interception!"), };

			highlightDefinitions[3].Id = "STEAL_SAVE";
			highlightDefinitions[3].HighlightTags = Highlights.HighlightType.Achievement;
			highlightDefinitions[3].Significance = Highlights.HighlightSignificance.Good;
			highlightDefinitions[3].UserDefaultInterest = true;
			highlightDefinitions[3].NameTranslationTable = new Highlights.TranslationEntry[] { new Highlights.TranslationEntry("en-US", "Steal counts as Save!"), };

			highlightDefinitions[4].Id = "ASSIST";
			highlightDefinitions[4].HighlightTags = Highlights.HighlightType.Achievement;
			highlightDefinitions[4].Significance = Highlights.HighlightSignificance.Good;
			highlightDefinitions[4].UserDefaultInterest = true;
			highlightDefinitions[4].NameTranslationTable = new Highlights.TranslationEntry[] { new Highlights.TranslationEntry("en-US", "Scoring Assist!"), };

			Highlights.ConfigureHighlights(highlightDefinitions, "en-US", configStepCallback);
			// Open Groups
			Highlights.OpenGroupParams ogp1 = new Highlights.OpenGroupParams();
			ogp1.Id = "PERSONAL_HIGHLIGHT_GROUP";
			ogp1.GroupDescriptionTable = new Highlights.TranslationEntry[] { new Highlights.TranslationEntry("en-US", "Personal Highlight Group"), };
			Highlights.OpenGroup(ogp1, configStepCallback);

			Highlights.OpenGroupParams ogp2 = new Highlights.OpenGroupParams();
			ogp2.Id = "PERSONAL_TEAM_HIGHLIGHT_GROUP";
			ogp2.GroupDescriptionTable = new Highlights.TranslationEntry[] { new Highlights.TranslationEntry("en-US", "Personal Team Highlight Group"), };
			Highlights.OpenGroup(ogp2, configStepCallback);

			Highlights.OpenGroupParams ogp3 = new Highlights.OpenGroupParams();
			ogp3.Id = "OPPOSING_TEAM_HIGHLIGHT_GROUP";
			ogp3.GroupDescriptionTable = new Highlights.TranslationEntry[] { new Highlights.TranslationEntry("en-US", "Opposing Team Highlight Group"), };
			Highlights.OpenGroup(ogp3, configStepCallback);

			GetNVHighlightsCount();
			return 1;
		}

		public static bool DoNVClipsExist()
		{
			return nvHighlightClipCount > 0;
		}

		public static void ShowNVHighlights()
		{
			if (didHighlightsInit)
			{
				Highlights.GroupView[] gViews = new Highlights.GroupView[3];
				Highlights.GroupView gv1 = new Highlights.GroupView();
				gv1.GroupId = "PERSONAL_HIGHLIGHT_GROUP";
				gv1.SignificanceFilter = Highlights.HighlightSignificance.Good;
				gv1.TagFilter = Highlights.HighlightType.Achievement;
				gViews[0] = gv1;
				Highlights.GroupView gv2 = new Highlights.GroupView();
				gv2.GroupId = "PERSONAL_TEAM_HIGHLIGHT_GROUP";
				gv2.SignificanceFilter = Highlights.HighlightSignificance.Good;
				gv2.TagFilter = Highlights.HighlightType.Achievement;
				gViews[1] = gv2;
				Highlights.GroupView gv3 = new Highlights.GroupView();
				gv3.GroupId = "OPPOSING_TEAM_HIGHLIGHT_GROUP";
				gv3.SignificanceFilter = Highlights.HighlightSignificance.Good;
				gv3.TagFilter = Highlights.HighlightType.Achievement;
				gViews[2] = gv3;

				Highlights.OpenSummary(gViews, openSummaryCallback);
			}
		}

		public static void GetNVHighlightsCount()
		{
			if (didHighlightsInit)
			{
				nvHighlightClipCount = 0;
				Highlights.GroupView groupView = new Highlights.GroupView();
				groupView.GroupId = "PERSONAL_HIGHLIGHT_GROUP";
				Highlights.GetNumberOfHighlights(groupView, getNumOfHighlightsCallback);

				Highlights.GroupView groupView2 = new Highlights.GroupView();
				groupView2.GroupId = "PERSONAL_TEAM_HIGHLIGHT_GROUP";
				Highlights.GetNumberOfHighlights(groupView2, getNumOfHighlightsCallback);

				Highlights.GroupView groupView3 = new Highlights.GroupView();
				groupView3.GroupId = "OPPOSING_TEAM_HIGHLIGHT_GROUP";
				Highlights.GetNumberOfHighlights(groupView3, getNumOfHighlightsCallback);
			}
		}

		public static void NVSetVideoCallback(Highlights.ReturnCode ret, int id)
		{
			if (ret == Highlights.ReturnCode.SUCCESS)
			{
				nvHighlightClipCount++;
				Console.WriteLine("SetVideoCallback " + id + " returns success");
			}
			else
			{
				Console.WriteLine("SetVideoCallback " + id + " returns unsuccess");
			}
		}
		public static void NVCloseGroupCallback(Highlights.ReturnCode ret, int id)
		{
			if (ret == Highlights.ReturnCode.SUCCESS)
			{
				nvHighlightClipCount = 0;
				Console.WriteLine("CloseGroupCallback " + id + " returns success");
			}
			else
			{
				Console.WriteLine("CloseGroupCallback " + id + " returns unsuccess");
			}
		}
		public static void NVConfigCallback(Highlights.ReturnCode ret, int id)
		{
			if (ret == Highlights.ReturnCode.SUCCESS)
			{
				nvHighlightClipCount = 0;
				Console.WriteLine("ConfigStep " + id + " returns success");
			}
			else
			{
				Console.WriteLine("ConfigStep " + id + " returns unsuccess");
			}
		}
		public static void NVGetNumberOfHighlightsCallback(Highlights.ReturnCode ret, int number, int id)
		{
			if (ret == Highlights.ReturnCode.SUCCESS)
			{
				nvHighlightClipCount += number;
				Console.WriteLine("GetNumberOfHighlightsCallback " + id + " returns " + number);
			}
			else
			{
				Console.WriteLine("GetNumberOfHighlightsCallback " + id + " returns unsuccess");
			}
		}
		/// <summary>
		/// This is just a failsafe so that the program doesn't leave a dangling thread.
		/// </summary>
		async static Task KillAll(Thread statsThread, Thread fullLogThread, Thread autorestartThread, Thread fetchThread, Thread milkThread, HTTPServer httpServer = null)
		{
			if (httpServer != null)
				httpServer.Stop();
		}

		async static Task GentleClose()
		{
			running = false;
			while (statsThread.IsAlive || fullLogThread.IsAlive)
			{
				if (fullLogThread.IsAlive)
				{
					closingWindow.label.Content = "Compressing Replay File...";
				}
				else
				{
					closingWindow.label.Content = "Closing...";
				}
				await Task.Delay(10);
			}


			app.ExitApplication();
		}

		/// <summary>
		/// Thread that actually does the GET requests or reading from file. 
		/// Once a line has been used, this thread gets a new one.
		/// </summary>
		public static void FetchThread()
		{
			StreamReader fileReader = null;

			if (READ_FROM_FILE)
			{
				filesInFolder = Directory.GetFiles(readFromFolder, "*.zip").ToList();
				filesInFolder.Sort();
				fileReader = ExtractFile(fileReader, filesInFolder[readFromFolderIndex++]);
			}


			while (running && !fileFinishedReading)
			{
				while (paused && running)
				{
					Thread.Sleep(10);
				}

				if (READ_FROM_FILE)
				{
					if (fileReader != null)
					{
						string rawJSON = fileReader.ReadLine();
						if (rawJSON == null)
						{
							fileReader.Close();
							if (readFromFolderIndex >= filesInFolder.Count)
							{
								fileFinishedReading = true;
							}
							else
							{
								fileReader = ExtractFile(fileReader, filesInFolder[readFromFolderIndex++]);
							}
						}
						else
						{
							string[] splitJSON = rawJSON.Split('\t');
							string onlyJSON, onlyTime;
							if (splitJSON.Length > 1)
							{
								onlyJSON = splitJSON[1];
								onlyTime = splitJSON[0];
							}
							else
							{
								onlyJSON = splitJSON[0];
								string fileName = filesInFolder[readFromFolderIndex - 1].Split('\\').Last();
								onlyTime = fileName.Substring(4, fileName.Length - 8);
							}

							inGame = true;

							if (readIntoQueue)
							{
								lastJSONQueue.Enqueue(onlyJSON);
								lastDateTimeStringQueue.Enqueue(onlyTime);
							}
							else
							{
								lock (lastJSONLock)
								{
									lastDateTimeString = onlyTime;
									lastJSON = onlyJSON;
									lastJSONUsed = false;
								}
							}
						}
					}
					else
					{
						LogRow(LogType.Error, "File doesn't exist or something");
						return;
					}

					if (readIntoQueue)
					{
						if (lastJSONQueue.Count > 100000)
						{
							Console.WriteLine("Got 100k lines ahead");
							// sleep for 1 sec to let the other thread catch up
							Thread.Sleep(1000);
						}
					}
					else
					{
						// wait until we need to get another row
						while (!lastJSONUsed)
						{
							Thread.Sleep(1);
						}
					}
				}

				{
					WebResponse response;
					StreamReader sReader;


					// Do we get a response?
					try
					{
						// Create Session.
						WebRequest request = WebRequest.Create("http://" + echoVRIP + ":" + echoVRPort + "/session");
						response = request.GetResponse();
					}
					catch (Exception)
					{
						// Don't update so quick if we aren't in a match anyway
						Thread.Sleep(2000);

						// split file between matches
						if (Settings.Default.whenToSplitReplays < 3)
						{
							NewFilename();
						}
						LogRow(LogType.Info, "Not in Match");
						inGame = false;


						lock (lastJSONLock)
						{
							lastJSON = null;
						}

						continue;
					}

					lastDataTime = DateTime.Now;
					inGame = true;

					Stream dataStream = response.GetResponseStream();
					sReader = new StreamReader(dataStream);

					// Session Contents
					string rawJSON = sReader.ReadToEnd();

					// pls close (;-;)
					if (sReader != null)
						sReader.Close();
					if (response != null)
						response.Close();

					lock (lastJSONLock)
					{
						lastJSON = rawJSON;
						lastJSONUsed = false;
					}
				}
			}
		}

		/// <summary>
		/// Thread for logging only stats
		/// </summary>
		public static void StatsThread()
		{
			// TODO these times aren't used, but we could do a difference on before and after times to 
			// calculate an accurate deltaTime. Right now the execution time isn't taken into account.
			var time = DateTime.Now;
			var deltaTimeSpan = new TimeSpan(0, 0, 0, 0, statsDeltaTimes[deltaTimeIndexStats]);

			Thread.Sleep(10);

			// Session pull loop.
			while (running)
			{
				if (enableStatsLogging && inGame)
				{
					try
					{
						string json, recordedTime;
						if (READ_FROM_FILE && readIntoQueue)
						{
							if (!lastJSONQueue.TryDequeue(out json))
							{
								if (fileFinishedReading)
								{
									running = false;
								}
								Thread.Sleep(1);
								continue;
							}
							lastDateTimeStringQueue.TryDequeue(out recordedTime);
						}

						lock (lastJSONLock)
						{
							// no new frame to read
							if (lastJSON == null || lastJSONUsed)
							{
								continue;
							}

							lastJSONUsed = true;
							json = lastJSON;
							recordedTime = lastDateTimeString;
						}

						// make sure there is a valid echovr path saved
						if (Settings.Default.echoVRPath == "")
						{
							UpdateEchoExeLocation();
						}


						// Convert session contents into game_instance class.
						g_Instance game_Instance = JsonConvert.DeserializeObject<g_Instance>(json);

						// add the recorded time
						if (recordedTime != string.Empty)
						{
							if (!DateTime.TryParse(recordedTime, out DateTime dateTime))
							{
								DateTime.TryParseExact(recordedTime, "yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTime);
							}
							if (dateTime != null)
							{
								game_Instance.recorded_time = dateTime;
							}
						}

						// prepare the raw api conversion for use
						game_Instance.teams[0].color = TeamColor.blue;
						game_Instance.teams[1].color = TeamColor.orange;
						game_Instance.teams[2].color = TeamColor.spectator;

						if (game_Instance.teams[0].players == null) game_Instance.teams[0].players = new List<g_Player>();
						if (game_Instance.teams[1].players == null) game_Instance.teams[1].players = new List<g_Player>();
						if (game_Instance.teams[2].players == null) game_Instance.teams[2].players = new List<g_Player>();

						// for the very first frame, duplicate it to the "previous" frame
						if (lastFrame == null)
						{
							lastFrame = game_Instance;
							DiscordRichPresence.lastDiscordPresenceTime = DateTime.Now;
						}

						if (matchData == null)
						{
							matchData = new MatchData(game_Instance);
							UpdateStatsIngame(game_Instance);
						}

						milkFramesToSave.Clear();
						milkFramesToSave.Push(game_Instance);

						ProcessFrame(game_Instance);

						//if (DateTime.Now - DiscordRichPresence.lastDiscordPresenceTime > TimeSpan.FromSeconds(1))
						//{
						//	DiscordRichPresence.ProcessDiscordPresence(game_Instance);
						//}
					}
					catch (Exception ex)
					{
						LogRow(LogType.Error, "Big oopsie. Please catch inside. " + ex);
					}

					if (READ_FROM_FILE && !realtimeWhenReadingFile)
					{
						while (running)
						{
							if (!lastJSONUsed)
							{
								break;
							}
							//Thread.Sleep(1);
						}
					}

					Thread.Sleep(statsDeltaTimes[deltaTimeIndexStats]);
				}
				else
				{
					Thread.Sleep(1000);
				}
			}
		}

		public static void MilkThread()
		{
			Thread.Sleep(2000);
			int frameCount = 0;
			// Session pull loop.
			while (running)
			{
				if (milkFramesToSave.TryPop(out g_Instance frame))
				{
					if (milkData == null)
					{
						milkData = new Milk(frame);
					}
					else
					{
						milkData.AddFrame(frame);
					}
					frameCount++;
				}

				// only save every once in a while
				if (frameCount > 200)
				{
					frameCount = 0;
					string filePath = Path.Combine(saveFolder, fileName + ".milk");
					File.WriteAllBytes(filePath, milkData.GetBytes());
				}

				Thread.Sleep(fullDeltaTimes[deltaTimeIndexFull]);
			}
		}

		/// <summary>
		/// Thread for logging all JSON data
		/// </summary>
		public static void FullLogThread()
		{
			Thread.Sleep(2000);
			lastDataTime = DateTime.Now;

			NewFilename();

			// Session pull loop.
			while (running)
			{
				if (enableFullLogging && inGame)
				{
					try
					{
						string json;
						lock (lastJSONLock)
						{
							if (lastJSON == null) continue;

							lastJSONUsed = true;
							json = lastJSON;
						}

						// if this is not a lobby api frame
						if (json.Length > 800)
						{
							bool log = false;
							if (Settings.Default.onlyRecordPrivateMatches)
							{
								g_InstanceSimple obj = JsonConvert.DeserializeObject<g_InstanceSimple>(json);
								if (obj.private_match)
								{
									log = true;
								}
							}
							else
							{
								log = true;
							}

							if (log)
							{
								WriteToFile(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss.fff") + "\t" + json);
							}
						}
					}
					catch (Exception ex)
					{
						Console.WriteLine("Big oopsie. Please catch inside. " + ex);
					}

					Thread.Sleep(fullDeltaTimes[deltaTimeIndexFull]);
				}
				else
				{
					Thread.Sleep(100);
				}
			}

			// causes a final zip if that's needed
			NewFilename();
		}

		/// <summary>
		/// Thread to detect crashes and restart EchoVR
		/// </summary>
		public static void AutorestartThread()
		{
			lastDataTime = DateTime.Now;

			if (READ_FROM_FILE) autoRestart = false;

			// Session pull loop.
			while (running)
			{
				if (autoRestart)
				{

					// only start worrying once 15 seconds have passed
					if (DateTime.Compare(lastDataTime.AddMinutes(.25f), DateTime.Now) < 0)
					{
						LogRow(LogType.Info, "Time left until restart: " +
							(lastDataTime.AddMinutes(minTillAutorestart) - DateTime.Now).Minutes + " min " +
							(lastDataTime.AddMinutes(minTillAutorestart) - DateTime.Now).Seconds + " sec");

						// If `minTillAutorestart` minutes have passed, restart EchoVR
						if (DateTime.Compare(lastDataTime.AddMinutes(minTillAutorestart), DateTime.Now) < 0)
						{
							// Get process name
							Process[] process = GetEchoVRProcess();
							var echoPath = Settings.Default.echoVRPath;

							if (process.Length > 0)
							{
								var echo_ = process[0];
								// Get process path
								// close client
								echo_.Kill();
								// restart client
								Process.Start(echoPath, "-spectatorstream");
							}
							else if (echoPath != null && echoPath != "")
							{
								// restart client
								Process.Start(echoPath, "-spectatorstream");
							}
							else
							{
								LogRow(LogType.Error, "Couldn't restart EchoVR because it isn't running");
							}

							// reset timer
							lastDataTime = DateTime.Now;
						}
					}
				}

				Thread.Sleep(1000);
			}
		}

		public static void LiveReplayHostingThread()
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

				Thread.Sleep(1000);
			}
		}

		private static async Task DoLiveReplayUpload(StringContent content)
		{
			try
			{
				// client_name is just for visibility in the log
				var response = await client.PostAsync("live_replay/" + lastFrame.sessionid + "?caprate=1&default=true&client_name=" + lastFrame.client_name, content);

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
				var process = Process.GetProcessesByName("echovr");
				if (process.Length > 0)
				{
					// Get process path
					var newEchoPath = process[0].MainModule.FileName;
					if (newEchoPath != null && newEchoPath != "")
					{
						Settings.Default.echoVRPath = newEchoPath;
						Settings.Default.Save();
					}
				}
				return process;
			}
			catch (Exception)
			{
				LogRow(LogType.Error, "Error getting process");
				return null;
			}
		}

		public static void KillEchoVR()
		{
			var process = Process.GetProcessesByName("echovr");
			if (process != null && process.Length > 0)
			{
				try
				{
					process[0].Kill();
				}
				catch (Exception ex)
				{
					LogRow(LogType.Error, "Failed to kill process\n" + ex);
				}
			}
		}

		public static void StartEchoVR(string joinType = "choose")
		{
			Process.Start(new ProcessStartInfo
			{
				FileName = "ignitebot://" + joinType + "/" + lastFrame.sessionid,
				UseShellExecute = true
			});
		}

		public static void UpdateEchoExeLocation()
		{
			// skip if we already have a valid path
			if (File.Exists(Settings.Default.echoVRPath)) return;

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
					var paths = new List<string>();
					foreach (string subkey in oculusReg.GetSubKeyNames())
					{
						paths.Add((string)oculusReg.OpenSubKey(subkey).GetValue("OriginalPath"));
					}

					const string echoDir = "Software\\ready-at-dawn-echo-arena\\bin\\win7\\echovr.exe";
					foreach (var path in paths)
					{
						string file = Path.Combine(path, echoDir);
						if (File.Exists(file))
						{
							Settings.Default.echoVRPath = file;
							Settings.Default.Save();
							return;
						}
					}
				}
			}
			catch (Exception)
			{
				LogRow(LogType.Error, "Can't get EchoVR path from registry");
			}
		}

		private static StreamReader ExtractFile(StreamReader fileReader, string fileName)
		{
			string tempDir = Path.Combine(saveFolder, "temp_zip_read\\");

			if (Directory.Exists(tempDir))
			{
				while (running)
				{
					try
					{
						Directory.Delete(tempDir, true);
						break;
					}
					catch (IOException)
					{
						Thread.Sleep(10);
					}
				}
			}

			Directory.CreateDirectory(tempDir);

			using (ZipArchive archive = ZipFile.OpenRead(fileName))
			{
				foreach (ZipArchiveEntry entry in archive.Entries)
				{
					// Gets the full path to ensure that relative segments are removed.
					string destinationPath = Path.GetFullPath(Path.Combine(tempDir, entry.FullName));

					entry.ExtractToFile(destinationPath);

					fileReader = new StreamReader(destinationPath);
				}
			}

			return fileReader;
		}

		private static void ReadSettings()
		{
			showDatabaseLog = Settings.Default.showDatabaseLog;
			enableLoggingRemote = Settings.Default.logToServer;
			autoRestart = Settings.Default.autoRestart;
			deltaTimeIndexStats = Settings.Default.targetDeltaTimeIndexStats;
			useCompression = Settings.Default.useCompression;
			batchWrites = Settings.Default.batchWrites;
			saveFolder = Settings.Default.saveFolder;
			enableFullLogging = Settings.Default.enableFullLogging;
			enableStatsLogging = Settings.Default.enableStatsLogging;
			deltaTimeIndexFull = Settings.Default.targetDeltaTimeIndexFull;
			echoVRIP = Settings.Default.echoVRIP;
			clearHighlightsOnExit = Settings.Default.clearHighlightsOnExit;
			ClientHighlightScope = (HighlightLevel)Settings.Default.clientHighlightScope;
			isNVHighlightsEnabled = Settings.Default.isNVHighlightsEnabled;
			if (!overrideEchoVRPort) echoVRPort = Settings.Default.echoVRPort;

			if (saveFolder == "none" || !Directory.Exists(saveFolder))
			{
				saveFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "IgniteBot\\replays");
				Directory.CreateDirectory(saveFolder);
				Settings.Default.saveFolder = saveFolder;
				Settings.Default.Save();
			}
		}

		/// <summary>
		/// Writes the data to the file
		/// </summary>
		/// <param name="data">The data to write</param>
		static void WriteToFile(string data)
		{
			if (batchWrites)
			{
				dataCache.Add(data);

				// if the time elapsed since last write is less than cutoff
				if (dataCache.Count * fullDeltaTimes[deltaTimeIndexFull] < 5000)
				{
					return;
				}
			}

			// Fail if the folder doesn't even exist
			if (!Directory.Exists(saveFolder))
			{
				return;
			}

			string filePath, directoryPath;

			// could combine with some other data path, such as AppData
			directoryPath = saveFolder;

			filePath = Path.Combine(directoryPath, fileName + ".echoreplay");

			lock (fileWritingLock)
			{
				StreamWriter streamWriter = new StreamWriter(filePath, true);

				if (batchWrites)
				{
					foreach (var row in dataCache)
					{
						streamWriter.WriteLine(row);
					}
					dataCache.Clear();
				}
				else
				{
					streamWriter.WriteLine(data);
				}

				streamWriter.Close();
			}
		}

		/// <summary>
		/// Goes through a "frame" (single JSON object) and generates the relevant events
		/// </summary>
		static void ProcessFrame(g_Instance frame)
		{
			// 'mpl_lobby_b2' may change in the future
			if (frame == null || string.IsNullOrWhiteSpace(frame.game_status)) return;

			if (frame.map_name == "mpl_lobby_b2") return;

			// if we entered a different match
			if (frame.sessionid != lastFrame.sessionid || lastFrame == null)
			{
				// We just discard the old match and hope it was already submitted

				lastFrame = frame;  // don't detect stats changes across matches
									// TODO discard old players

				inPostMatch = false;
				matchData = new MatchData(frame);
				UpdateStatsIngame(frame);
			}

			/// <summary>
			/// The time between the current frame and last frame in seconds based on the game clock
			/// </summary>
			float deltaTime = lastFrame.game_clock - frame.game_clock;
			if (deltaTime != 0)
			{
				if (smoothDeltaTime == -1) smoothDeltaTime = deltaTime;
				float smoothingFactor = .99f;
				smoothDeltaTime = smoothDeltaTime * smoothingFactor + deltaTime * (1 - smoothingFactor);
			}


			// Did a player join or leave?

			// is a player from the current frame not in the last frame? (Player Join 🤝)
			// Loop through teams.
			foreach (var team in frame.teams)
			{
				// Loop through players on team.
				foreach (var player in team.players)
				{
					player.team = team;
					if (!lastFrame.GetAllPlayers(true).Any(p => p.userid == player.userid))
					{
						// TODO find why this is crashing
						try
						{
							matchData.Events.Add(new EventData(matchData, EventData.EventType.player_joined, frame.game_clock, team, player, null, player.head.Position, Vector3.Zero));
							LogRow(LogType.File, frame.sessionid, frame.game_clock_display + " - Player Joined: " + player.name);

							// cache this players stats so they aren't overridden if they join again
							var playerData = matchData.GetPlayerData(player);
							// if player was in this match before
							if (playerData != null)
							{
								playerData.CacheStats(player.stats);
							}

							UpdateStatsIngame(frame);

							if (Settings.Default.playerJoinTTS)
							{
								synth.SpeakAsync(player.name + " joined " + team.color);
							}
						}
						catch (Exception ex)
						{
							LogRow(LogType.Error, ex.ToString());
						}


					}
				}
			}

			// Is a player from the last frame not in the current frame? (Player Leave 🚪)
			// Loop through teams.
			foreach (var team in lastFrame.teams)
			{
				// Loop through players on team.
				foreach (var player in team.players)
				{
					if (!frame.GetAllPlayers(true).Any(p => p.userid == player.userid))
					{
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

						if (Settings.Default.playerLeaveTTS)
						{
							synth.SpeakAsync(player.name + " left " + team.color);
						}
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

			// while playing and frames aren't identical
			if (frame.game_status == "playing" && deltaTime != 0)
			{
				inPostMatch = false;


				matchData.currentDiskTrajectory.Add(frame.disc.position.ToVector3());

				if (frame.disc.velocity.ToVector3().Equals(Vector3.Zero))
				{
					wasThrown = false;
				}

				// Generate "playing" events
				foreach (g_Team team in frame.teams)
				{
					foreach (g_Player player in team.players)
					{
						var lastPlayer = lastFrame.GetPlayer(player.userid);
						if (lastPlayer == null) continue;

						MatchPlayer playerData = matchData.GetPlayerData(team, player);
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

								LogRow(LogType.File, frame.sessionid, frame.game_clock_display + " - " + player.name + " boosted to " + boostSpeed.ToString("N1") + " m/s");


								// TTS
								if (playerData.Name == frame.client_name)
								{
									if (Settings.Default.maxBoostSpeedTTS)
									{
										synth.SpeakAsync(boostSpeed.ToString("N0") + " meters per second");
									}
								}
							}

							// update hand velocities
							playerData.UpdateAverageSpeedLHand(((player.lhand.Position - lastPlayer.lhand.Position) - playerSpeed).Length() / deltaTime);
							playerData.UpdateAverageSpeedRHand(((player.rhand.Position - lastPlayer.rhand.Position) - playerSpeed).Length() / deltaTime);

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
							}
							// move the playspace towards the current player position
							playerData.playspaceLocation += (player.head.Position - playerData.playspaceLocation).Normalized() * .05f * deltaTime;

							if (team.team != "SPECTATORS" && Math.Abs(smoothDeltaTime) < .1f && Math.Abs(deltaTime) < .1f && Vector3.Distance(player.head.Position, playerData.playspaceLocation) > 1.7f)
							{
								// playspace abuse happened
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
								LogRow(LogType.File, frame.sessionid, frame.game_clock_display + " - " + player.name + " abused their playspace");
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
							matchData.Events.Add(new EventData(matchData, EventData.EventType.save, frame.game_clock, team, player, null, player.head.Position, Vector3.Zero));
							LogRow(LogType.File, frame.sessionid, frame.game_clock_display + " - " + player.name + " made a save");
							string highlightGroupName = IsPlayerHighlightEnabled(player, frame);
							if (highlightGroupName.Length > 0)
							{
								Highlights.VideoHighlightParams vhp = new Highlights.VideoHighlightParams();
								vhp.groupId = highlightGroupName;
								vhp.highlightId = "SAVE";
								vhp.startDelta = -3000;
								vhp.endDelta = 2000;
								Highlights.SetVideoHighlight(vhp, videoCallback);
							}
						}

						// check steals 🕵️‍
						if (lastPlayer.stats.steals != player.stats.steals)
						{
							matchData.Events.Add(new EventData(matchData, EventData.EventType.steal, frame.game_clock, team, player, null, player.head.Position, Vector3.Zero));
							string highlightGroupName = IsPlayerHighlightEnabled(player, frame);
							if (highlightGroupName.Length > 0 && WasStealNearGoal(frame.disc.position.ToVector3(), team.color, frame))
							{
								Highlights.VideoHighlightParams vhp = new Highlights.VideoHighlightParams();
								vhp.groupId = highlightGroupName;
								vhp.highlightId = "STEAL_SAVE";
								vhp.startDelta = -3000;
								vhp.endDelta = 2000;
								Highlights.SetVideoHighlight(vhp, videoCallback);
								LogRow(LogType.File, frame.sessionid, frame.game_clock_display + " - " + player.name + " stole the disk near goal!");
							}
							else
							{
								LogRow(LogType.File, frame.sessionid, frame.game_clock_display + " - " + player.name + " stole the disk");
							}

						}

						// check stuns
						if (lastPlayer.stats.stuns != player.stats.stuns)
						{
							// try to match it to an existing stunnee

							// clean up the stun match list
							stunningMatchedPairs.RemoveAll(uat =>
							{
								if (uat[0] != null && uat[0].gameClock - frame.game_clock > stunMatchingTimeout) return true;
								else if (uat[1] != null && uat[1].gameClock - frame.game_clock > stunMatchingTimeout) return true;
								else return false;
							});

							bool added = false;
							foreach (var stunEvent in stunningMatchedPairs)
							{
								if (stunEvent[0] == null)
								{
									// if (stunEvent[1].player position is close to the stunner)
									if (stunEvent[1].player.name != player.name)
									{
										stunningMatchedPairs.Remove(stunEvent);

										var stunner = player;
										var stunnee = stunEvent[1].player;

										matchData.Events.Add(new EventData(matchData, EventData.EventType.stun, frame.game_clock, team, stunner, stunnee, stunnee.head.Position, Vector3.Zero));
										LogRow(LogType.File, frame.sessionid, frame.game_clock_display + " - " + stunner.name + " just stunned " + stunnee.name);
										added = true;
										break;
									}
								}
							}
							if (!added)
							{
								stunningMatchedPairs.Add(new UserAtTime[] { new UserAtTime { gameClock = frame.game_clock, player = player }, null });
							}
						}

						// check getting stunned 
						if (!lastPlayer.stunned && player.stunned)
						{
							// try to match it to an existing stun

							// clean up the stun match list
							stunningMatchedPairs.RemoveAll(uat =>
							{
								if (uat[0] != null && uat[0].gameClock - frame.game_clock > stunMatchingTimeout) return true;
								else if (uat[1] != null && uat[1].gameClock - frame.game_clock > stunMatchingTimeout) return true;
								else return false;
							});
							bool added = false;
							foreach (var stunEvent in stunningMatchedPairs)
							{
								if (stunEvent[1] == null)
								{
									// if (stunEvent[0].player position is close to the stunee)
									if (stunEvent[0].player.name != player.name)
									{
										stunningMatchedPairs.Remove(stunEvent);

										var stunner = stunEvent[0].player;
										var stunnee = player;

										matchData.Events.Add(new EventData(matchData, EventData.EventType.stun, frame.game_clock, team, stunner, stunnee, stunnee.head.Position, Vector3.Zero));
										LogRow(LogType.File, frame.sessionid, frame.game_clock_display + " - " + stunner.name + " just stunned " + stunnee.name);
										added = true;
										break;
									}
								}
							}
							if (!added)
							{
								stunningMatchedPairs.Add(new UserAtTime[] { null, new UserAtTime { gameClock = frame.game_clock, player = player } });
							}
						}

						// check disk was caught 🥊
						if (!lastPlayer.possession && player.possession)
						{
							matchData.Events.Add(new EventData(matchData, EventData.EventType.@catch, frame.game_clock, team, player, null, player.head.Position, Vector3.Zero));
							bool caughtThrow = false;
							string throwPlayerName = "";
							bool wasInt = false;

							if (lastThrowPlayerId > 0)
							{

								g_Instance lframe = lastFrame;

								foreach (var lteam in lframe.teams)
								{
									foreach (var lplayer in lteam.players)
									{
										if (lplayer.playerid == lastThrowPlayerId && lplayer.possession == true)
										{
											caughtThrow = true;
											throwPlayerName = lplayer.name;
											if (lteam.color != team.color)
											{
												wasInt = true;
											}
										}
									}
								}

							}
							if (caughtThrow)
							{
								if (wasInt && lastPlayer.stats.saves == player.stats.saves)
								{
									_ = DelayedCatchEvent(player, throwPlayerName);
									//LogRow(LogType.File, frame.sessionid, frame.game_clock_display + " - " + player.name + " intercepted a throw from " + throwPlayerName);
								}
								else
								{
									LogRow(LogType.File, frame.sessionid, frame.game_clock_display + " - " + player.name + " made a catch from a pass from " + throwPlayerName);
								}
							}
							else
							{
								LogRow(LogType.File, frame.sessionid, frame.game_clock_display + " - " + player.name + " made a catch");
							}
						}

						// check if the disk was caught using stats 🥊
						if (lastPlayer.stats.catches != player.stats.catches)
						{
							LogRow(LogType.File, frame.sessionid, frame.game_clock_display + " - " + player.name + " made a catch (stat)");
						}

						// check blocks 🧱
						if (lastPlayer.stats.blocks != player.stats.blocks)
						{
							matchData.Events.Add(new EventData(matchData, EventData.EventType.block, frame.game_clock, team, player, null, player.head.Position, Vector3.Zero));
							LogRow(LogType.File, frame.sessionid, frame.game_clock_display + " - " + player.name + " just blocked");
						}

						// check shots taken 🧺
						if (lastPlayer.stats.shots_taken != player.stats.shots_taken)
						{
							matchData.Events.Add(new EventData(matchData, EventData.EventType.shot_taken, frame.game_clock, team, player, null, player.head.Position, Vector3.Zero));
							LogRow(LogType.File, frame.sessionid, frame.game_clock_display + " - " + player.name + " just took a shot");
							if (lastThrowPlayerId == player.playerid)
							{
								lastThrowPlayerId = -1;
							}
						}

						// check blocks 🧱
						if (lastPlayer.stats.passes != player.stats.passes)
						{
							//matchData.Events.Add(new EventData(matchData, EventData.EventType.block, frame.game_clock, team, player, null, player.head.Position, Vector3.Zero));
							LogRow(LogType.File, frame.sessionid, frame.game_clock_display + " - " + player.name + " made a pass");
						}

						// check disk was thrown ⚾
						if (!wasThrown && player.possession && !lastFrame.disc.velocity.ToVector3().Equals(Vector3.Zero) && !frame.disc.velocity.ToVector3().Equals(Vector3.Zero) &&
							(frame.disc.velocity.ToVector3() - player.velocity.ToVector3()).Length() > 3)
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
							float underhandedness = 0;
							if (Vector3.Distance(lastPlayer.lhand.Position, lastFrame.disc.position.ToVector3()) <
								Vector3.Distance(lastPlayer.rhand.Position, lastFrame.disc.position.ToVector3()))
							{
								underhandedness = Vector3.Dot(lastPlayer.head.up.ToVector3(), lastPlayer.lhand.Position - lastPlayer.head.Position);
							}
							else
							{
								underhandedness = Vector3.Dot(lastPlayer.head.up.ToVector3(), lastPlayer.rhand.Position - lastPlayer.head.Position);
							}

							// wait to actually log this throw to get more accurate velocity
							_ = DelayedThrowEvent(player, leftHanded, underhandedness, frame.disc.velocity.ToVector3().Length());
						}

						// TODO check if a pass was made
						if (false)
						{
							matchData.Events.Add(new EventData(matchData, EventData.EventType.pass, frame.game_clock, team, player, null, player.head.Position, Vector3.Zero));
							LogRow(LogType.File, frame.sessionid, frame.game_clock_display + " - " + player.name + " made a pass");
						}

						// calculate joust time to center
						int side = (int)team.color;
						if (team.color != TeamColor.spectator && isJousting[side])
						{
							if (player.velocity.ToVector3().Length() > joustMaxSpeed[side])
							{
								joustMaxSpeed[side] = player.velocity.ToVector3().Length();
							}

							// if the player exited the tube
							if (player.head.Position.Z * (side * 2 - 1) < 40)
							{
								if (!playerData.exitedTube)
								{
									// set the max tube speed
									if (player.velocity.ToVector3().Length() > joustTubeExitSpeed[side])
									{
										joustTubeExitSpeed[side] = player.velocity.ToVector3().Length();
									}
									playerData.exitedTube = true;

									if (Settings.Default.tubeExitSpeedTTS)
									{
										if (player.name == frame.client_name)
										{
											synth.SpeakAsync("tube " + player.velocity.ToVector3().Length().ToString("N0") + " meters per second");
										}
									}
								}
							}
							else
							{
								playerData.exitedTube = false;
							}
							if (player.head.Position.Z * (side * 2 - 1) < 0)
							{
								EventData joustEvent = new EventData(
										matchData,
										EventData.EventType.joust_speed,
										frame.game_clock,
										team,
										player,
										(long)((joustStartTime - frame.game_clock) * 1000),
										Vector3.Zero,
										new Vector3(
											joustMaxSpeed[side],
											joustTubeExitSpeed[side],
											joustStartTime - frame.game_clock)
										);

								isJousting[side] = false;

								// only joust time
								if (Settings.Default.joustTimeTTS && !Settings.Default.joustSpeedTTS)
								{
									synth.SpeakAsync(team.color.ToString() + " " + (joustStartTime - frame.game_clock).ToString("N1"));
								}
								// only joust speed
								else if (!Settings.Default.joustTimeTTS && Settings.Default.joustSpeedTTS)
								{
									synth.SpeakAsync(team.color.ToString() + " " + joustMaxSpeed[side].ToString("N0") + " meters per second");
								}
								// both
								else if (Settings.Default.joustTimeTTS && Settings.Default.joustSpeedTTS)
								{
									synth.SpeakAsync(team.color.ToString() + " " + (joustStartTime - frame.game_clock).ToString("N1") + " " + joustMaxSpeed[side].ToString("N0") + " meters per second");
								}

								matchData.Events.Add(joustEvent);
								LogRow(LogType.File, frame.sessionid, frame.game_clock_display + " - " + team.color.ToString() +
									" team joust time: " + (joustStartTime - frame.game_clock).ToString("N2") +
									" s, Max speed: " + joustMaxSpeed[side].ToString("N2") +
									" m/s, Tube Exit Speed: " + joustTubeExitSpeed[side].ToString("N2") + " m/s");

								// Upload to Firebase 🔥
								_ = DoUploadEventFirebase(matchData, joustEvent);

								lastJousts.Enqueue(joustEvent);
								if (lastJousts.Count > 8)
								{
									lastJousts.TryDequeue(out var joust);
								}
							}
						}
					}
				}


				// generate general playing events (not player-specific)

				try
				{
					// check blue restart request ↩
					if (!lastFrame.blue_team_restart_request && frame.blue_team_restart_request)
					{
						matchData.Events.Add(new EventData(matchData, EventData.EventType.restart_request, lastFrame.game_clock, frame.teams[(int)TeamColor.blue], null, null, Vector3.Zero, Vector3.Zero));
						LogRow(LogType.File, frame.sessionid, frame.game_clock_display + " - " + "blue team restart request");
					}
					// check orange restart request ↩
					if (!lastFrame.orange_team_restart_request && frame.orange_team_restart_request)
					{
						matchData.Events.Add(new EventData(matchData, EventData.EventType.restart_request, lastFrame.game_clock, frame.teams[(int)TeamColor.orange], null, null, Vector3.Zero, Vector3.Zero));
						LogRow(LogType.File, frame.sessionid, frame.game_clock_display + " - " + "orange team restart request");
					}
				}
				catch (Exception)
				{
					LogRow(LogType.Error, "Error with restart request parsing");
				}
			}

			lastLastLastFrame = lastLastFrame;
			lastLastFrame = lastFrame;
			lastFrame = frame;
		}

		private static async Task DelayedCatchEvent(g_Player originalPlayer, string throwPlayerName)
		{
			// TODO look through again
			// wait some time before re-checking the throw velocity
			await Task.Delay(2000);

			g_Instance frame = lastFrame;

			foreach (var team in frame.teams)
			{
				foreach (var player in team.players)
				{
					if (player.playerid == originalPlayer.playerid)
					{
						if (player.stats.saves == originalPlayer.stats.saves)
						{
							LogRow(LogType.File, frame.sessionid, frame.game_clock_display + " - " + player.name + " intercepted a throw from " + throwPlayerName);
							string highlightGroupName = IsPlayerHighlightEnabled(player, frame);
							if (highlightGroupName.Length > 0)
							{
								Highlights.VideoHighlightParams vhp = new Highlights.VideoHighlightParams();
								vhp.groupId = highlightGroupName;
								vhp.highlightId = "INTERCEPTION";
								vhp.startDelta = -5000;
								vhp.endDelta = 4000;
								Highlights.SetVideoHighlight(vhp, videoCallback);
							}
						}
					}

				}
			}

		}

		private static async Task DelayedThrowEvent(g_Player originalPlayer, bool leftHanded, float underhandedness, float origSpeed)
		{
			// wait some time before re-checking the throw velocity
			await Task.Delay(100);

			g_Instance frame = lastFrame;

			foreach (var team in frame.teams)
			{
				foreach (var player in team.players)
				{
					if (player.playerid == originalPlayer.playerid)
					{
						if (player.possession && !frame.disc.velocity.ToVector3().Equals(Vector3.Zero))
						{
							matchData.Events.Add(new EventData(matchData, EventData.EventType.@throw, frame.game_clock, team, player, null, player.head.Position, frame.disc.velocity.ToVector3()));
							LogRow(LogType.File, frame.sessionid, frame.game_clock_display + " - " + player.name + " threw the disk at " +
								frame.disc.velocity.ToVector3().Length().ToString("N2") + " m/s with their " + (leftHanded ? "left" : "right") + " hand");
							matchData.currentDiskTrajectory.Clear();

							// add throw data type
							matchData.Throws.Add(new ThrowData(matchData, frame.game_clock, player, frame.disc.position.ToVector3(), frame.disc.velocity.ToVector3(), leftHanded, underhandedness));

							//if (Settings.Default.throwSpeedTTS)
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
		/// Gets the server IP from the logs
		/// </summary>
		/// <returns></returns>
		private static string GetServerIP()
		{
			if (Settings.Default.echoVRPath != "")
			{
				try
				{
					string logPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Settings.Default.echoVRPath), "..", "..", "_local", "r14logs"));
					List<string> logs = Directory.GetFiles(logPath).Where(f => !f.Contains("_json") && f.Contains(".log")).ToList();
					logs.Sort();
					string file = logs.Last();
					Stream stream = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
					StreamReader streamReader = new StreamReader(stream);
					string logData = streamReader.ReadToEnd();
					string searchStr = "[NSLOBBY] connected to host peer [";
					int loc = logData.LastIndexOf(searchStr);
					int endLoc = logData.IndexOf(":", loc);
					return logData.Substring(loc + searchStr.Length, endLoc - (loc + searchStr.Length));
				}
				catch (Exception e)
				{
					LogRow(LogType.Error, "Failed to read log file for server ip.\n" + e);
					return "";
				}
			}

			return "";
		}

		/// <summary>
		/// Function used to excute certain behavior based on frame given and previous frame(s).
		/// </summary>
		/// <param name="frame"></param>
		/// <param name="deltaTime"></param>
		private static void ProcessGameStateChange(g_Instance frame, float deltaTime)
		{
			LogRow(LogType.File, frame.sessionid, frame.game_clock_display + " - Entered state: " + frame.game_status);

			switch (frame.game_status)
			{
				case "pre_match":
					// if we just came from a playing state, this was a reset - requires a high enough polling rate
					if (lastFrame.game_status == "playing" || lastFrame.game_status == "round_start")
					{
						g_Instance frameToUse = lastLastFrame;
						if (lastValidSumOfStatsAge < 30)
						{
							frameToUse = lastValidStatsFrame;
						}
						EventMatchFinished(frameToUse, MatchData.FinishReason.reset, lastFrame.game_clock);
					}
					break;

				// round began
				case "round_start":
					inPostMatch = false;

					// if we just started a new 'round' (so stats haven't been reset)
					if (lastFrame.game_status == "round_over")
					{
						if (!READ_FROM_FILE)
						{
							UpdateStatsIngame(frame, false, false);
						}

						// This could cause a problem if someone is a spectator (their stats don't get reset, but they're not in the game) during the round transition
						foreach (MatchPlayer player in matchData.AllPlayers)
						{
							g_Player p = new g_Player
							{
								userid = player.Id
							};

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
					}

					if (!READ_FROM_FILE)
					{
						UpdateStatsIngame(frame);
					}

					break;

				// round really began
				case "playing":
					#region Started Playing
					if (!READ_FROM_FILE)
					{
						UpdateStatsIngame(frame);
					}

					// Loop through teams.
					foreach (var team in frame.teams)
					{
						// Loop through players on team.
						foreach (var player in team.players)
						{

							// reset playspace
							var playerData = matchData.GetPlayerData(team, player);
							if (playerData != null)
							{
								playerData.playspaceLocation = player.head.Position;
							}

						}
					}

					// start a joust if the disc is in the center
					if (lastFrame.game_status == "round_start")
					{
						// if the disc is in the center of the arena
						if (Math.Abs(frame.disc.position.ToVector3().Z) < .1f)
						{
							for (int i = 0; i < 2; i++)
							{
								isJousting[i] = true;
								joustTubeExitSpeed[i] = 0;
								joustMaxSpeed[i] = 0;
							}
							joustStartTime = frame.game_clock;
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
					else if (lastFrame.game_clock < deltaTime * 10 || lastFrame.game_status == "post_sudden_death" || deltaTime < 0)
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
					if (frame.private_match && Settings.Default.whenToSplitReplays == 1)
					{
						NewFilename();
					}
					//EventMatchFinished(frame, MatchData.FinishReason.not_finished);
					break;

				case "pre_sudden_death":
					LogRow(LogType.Error, "pre_sudden_death");
					break;
				case "sudden_death":
					// this happens right as the match finishes in a tie
					matchData.overtimeCount++;
					break;
				case "post_sudden_death":
					LogRow(LogType.Error, "post_sudden_death");
					break;


			}
		}

		private static string IsPlayerHighlightEnabled(g_Player player, g_Instance frame)
		{
			if (didHighlightsInit && isNVHighlightsEnabled)
			{
				TeamColor clientTeam = frame.teams.FirstOrDefault(t => t.players.Exists(p => p.name == frame.client_name)).color;
				if (player.name == frame.client_name)
				{
					return "PERSONAL_HIGHLIGHT_GROUP";
				}
				else if (ClientHighlightScope != HighlightLevel.CLIENT_ONLY && player.team.color == clientTeam)
				{
					return "PERSONAL_TEAM_HIGHLIGHT_GROUP";
				}
				else if (ClientHighlightScope == HighlightLevel.ALL || clientTeam == TeamColor.spectator)
				{
					return "OPPOSING_TEAM_HIGHLIGHT_GROUP";
				}
				else
				{
					return "";
				}
			}
			return "";
		}

		private static string IsPlayerHighlightEnabled(string playerName, g_Instance frame)
		{
			if (playerName == "[INVALID]") return "";
			g_Player highlightPlayer = frame.teams.Find(t => t.players.Exists(p => p.name == playerName)).players.Find(p => p.name == playerName);
			return IsPlayerHighlightEnabled(highlightPlayer, frame);
		}

		private static bool WasStealNearGoal(Vector3 disckPos, TeamColor playerTeamColor, g_Instance frame)
		{
			float stealSaveRadius = 2.2f;

			float goalXPos = 0f;
			float goalYPos = 0f;
			float goalZPos = 36f;
			if (playerTeamColor == TeamColor.blue)
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
			// wait some time before re-checking the throw velocity
			await Task.Delay(100);

			var frame = lastFrame;

			// Calculate the exact position within the goal that the disc was shot
			Vector3 discPos = frame.disc.position.ToVector3();
			Vector3 discVel = lastFrame.disc.velocity.ToVector3();
			Vector2 goalPos;
			bool backboard = false;
			float angleIntoGoal = 0;
			if (discVel != Vector3.Zero)
			{
				Vector3 actualGoalPos = discPos.Z < 0 ? new Vector3(0, 0, -36) : new Vector3(0, 0, 36);
				float angleIntoGoalRad = (float)(Math.Acos(Vector3.Dot(discVel, new Vector3(0, 0, 1) * (discPos.Z < 0 ? -1 : 1)) / discVel.Length()));
				angleIntoGoal = (float)(angleIntoGoalRad * (180 / Math.PI));
				float distToGoal = (float)((actualGoalPos.Z - discPos.Z) / Math.Cos(angleIntoGoalRad));
				Vector3 discDirection = discVel / discVel.Length();
				Vector3 goalPos3D = discPos + distToGoal * discDirection;
				goalPos = new Vector2(goalPos3D.X * (goalPos3D.Z < 0 ? -1 : 1), goalPos3D.Y);

				// make the angle negative if backboard
				if (angleIntoGoal > 90)
				{
					angleIntoGoal = 180 - angleIntoGoal;
					backboard = true;
				}
			}
			else
			{
				goalPos = new Vector2(frame.disc.position.ToVector3().X, frame.disc.position.ToVector3().Y);
			}
			string highlightGroupName = IsPlayerHighlightEnabled(frame.last_score.person_scored, frame);
			if (highlightGroupName.Length > 0)
			{
				Highlights.VideoHighlightParams vhp = new Highlights.VideoHighlightParams();
				vhp.groupId = highlightGroupName;
				vhp.highlightId = "SCORE";
				vhp.startDelta = -3000;
				vhp.endDelta = 2000;
				Highlights.SetVideoHighlight(vhp, videoCallback);
			}
			else
			{
				highlightGroupName = IsPlayerHighlightEnabled(frame.last_score.assist_scored, frame);
				if (highlightGroupName.Length > 0)
				{
					Highlights.VideoHighlightParams vhp = new Highlights.VideoHighlightParams();
					vhp.groupId = highlightGroupName;
					vhp.highlightId = "ASSIST";
					vhp.startDelta = -3000;
					vhp.endDelta = 2000;
					Highlights.SetVideoHighlight(vhp, videoCallback);
				}
			}

			// Call the Score event
			LogRow(LogType.File, frame.sessionid,
				frame.game_clock_display + " - " + frame.last_score.person_scored + " scored at " +
				frame.last_score.disc_speed.ToString("N2") + " m/s from " + frame.last_score.distance_thrown.ToString("N2") + " m away" +
				(frame.last_score.assist_scored == "[INVALID]" ? "!" : (", assisted by " + frame.last_score.assist_scored + "!")));

			// show the scores in the log
			LogRow(LogType.File, frame.sessionid, frame.game_clock_display + " - ORANGE: " + frame.orange_points + "  BLUE: " + frame.blue_points);

			if (Settings.Default.goalDistanceTTS)
			{
				synth.SpeakAsync(frame.last_score.distance_thrown.ToString("N1") + " meters");
			}
			if (Settings.Default.goalSpeedTTS)
			{
				synth.SpeakAsync(frame.last_score.disc_speed.ToString("N1") + " meters per second");
			}

			g_Player scorer = frame.GetPlayer(frame.last_score.person_scored);
			var scorerPlayerData = matchData.GetPlayerData(scorer);
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
					discPos.Z < 0 ? TeamColor.blue : TeamColor.orange,
					leftHanded,
					underhandedness,
					matchData.currentDiskTrajectory
				);
			matchData.Goals.Add(goalEvent);
			lastGoals.Enqueue(goalEvent);
			if (lastGoals.Count > 5)
			{
				lastGoals.TryDequeue(out var goal);
			}

			// Upload to Firebase 🔥
			_ = DoUploadEventFirebase(matchData, goalEvent);

			UpdateStatsIngame(frame);
		}

		/// <summary>
		/// Can be called often to update the ingame player stats
		/// </summary>
		/// <param name="frame">The current frame</param>
		public static void UpdateStatsIngame(g_Instance frame, bool endOfMatch = false, bool allowUpload = true, bool manual = false)
		{
			if (inPostMatch)
			{
				return;
			}

			// team names may have changed
			matchData.teams[TeamColor.blue].teamName = frame.teams[0].team;
			matchData.teams[TeamColor.orange].teamName = frame.teams[1].team;

			if (frame.teams[0].stats != null)
			{
				matchData.teams[TeamColor.blue].points = frame.blue_points;
			}
			if (frame.teams[1].stats != null)
			{
				matchData.teams[TeamColor.orange].points = frame.orange_points;
			}

			UpdateAllPlayers(frame);

			// don't update right at the end of the match anyway
			if (Settings.Default.whenToUploadLogs != 2)
			{
				// if end of match upload
				if (endOfMatch)
				{
					UploadMatchBatch(true);
				}
				// if during-match upload
				else if (manual || (Settings.Default.whenToUploadLogs == 1 && frame.game_status != "pre_match"))
				{
					UploadMatchBatch(false);
				}
				else
				{
					Console.WriteLine("Won't upload right now.");
				}
			}
		}
		/// <summary>
		/// Function to wrap up the match once we've entered post_match, restarted, or left spectate unexpectedly (crash)
		/// </summary>
		/// <param name="frame"></param>
		/// <param name="reason"></param>
		/// <param name="endTime"></param>
		private static void EventMatchFinished(g_Instance frame, MatchData.FinishReason reason, float endTime = 0)
		{
			matchData.endTime = endTime;
			matchData.finishReason = reason;

			LogRow(LogType.File, frame.sessionid, "Match Finished: " + reason);
			UpdateStatsIngame(frame, true);

			// if we here reset for public matches as well, then there would be super small files at the end of matches
			if (matchData.firstFrame.private_match && Settings.Default.whenToSplitReplays < 1)
			{
				// wait a little bit to actually split, so that the end of the match isn't cut off
				_ = DelayedNewFilename();
			}

			lastMatches.Enqueue(matchData);
			if (lastMatches.Count > 5)
			{
				lastMatches.TryDequeue(out var match);
			}
			lastMatchData = matchData;
			matchData = null;

			inPostMatch = true;

			// show the scores in the log
			LogRow(LogType.File, frame.sessionid, frame.game_clock_display + " - ORANGE: " + frame.orange_points + "  BLUE: " + frame.blue_points);

		}

		public static void UploadMatchBatch(bool final = false)
		{
			BatchOutputFormat data = new BatchOutputFormat();
			data.final = final;
			data.match_data = matchData.ToDict();
			matchData.AllPlayers.ForEach(e => data.match_players.Add(e.ToDict()));
			matchData.Events.ForEach(e => { if (!e.inDB) data.events.Add(e.ToDict()); e.inDB = true; });
			matchData.Goals.ForEach(e => { if (!e.inDB) data.goals.Add(e.ToDict()); e.inDB = true; });
			matchData.Throws.ForEach(e => { if (!e.inDB) data.throws.Add(e.ToDict()); e.inDB = true; });

			string dataString = JsonConvert.SerializeObject(data);
			string hash;
			using (SHA256 sha = SHA256.Create())
			{
				var rawHash = sha.ComputeHash(Encoding.ASCII.GetBytes(dataString + matchData.firstFrame.client_name));

				// Convert the byte array to hexadecimal string
				StringBuilder sb = new StringBuilder();
				for (int i = 0; i < rawHash.Length; i++)
				{
					sb.Append(rawHash[i].ToString("X2"));
				}
				hash = sb.ToString().ToLower();
			}

			_ = DoUploadMatchBatch(dataString, hash, matchData.firstFrame.client_name);
			_ = DoUploadMatchBatchFirebase(data);
		}

		static async Task DoUploadMatchBatch(string data, string hash, string client_name)
		{
			var content = new StringContent(data, Encoding.UTF8, "application/json");

			try
			{
				var response = await client.PostAsync("add_data?hashkey=" + hash + "&client_name=" + client_name, content);
				LogRow(LogType.Info, "[DB][Response] " + response.Content.ReadAsStringAsync().Result);
			}
			catch
			{
				LogRow(LogType.Error, "Can't connect to the DB server");
			}
		}

		static async Task DoUploadEventFirebase(MatchData matchData, GoalData goalData)
		{
#if INCLUDE_FIRESTORE
			if (Settings.Default.uploadToFirestore)
			{
				if (!Personal)
				{
					string season = currentSeasonName;

					var match_data = matchData.ToDict();

					// update the match stats
					CollectionReference matchesRef = db.Collection("series/" + season + "/match_stats");
					DocumentReference matchDataRef = matchesRef.Document(match_data["match_time"] + "_" + match_data["session_id"]);
					CollectionReference eventsRef = matchDataRef.Collection("events");

					try
					{
						Dictionary<string, object> data = goalData.ToDict();
						// add the event type, since this isn't a normal type of event
						data["event_type"] = "goal";
						DocumentReference eventRef = await eventsRef.AddAsync(data);
						LogRow(LogType.File, lastFrame.sessionid, "-- Uploading Event Data --");
					}
					catch (Exception e)
					{
						LogRow(LogType.Error, "Error uploading to firebase.\n" + e.Message + "\n" + e.StackTrace);
						throw;
					}
				}
			}
#endif
		}

		static async Task DoUploadEventFirebase(MatchData matchData, EventData eventData)
		{
#if INCLUDE_FIRESTORE
			if (Settings.Default.uploadToFirestore)
			{
				if (!Personal)
				{
					string season = currentSeasonName;

					var match_data = matchData.ToDict();

					// update the match stats
					CollectionReference matchesRef = db.Collection("series/" + season + "/match_stats");
					DocumentReference matchDataRef = matchesRef.Document(match_data["match_time"] + "_" + match_data["session_id"]);
					CollectionReference eventsRef = matchDataRef.Collection("events");

					try
					{
						DocumentReference eventRef = await eventsRef.AddAsync(eventData.ToDict());
						LogRow(LogType.File, lastFrame.sessionid, "-- Uploading Event Data --");
					}
					catch (Exception e)
					{
						LogRow(LogType.Error, "Error uploading to firebase.\n" + e.Message + "\n" + e.StackTrace);
						throw;
					}
				}
			}
#endif
		}


		static async Task DoUploadMatchBatchFirebase(BatchOutputFormat data)
		{
#if INCLUDE_FIRESTORE
			if (Settings.Default.uploadToFirestore)
			{
				if (!Personal)
				{
					WriteBatch batch = db.StartBatch();

					string season = currentSeasonName;

					// update the cumulative player stats
					CollectionReference playersRef = db.Collection("series/" + season + "/player_stats");
					foreach (Dictionary<string, object> p in data.match_players)
					{
						DocumentReference playerRef = playersRef.Document(p["player_name"].ToString());

						batch.Set(playerRef, p, SetOptions.MergeAll);
					}

					// update the match stats
					CollectionReference matchesRef = db.Collection("series/" + season + "/match_stats");
					DocumentReference matchDataRef = matchesRef.Document(data.match_data["match_time"] + "_" + data.match_data["session_id"]);
					batch.Set(matchDataRef, data.match_data, SetOptions.MergeAll);

					// update the match players
					foreach (var p in data.match_players)
					{
						DocumentReference matchPlayerRef = matchDataRef.Collection("players").Document(p["player_name"].ToString());
						batch.Set(matchPlayerRef, p, SetOptions.MergeAll);
					}

					try
					{
						await batch.CommitAsync();
						LogRow(LogType.File, lastFrame.sessionid, "-- Uploading Data --");
					}
					catch (Exception e)
					{
						LogRow(LogType.Error, "Error uploading to firebase.\n" + e.Message + "\n" + e.StackTrace);
						throw;
					}
				}
			}
#endif
		}

		// Update existing player with stats from game.
		static void UpdateSinglePlayer(g_Instance frame, g_Team team, g_Player player, int won)
		{
			if (!matchData.teams.ContainsKey(team.color))
			{
				LogRow(LogType.Error, "No team. Wat."); return;
			}

			TeamData teamData = matchData.teams[team.color];

			// add a new match player if they didn't exist before
			if (!teamData.players.ContainsKey(player.userid))
			{
				teamData.players.Add(player.userid, new MatchPlayer(matchData, teamData, player));
			}

			MatchPlayer playerData = teamData.players[player.userid];

			playerData.Level = player.level;
			playerData.Number = player.number;
			playerData.PossessionTime = player.stats.possession_time;
			playerData.Points = player.stats.points;
			playerData.ShotsTaken = player.stats.shots_taken;
			playerData.Saves = player.stats.saves;
			//playerData.GoalsNum = player.stats.goals;	// disabled in favor of manual increment because the api is broken here
			playerData.Passes = player.stats.passes;
			playerData.Catches = player.stats.catches;
			playerData.Steals = player.stats.steals;
			playerData.Stuns = player.stats.stuns;
			playerData.Blocks = player.stats.blocks;
			playerData.Interceptions = player.stats.interceptions;
			playerData.Assists = player.stats.assists;
			playerData.Won = won;
		}

		static void UpdateAllPlayers(g_Instance frame)
		{
			// Loop through teams.
			foreach (var team in frame.teams)
			{
				// Loop through players on team.
				foreach (var player in team.players)
				{
					TeamColor winningTeam = frame.blue_points > frame.orange_points ? TeamColor.blue : TeamColor.orange;
					int won = team.color == winningTeam ? 1 : 0;

					UpdateSinglePlayer(frame, team, player, won);
				}
			}
		}
		/// <summary>
		/// Clears ALL NVidia Highlight clips that were not saved by the user via the NVidia UI.
		/// </summary>
		public static void ClearUnsavedNVHighlights(bool reopenGroup = false)
		{
			if (didHighlightsInit)
			{
				Highlights.CloseGroupParams cgp = new Highlights.CloseGroupParams { id = "PERSONAL_HIGHLIGHT_GROUP", destroyHighlights = true };
				Highlights.CloseGroup(cgp, closeGroupCallback);
				cgp = new Highlights.CloseGroupParams { id = "PERSONAL_TEAM_HIGHLIGHT_GROUP", destroyHighlights = true };
				Highlights.CloseGroup(cgp, closeGroupCallback);
				cgp = new Highlights.CloseGroupParams { id = "OPPOSING_TEAM_HIGHLIGHT_GROUP", destroyHighlights = true };
				Highlights.CloseGroup(cgp, closeGroupCallback);
				if (reopenGroup)
				{
					Highlights.OpenGroupParams ogp1 = new Highlights.OpenGroupParams();
					ogp1.Id = "PERSONAL_HIGHLIGHT_GROUP";
					ogp1.GroupDescriptionTable = new Highlights.TranslationEntry[] { new Highlights.TranslationEntry("en-US", "Personal Highlight Group"), };
					Highlights.OpenGroup(ogp1, configStepCallback);
					ogp1.Id = "PERSONAL_TEAM_HIGHLIGHT_GROUP";
					ogp1.GroupDescriptionTable = new Highlights.TranslationEntry[] { new Highlights.TranslationEntry("en-US", "Personal Team Highlight Group"), };
					Highlights.OpenGroup(ogp1, configStepCallback);
					ogp1.Id = "OPPOSING_TEAM_HIGHLIGHT_GROUP";
					ogp1.GroupDescriptionTable = new Highlights.TranslationEntry[] { new Highlights.TranslationEntry("en-US", "Opposing Team Highlight Group"), };
					Highlights.OpenGroup(ogp1, configStepCallback);
				}
			}
		}


		/// <summary>
		/// Generates a new filename from the current time.
		/// </summary>
		public static void NewFilename()
		{
			lock (fileWritingLock)
			{
				string lastFilename = fileName;
				fileName = DateTime.Now.ToString("rec_yyyy-MM-dd_HH-mm-ss");

				// compress the file
				if (useCompression)
				{
					if (File.Exists(Path.Combine(saveFolder, lastFilename + ".echoreplay")))
					{
						string tempDir = Path.Combine(saveFolder, "temp_zip");
						Directory.CreateDirectory(tempDir);
						File.Move(Path.Combine(saveFolder, lastFilename + ".echoreplay"), Path.Combine(saveFolder, "temp_zip", lastFilename + ".echoreplay"));      // TODO can fail because in use
						ZipFile.CreateFromDirectory(tempDir, Path.Combine(saveFolder, lastFilename + ".echoreplay"));
						Directory.Delete(tempDir, true);
					}
				}
			}
		}

		private static async Task DelayedNewFilename()
		{
			// wait some time before calling
			await Task.Delay(5000);

			NewFilename();
		}

		public enum AuthCode
		{
			network_error,
			denied,
			approved
		}

		public static AuthCode CheckAccessCode(string accessCode)
		{
			if (accessCode == "") return AuthCode.denied;

			try
			{
				WebRequest request = WebRequest.Create(APIURL + "ignitebot_auth?key=" + accessCode);

				// Get the response.
				WebResponse response = request.GetResponse();

				// Display the status.
				Console.WriteLine(((HttpWebResponse)response).StatusDescription);

				string responseFromServer;

				// Get the stream containing content returned by the server.
				// The using block ensures the stream is automatically closed.
				using (Stream dataStream = response.GetResponseStream())
				{
					// Open the stream using a StreamReader for easy access.
					StreamReader reader = new StreamReader(dataStream);
					// Read the content.
					responseFromServer = reader.ReadToEnd();

				}

				// Close the response.
				response.Close();

				// Display the content.
				Dictionary<string, string> respObj = JsonConvert.DeserializeObject<Dictionary<string, string>>(responseFromServer);
				if (respObj["auth"] == "true")
				{
					currentAccessCodeUsername = respObj["username"];
					currentSeasonName = respObj["season_name"];

					client.DefaultRequestHeaders.Remove("access-code");
					client.DefaultRequestHeaders.Add("access-code", currentSeasonName);

					return AuthCode.approved;
				}

				return AuthCode.denied;
			}
			catch
			{
				LogRow(LogType.Error, "Can't connect to the DB server");
				return AuthCode.network_error;
			}
		}



		public static JToken ReadEchoVRSettings()
		{
			string file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "rad", "loneecho", "settings_mp_v2.json");
			if (!File.Exists(file))
			{
				LogRow(LogType.Error, "Can't find the EchoVR settings file");
				return null;
			}

			return JsonConvert.DeserializeObject<JToken>(File.ReadAllText(file));
		}

		public static void WriteEchoVRSettings(JToken settings)
		{
			string file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "rad", "loneecho", "settings_mp_v2.json");
			if (!File.Exists(file))
			{
				throw new NullReferenceException("Can't find the EchoVR settings file");
			}

			var settingsString = JsonConvert.SerializeObject(settings, Formatting.Indented);
			File.WriteAllText(file, settingsString);
		}


		public static void RegisterUriScheme(string UriScheme, string FriendlyName)
		{
			using (var key = Registry.CurrentUser.CreateSubKey("SOFTWARE\\Classes\\" + UriScheme))
			{
				string applicationLocation = typeof(Program).Assembly.Location;

				key.SetValue("", "URL:" + FriendlyName);
				key.SetValue("URL Protocol", "");

				using (var defaultIcon = key.CreateSubKey("DefaultIcon"))
				{
					defaultIcon.SetValue("", applicationLocation + ",1");
				}

				using (var commandKey = key.CreateSubKey(@"shell\open\command"))
				{
					commandKey.SetValue("", "\"" + applicationLocation + "\" \"%1\"");
				}
			}
		}

		public static bool CheckIfLaunchedWithCustomURLHandlerParam(string[] args)
		{
			// join a match directly
			if (args.Length > 0 && (args[0].Contains("ignitebot://") || args[0].Contains("atlas://")))
			{
				string[] parts = args[0].Split('/');
				if (parts.Length != 4)
				{
					LogRow(LogType.Error, "ERROR 3452. Incorrectly formatted IgniteBot or Atlas link");
					new MessageBox($"Incorrectly formatted IgniteBot or Atlas link: wrong number of '/' characters for link:\n{args[0]}\n{parts.Length}", "Error", Quit).Show();
				}

				bool spectating = false;
				switch (parts[2])
				{
					case "spectate":
					case "s":
						spectating = true;
						break;
					case "join":
					case "j":
						spectating = false;
						break;
					case "choose":
					case "c":
						// hand the whole thing off to the popup window
						new ChooseJoinTypeDialog(parts[3]).Show();
						return true;
					default:
						LogRow(LogType.Error, "ERROR 8675. Incorrectly formatted IgniteBot or Atlas link");
						new MessageBox("Incorrectly formatted IgniteBot or Atlas link: Incorrect join type.", "Error", Quit).Show();
						return true;
				}


				// start client
				var echoPath = Settings.Default.echoVRPath;
				if (!string.IsNullOrEmpty(echoPath))
				{
					Process.Start(echoPath, (spectating ? "-spectatorstream " : " ") + "-lobbyid " + parts[3]);
				}
				else
				{
					new MessageBox("EchoVR exe path not set. Run the IgniteBot while in a lobby or game with the API enabled at least once first.", "Error", Quit).Show();
				}
				return true;
			}

			return false;
		}

		internal static void Quit()
		{
			if (closingWindow != null)
			{
				// already trying to close
				return;
			}
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

		public static float DistanceTo(this Vector3 v1, Vector3 v2)
		{
			return (float)Math.Sqrt(Math.Pow(v1.X - v2.X, 2) + Math.Pow(v1.Y - v2.Y, 2) + Math.Pow(v1.Z - v2.Z, 2));
		}

		public static Vector3 Normalized(this Vector3 v1)
		{
			return v1 / v1.Length();
		}
	}


}
