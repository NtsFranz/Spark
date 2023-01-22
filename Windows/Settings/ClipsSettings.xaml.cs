using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Communication;
using OBSWebsocketDotNet.Types;
using OBSWebsocketDotNet.Types.Events;

namespace Spark
{
	public partial class ClipsSettings
	{
		// set to false initially so that loading the settings from disk doesn't activate the events
		private bool initialized;
		private readonly Timer outputUpdateTimer = new Timer();

		private bool sceneDropdownListenersActive = false;

		private readonly List<Keyboard.DirectXKeyStrokes> medalTVInputs = new List<Keyboard.DirectXKeyStrokes>
		{
			Keyboard.DirectXKeyStrokes.DIK_F8,
			Keyboard.DirectXKeyStrokes.DIK_F12,
			Keyboard.DirectXKeyStrokes.DIK_BACKSLASH,
			Keyboard.DirectXKeyStrokes.DIK_HOME,
			Keyboard.DirectXKeyStrokes.DIK_SCROLL,
		};

		public ClipsSettings()
		{
			InitializeComponent();

			Program.obs.ws.Connected += OBSConnected;
			Program.obs.ws.Disconnected += OBSDisconnected;

			obsConnectButton.Content = Program.obs.connected
				? Properties.Resources.Disconnect
				: Properties.Resources.Connect;


			if (Program.obs.connected)
			{
				Task.Run(() => { Dispatcher.Invoke(RefreshSceneList); });
			}

			RefreshReplayBufferVisibility();

			Program.obs.ws.ReplayBufferStateChanged += ReplayBufferChanged;

			inGameScene.SelectionChanged += InGameSceneChanged;
			betweenGameScene.SelectionChanged += BetweenGameSceneChanged;
			goalReplayScene.SelectionChanged += GoalReplaySceneChanged;
			saveReplayScene.SelectionChanged += SaveReplaySceneChanged;

			// if (MedalTVInputs.Contains((Keyboard.DirectXKeyStrokes)SparkSettings.instance.medalClipKey))
			// {
			medalClipHotkeyDropdown.SelectedIndex = medalTVInputs.IndexOf((Keyboard.DirectXKeyStrokes)SparkSettings.instance.medalClipKey);
			// }
			// else
			// {
			// 	medalClipHotkeyDropdown.SelectedIndex = -1;
			// }

			// passwordbox can't use binding
			obsPasswordBox.Password = SparkSettings.instance.obsPassword;
			DoClipLengthSum();

			LoadDevices();

			outputUpdateTimer.Interval = 150;
			outputUpdateTimer.Elapsed += Update;
			outputUpdateTimer.Enabled = true;

			initialized = true;
		}

		private void RefreshSceneList()
		{
			if (!Program.obs.connected) return;
			
			sceneDropdownListenersActive = false;

			List<string> sceneNames = Program.obs.ws.GetSceneList().Scenes.Select((scene) => scene.Name).ToList();

			inGameScene.Items.Clear();
			betweenGameScene.Items.Clear();
			goalReplayScene.Items.Clear();
			saveReplayScene.Items.Clear();

			inGameScene.Items.Add(new ComboBoxItem { Content = "--- Do Not Switch ---" });
			betweenGameScene.Items.Add(new ComboBoxItem { Content = "--- Do Not Switch ---" });
			goalReplayScene.Items.Add(new ComboBoxItem { Content = "--- Do Not Switch ---" });
			saveReplayScene.Items.Add(new ComboBoxItem { Content = "--- Do Not Switch ---" });
			
			inGameScene.SelectedIndex = 0;
			betweenGameScene.SelectedIndex = 0;
			goalReplayScene.SelectedIndex = 0;
			saveReplayScene.SelectedIndex = 0;

			for (int i = 0; i < sceneNames.Count; i++)
			{
				inGameScene.Items.Add(new ComboBoxItem
				{
					Content = sceneNames[i]
				});
				if (SparkSettings.instance.obsInGameScene == sceneNames[i])
				{
					inGameScene.SelectedIndex = i + 1;
				}

				betweenGameScene.Items.Add(new ComboBoxItem
				{
					Content = sceneNames[i]
				});
				if (SparkSettings.instance.obsBetweenGameScene == sceneNames[i])
				{
					betweenGameScene.SelectedIndex = i + 1;
				}

				goalReplayScene.Items.Add(new ComboBoxItem
				{
					Content = sceneNames[i]
				});
				if (SparkSettings.instance.obsGoalReplayScene == sceneNames[i])
				{
					goalReplayScene.SelectedIndex = i + 1;
				}

				saveReplayScene.Items.Add(new ComboBoxItem
				{
					Content = sceneNames[i]
				});
				if (SparkSettings.instance.obsSaveReplayScene == sceneNames[i])
				{
					saveReplayScene.SelectedIndex = i + 1;
				}
			}
			sceneDropdownListenersActive = true;
		}


