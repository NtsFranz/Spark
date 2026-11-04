//using ButterReplays;

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ButterReplays;
using System.Windows;

namespace Spark
{
	class SparkSettings
	{
		[JsonIgnore]
		public Visibility EchoVRPathNotSetVisibility
		{
			get
			{
				bool echoVrInstalled = false;
				if (!string.IsNullOrEmpty(echoVRPath))
				{
					try
					{
						string exeDir = Path.GetDirectoryName(echoVRPath);
						if (exeDir != null)
						{
							string arenaPath = Path.GetFullPath(Path.Combine(exeDir, "..", ".."));
							if (Directory.Exists(arenaPath) && Path.GetFileName(arenaPath).Equals("ready-at-dawn-echo-arena", StringComparison.OrdinalIgnoreCase))
							{
								echoVrInstalled = true;
							}
						}
					}
					catch (Exception)
					{
						// path is likely invalid, so we'll consider it not installed.
					}
				}
				return echoVrInstalled ? Visibility.Collapsed : Visibility.Visible;
			}
		}
		
		#region Settings

		public bool startOnBoot { get; set; } = false;
		public bool startMinimized { get; set; } = false;
		public bool closeButtonExitsApp { get; set; } = false;
		public bool autoRestart { get; set; } = false;
		public bool capturevp2 { get; set; } = false;
		public bool capturevp2VR { get; set; } = false;
		public bool showDatabaseLog { get; set; } = false;
		public bool discordRichPresence { get; set; } = true;
		public bool discordRichPresenceServerLocation { get; set; } = false;
		public bool discordRichPresenceSpectator { get; set; } = false;
		public bool logToServer { get; set; } = false;
		private string _echoVRPath = "";
		public string echoVRPath
		{
			get => _echoVRPath;
			set => _echoVRPath = value.Trim().Trim('"');
		}
		public string echoVRIP { get; set; } = "127.0.0.1";
		public int echoVRPort { get; set; } = 6721;
		public bool enableStatsLogging { get; set; } = false;
		public bool lowFrequencyMode { get; set; } = false;
		public bool uploadToIgniteDB { get; set; } = false;
		public bool uploadToFirestore { get; set; } = true;
		public bool saveEventsToCSV { get; set; } = false;
		public bool fetchBones { get; set; } = false;
		// Simple Spectate Mode — called Quest Spectator when these were added. The keys keep the old
		// name because they're persisted in settings.json; renaming them would silently reset
		// everyone's saved choices.
		public bool questSpectatorAutoJoin { get; set; } = true;
		public bool questSpectatorAnonymous { get; set; } = true;
		public bool questSpectatorFollowSelf { get; set; } = false;

		/// <summary>Cumulative wall-clock seconds Spark has been running across every past launch, not counting the current one — see LiveWindow.UpdateSessionCard.</summary>
		public double totalPlaytimeSeconds { get; set; } = 0;

		/// <summary>
		/// Contribute <see cref="totalPlaytimeSeconds"/> to the community-wide playtime total shown
		/// on the Session card, and to the public leaderboard. Sends the Discord name (falling back
		/// to <see cref="client_name"/> when not logged in), an anonymous per-install hash and a
		/// seconds count — see Program.LeaderboardDisplayName and Program.ReportPlaytime. Turning
		/// this off stops reporting but still shows the global figure.
		/// </summary>
		public bool shareGlobalPlaytime { get; set; } = true;

		/// <summary>
		/// Enable replay files
		/// </summary>
		public bool enableFullLogging { get; set; } = false;

        /// <summary>Legacy four-step speech speed, 1-based. Only read to migrate to <see cref="ttsSpeedMultiplier"/>.</summary>
        public int ttsSpeedIndex { get; set; } = 1;
		/// <summary>
		/// Speech speed as a multiple of normal: one of TTSController.SpeedOptions. Replaces
		/// <see cref="ttsSpeedIndex"/> and <see cref="TTSSpeed"/>, which only knew four speeds.
		/// </summary>
		public double ttsSpeedMultiplier { get; set; } = 1.0;
		public bool onlyRecordPrivateMatches { get; set; } = false;
		public bool batchWrites { get; set; } = true;
		public bool useCompression { get; set; } = true;
		public int targetDeltaTimeIndexFull { get; set; } = 1;
		public string saveFolder { get; set; } = "none";
		public int whenToSplitReplays { get; set; } = 0;
		public ButterFile.CompressionFormat butterCompressionFormat { get; set; } = ButterFile.CompressionFormat.gzip;
		public bool saveButterFiles { get; set; } = false;
		public bool saveTapeFiles { get; set; } = false;
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
		public bool mapTestingInviteShown { get; set; } = false;
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

