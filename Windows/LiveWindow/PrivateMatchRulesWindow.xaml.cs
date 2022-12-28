using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using EchoVRAPI;
using Newtonsoft.Json;

namespace Spark
{
	class PrivateMatchRulePreset
	{
		public string description;
		public string last_updated;
		public PrivateMatchRules rules;
	}

	public partial class PrivateMatchRulesWindow
	{
		private readonly Timer outputUpdateTimer = new Timer();

		private readonly PrivateMatchRules rules = new PrivateMatchRules();

		private bool listenersActive;
		private bool changingRules;

		private PrivateMatchRules Rules
		{
			get => rules;
			set
			{
				// copies the values rather than replacing reference
				rules.minutes = value.minutes;
				rules.seconds = value.seconds;
				rules.blue_score = value.blue_score;
				rules.orange_score = value.orange_score;
				rules.disc_location = value.disc_location;
				rules.goal_stops_time = value.goal_stops_time;
				rules.respawn_time = value.respawn_time;
				rules.catapult_time = value.catapult_time;
				rules.round_count = value.round_count;
				rules.rounds_played = value.rounds_played;
				rules.round_wait_time = value.round_wait_time;
				rules.carry_points_over = value.carry_points_over;
				rules.blue_rounds_won = value.blue_rounds_won;
				rules.orange_rounds_won = value.orange_rounds_won;
				rules.overtime = value.overtime;
				rules.standard_chassis = value.standard_chassis;
				rules.mercy_enabled = value.mercy_enabled;
				rules.mercy_score_diff = value.mercy_score_diff;
				rules.team_only_voice = value.team_only_voice;
				rules.disc_curve = value.disc_curve;
				rules.self_goaling = value.self_goaling;
				rules.goalie_ping_adv = value.goalie_ping_adv;

				RefreshWindow();

				SettingsUpdated?.Invoke();
			}
		}

		public Action SettingsUpdated;
		private DateTime lastSetTime = DateTime.UtcNow;

		private Dictionary<string, PrivateMatchRulePreset> presets = new Dictionary<string, PrivateMatchRulePreset>()
		{
			{
				"Default", new PrivateMatchRulePreset()
				{
					last_updated = "2022-07-23",
					description = "",
					rules = new PrivateMatchRules()
					{
						minutes = 10,
						seconds = 0,
						blue_score = 0,
						orange_score = 0,
						disc_location = PrivateMatchRules.DiscLocation.mid,
						goal_stops_time = false,
						respawn_time = 3,
						catapult_time = 12,
						round_count = 3,
						rounds_played = PrivateMatchRules.RoundsPlayed.best_of,
						round_wait_time = 59,
						carry_points_over = false,
						blue_rounds_won = 0,
						orange_rounds_won = 0,
						overtime = PrivateMatchRules.Overtime.round_end,
						standard_chassis = true,
						mercy_enabled = true,
						mercy_score_diff = 20,
						team_only_voice = true,
						disc_curve = false,
						self_goaling = true,
						goalie_ping_adv = false
					}
				}
			}
		};

		#region Properties

		public int Minutes
		{
			get => Rules.minutes;
			set
			{
				Rules.minutes = value;
				SettingsUpdated?.Invoke();
			}
		}

		public int Seconds
		{
			get => Rules.seconds;
			set
			{
				Rules.seconds = value;
				SettingsUpdated?.Invoke();
			}
		}

		public int BlueScore
		{
			get => Rules.blue_score;
			set
			{
				Rules.blue_score = value;
				SettingsUpdated?.Invoke();
			}
		}

		public int OrangeScore
		{
			get => Rules.orange_score;
			set
			{
				Rules.orange_score = value;
				SettingsUpdated?.Invoke();
			}
		}

		public int DiscLocation
		{
			get => (int)Rules.disc_location;
			set
			{
				Rules.disc_location = (PrivateMatchRules.DiscLocation)value;
				SettingsUpdated?.Invoke();
			}
		}

		public bool GoalStopsTime
		{
			get => Rules.goal_stops_time;
			set
			{
				Rules.goal_stops_time = value;
				SettingsUpdated?.Invoke();
			}
		}

		public int RespawnTime
		{
			get => Rules.respawn_time;
			set
			{
				Rules.respawn_time = value;
				SettingsUpdated?.Invoke();
			}
		}

		public int CatapultTime
		{
			get => Rules.catapult_time;
			set
			{
				Rules.catapult_time = value;
				SettingsUpdated?.Invoke();
			}
		}

		public int RoundCount
		{
			get => Rules.round_count;
			set
			{
				Rules.round_count = value;
				SettingsUpdated?.Invoke();
			}
		}

		public int RoundsPlayed
		{
			get => (int)Rules.rounds_played;
			set
			{
				Rules.rounds_played = (PrivateMatchRules.RoundsPlayed)value;
				SettingsUpdated?.Invoke();
			}
		}

