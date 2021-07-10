using System;
using System.Numerics;
using System.Windows;
using Spark.Properties;

namespace Spark
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		//#if WINDOWS_STORE_RELEASE
		//		protected override void OnActivated(IActivatedEventArgs args)
		//		{
		//			if (args.Kind == ActivationKind.Protocol)
		//			{
		//				ProtocolActivatedEventArgs eventArgs = args as ProtocolActivatedEventArgs;
		//				// TODO: Handle URI activation
		//				// The received URI is eventArgs.Uri.AbsoluteUri
		//			}
		//		}
		//#endif

		protected override void OnStartup(StartupEventArgs e)
		{
			// load settings file
			SparkSettings.Load();

			if (SparkSettings.instance == null)
			{
				new MessageBox($"Error accessing settings.\nTry renaming/deleting the file in C:\\Users\\[USERNAME]\\AppData\\Roaming\\IgniteVR\\Spark\\settings.json").Show();
				return;
			}

			if (!SparkSettings.instance.jsonSettingsCreated)
			{
				// Reload old settings file
				if (Settings.Default.UpdateSettings)
				{
					Settings.Default.Upgrade();
					Settings.Default.UpdateSettings = false;
					Settings.Default.Save();
				}

				// only load the previous settings if they were actually used, otherwise just use the json defaults
				if (!string.IsNullOrEmpty(Settings.Default.client_name))
				{
					LoadPreviousSettings();
				}
			}

			SparkSettings.instance.jsonSettingsCreated = true;


			System.Threading.Thread.CurrentThread.CurrentUICulture = SparkSettings.instance.languageIndex switch
			{
				0 => new System.Globalization.CultureInfo("en"),
				1 => new System.Globalization.CultureInfo("ja-JP"),
				_ => System.Threading.Thread.CurrentThread.CurrentUICulture
			};

			ThemesController.SetTheme((ThemesController.ThemeTypes)SparkSettings.instance.theme);
			CheckWindowPositionsValid();

			base.OnStartup(e);

			Program.Main(e.Args, this);
		}

		private static void LoadPreviousSettings()
		{
			SparkSettings.instance.startOnBoot = Settings.Default.startOnBoot;
			SparkSettings.instance.startMinimized = Settings.Default.startMinimized;
			SparkSettings.instance.autoRestart = Settings.Default.autoRestart;
			SparkSettings.instance.capturevp2 = Settings.Default.capturevp2;
			SparkSettings.instance.showDatabaseLog = Settings.Default.showDatabaseLog;
			SparkSettings.instance.discordRichPresence = Settings.Default.discordRichPresence;
			SparkSettings.instance.logToServer = Settings.Default.logToServer;
			SparkSettings.instance.echoVRPath = Settings.Default.echoVRPath;
			SparkSettings.instance.echoVRIP = Settings.Default.echoVRIP;
			SparkSettings.instance.echoVRPort = Settings.Default.echoVRPort;
			SparkSettings.instance.enableStatsLogging = Settings.Default.enableStatsLogging;
			SparkSettings.instance.targetDeltaTimeIndexStats = Settings.Default.targetDeltaTimeIndexStats;
			SparkSettings.instance.uploadToIgniteDB = Settings.Default.uploadToIgniteDB;
			SparkSettings.instance.uploadToFirestore = Settings.Default.uploadToFirestore;
			SparkSettings.instance.enableFullLogging = Settings.Default.enableFullLogging;
			SparkSettings.instance.onlyRecordPrivateMatches = Settings.Default.onlyRecordPrivateMatches;
			SparkSettings.instance.batchWrites = Settings.Default.batchWrites;
			SparkSettings.instance.useCompression = Settings.Default.useCompression;
			SparkSettings.instance.targetDeltaTimeIndexFull = Settings.Default.targetDeltaTimeIndexFull;
			SparkSettings.instance.saveFolder = Settings.Default.saveFolder;
			SparkSettings.instance.whenToSplitReplays = Settings.Default.whenToSplitReplays;
			SparkSettings.instance.showConsoleOnStart = Settings.Default.showConsoleOnStart;
			SparkSettings.instance.outputGameStateEvents = Settings.Default.outputGameStateEvents;
			SparkSettings.instance.outputScoreEvents = Settings.Default.outputScoreEvents;
			SparkSettings.instance.outputStunEvents = Settings.Default.outputStunEvents;
			SparkSettings.instance.outputDiscThrownEvents = Settings.Default.outputDiscThrownEvents;
			SparkSettings.instance.outputDiscCaughtEvents = Settings.Default.outputDiscCaughtEvents;
			SparkSettings.instance.outputDiscStolenEvents = Settings.Default.outputDiscStolenEvents;
			SparkSettings.instance.outputSaveEvents = Settings.Default.outputSaveEvents;
			SparkSettings.instance.accessCode = Settings.Default.accessCode;
			SparkSettings.instance.outputOther = Settings.Default.outputOther;
			SparkSettings.instance.atlasShowing = Settings.Default.atlasShowing;
			SparkSettings.instance.speedometerStreamerMode = Settings.Default.speedometerStreamerMode;
			SparkSettings.instance.playspaceStreamerMode = Settings.Default.playspaceStreamerMode;
			SparkSettings.instance.joustTimeTTS = Settings.Default.joustTimeTTS;
			SparkSettings.instance.joustSpeedTTS = Settings.Default.joustSpeedTTS;
			SparkSettings.instance.serverLocationTTS = Settings.Default.serverLocationTTS;
			SparkSettings.instance.maxBoostSpeedTTS = Settings.Default.maxBoostSpeedTTS;
			SparkSettings.instance.TTSSpeed = Settings.Default.TTSSpeed;
			SparkSettings.instance.playerJoinTTS = Settings.Default.playerJoinTTS;
			SparkSettings.instance.playerLeaveTTS = Settings.Default.playerLeaveTTS;
			SparkSettings.instance.playerSwitchTeamTTS = Settings.Default.playerSwitchTeamTTS;
			SparkSettings.instance.tubeExitSpeedTTS = Settings.Default.tubeExitSpeedTTS;
			SparkSettings.instance.discordOAuthRefreshToken = Settings.Default.discordOAuthRefreshToken;
			SparkSettings.instance.throwSpeedTTS = Settings.Default.throwSpeedTTS;
			SparkSettings.instance.goalSpeedTTS = Settings.Default.goalSpeedTTS;
			SparkSettings.instance.goalDistanceTTS = Settings.Default.goalDistanceTTS;
			SparkSettings.instance.accessMode = Settings.Default.accessMode;
			SparkSettings.instance.clientHighlightScope = Settings.Default.clientHighlightScope;
			SparkSettings.instance.clearHighlightsOnExit = Settings.Default.clearHighlightsOnExit;
			SparkSettings.instance.isNVHighlightsEnabled = Settings.Default.isNVHighlightsEnabled;
			SparkSettings.instance.nvHighlightsSecondsBefore = Settings.Default.nvHighlightsSecondsBefore;
			SparkSettings.instance.nvHighlightsSecondsAfter = Settings.Default.nvHighlightsSecondsAfter;
			SparkSettings.instance.pausedTTS = Settings.Default.pausedTTS;
			SparkSettings.instance.alternateEchoVRIP = Settings.Default.alternateEchoVRIP;
			SparkSettings.instance.nvHighlightsSpectatorRecord = Settings.Default.nvHighlightsSpectatorRecord;
			SparkSettings.instance.atlasLinkStyle = Settings.Default.atlasLinkStyle;
			SparkSettings.instance.atlasLinkUseAngleBrackets = Settings.Default.atlasLinkUseAngleBrackets;
			SparkSettings.instance.firstTimeSetupShown = Settings.Default.firstTimeSetupShown;
			SparkSettings.instance.isAutofocusEnabled = Settings.Default.isAutofocusEnabled;
			SparkSettings.instance.loneEchoSubtitlesStreamerMode = Settings.Default.loneEchoSubtitlesStreamerMode;
			SparkSettings.instance.loneEchoPath = Settings.Default.loneEchoPath;
			SparkSettings.instance.liveWindowTop = (float)Settings.Default.liveWindowTop;
			SparkSettings.instance.liveWindowLeft = (float)Settings.Default.liveWindowLeft;
			SparkSettings.instance.settingsWindowTop = (float)Settings.Default.settingsWindowTop;
			SparkSettings.instance.settingsWindowLeft = (float)Settings.Default.settingsWindowLeft;
			SparkSettings.instance.client_name = Settings.Default.client_name;
			SparkSettings.instance.atlasLinkAppendTeamNames = Settings.Default.atlasLinkAppendTeamNames;
			SparkSettings.instance.atlasHostingVisibility = Settings.Default.atlasHostingVisibility;
			SparkSettings.instance.playspaceTTS = Settings.Default.playspaceTTS;
			SparkSettings.instance.ttsVoice = Settings.Default.ttsVoice;
			SparkSettings.instance.languageIndex = Settings.Default.languageIndex;
			SparkSettings.instance.theme = Settings.Default.theme;
			SparkSettings.instance.replayBufferLength = (float)Settings.Default.replayBufferLength;
			SparkSettings.instance.enableReplayBuffer = Settings.Default.enableReplayBuffer;
			SparkSettings.instance.replayClipPlayspace = Settings.Default.replayClipPlayspace;
			SparkSettings.instance.replayClipGoal = Settings.Default.replayClipGoal;
			SparkSettings.instance.replayClipSave = Settings.Default.replayClipSave;
			SparkSettings.instance.betaUpdates = Settings.Default.betaUpdates;
			SparkSettings.instance.obsIP = Settings.Default.obsIP;
			SparkSettings.instance.obsPassword = Settings.Default.obsPassword;
			SparkSettings.instance.obsAutoconnect = Settings.Default.obsAutoconnect;
			SparkSettings.instance.obsClipPlayspace = Settings.Default.obsClipPlayspace;
			SparkSettings.instance.obsClipGoal = Settings.Default.obsClipGoal;
			SparkSettings.instance.obsClipAssist = Settings.Default.obsClipAssist;
			SparkSettings.instance.obsClipSave = Settings.Default.obsClipSave;
			SparkSettings.instance.replayClipAssist = Settings.Default.replayClipAssist;
			SparkSettings.instance.replayClipSecondsAfter = Settings.Default.replayClipSecondsAfter;
			SparkSettings.instance.obsClipSecondsAfter = Settings.Default.obsClipSecondsAfter;
			SparkSettings.instance.replayClipSecondsBefore = Settings.Default.replayClipSecondsBefore;
			SparkSettings.instance.obsClipSecondsBefore = Settings.Default.obsClipSecondsBefore;
			SparkSettings.instance.obsAutostartReplayBuffer = Settings.Default.obsAutostartReplayBuffer;
			SparkSettings.instance.obsClipInterception = Settings.Default.obsClipInterception;
			SparkSettings.instance.replayClipInterception = Settings.Default.replayClipInterception;
			SparkSettings.instance.nvHighlightsPlayerScope = Settings.Default.nvHighlightsPlayerScope;
			SparkSettings.instance.replayClipPlayerScope = Settings.Default.replayClipPlayerScope;
			SparkSettings.instance.obsPlayerScope = Settings.Default.obsPlayerScope;
			SparkSettings.instance.replayClipSpectatorRecord = Settings.Default.replayClipSpectatorRecord;
			SparkSettings.instance.obsSpectatorRecord = Settings.Default.obsSpectatorRecord;
			SparkSettings.instance.dashboardItem1 = Settings.Default.dashboardItem1;
			SparkSettings.instance.useWavenetVoices = Settings.Default.useWavenetVoices;
			SparkSettings.instance.spectatorCamera = Settings.Default.spectatorCamera;
			SparkSettings.instance.hideEchoVRUI = Settings.Default.hideEchoVRUI;
			SparkSettings.instance.followPlayerCameraMode = Settings.Default.followClientSpectatorCameraMode;
			SparkSettings.instance.toggleMinimapAfterGoals = Settings.Default.toggleMinimapAfterGoals;
			SparkSettings.instance.chooseRegionIndex = Settings.Default.chooseRegionIndex;
			SparkSettings.instance.chooseRegionSpectator = Settings.Default.chooseRegionSpectator;
			SparkSettings.instance.obsInGameScene = Settings.Default.obsInGameScene;
			SparkSettings.instance.obsBetweenGameScene = Settings.Default.obsBetweenGameScene;
		}


		private static void CheckWindowPositionsValid()
		{
			if (OffScreen(
				new Vector2(
					SparkSettings.instance.liveWindowLeft,
					SparkSettings.instance.liveWindowTop
					),
				new Vector2(500, 500)))
			{
				SparkSettings.instance.liveWindowLeft = 0;
				SparkSettings.instance.liveWindowTop = 0;
			}

			if (OffScreen(
				new Vector2(
					SparkSettings.instance.settingsWindowLeft,
					SparkSettings.instance.settingsWindowTop
					),
				new Vector2(500, 500)))
			{
				SparkSettings.instance.settingsWindowLeft = 0;
				SparkSettings.instance.settingsWindowTop = 0;
			}

		}

		private static bool OffScreen(Vector2 topLeft, Vector2 size)
		{
			return
				(topLeft.X <= SystemParameters.VirtualScreenLeft - size.X) ||
				(topLeft.Y <= SystemParameters.VirtualScreenTop - size.Y) ||
				(SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth <= topLeft.X) ||
				(SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight <= topLeft.Y);
		}

		public void ExitApplication()
		{
			Program.running = false;
			Current.Shutdown();
			Environment.Exit(Environment.ExitCode);
		}
	}
}