		// Custom theme colours (hex strings, e.g. "#151515")
		public string customThemeDark  { get; set; } = "#151515";
		public string customThemeMid   { get; set; } = "#363636";
		public string customThemeLight { get; set; } = "#3E3E3E";
		public bool betaUpdates { get; set; } = false;
		public int dashboardItem1 { get; set; } = 0;
		public int dashboardJoustTimeOrder { get; set; } = 0;
		public int spectatorCamera { get; set; } = 0;
		public bool hideEchoVRUI { get; set; } = false;
		public int followPlayerCameraMode { get; set; } = 0;
		public string followPlayerName { get; set; } = "";
		public int goProTargetHand { get; set; } = 0;
		public float goProWiderFov { get; set; } = 100f;
		public string goProPlayerName { get; set; } = "";
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
		/// <summary>The "Spectator overlay" toggle in the Live window: shows the Spark arena overlay over the Echo VR window.</summary>
		public bool spectatorOverlayEnabled { get; set; } = false;
		/// <summary>Hide Echo's built-in spectator HUD while the Spark overlay is showing.</summary>
		public bool spectatorStreamHideGameHud { get; set; } = true;
		public string sparkExeLocation { get; set; } = "";
		public bool allowSpectateMeOnLocalPC { get; set; } = false;
		public bool useAnonymousSpectateMe { get; set; } = true;
		public bool spectateMeOnByDefault { get; set; } = false;
		public string webBrowserHomeURL { get; set; } = "https://discord.com/app";
		public bool showDashboardTab { get; set; } = true;
		public bool showPortalTab { get; set; } = true;
		public bool showWebBrowserTab { get; set; } = true;
		public bool showDownloadEchoVRTab { get; set; } = true;
		public bool showLinksTab { get; set; } = false;
		public bool showEventLogTab { get; set; } = true;
		public bool showScoreboardTab { get; set; } = false;
		public bool showSpeakerSystemTab { get; set; } = true;
		public bool showServerInfoTab { get; set; } = true;
		public bool showWriteAPITab { get; set; } = true;
		public bool showEchoGPTab { get; set; } = false;
		public bool showPrivateMatchRulesTab { get; set; } = true;
		public bool showCreateServerTab { get; set; } = true;
		public bool showPlayerCardTab { get; set; } = true;
		public bool showFriendsTab { get; set; } = true;
		public bool showReplayAnalyserTab { get; set; } = true;
		/// <summary>
		/// Folder the Replay Analyser tab reads. Empty means follow <see cref="saveFolder"/>, which is
		/// where Spark records to; it only holds a path when the user has picked somewhere else.
		/// </summary>
		public string replayAnalyserFolder { get; set; } = "";
		public string myFriendCode { get; set; } = "";
		public List<string> friendCodes { get; set; } = new List<string>();
		public string ignoredUpdateVersion { get; set; } = "";
		public bool combatUpdatePopupShown { get; set; } = false;
		public bool combatAPIDismissed { get; set; } = false;



		#region TTS

		public bool throwSpeedTTS { get; set; } = false;
		public bool goalSpeedTTS { get; set; } = false;
		public bool goalDistanceTTS { get; set; } = false;
		public bool joustTimeTTS { get; set; } = false;
		public bool joustSpeedTTS { get; set; } = false;
		public bool serverLocationTTS { get; set; } = false;
		public bool maxBoostSpeedTTS { get; set; } = false;
		/// <summary>Legacy four-step speech speed's list position. Only read to migrate to <see cref="ttsSpeedMultiplier"/>.</summary>
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
		public string ttsCacheFolder { get; set; } = "";
		public int ttsCacheSizeBytes { get; set; } = 100000000;
		public bool pingSpikeTTS { get; set; } = false;
		/// <summary>With <see cref="pingSpikeTTS"/> on, only announce ping spikes while in a private match.</summary>
		public bool pingSpikeTTSPrivateOnly { get; set; } = false;
		public bool ttsSpecific { get; set; } = false;

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

		#region MediaController

		/// <summary>Whether the EchoVR mute-button media controller is active.</summary>
		public bool mediaControllerEnabled { get; set; } = false;

		/// <summary>Automatically attempt to reconnect when EchoVR is not found.</summary>
		public bool mediaControllerAutoReconnect { get; set; } = true;

		/// <summary>Number of clicks to trigger Previous Track.</summary>
		public int mediaControllerPrevClicks { get; set; } = 3;

		/// <summary>Number of clicks to trigger Next Track.</summary>
		public int mediaControllerNextClicks { get; set; } = 4;

		/// <summary>Seconds the button must be held to trigger Play/Pause.</summary>
		public double mediaControllerHoldThreshold { get; set; } = 3.0;