		private void Update(object source, ElapsedEventArgs e)
		{
			if (Program.running)
			{
				Dispatcher.Invoke(() =>
				{
					MicLevel.Value = Program.speechRecognizer.GetMicLevel();
					SpeakerLevel.Value = Program.speechRecognizer.GetSpeakerLevel();
				});
			}
		}

		private void ReplayBufferChanged(object sender, ReplayBufferStateChangedEventArgs changed)
		{
			Task.Run(async () =>
			{
				await Task.Delay(100);
				Dispatcher.Invoke(RefreshReplayBufferVisibility);
			});
		}

		private void RefreshReplayBufferVisibility()
		{
			if (!Program.obs.connected)
			{
				obsStartReplayBufferButton.Visibility = Visibility.Collapsed;
				obsStartReplayBufferButton.IsEnabled = false;
				obsStopReplayBufferButton.Visibility = Visibility.Collapsed;
				obsStopReplayBufferButton.IsEnabled = false;

				return;
			}

			switch (Program.obs.replayBufferState)
			{
				case OutputState.OBS_WEBSOCKET_OUTPUT_STARTING:
					obsStartReplayBufferButton.Visibility = Visibility.Visible;
					obsStartReplayBufferButton.IsEnabled = false;
					obsStopReplayBufferButton.Visibility = Visibility.Collapsed;
					obsStopReplayBufferButton.IsEnabled = true;
					ReplayBufferNotEnabled.Visibility = Visibility.Collapsed;
					break;
				case OutputState.OBS_WEBSOCKET_OUTPUT_STARTED:
					obsStartReplayBufferButton.Visibility = Visibility.Collapsed;
					obsStartReplayBufferButton.IsEnabled = true;
					obsStopReplayBufferButton.Visibility = Visibility.Visible;
					obsStopReplayBufferButton.IsEnabled = true;
					ReplayBufferNotEnabled.Visibility = Visibility.Collapsed;
					break;
				case OutputState.OBS_WEBSOCKET_OUTPUT_STOPPING:
					obsStartReplayBufferButton.Visibility = Visibility.Collapsed;
					obsStartReplayBufferButton.IsEnabled = false;
					obsStopReplayBufferButton.Visibility = Visibility.Visible;
					obsStopReplayBufferButton.IsEnabled = false;
					ReplayBufferNotEnabled.Visibility = Visibility.Collapsed;
					break;
				case OutputState.OBS_WEBSOCKET_OUTPUT_STOPPED:
					obsStartReplayBufferButton.Visibility = Visibility.Visible;
					obsStartReplayBufferButton.IsEnabled = true;
					obsStopReplayBufferButton.Visibility = Visibility.Collapsed;
					obsStopReplayBufferButton.IsEnabled = true;
					ReplayBufferNotEnabled.Visibility = Visibility.Collapsed;
					break;
				case OutputState.OBS_WEBSOCKET_OUTPUT_PAUSED:
					break;
				case OutputState.OBS_WEBSOCKET_OUTPUT_RESUMED:
					break;
				case null:
					obsStartReplayBufferButton.Visibility = Visibility.Collapsed;
					obsStartReplayBufferButton.IsEnabled = false;
					obsStopReplayBufferButton.Visibility = Visibility.Collapsed;
					obsStopReplayBufferButton.IsEnabled = false;
					ReplayBufferNotEnabled.Visibility = Visibility.Visible;
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		private void OBSConnected(object sender, EventArgs e)
		{
			Task.Run(async () =>
			{
				Dispatcher.Invoke(() => { obsConnectButton.Content = Properties.Resources.Disconnect; });
				await Task.Delay(100);
				Dispatcher.Invoke(() =>
				{
					RefreshSceneList();
					RefreshReplayBufferVisibility();
				});
			});
		}

		private void OBSDisconnected(object sender, ObsDisconnectionInfo e)
		{
			Dispatcher.Invoke(async () =>
			{
				obsConnectButton.Content = Properties.Resources.Connect;
				await Task.Delay(100);
				RefreshReplayBufferVisibility();
			});
		}

		public bool NVHighlightsEnabled
		{
			get => SparkSettings.instance.isNVHighlightsEnabled;
			set
			{
				switch (HighlightsHelper.isNVHighlightsEnabled)
				{
					case true when !value:
						HighlightsHelper.CloseNVHighlights(true);
						break;
					case false when value:
					{
						if (HighlightsHelper.SetupNVHighlights() < 0)
						{
							HighlightsHelper.isNVHighlightsEnabled = false;
							SparkSettings.instance.isNVHighlightsEnabled = false;
							enableNVHighlightsCheckbox.IsChecked = false;
							enableNVHighlightsCheckbox.IsEnabled = false;
							enableNVHighlightsCheckbox.Content = Properties.Resources
								.NVIDIA_Highlights_failed_to_initialize_or_isn_t_supported_by_your_PC;
							return;
						}

						enableNVHighlightsCheckbox.Content = Properties.Resources.Enable_NVIDIA_Highlights;
						break;
					}
				}

				HighlightsHelper.isNVHighlightsEnabled = value;
				SparkSettings.instance.isNVHighlightsEnabled = HighlightsHelper.isNVHighlightsEnabled;

				nvHighlightsBox.IsEnabled = HighlightsHelper.isNVHighlightsEnabled;
				nvHighlightsBox.Opacity = HighlightsHelper.isNVHighlightsEnabled ? 1 : .5;
			}
		}

		private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
		{
			Regex regex = new Regex("[^0-9]+");
			e.Handled = regex.IsMatch(e.Text);
		}

		private void ClearHighlightsEvent(object sender, RoutedEventArgs e)
		{
			if (!initialized) return;
			HighlightsHelper.ClearUnsavedNVHighlights(true);
			clearHighlightsButton.IsEnabled = false;
			clearHighlightsButton.Content = "Clear 0 Unsaved Highlights";
			new MessageBox(Properties.Resources.highlights_cleared).Show();
		}

		public string ClearHighlightsButtonContent =>
			$"Clear {HighlightsHelper.nvHighlightClipCount} Unsaved Highlights";

		public string EnableHighlightsContent => HighlightsHelper.isNVHighlightsSupported
			? Properties.Resources.Enable_NVIDIA_Highlights
			: Properties.Resources.Highlights_isn_t_supported_by_your_PC;

		public bool HighlightsSupported => HighlightsHelper.isNVHighlightsSupported;
		public bool DoNVClipsExist => HighlightsHelper.DoNVClipsExist();


		private void ClipNow(object sender, RoutedEventArgs e)
		{
			Program.replayFilesManager.SaveReplayClip("manual");
		}

		private void OBSConnect(object sender, RoutedEventArgs e)
		{
			Task.Run(() =>
			{
				if (!Program.obs.connected || !Program.obs.ws.IsConnected)
				{
					try
					{
						Program.obs.ws.Connect(SparkSettings.instance.obsIP, SparkSettings.instance.obsPassword);
						// Program.obs.instance.GetReplayBufferStatus();
					}
					catch (AuthFailureException)
					{
						Logger.LogRow(Logger.LogType.Error, "Failed to connect to OBS. AuthFailure");
						Dispatcher.Invoke(() => { new MessageBox("Authentication failed.", Properties.Resources.Error).Show(); });
						Program.obs.ws.Disconnect();
						return;
					}
					catch (ErrorResponseException ex)
					{
						Logger.LogRow(Logger.LogType.Error, $"Failed to connect to OBS.\n{ex}");
						Dispatcher.Invoke(() => { new MessageBox("Connect failed.", Properties.Resources.Error).Show(); });
						Program.obs.ws.Disconnect();
						return;
					}
					catch (Exception ex)
					{
						Logger.LogRow(Logger.LogType.Error, $"Failed to connect to OBS for another reason.\n{ex}");
						Dispatcher.Invoke(() => { new MessageBox("Connect failed.", Properties.Resources.Error).Show(); });
						Program.obs.ws.Disconnect();
						return;
					}
				}
				else
				{
					Program.obs.ws.Disconnect();
				}
			});
		}

		private enum ClipsTab
		{
			NVHighlights,
			echoreplay,
			OBS,
			Medal,
			Voice
		}

		private ClipsTab clipsTab;

		private void ClipTypeTabChanged(object sender, SelectionChangedEventArgs e)
		{
			if (!initialized) return;
			TabControl control = (TabControl)sender;
			clipsEventsBox.Header = $"Clip Settings ({((TabItem)control.SelectedValue).Header})";
			clipsTab = (ClipsTab)control.SelectedIndex;

			// if NV Highlights
			if (clipsTab == ClipsTab.NVHighlights)
			{
				nvHighlightsChooseEventsInOverlayLabel.Visibility = Visibility.Visible;
				eventsToChoose.Visibility = Visibility.Collapsed;
			}
			else
			{
				nvHighlightsChooseEventsInOverlayLabel.Visibility = Visibility.Collapsed;
				eventsToChoose.Visibility = Visibility.Visible;
			}

			// if obs
			if (clipsTab == ClipsTab.OBS)
			{
				clipsEventsBox.Visibility = Visibility.Visible;
				labelBefore.Visibility = Visibility.Collapsed;
				secondsBefore.Visibility = Visibility.Collapsed;
				labelTotal.Visibility = Visibility.Collapsed;
				totalSeconds.Visibility = Visibility.Collapsed;
				goalReplaySceneBox.Visibility = Visibility.Visible;
				saveReplaySceneBox.Visibility = Visibility.Visible;
			}
			else if (clipsTab == ClipsTab.Medal)
			{
				clipsEventsBox.Visibility = Visibility.Visible;
				labelBefore.Visibility = Visibility.Collapsed;
				secondsBefore.Visibility = Visibility.Collapsed;
				labelTotal.Visibility = Visibility.Collapsed;
				totalSeconds.Visibility = Visibility.Collapsed;
				goalReplaySceneBox.Visibility = Visibility.Collapsed;
				saveReplaySceneBox.Visibility = Visibility.Collapsed;
			}
			else if (clipsTab == ClipsTab.Voice)
			{
				clipsEventsBox.Visibility = Visibility.Collapsed;
			}
			else
			{
				clipsEventsBox.Visibility = Visibility.Visible;
				labelBefore.Visibility = Visibility.Visible;
				secondsBefore.Visibility = Visibility.Visible;
				labelTotal.Visibility = Visibility.Visible;
				totalSeconds.Visibility = Visibility.Visible;
				goalReplaySceneBox.Visibility = Visibility.Collapsed;
				saveReplaySceneBox.Visibility = Visibility.Collapsed;
			}

			RefreshAllSettings(null, null);
			DoClipLengthSum();
		}

		private void OBSPasswordChanged(object sender, RoutedEventArgs e)
		{
			SparkSettings.instance.obsPassword = ((PasswordBox)sender).Password;
		}

		private void OBSStartReplayBuffer(object sender, RoutedEventArgs e)
		{
			if (Program.obs.connected && Program.obs.replayBufferState != null)
			{
				try
				{
					Program.obs.ws.StartReplayBuffer();
				}
				catch (Exception exp)
				{
					Logger.LogRow(Logger.LogType.Error, $"Couldn't start replay buffer\n{exp}");

					Dispatcher.Invoke(() => { new MessageBox("Replay buffer not enabled in OBS.", Properties.Resources.Error).Show(); });
				}
			}
		}

		private void OBSStopReplayBuffer(object sender, RoutedEventArgs e)
		{
			if (Program.obs.connected)
			{
				try
				{
					Program.obs.ws.StopReplayBuffer();
				}
				catch (Exception exp)
				{
					Logger.LogRow(Logger.LogType.Error, $"Couldn't stop replay buffer\n{exp}");
				}
			}
		}

		public int ClipsPlayerScope
		{
			get
			{
				return clipsTab switch
				{
					ClipsTab.NVHighlights => SparkSettings.instance.nvHighlightsPlayerScope,
					ClipsTab.echoreplay => SparkSettings.instance.replayClipPlayerScope,
					ClipsTab.OBS => SparkSettings.instance.obsPlayerScope,
					ClipsTab.Medal => SparkSettings.instance.medalClipPlayerScope,
					_ => 0,
				};
			}
			set
			{
				switch (clipsTab)
				{
					case ClipsTab.NVHighlights:
						SparkSettings.instance.nvHighlightsPlayerScope = value;
						break;
					case ClipsTab.echoreplay:
						SparkSettings.instance.replayClipPlayerScope = value;
						break;
					case ClipsTab.OBS:
						SparkSettings.instance.obsPlayerScope = value;
						break;
					case ClipsTab.Medal:
						SparkSettings.instance.medalClipPlayerScope = value;
						break;
				}
			}
		}

		public bool ClipsSpectatorRecord
		{
			get
			{
				return clipsTab switch
				{
					ClipsTab.NVHighlights => SparkSettings.instance.nvHighlightsSpectatorRecord,
					ClipsTab.echoreplay => SparkSettings.instance.replayClipSpectatorRecord,
					ClipsTab.OBS => SparkSettings.instance.obsSpectatorRecord,
					ClipsTab.Medal => SparkSettings.instance.medalClipSpectatorRecord,
					_ => false,
				};
			}
			set
			{
				switch (clipsTab)
				{
					case ClipsTab.NVHighlights:
						SparkSettings.instance.nvHighlightsSpectatorRecord = value;
						break;
					case ClipsTab.echoreplay:
						SparkSettings.instance.replayClipSpectatorRecord = value;
						break;
					case ClipsTab.OBS:
						SparkSettings.instance.obsSpectatorRecord = value;
						break;
					case ClipsTab.Medal:
						SparkSettings.instance.medalClipSpectatorRecord = value;
						break;
				}
			}
		}

		private void DoClipLengthSum()
		{
			totalSeconds.Text = (float.Parse(secondsBefore.Text) + float.Parse(secondsAfter.Text)).ToString();
		}

		public string SecondsBefore
		{
			get
			{
				return clipsTab switch
				{
					ClipsTab.NVHighlights => SparkSettings.instance.nvHighlightsSecondsBefore.ToString(),
					ClipsTab.echoreplay => SparkSettings.instance.replayClipSecondsBefore.ToString(),
					ClipsTab.OBS => SparkSettings.instance.obsClipSecondsBefore.ToString(),
					ClipsTab.Medal => SparkSettings.instance.medalClipSecondsBefore.ToString(),
					_ => "0",
				};
			}
			set
			{
				if (!float.TryParse(value, out float sec)) return;
				switch (clipsTab)
				{
					case ClipsTab.NVHighlights:
						SparkSettings.instance.nvHighlightsSecondsBefore = sec;
						break;
					case ClipsTab.echoreplay:
						SparkSettings.instance.replayClipSecondsBefore = sec;
						break;
					case ClipsTab.OBS:
						SparkSettings.instance.obsClipSecondsBefore = sec;
						break;
					case ClipsTab.Medal:
						SparkSettings.instance.medalClipSecondsBefore = sec;
						break;
				}

				DoClipLengthSum();
			}
		}

		public string GoalReplayLength
		{
			get
			{
				return clipsTab switch
				{
					ClipsTab.OBS => SparkSettings.instance.obsGoalReplayLength.ToString(),
					_ => "0",
				};
			}
			set
			{
				if (!float.TryParse(value, out float sec)) return;
				switch (clipsTab)
				{
					case ClipsTab.OBS:
						SparkSettings.instance.obsGoalReplayLength = sec;
						break;
				}
			}
		}

		public string SaveReplayLength
		{
			get
			{
				return clipsTab switch
				{
					ClipsTab.OBS => SparkSettings.instance.obsSaveReplayLength.ToString(),
					_ => "0",
				};
			}
			set
			{
				if (!float.TryParse(value, out float sec)) return;
				switch (clipsTab)
				{
					case ClipsTab.OBS:
						SparkSettings.instance.obsSaveReplayLength = sec;
						break;
				}
			}
		}

		public string SecondsAfter
		{
			get
			{
				return clipsTab switch
				{
					ClipsTab.NVHighlights => SparkSettings.instance.nvHighlightsSecondsAfter.ToString(),
					ClipsTab.echoreplay => SparkSettings.instance.replayClipSecondsAfter.ToString(),
					ClipsTab.OBS => SparkSettings.instance.obsClipSecondsAfter.ToString(),
					ClipsTab.Medal => SparkSettings.instance.medalClipSecondsAfter.ToString(),
					_ => "0",
				};
			}
			set
			{
				if (!float.TryParse(value, out float sec)) return;
				switch (clipsTab)
				{
					case ClipsTab.NVHighlights:
						SparkSettings.instance.nvHighlightsSecondsAfter = sec;
						break;
					case ClipsTab.echoreplay:
						SparkSettings.instance.replayClipSecondsAfter = sec;
						break;
					case ClipsTab.OBS:
						SparkSettings.instance.obsClipSecondsAfter = sec;
						break;
					case ClipsTab.Medal:
						SparkSettings.instance.medalClipSecondsAfter = sec;
						break;
				}

				DoClipLengthSum();
			}
		}

		public string GoalSecondsAfter
		{
			get
			{
				return clipsTab switch
				{
					ClipsTab.OBS => SparkSettings.instance.obsGoalSecondsAfter.ToString(),
					_ => "0",
				};
			}
			set
			{
				if (!float.TryParse(value, out float sec)) return;
				switch (clipsTab)
				{
					case ClipsTab.OBS:
						SparkSettings.instance.obsGoalSecondsAfter = sec;
						break;
				}

				DoClipLengthSum();
			}
		}

		public string SaveSecondsAfter
		{
			get
			{
				return clipsTab switch
				{
					ClipsTab.OBS => SparkSettings.instance.obsSaveSecondsAfter.ToString(),
					_ => "0",
				};
			}
			set
			{
				if (!float.TryParse(value, out float sec)) return;
				switch (clipsTab)
				{
					case ClipsTab.OBS:
						SparkSettings.instance.obsSaveSecondsAfter = sec;
						break;
				}

				DoClipLengthSum();
			}
		}

		#region Event type settings

		public bool ClipEmoteSetting
		{
			get
			{
				return clipsTab switch
				{
					ClipsTab.echoreplay => SparkSettings.instance.replayClipEmote,
					ClipsTab.OBS => SparkSettings.instance.obsClipEmote,
					ClipsTab.Medal => SparkSettings.instance.medalClipEmote,
					_ => false,
				};
			}
			set
			{
				switch (clipsTab)
				{
					case ClipsTab.echoreplay:
						SparkSettings.instance.replayClipEmote = value;
						break;
					case ClipsTab.OBS:
						SparkSettings.instance.obsClipEmote = value;
						break;
					case ClipsTab.Medal:
						SparkSettings.instance.medalClipEmote = value;
						break;
				}
			}
		}

		public bool ClipPlayspaceSetting
		{
			get
			{
				return clipsTab switch
				{
					ClipsTab.echoreplay => SparkSettings.instance.replayClipPlayspace,
					ClipsTab.OBS => SparkSettings.instance.obsClipPlayspace,
					ClipsTab.Medal => SparkSettings.instance.medalClipPlayspace,
					_ => false,
				};
			}
			set
			{
				switch (clipsTab)
				{
					case ClipsTab.echoreplay:
						SparkSettings.instance.replayClipPlayspace = value;
						break;
					case ClipsTab.OBS:
						SparkSettings.instance.obsClipPlayspace = value;
						break;
					case ClipsTab.Medal:
						SparkSettings.instance.medalClipPlayspace = value;
						break;
				}
			}
		}

		public bool ClipGoalSetting
		{
			get
			{
				return clipsTab switch
				{
					ClipsTab.echoreplay => SparkSettings.instance.replayClipGoal,
					ClipsTab.OBS => SparkSettings.instance.obsClipGoal,
					ClipsTab.Medal => SparkSettings.instance.medalClipGoal,
					_ => false,
				};
			}
			set
			{
				switch (clipsTab)
				{
					case ClipsTab.echoreplay:
						SparkSettings.instance.replayClipGoal = value;
						break;
					case ClipsTab.OBS:
						SparkSettings.instance.obsClipGoal = value;
						break;
					case ClipsTab.Medal:
						SparkSettings.instance.medalClipGoal = value;
						break;
				}
			}
		}

		public bool ClipSaveSetting
		{
			get
			{
				return clipsTab switch
				{
					ClipsTab.echoreplay => SparkSettings.instance.replayClipSave,
					ClipsTab.OBS => SparkSettings.instance.obsClipSave,
					ClipsTab.Medal => SparkSettings.instance.medalClipSave,
					_ => false,
				};
			}
			set
			{
				switch (clipsTab)
				{
					case ClipsTab.echoreplay:
						SparkSettings.instance.replayClipSave = value;
						break;
					case ClipsTab.OBS:
						SparkSettings.instance.obsClipSave = value;
						break;
					case ClipsTab.Medal:
						SparkSettings.instance.medalClipSave = value;
						break;
				}
			}
		}

		public bool ClipAssistSetting
		{
			get
			{
				return clipsTab switch
				{
					ClipsTab.echoreplay => SparkSettings.instance.replayClipAssist,
					ClipsTab.OBS => SparkSettings.instance.obsClipAssist,
					ClipsTab.Medal => SparkSettings.instance.medalClipAssist,
					_ => false,
				};
			}
			set
			{
				switch (clipsTab)
				{
					case ClipsTab.echoreplay:
						SparkSettings.instance.replayClipAssist = value;
						break;
					case ClipsTab.OBS:
						SparkSettings.instance.obsClipAssist = value;
						break;
					case ClipsTab.Medal:
						SparkSettings.instance.medalClipAssist = value;
						break;
				}
			}
		}

		public bool ClipInterceptionSetting
		{
			get
			{
				return clipsTab switch
				{
					ClipsTab.echoreplay => SparkSettings.instance.replayClipInterception,
					ClipsTab.OBS => SparkSettings.instance.obsClipInterception,
					ClipsTab.Medal => SparkSettings.instance.medalClipInterception,
					_ => false,
				};
			}
			set
			{
				switch (clipsTab)
				{
					case ClipsTab.echoreplay:
						SparkSettings.instance.replayClipInterception = value;
						break;
					case ClipsTab.OBS:
						SparkSettings.instance.obsClipInterception = value;
						break;
					case ClipsTab.Medal:
						SparkSettings.instance.medalClipInterception = value;
						break;
				}
			}
		}

		public bool ClipNeutralJoustSetting
		{
			get
			{
				return clipsTab switch
				{
					ClipsTab.echoreplay => SparkSettings.instance.replayClipNeutralJoust,
					ClipsTab.OBS => SparkSettings.instance.obsClipNeutralJoust,
					ClipsTab.Medal => SparkSettings.instance.medalClipNeutralJoust,
					_ => false,
				};
			}
			set
			{
				switch (clipsTab)
				{
					case ClipsTab.echoreplay:
						SparkSettings.instance.replayClipNeutralJoust = value;
						break;
					case ClipsTab.OBS:
						SparkSettings.instance.obsClipNeutralJoust = value;
						break;
					case ClipsTab.Medal:
						SparkSettings.instance.medalClipNeutralJoust = value;
						break;
				}
			}
		}

		public bool ClipDefensiveJoustSetting
		{
			get
			{
				return clipsTab switch
				{
					ClipsTab.echoreplay => SparkSettings.instance.replayClipDefensiveJoust,
					ClipsTab.OBS => SparkSettings.instance.obsClipDefensiveJoust,
					ClipsTab.Medal => SparkSettings.instance.medalClipDefensiveJoust,
					_ => false,
				};
			}
			set
			{
				switch (clipsTab)
				{
					case ClipsTab.echoreplay:
						SparkSettings.instance.replayClipDefensiveJoust = value;
						break;
					case ClipsTab.OBS:
						SparkSettings.instance.obsClipDefensiveJoust = value;
						break;
					case ClipsTab.Medal:
						SparkSettings.instance.medalClipDefensiveJoust = value;
						break;
				}
			}
		}

		#endregion

		private void InGameSceneChanged(object sender, SelectionChangedEventArgs e)
		{
			if (!sceneDropdownListenersActive)
			{
				e.Handled = true;
				return;
			}
			SparkSettings.instance.obsInGameScene = (string)((ComboBoxItem)((ComboBox)sender).SelectedValue).Content;
		}

		private void BetweenGameSceneChanged(object sender, SelectionChangedEventArgs e)
		{
			if (!sceneDropdownListenersActive)
			{
				e.Handled = true;
				return;
			}
			SparkSettings.instance.obsBetweenGameScene =
				(string)((ComboBoxItem)((ComboBox)sender).SelectedValue).Content;
		}

		private void GoalReplaySceneChanged(object sender, SelectionChangedEventArgs e)
		{
			if (!sceneDropdownListenersActive)
			{
				e.Handled = true;
				return;
			}
			SparkSettings.instance.obsGoalReplayScene =
				(string)((ComboBoxItem)((ComboBox)sender).SelectedValue).Content;
		}

		private void SaveReplaySceneChanged(object sender, SelectionChangedEventArgs e)
		{
			if (!sceneDropdownListenersActive)
			{
				e.Handled = true;
				return;
			}
			SparkSettings.instance.obsSaveReplayScene =
				(string)((ComboBoxItem)((ComboBox)sender).SelectedValue).Content;
		}

		private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
		{
			try
			{
				Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
				e.Handled = true;
			}
			catch (Exception ex)
			{
				Logger.LogRow(Logger.LogType.Error, ex.ToString());
			}
		}

		private void RefreshAllSettings(object sender, SelectionChangedEventArgs e)
		{
			if (initialized)
			{
				DataContext = null;
				DataContext = this;
			}
		}


		private void LoadDevices()
		{
			for (int deviceId = 0; deviceId < WaveIn.DeviceCount; deviceId++)
			{
				WaveInCapabilities deviceInfo = WaveIn.GetCapabilities(deviceId);
				MicSelection.Items.Add(deviceInfo.ProductName);
				if (deviceInfo.ProductName == SparkSettings.instance.microphone)
				{
					MicSelection.SelectedIndex = deviceId;
				}
			}

			using MMDeviceEnumerator enumerator = new MMDeviceEnumerator();
			List<string> devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
				.Select(d => d.FriendlyName).ToList();

			for (int i = 0; i < devices.Count; i++)
			{
				SpeakerSelection.Items.Add(devices[i]);
				if (devices[i] == SparkSettings.instance.speaker)
				{
					SpeakerSelection.SelectedIndex = i;
				}
			}
		}


		private void VoiceRecognitionUnchecked(object sender, RoutedEventArgs e)
		{
			Program.speechRecognizer.Enabled = false;
		}

		private void VoiceRecognitionChecked(object sender, RoutedEventArgs e)
		{
			Program.speechRecognizer.Enabled = true;
		}

		private void MicrophoneChanged(object sender, SelectionChangedEventArgs e)
		{
			// Program.speechRecognizer.RefreshMicrophoneSetting();
		}

		/// <summary>
		/// Open the Speech, Inking and Typing page under Settings -> Privacy, enabling a user to accept the 
		/// Microsoft Privacy Policy, and enable personalization.
		/// </summary>
		private void OpenPrivacySettings(object sender, RoutedEventArgs e)
		{
			try
			{
				Process.Start(new ProcessStartInfo
				{
					FileName = "ms-settings:privacy-speech",
					UseShellExecute = true
				});
			}
			catch (Exception ex)
			{
				Logger.LogRow(Logger.LogType.Error, "Failed to launch speech settings\n" + ex);
			}
		}

		private void MicrophoneSelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			SparkSettings.instance.microphone = MicSelection.SelectedItem.ToString();
			if (initialized) Task.Run(Program.speechRecognizer.ReloadMic);
		}

		private void SpeakerSelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			SparkSettings.instance.speaker = SpeakerSelection.SelectedItem.ToString();
			if (initialized) Task.Run(Program.speechRecognizer.ReloadSpeaker);
		}

		private void MedalClipHotkeyDropdownOnSelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			SparkSettings.instance.medalClipKey = (int)medalTVInputs[((ComboBox)sender).SelectedIndex];
		}

		private void AddSparkSourcesOBS(object sender, RoutedEventArgs e)
		{
			Program.obs.AddSparkSources();
		}
	}
}