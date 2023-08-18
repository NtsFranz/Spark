//using ButterReplays;

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ButterReplays;

namespace Spark
{
	class SparkSettings
	{
		#region Settings

		public bool startOnBoot { get; set; } = false;
		public bool startMinimized { get; set; } = false;
		public bool autoRestart { get; set; } = false;
		public bool capturevp2 { get; set; } = false;
		public bool capturevp2VR { get; set; } = false;
		public bool showDatabaseLog { get; set; } = false;
		public bool discordRichPresence { get; set; } = true;
		public bool discordRichPresenceServerLocation { get; set; } = false;
		public bool discordRichPresenceSpectator { get; set; } = false;
		public bool logToServer { get; set; } = false;
		public string echoVRPath { get; set; } = "";
		public string echoVRIP { get; set; } = "127.0.0.1";
		public int echoVRPort { get; set; } = 6721;
		public bool enableStatsLogging { get; set; } = false;
		public bool lowFrequencyMode { get; set; } = false;
		public bool uploadToIgniteDB { get; set; } = false;
		public bool uploadToFirestore { get; set; } = true;
		public bool saveEventsToCSV { get; set; } = false;
		public bool fetchBones { get; set; } = false;

		/// <summary>
		/// Enable replay files
		/// </summary>
		public bool enableFullLogging { get; set; } = false;

		public bool onlyRecordPrivateMatches { get; set; } = false;
		public bool batchWrites { get; set; } = true;
		public bool useCompression { get; set; } = true;
		public int targetDeltaTimeIndexFull { get; set; } = 1;
		public string saveFolder { get; set; } = "none";
		public int whenToSplitReplays { get; set; } = 0;
		public ButterFile.CompressionFormat butterCompressionFormat { get; set; } = ButterFile.CompressionFormat.gzip;
		public bool saveButterFiles { get; set; } = false;
		public bool saveEchoreplayFiles { get; set; } = true;
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
		public string discordOAuthRefreshToken { get; set; } = "";
		public string accessMode { get; set; } = "";
		public string alternateEchoVRIP { get; set; } = "127.0.0.1";
		public bool nvHighlightsSpectatorRecord { get; set; } = false;
		public int atlasLinkStyle { get; set; } = 0;
		public bool atlasLinkUseAngleBrackets { get; set; } = true;
		public bool firstTimeSetupShown { get; set; } = false;
		public bool firstTimeOBSv28 { get; set; } = true;
		public bool isAutofocusEnabled { get; set; } = false;
		public bool loneEchoSubtitlesStreamerMode { get; set; } = false;
		public bool loneEchoSpeedometerStreamerMode { get; set; } = false;
		public int loneEchoVersion { get; set; } = 0;
		public int speedometerGameVersion { get; set; } = 1;
		public string loneEchoPath { get; set; } = "";
		public string loneEcho2Path { get; set; } = "";
		public float liveWindowTop { get; set; } = 10;
		public float liveWindowLeft { get; set; } = 10;
		public float settingsWindowTop { get; set; } = 20;
		public float settingsWindowLeft { get; set; } = 20;
		public string client_name { get; set; } = "";
		public bool atlasLinkAppendTeamNames { get; set; } = false;
		public int atlasHostingVisibility { get; set; } = 0;
		public int languageIndex { get; set; } = 0;
		public int theme { get; set; } = 0;
		public bool betaUpdates { get; set; } = false;
		public int dashboardItem1 { get; set; } = 0;
		public int dashboardJoustTimeOrder { get; set; } = 0;
		public int spectatorCamera { get; set; } = 0;
		public bool hideEchoVRUI { get; set; } = false;
		public int followPlayerCameraMode { get; set; } = 0;
		public string followPlayerName { get; set; } = "";
		public bool discHolderFollowRestrictTeam { get; set; } = false;
		public int discHolderFollowCamMode { get; set; } = 0;
		public bool toggleMinimapAfterGoals { get; set; } = false;
		public bool alwaysHideMinimap { get; set; } = false;
		public bool mutePlayerComms { get; set; } = false;
		public bool muteEnemyTeam { get; set; } = false;
		public bool hideNameplates { get; set; } = false;
		public int chooseRegionIndex { get; set; } = 0;
		public int chooseMapIndex { get; set; } = 0;
		public int chooseGameTypeIndex { get; set; } = 0;
		public bool chooseRegionSpectator { get; set; } = false;
		public bool chooseRegionNoOVR { get; set; } = false;
		public bool sparkLinkNoOVR { get; set; } = false;
		public bool sparkLinkForceLaunchNewInstance { get; set; } = false;
		public bool spectatorStreamCombat { get; set; } = false;
		public bool spectatorStreamNoOVR { get; set; } = false;
		public string sparkExeLocation { get; set; } = "";
		public bool allowSpectateMeOnLocalPC { get; set; } = false;
		public bool useAnonymousSpectateMe { get; set; } = true;
		public bool spectateMeOnByDefault { get; set; } = false;