		public int RoundWaitTime
		{
			get => Rules.round_wait_time;
			set
			{
				Rules.round_wait_time = value;
				SettingsUpdated?.Invoke();
			}
		}

		public bool CarryPointsOver
		{
			get => Rules.carry_points_over;
			set
			{
				Rules.carry_points_over = value;
				SettingsUpdated?.Invoke();
			}
		}

		public int BlueRoundsWon
		{
			get => Rules.blue_rounds_won;
			set
			{
				Rules.blue_rounds_won = value;
				SettingsUpdated?.Invoke();
			}
		}

		public int OrangeRoundsWon
		{
			get => Rules.orange_rounds_won;
			set
			{
				Rules.orange_rounds_won = value;
				SettingsUpdated?.Invoke();
			}
		}

		public int Overtime
		{
			get => (int)Rules.overtime;
			set
			{
				Rules.overtime = (PrivateMatchRules.Overtime)value;
				SettingsUpdated?.Invoke();
			}
		}

		public bool StandardChassis
		{
			get => Rules.standard_chassis;
			set
			{
				Rules.standard_chassis = value;
				SettingsUpdated?.Invoke();
			}
		}

		public bool MercyEnabled
		{
			get => Rules.mercy_enabled;
			set
			{
				Rules.mercy_enabled = value;
				SettingsUpdated?.Invoke();
			}
		}

		public int MercyScoreDiff
		{
			get => Rules.mercy_score_diff;
			set
			{
				Rules.mercy_score_diff = value;
				SettingsUpdated?.Invoke();
			}
		}

		public bool TeamOnlyVoice
		{
			get => Rules.team_only_voice;
			set
			{
				Rules.team_only_voice = value;
				SettingsUpdated?.Invoke();
			}
		}

		public bool DiscCurve
		{
			get => Rules.disc_curve;
			set
			{
				Rules.disc_curve = value;
				SettingsUpdated?.Invoke();
			}
		}

		public bool SelfGoaling
		{
			get => Rules.self_goaling;
			set
			{
				Rules.self_goaling = value;
				SettingsUpdated?.Invoke();
			}
		}

		public bool GoaliePingAdv
		{
			get => Rules.goalie_ping_adv;
			set
			{
				Rules.goalie_ping_adv = value;
				SettingsUpdated?.Invoke();
			}
		}

		#endregion

		public PrivateMatchRulesWindow()
		{
			InitializeComponent();

			// add local presets
			foreach (KeyValuePair<string, PrivateMatchRulePreset> preset in presets)
			{
				PresetSelector.Items.Add(preset.Key);
			}

			// add presets from server
			FetchUtils.GetRequestCallback($"{Program.APIURL}/v2/private_match_rules", null, resp =>
			{
				Dictionary<string, PrivateMatchRulePreset> dict = JsonConvert.DeserializeObject<Dictionary<string, PrivateMatchRulePreset>>(resp);
				if (dict != null)
				{
					Dispatcher.Invoke(() =>
					{
						foreach (KeyValuePair<string, PrivateMatchRulePreset> preset in dict)
						{
							if (!presets.ContainsKey(preset.Key))
							{
								presets[preset.Key] = preset.Value;
								PresetSelector.Items.Add(preset.Key);
							}
						}
					});
				}
			});

			string[] blocks =
			{
				"MinutesBlock",
				"SecondsBlock",
				"OrangeScoreBlock",
				"BlueScoreBlock",
				"RespawnTimeBlock",
				"CatapultTimeBlock",
				"RoundCountBlock",
				"RoundWaitTimeBlock",
				"BlueRoundsWonBlock",
				"OrangeRoundsWonBlock",
				"MercyScoreDiffBlock"
			};

			foreach (string block in blocks)
			{
				StackPanel panel = FindName(block) as StackPanel;
				TextBox input = panel?.Children.OfType<TextBox>().FirstOrDefault();

				if (input != null)
				{
					Button left = panel.Children.OfType<Button>().FirstOrDefault();
					if (left != null)
					{
						left.Click += (_, _) =>
						{
							input.Focus();
							input.Text = int.TryParse(input.Text, out int val) ? Math.Clamp(val - 1, 0, 1000).ToString() : "0";
							left.Focus();
						};
					}

					Button right = panel.Children.OfType<Button>().LastOrDefault();
					if (right != null)
					{
						right.Click += (_, _) =>
						{
							input.Focus();
							input.Text = int.TryParse(input.Text, out int val) ? Math.Clamp(val + 1, 0, 1000).ToString() : "0";
							right.Focus();
						};
					}
				}
			}

			SettingsUpdated += OnSettingsUpdated;
			listenersActive = true;
		}

