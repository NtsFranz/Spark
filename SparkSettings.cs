using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Spark
{
	class SparkSettings
	{
		#region Settings
		public bool startOnBoot { get; set; } = false;
		public bool startMinimized { get; set; } = false;
		public bool autoRestart { get; set; } = false;
		public bool capturevp2 { get; set; } = false;
		public bool showDatabaseLog { get; set; } = false;
		public bool discordRichPresence { get; set; } = true;
		public bool logToServer { get; set; } = false;
		public string echoVRPath { get; set; } = "";
		public string echoVRIP { get; set; } = "127.0.0.1";
		public int echoVRPort { get; set; } = 6721;
		public bool enableStatsLogging { get; set; } = false;
		public int targetDeltaTimeIndexStats { get; set; } = 0;
		public bool uploadToIgniteDB { get; set; } = false;
		public bool uploadToFirestore { get; set; } = true;
		public bool enableFullLogging { get; set; } = false;
		public bool onlyRecordPrivateMatches { get; set; } = false;
		public bool batchWrites { get; set; } = true;
		public bool useCompression { get; set; } = true;
		public int targetDeltaTimeIndexFull { get; set; } = 1;
		public string saveFolder { get; set; } = "none";
		public int whenToSplitReplays { get; set; } = 0;
		public bool showConsoleOnStart { get; set; } = false;
		public bool outputGameStateEvents { get; set; } = true;
		public bool outputScoreEvents { get; set; } = true;
		public bool outputStunEvents { get; set; } = true;
		public bool outputDiscThrownEvents { get; set; } = true;
		public bool outputDiscCaughtEvents { get; set; } = true;
		public bool outputDiscStolenEvents { get; set; } = true;
		public bool outputSaveEvents { get; set; } = true;
		public string accessCode { get; set; } = "";
		public bool outputOther { get; set; } = true;
		public bool atlasShowing { get; set; } = false;
		public bool speedometerStreamerMode { get; set; } = false;
		public bool playspaceStreamerMode { get; set; } = false;
		public bool joustTimeTTS { get; set; } = false;
		public bool joustSpeedTTS { get; set; } = false;
		public bool serverLocationTTS { get; set; } = false;
		public bool maxBoostSpeedTTS { get; set; } = false;
		public int TTSSpeed { get; set; } = 1;
		public bool playerJoinTTS { get; set; } = false;
		public bool playerLeaveTTS { get; set; } = false;
		public bool playerSwitchTeamTTS { get; set; } = false;
		public bool tubeExitSpeedTTS { get; set; } = false;
		public string discordOAuthRefreshToken { get; set; } = "";
		public bool throwSpeedTTS { get; set; } = false;
		public bool goalSpeedTTS { get; set; } = false;
		public bool goalDistanceTTS { get; set; } = false;
		public string accessMode { get; set; } = "";
		public int clientHighlightScope { get; set; } = 0;
		public bool clearHighlightsOnExit { get; set; } = false;
		public bool isNVHighlightsEnabled { get; set; } = false;
		public float nvHighlightsSecondsBefore { get; set; } = 7;
		public float nvHighlightsSecondsAfter { get; set; } = 3;
		public bool pausedTTS { get; set; } = false;
		public string alternateEchoVRIP { get; set; } = "127.0.0.1";
		public bool nvHighlightsSpectatorRecord { get; set; } = false;
		public int atlasLinkStyle { get; set; } = 0;
		public bool atlasLinkUseAngleBrackets { get; set; } = true;
		public bool firstTimeSetupShown { get; set; } = false;
		public bool isAutofocusEnabled { get; set; } = false;
		public bool loneEchoSubtitlesStreamerMode { get; set; } = false;
		public string loneEchoPath { get; set; } = "";
		public int liveWindowTop { get; set; } = 10;
		public int liveWindowLeft { get; set; } = 10;
		public int settingsWindowTop { get; set; } = 20;
		public int settingsWindowLeft { get; set; } = 20;
		public string client_name { get; set; } = "";
		public bool atlasLinkAppendTeamNames { get; set; } = false;
		public int atlasHostingVisibility { get; set; } = 0;
		public bool playspaceTTS { get; set; } = false;
		public int ttsVoice { get; set; } = 0;
		public int languageIndex { get; set; } = 0;
		public int theme { get; set; } = 0;
		public float replayBufferLength { get; set; } = 15;
		public bool enableReplayBuffer { get; set; } = false;
		public bool replayClipPlayspace { get; set; } = false;
		public bool replayClipGoal { get; set; } = false;
		public bool replayClipSave { get; set; } = false;
		public bool betaUpdates { get; set; } = false;
		public string obsIP { get; set; } = "ws://127.0.0.1:4444";
		public string obsPassword { get; set; } = "";
		public bool obsAutoconnect { get; set; } = false;
		public bool obsClipPlayspace { get; set; } = false;
		public bool obsClipGoal { get; set; } = false;
		public bool obsClipAssist { get; set; } = false;
		public bool obsClipSave { get; set; } = false;
		public bool replayClipAssist { get; set; } = false;
		public float replayClipSecondsAfter { get; set; } = 3;
		public float obsClipSecondsAfter { get; set; } = 3;
		public float replayClipSecondsBefore { get; set; } = 7;
		public float obsClipSecondsBefore { get; set; } = 7;
		public bool obsAutostartReplayBuffer { get; set; } = false;
		public bool obsClipInterception { get; set; } = false;
		public bool replayClipInterception { get; set; } = false;
		public int nvHighlightsPlayerScope { get; set; } = 0;
		public int replayClipPlayerScope { get; set; } = 0;
		public int obsPlayerScope { get; set; } = 0;
		public bool replayClipSpectatorRecord { get; set; } = false;
		public bool obsSpectatorRecord { get; set; } = false;
		public int dashboardItem1 { get; set; } = 0;
		public bool useWavenetVoices { get; set; } = false;
		public int spectatorCamera { get; set; } = 0;
		public bool hideEchoVRUI { get; set; } = false;
		public int followClientSpectatorCameraMode { get; set; } = 0;
		public bool toggleMinimapAfterGoals { get; set; } = false;
		public int chooseRegionIndex { get; set; } = 0;
		public bool chooseRegionSpectator { get; set; } = false;
		public string obsInGameScene { get; set; } = "";
		public string obsBetweenGameScene { get; set; } = "";
		#endregion




		public static SparkSettings instance;


		public void Save()
		{
			try
			{
				string json = JsonConvert.SerializeObject(this);
				string filename = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "settings.json");

				Task.Run(() =>
				{
					try
					{
						string json = JsonConvert.SerializeObject(this);
						File.WriteAllText(filename, json);
					}
					catch (Exception e)
					{
						Console.WriteLine($"Error writing to settings file\n{e}");
					}
				});
			}
			catch (Exception e)
			{
				Console.WriteLine($"Error writing to settings file (outside)\n{e}");
			}
		}

		public static void Load()
		{
			try
			{
				string filename = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "settings.json");
				if (File.Exists(filename))
				{
					string json = File.ReadAllText(filename);
					instance = JsonConvert.DeserializeObject<SparkSettings>(json);
				}
				else
				{
					Console.WriteLine($"Settings file doesn't exist, creating.");
					instance = new SparkSettings();
				}
			}
			catch (Exception e)
			{
				Console.WriteLine($"Error reading settings file\n{e}");
				instance = new SparkSettings();
			}
		}
	}
}