		public Dictionary<string, bool> autoUploadProfiles { get; } = new Dictionary<string, bool>();

		#region TTS

		public bool throwSpeedTTS { get; set; } = false;
		public bool goalSpeedTTS { get; set; } = false;
		public bool goalDistanceTTS { get; set; } = false;
		public bool joustTimeTTS { get; set; } = false;
		public bool joustSpeedTTS { get; set; } = false;
		public bool serverLocationTTS { get; set; } = false;
		public bool maxBoostSpeedTTS { get; set; } = false;
		public int TTSSpeed { get; set; } = 1;
		public bool playerJoinTTS { get; set; } = false;
		public bool playerLeaveTTS { get; set; } = false;
		public bool playerSwitchTeamTTS { get; set; } = false;
		public bool tubeExitSpeedTTS { get; set; } = false;
		public bool pausedTTS { get; set; } = false;
		public bool useWavenetVoices { get; set; } = false;
		public bool playspaceTTS { get; set; } = false;
		public bool rulesChangedTTS { get; set; } = false;
		public int ttsVoice { get; set; } = 0;
		public int ttsCacheSizeBytes { get; set; } = 100000000;

		#endregion

		#region Clips

		// .echoreplay
		public bool enableReplayBuffer { get; set; } = false;
		public float replayBufferLength { get; set; } = 15;

		public float replayClipSecondsBefore { get; set; } = 7;
		public float replayClipSecondsAfter { get; set; } = 3;
		public int replayClipPlayerScope { get; set; } = 0;
		public bool replayClipSpectatorRecord { get; set; } = false;

		public bool replayClipEmote { get; set; } = false;
		public bool replayClipPlayspace { get; set; } = false;
		public bool replayClipGoal { get; set; } = false;
		public bool replayClipAssist { get; set; } = false;
		public bool replayClipSave { get; set; } = false;
		public bool replayClipInterception { get; set; } = false;
		public bool replayClipNeutralJoust { get; set; } = false;
		public bool replayClipDefensiveJoust { get; set; } = false;


		// nv highlights
		public int clientHighlightScope { get; set; } = 0;
		public bool clearHighlightsOnExit { get; set; } = false;
		public bool isNVHighlightsEnabled { get; set; } = false;
		public float nvHighlightsSecondsBefore { get; set; } = 7;
		public float nvHighlightsSecondsAfter { get; set; } = 3;
		public int nvHighlightsPlayerScope { get; set; } = 0;
		public bool onlyActivateHighlightsWhenGameIsOpen { get; set; } = false;

		// obs
		public string obsIP { get; set; } = "ws://127.0.0.1:4455";
		public string obsPassword { get; set; } = "";
		public bool obsAutoconnect { get; set; } = false;
		public bool obsPauseRecordingWithGameClock { get; set; }
		
		public bool obsClipEmote { get; set; } = false;
		public bool obsClipPlayspace { get; set; } = false;
		public bool obsClipGoal { get; set; } = false;
		public bool obsClipAssist { get; set; } = false;
		public bool obsClipSave { get; set; } = false;
		public float obsClipSecondsAfter { get; set; } = 3;
		public float obsGoalSecondsAfter { get; set; } = 3;
		public float obsSaveSecondsAfter { get; set; } = 3;
		public float obsGoalReplayLength { get; set; } = 5;
		public float obsSaveReplayLength { get; set; } = 5;
		public float obsClipSecondsBefore { get; set; } = 7;
		public bool obsAutostartReplayBuffer { get; set; } = false;
		public bool obsClipInterception { get; set; } = false;
		public bool obsClipNeutralJoust { get; set; } = false;
		public bool obsClipDefensiveJoust { get; set; } = false;
		public int obsPlayerScope { get; set; } = 0;
		public bool obsSpectatorRecord { get; set; } = false;
		public string obsInGameScene { get; set; } = "";
		public string obsBetweenGameScene { get; set; } = "";
		public string obsGoalReplayScene { get; set; } = "";
		public string obsSaveReplayScene { get; set; } = "";

		// medal

		public float medalClipSecondsBefore { get; set; } = 7;
		public float medalClipSecondsAfter { get; set; } = 3;
		public int medalClipPlayerScope { get; set; } = 0;
		public bool medalClipSpectatorRecord { get; set; } = false;

		public bool medalClipEmote { get; set; } = false;
		public bool medalClipPlayspace { get; set; } = false;
		public bool medalClipGoal { get; set; } = false;
		public bool medalClipAssist { get; set; } = false;
		public bool medalClipSave { get; set; } = false;
		public bool medalClipInterception { get; set; } = false;
		public bool medalClipNeutralJoust { get; set; } = false;
		public bool medalClipDefensiveJoust { get; set; } = false;