		private void OnSettingsUpdated()
		{
			if (!listenersActive) return;

			changingRules = true;

			lastSetTime = DateTime.UtcNow;

			// send POST request to update settings /set_rules
			try
			{
				// set the rules one at a time
				Task.Run(async () =>
				{
					string json = JsonConvert.SerializeObject(Rules);
					Dictionary<string, object> dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
					if (dict != null)
					{
						foreach (KeyValuePair<string, object> keyValue in dict)
						{
							string body = JsonConvert.SerializeObject(new Dictionary<string, object>()
							{
								{ keyValue.Key, keyValue.Value }
							});
							await FetchUtils.PostRequestAsync($"http://{Program.echoVRIP}:{Program.echoVRPort}/set_rules", null, body);
							lastSetTime = DateTime.UtcNow;
						}
					}
				});

				// set the rules all at once
				// string body = JsonConvert.SerializeObject(Rules);
				// FetchUtils.PostRequestCallback($"http://{Program.echoVRIP}:{Program.echoVRPort}/set_rules", null, body, null);
			}
			catch (Exception e)
			{
				Logger.LogRow(Logger.LogType.Error, $"Error setting private match rules to game.\n{e}");
			}

			listenersActive = false;
			MatchToPreset();
			listenersActive = true;
			changingRules = false;
		}

		private void MatchToPreset()
		{
			foreach (KeyValuePair<string, PrivateMatchRulePreset> preset in presets)
			{
				if (preset.Value.rules.Equals(Rules))
				{
					PresetSelector.SelectedValue = preset.Key;
					return;
				}
			}

			PresetSelector.SelectedIndex = 0;
		}

		private void GetSettingsFromGame()
		{
			try
			{
				// FetchUtils.GetRequestCallback($"{Program.APIURL}/get_rules", null, resp =>
				FetchUtils.GetRequestCallback($"http://{Program.echoVRIP}:{Program.echoVRPort}/get_rules", null, resp =>
				{
					PrivateMatchRules newRules = JsonConvert.DeserializeObject<PrivateMatchRules>(resp);
					if (newRules != null)
					{
						Dispatcher.Invoke(() =>
						{
							listenersActive = false;
							Rules = newRules;
							MatchToPreset();
							LastChangedBy.Text = Program.lastFrame?.rules_changed_by ?? "---";
							listenersActive = true;
						});
					}
				});
			}
			catch (Exception e)
			{
				Logger.LogRow(Logger.LogType.Error, $"Error sending private match rules to game.\n{e}");
				listenersActive = true;
			}
		}

		private void RefreshWindow()
		{
			DataContext = null;
			DataContext = this;
		}

		private void OnControlLoaded(object sender, RoutedEventArgs e)
		{
			outputUpdateTimer.Interval = 1000;
			outputUpdateTimer.Elapsed += Update;
			outputUpdateTimer.Enabled = true;
		}


		private void Update(object source, ElapsedEventArgs e)
		{
			if (Program.running)
			{
				Dispatcher.Invoke(() =>
				{
					if (Program.InGame && Program.lastFrame != null &&
					    (DateTime.UtcNow - lastSetTime).TotalSeconds > 1 && // if we didn't just change something locally
					    !changingRules &&
					    Equals(Program.liveWindow.tabControl.SelectedItem, Program.liveWindow.PrivateMatchRulesTab)
					   )
					{
						GetSettingsFromGame();
					}
				});
			}
		}

		private void LoadCustom(object sender, RoutedEventArgs e)
		{
			// Configure open file dialog box
			Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog
			{
				FileName = "", // Default file name
				DefaultExt = ".json", // Default file extension
				Filter = "JSON documents (.json)|*.json" // Filter files by extension
			};

			// Show open file dialog box
			bool? result = dlg.ShowDialog();

			// Process open file dialog box results
			if (result == true)
			{
				// Open document
				string filename = dlg.FileName;
				string text = File.ReadAllText(filename);
				PrivateMatchRules newRules = JsonConvert.DeserializeObject<PrivateMatchRules>(text);
				if (newRules != null)
				{
					Rules = newRules;
				}
			}
		}

		private void SaveCurrentRules(object sender, RoutedEventArgs e)
		{
			Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog
			{
				FileName = "private_match_rules", // Default file name
				DefaultExt = ".json", // Default file extension
				Filter = "JSON documents (.json)|*.json" // Filter files by extension
			};

			// Show save file dialog box
			bool? result = dlg.ShowDialog();

			// Process save file dialog box results
			if (result == true)
			{
				// Save document
				string filename = dlg.FileName;
				File.WriteAllText(filename, JsonConvert.SerializeObject(Rules, Formatting.Indented));
			}
		}

		private void PresetChanged(object sender, SelectionChangedEventArgs e)
		{
			if (PresetSelector.SelectedIndex == 0) return;

			string rulesetName = PresetSelector.SelectedValue.ToString() ?? string.Empty;
			if (presets.ContainsKey(rulesetName))
			{
				Rules = presets[rulesetName].rules;
			}
		}
	}
}