		/// <summary>Time window (seconds) in which multi-clicks are counted.</summary>
		public double mediaControllerClickTimeout { get; set; } = 0.8;

		/// <summary>Minimum press duration (seconds) to count as a click (debounce).</summary>
		public double mediaControllerDebounceDelay { get; set; } = 0.05;

		/// <summary>Minimum press duration (seconds) required to register a click at all.</summary>
		public double mediaControllerDetectionThreshold { get; set; } = 0.1;

		/// <summary>Whether a custom keyboard action is enabled.</summary>
		public bool mediaControllerCustomEnabled { get; set; } = false;

		/// <summary>Type of trigger for the custom action: 0=None, 1=Hold, 2=Clicks.</summary>
		public int mediaControllerCustomTrigger { get; set; } = 1;

		/// <summary>Number of clicks to trigger the custom action (if type is Clicks).</summary>
		public int mediaControllerCustomClicks { get; set; } = 4;

		/// <summary>Primary scancode for the custom action shortcut.</summary>
		public int mediaControllerCustomKey1 { get; set; } = 0x38; // DIK_LMENU (Alt)

		/// <summary>Secondary scancode for the custom action shortcut.</summary>
		public int mediaControllerCustomKey2 { get; set; } = 0x44; // DIK_F10

		/// <summary>Seconds the button must be held to trigger the custom action (if type is Hold).</summary>
		public double mediaControllerCustomHoldThreshold { get; set; } = 2.0;

	#endregion

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
		/// <summary>Overlay shown over the game by the Spectator overlay toggle: a path on the overlay server, a URL, or a file:/// URI.</summary>
		public string gameOverlayUrl { get; set; } = "/arena_overlay";
		/// <summary>URLs players typed into the overlay picker, kept so they stay in the list.</summary>
		public List<string> gameOverlayCustomUrls { get; set; } = new List<string>();

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

		#region Migrations

		/// <summary>
		/// Bump this when adding a migration below. Settings files written before migrations existed
		/// have no <see cref="settingsVersion"/> key at all, and Json.NET leaves absent keys at their
		/// initialiser — so this has to default to 0, not to the current version, or every existing
		/// file would claim to be up to date.
		/// </summary>
		private const int CurrentSettingsVersion = 2;

		public int settingsVersion { get; set; } = 0;

		/// <summary>
		/// Brings an older settings file up to date.
		///
		/// Changing a property's default only affects fresh installs: Save() writes every property,
		/// so an existing settings.json already has an explicit value for it and deserialisation puts
		/// that back over the new default. Anything that needs to reach people who already have a
		/// settings file has to be applied here instead.
		///
		/// Each step runs once. The version is stamped and saved immediately afterwards, so a user
		/// who undoes one of these changes doesn't get it forced on them again next launch.
		/// </summary>
		private static void Migrate()
		{
			if (instance == null) return;
			if (instance.settingsVersion >= CurrentSettingsVersion) return;

			// v1 — the Friends tab shipped hidden, then became a default-on feature. Existing users
			// would otherwise never see it, since their file pins it to the old default.
			if (instance.settingsVersion < 1)
			{
				instance.showFriendsTab = true;
			}

			// v2 — speech speed went from four named steps to a multiplier in tenths. The step that
			// actually set the voice was ttsSpeedIndex (1-based: Slow, Normal, Fast, Very Fast), but it
			// defaulted to 1, Slow, while the settings list showed Normal. An index still at that
			// default beside a list position also at its default means the speed was never touched, so
			// it becomes the Normal the settings showed rather than the slow voice the mismatch played.
			if (instance.settingsVersion < 2)
			{
				instance.ttsSpeedMultiplier = (instance.ttsSpeedIndex, instance.TTSSpeed) switch
				{
					(1, 1) => 1.0,
					(1, _) => 0.6,
					(3, _) => 1.4,
					(4, _) => 1.8,
					_ => 1.0,
				};
			}

			Console.WriteLine($"Migrated settings from version {instance.settingsVersion} to {CurrentSettingsVersion}.");
			instance.settingsVersion = CurrentSettingsVersion;
			instance.Save();
		}

		#endregion


		public void Save()
		{
			try
			{
				string filename = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IgniteVR", "Spark", "settings.json");

				Task.Run(() =>
				{
					try
					{
						if (!Directory.Exists(Path.GetDirectoryName(filename)))
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

			try
			{
				Migrate();
			}
			catch (Exception e)
			{
				// A failed migration mustn't stop Spark from starting — worst case the user keeps
				// their old value and we try again next launch.
				Console.WriteLine($"Error migrating settings\n{e}");
			}
		}
	}
}