		public int medalClipKey { get; set; } = 0x42;

		// voice
		public bool enableVoiceRecognition { get; set; } = false;
		public bool enableVoiceRecognitionMic { get; set; } = true;
		public bool enableVoiceRecognitionSpeaker { get; set; } = true;
		public bool clipThatDetectionNVHighlights { get; set; } = true;
		public bool clipThatDetectionMedal { get; set; } = true;
		public bool badWordDetectionNVHighlights { get; set; } = false;
		public bool badWordDetectionMedal { get; set; } = false;
		public string microphone { get; set; } = "";
		public string speaker { get; set; } = "";

		#endregion

		public LoggingSettings eventLog { get; set; } = new LoggingSettings();

		[Serializable]
		public class LoggingSettings
		{
			public bool goals { get; set; } = true;
			public bool stuns { get; set; } = true;
			public bool steals { get; set; } = true;
			public bool saves { get; set; } = true;
			public bool turnovers { get; set; } = true;
			public bool restartRequests { get; set; } = true;
			public bool pauseRequests { get; set; } = true;
			public bool pauseEvents { get; set; } = true;
			public bool unPauseRequests { get; set; } = true;
			public bool playspaceAbuses { get; set; } = false;
			public bool localThrows { get; set; } = true;
			public bool throws { get; set; } = true;
			public bool neutralJousts { get; set; } = true;
			public bool defensiveJousts { get; set; } = true;
			public bool shotAttempts { get; set; } = true;
			public bool passes { get; set; } = true;
			public bool bigBoosts { get; set; } = true;
			public bool playerJoins { get; set; } = true;
			public bool playerLeaves { get; set; } = true;
			public bool playerSwitchedTeams { get; set; } = true;
			public bool largePings { get; set; } = true;
			public bool interceptions { get; set; } = true;
			public bool catches { get; set; } = true;
		}

		#region Overlays

		/// <summary>
		/// 0 for manual, 1 for vrml api
		/// </summary>
		public int overlaysTeamSource { get; set; } = 1;

		public string overlaysManualTeamNameOrange { get; set; } = "";
		public string overlaysManualTeamNameBlue { get; set; } = "";
		public string overlaysManualTeamLogoOrange { get; set; } = "";
		public string overlaysManualTeamLogoBlue { get; set; } = "";

		/// <summary>
		/// Can be used to store generic data without schema changes to Spark.
		/// Used for caster names/urls...
		/// </summary>
		public Dictionary<string, object> casterPrefs { get; set; } = new Dictionary<string, object>();
		
		
		/// <summary>
		/// Can be used to store generic data without schema changes to Spark.
		/// </summary>
		public Dictionary<string, object> data { get; set; } = new Dictionary<string, object>();

		/// <summary>
		/// 0: automatic, 1: manual
		/// </summary>
		public bool overlaysRoundScoresManual { get; set; } = false;

		public int overlaysManualRoundCount { get; set; } = 3;
		public int[] overlaysManualRoundScoresOrange { get; set; } = null;
		public int[] overlaysManualRoundScoresBlue { get; set; } = null;
		public string gameOverlayUrl { get; set; } = "http://localhost:6724/configurable_overlay";

		[Serializable]
		public class ConfigurableOverlaySettings
		{
			public bool minimap { get; set; } = true;
			public bool compact_minimap { get; set; } = false;
			public bool player_rosters { get; set; } = true;
			public bool main_banner { get; set; } = true;
			public bool neutral_jousts { get; set; } = true;
			public bool defensive_jousts { get; set; } = true;
			public bool event_log { get; set; } = true;
			public bool playspace { get; set; } = true;
			public bool player_speed { get; set; } = true;
			public bool disc_speed { get; set; } = true;
			public bool show_team_logos { get; set; } = true;
			public bool show_team_names { get; set; } = true;
		}

		public ConfigurableOverlaySettings configurableOverlaySettings { get; set; } = new ConfigurableOverlaySettings();

		#endregion

		#endregion


		public static SparkSettings instance;


		public void Save()
		{
			try
			{
				string filename = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IgniteVR", "Spark", "settings.json");

				Task.Run(() =>
				{
					try
					{
						if (!File.Exists(Path.GetDirectoryName(filename)))
						{
							Directory.CreateDirectory(Path.GetDirectoryName(filename));
						}

						string json = JsonConvert.SerializeObject(this, Formatting.Indented);
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
				Console.WriteLine("Reading settings file.");
				string filename = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IgniteVR", "Spark", "settings.json");
				if (File.Exists(filename))
				{
					string json = File.ReadAllText(filename);
					instance = JsonConvert.DeserializeObject<SparkSettings>(json);
					// instance = JsonSerializer.Deserialize<SparkSettings>(json);
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