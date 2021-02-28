using IgniteBot.Properties;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace IgniteBot
{
	/// <summary>
	/// Interaction logic for AtlasLinks.xaml
	/// </summary>
	public partial class AtlasLinks : Window
	{
		private bool initialized = false;
		public bool OnlyCastersChecked { get; set; }

		private readonly System.Timers.Timer outputUpdateTimer = new System.Timers.Timer();

		public AtlasLinks()
		{
			InitializeComponent();


			alternateIPTextBox.Text = Settings.Default.echoVRIP;
			ipSourceDropdown.SelectedIndex = Settings.Default.echoVRIP == "127.0.0.1" ? 0 : 1;

			surroundWithAngleBracketsCheckbox.IsChecked = Settings.Default.atlasLinkUseAngleBrackets;
			linkTypeComboBox.SelectedIndex = Settings.Default.atlasLinkStyle;

			RefreshCurrentLink();


			GetAtlasMatches();


			outputUpdateTimer.Interval = 500;
			outputUpdateTimer.Elapsed += Update;
			outputUpdateTimer.Enabled = true;

			initialized = true;
		}

		// runs every 500 ms
		private void Update(object source, ElapsedEventArgs e)
		{
			if (Program.running)
			{
				Dispatcher.Invoke(() =>
				{
					hostMatchButton.IsEnabled = Program.lastFrame != null && Program.lastFrame.private_match == false;

					if (Program.lastFrame != null)
					{
						joinLink.Text = CurrentLink(Program.lastFrame.sessionid);
					}
				});
			}
		}

		private string CurrentLink(string sessionid)
		{
			string link = "";
			if (Settings.Default.atlasLinkUseAngleBrackets)
			{
				switch (Settings.Default.atlasLinkStyle)
				{
					case 0:
						link = "<ignitebot://choose/" + sessionid + ">";
						break;
					case 1:
						link = "<atlas://j/" + sessionid + ">";
						break;
					case 2:
						link = "<atlas://s/" + sessionid + ">";
						break;
				}
			}
			else
			{
				switch (Settings.Default.atlasLinkStyle)
				{
					case 0:
						link = "ignitebot://choose/" + sessionid;
						break;
					case 1:
						link = "atlas://j/" + sessionid;
						break;
					case 2:
						link = "atlas://s/" + sessionid;
						break;
				}
			}

			if (Settings.Default.atlasLinkAppendTeamNames)
			{
				if (Program.matchData != null &&
					Program.matchData.teams[g_Team.TeamColor.blue] != null &&
					Program.matchData.teams[g_Team.TeamColor.orange] != null &&
					!string.IsNullOrEmpty(Program.matchData.teams[g_Team.TeamColor.blue].vrmlTeamName) &&
					!string.IsNullOrEmpty(Program.matchData.teams[g_Team.TeamColor.orange].vrmlTeamName))
				{
					link += $" {Program.matchData.teams[g_Team.TeamColor.orange].vrmlTeamName} vs {Program.matchData.teams[g_Team.TeamColor.blue].vrmlTeamName}";
				}
			}

			return link;
		}

		public void GetLinks(object sender, RoutedEventArgs e)
		{
			string ip = alternateIPTextBox.Text;
			Task.Run(() => Program.GetAsync($"http://{ip}:6721/session", null, (responseJSON) =>
			{
				try
				{
					g_InstanceSimple obj = JsonConvert.DeserializeObject<g_InstanceSimple>(responseJSON);

					if (obj != null && !string.IsNullOrEmpty(obj.sessionid))
					{
						Dispatcher.Invoke(() =>
						{
							joinLink.Text = CurrentLink(obj.sessionid);

							Settings.Default.alternateEchoVRIP = alternateIPTextBox.Text;
							Settings.Default.Save();
						});
					}

				}
				catch (Exception e)
				{
					Logger.LogRow(Logger.LogType.Error, $"Can't parse response\n{e}");
				}
			}));
		}

		private void closeButton_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}

		private void HostMatchClicked(object sender, RoutedEventArgs e)
		{
			HostAtlasMatch();
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
			public string session_id;
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
			/// If this is true, users with the casteer login in the IgniteBot can see this match
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
			public string matchid;
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
					var values = new Dictionary<string, object>
					{
						{"session_id", session_id },
						{"blue_team_members", blue_team },
						{"orange_team_members", orange_team },
						{"is_protected", is_protected },
						{"server_location", server_location},
						{"server_score", server_score},
						{"username", username },
					};
					return values;
				}
				catch (Exception e)
				{
					Logger.LogRow(Logger.LogType.Error, "Can't serialize atlas match data.\n" + e.Message + "\n" + e.StackTrace);
					return new Dictionary<string, object>
					{
						{"none", 0 }
					};
				}
			}
		}

		public void UpdateUIWithAtlasMatches(IEnumerable<AtlasMatch> matches)
		{
			try
			{
				Dispatcher.Invoke(() =>
				{
					// remove all the old children
					MatchesBox.Children.RemoveRange(0, MatchesBox.Children.Count);

					foreach (AtlasMatch match in matches)
					{
						var content = new Grid();
						var header = new StackPanel
						{
							Orientation = Orientation.Horizontal,
							VerticalAlignment = VerticalAlignment.Top,
							HorizontalAlignment = HorizontalAlignment.Right,
							Margin = new Thickness(0, 0, 10, 0)
						};
						header.Children.Add(new Label
						{
							Content = match.is_protected ? "Casters Only" : "Public"
						});
						if (!match.is_protected || !Program.Personal)
						{
							var copyLinkButton = new Button
							{
								Content = "Copy Atlas Link",
								Margin = new Thickness(100, 0, 0, 0),
								Padding = new Thickness(10, 0, 10, 0),

							};
							copyLinkButton.Click += (s, e) =>
							{
								Clipboard.SetText(CurrentLink(match.session_id));
							};
							header.Children.Add(copyLinkButton);
							var joinButton = new Button
							{
								Content = "Join",
								Margin = new Thickness(20, 0, 0, 0),
								Padding = new Thickness(10, 0, 10, 0)
							};
							joinButton.Click += (s, e) =>
							{
								Process.Start(new ProcessStartInfo
								{
									FileName = "ignitebot://choose/" + match.session_id,
									UseShellExecute = true
								});
							};
							header.Children.Add(joinButton);
						}

						content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
						content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
						content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
						content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });

						content.ShowGridLines = true;

						var blueLogo = new Image
						{
							Width = 100,
							Height = 100
						};
						if (match.blue_team_info.team_logo != string.Empty)
						{
							blueLogo.Source = new BitmapImage(new Uri(match.blue_team_info.team_logo));
						}
						var blueLogoBox = new StackPanel
						{
							Orientation = Orientation.Vertical,
							Margin = new Thickness(10, 10, 10, 10)
						};
						blueLogoBox.SetValue(Grid.ColumnProperty, 0);
						blueLogoBox.Children.Add(blueLogo);
						blueLogoBox.Children.Add(new Label
						{
							Content = match.blue_team_info.team_name,
							HorizontalAlignment = HorizontalAlignment.Center

						});


						var orangeLogo = new Image
						{
							Width = 100,
							Height = 100
						};
						if (match.orange_team_info.team_logo != string.Empty)
						{
							orangeLogo.Source = new BitmapImage(new Uri(match.orange_team_info.team_logo));
						}
						var orangeLogoBox = new StackPanel
						{
							Orientation = Orientation.Vertical,
							Margin = new Thickness(10, 10, 10, 10)
						};
						orangeLogoBox.SetValue(Grid.ColumnProperty, 3);
						orangeLogoBox.Children.Add(orangeLogo);
						orangeLogoBox.Children.Add(new Label
						{
							Content = match.orange_team_info.team_name,
							HorizontalAlignment = HorizontalAlignment.Center
						});

						var bluePlayers = new TextBlock
						{
							Text = string.Join('\n', match.blue_team),
							Margin = new Thickness(10, 10, 10, 10),
							TextAlignment = TextAlignment.Right
						};
						bluePlayers.SetValue(Grid.ColumnProperty, 1);
						var orangePlayers = new TextBlock
						{
							Text = string.Join('\n', match.orange_team),
							Margin = new Thickness(10, 10, 10, 10)
						};
						orangePlayers.SetValue(Grid.ColumnProperty, 2);
						var sessionIdTextBox = new Label
						{
							Content = match.session_id
						};
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
				Logger.LogRow(Logger.LogType.Error, $"Error showing matches in UI\n{e}");
			}
		}

		public void HostAtlasMatch()
		{
			Program.atlasHostingThread = new Thread(AtlasHostingThread);
			Program.atlasHostingThread.IsBackground = true;
			Program.atlasHostingThread.Start();
		}

		private void AtlasHostingThread()
		{
			string matchesAPIURL = "https://ignitevr.gg/cgi-bin/EchoStats.cgi/host_atlas_match";

			// TODO show error message instead of just quitting
			if (Program.lastFrame == null || Program.lastFrame.teams == null) return;

			Program.hostedAtlasSessionId = Program.lastFrame.sessionid;

			AtlasMatch match = new AtlasMatch
			{
				session_id = Program.lastFrame.sessionid,
				blue_team = Program.lastFrame.teams[0].player_names.ToArray(),
				orange_team = Program.lastFrame.teams[1].player_names.ToArray(),
				is_protected = OnlyCastersChecked,
				visible_to_casters = OnlyCastersChecked,
				server_location = Program.matchData.ServerLocation,
				server_score = Program.matchData.ServerScore,
				username = Program.lastFrame.client_name
			};

			while (Program.running)
			{
				if (Program.lastFrame == null || Program.lastFrame.teams == null) return;

				// TODO take down the match
				if (Program.hostedAtlasSessionId != Program.lastFrame.sessionid) return;

				bool diff = 
					match.blue_team.Length != Program.lastFrame.teams[0].players.Count ||
					match.orange_team.Length != Program.lastFrame.teams[1].players.Count ||
					match.blue_points != Program.lastFrame.teams[0].stats.points ||
					match.orange_points != Program.lastFrame.teams[1].stats.points ||
					match.is_protected != OnlyCastersChecked ||
					match.visible_to_casters != OnlyCastersChecked;

				if (diff)
				{
					// actually update values
					match.blue_team = Program.lastFrame.teams[0].player_names.ToArray();
					match.orange_team = Program.lastFrame.teams[1].player_names.ToArray();
					match.blue_points = Program.lastFrame.teams[0].stats.points;
					match.orange_points = Program.lastFrame.teams[1].stats.points;
					match.is_protected = OnlyCastersChecked;
					match.visible_to_casters = OnlyCastersChecked;
					match.server_score = Program.matchData.ServerScore;
					match.username = Program.lastFrame.client_name;

					string data = JsonConvert.SerializeObject(match.ToDict());
					Task.Run(() => Program.PostAsync(matchesAPIURL, new Dictionary<string, string>() { { "x-api-key", DiscordOAuth.igniteUploadKey } }, data, (responseJSON) =>
					{
						GetAtlasMatches();
					}));
				}

				Thread.Sleep(100);
			}
		}

		public void GetAtlasMatches()
		{
			AtlasMatchResponse oldAtlasResponse = null;
			AtlasMatchResponse igniteAtlasResponse = null;

			bool oldAtlasDone = false;
			bool igniteAtlasDone = false;

			Task.Run(() => Program.PostAsync("https://echovrconnect.appspot.com/api/v1/player/" + Settings.Default.client_name, new Dictionary<string, string> { { "User-Agent", "Atlas/0.5.8" } }, "", (responseJSON) =>
			{
				try
				{
					oldAtlasResponse = JsonConvert.DeserializeObject<AtlasMatchResponse>(responseJSON);
					oldAtlasDone = true;
				}
				catch (Exception e)
				{
					oldAtlasDone = true;
					Logger.LogRow(Logger.LogType.Error, $"Can't parse Atlas matches response\n{e}");
				}
			}));


			string matchesAPIURL = "https://ignitevr.gg/cgi-bin/EchoStats.cgi/atlas_matches";
			Task.Run(() => Program.GetAsync(matchesAPIURL, new Dictionary<string, string>() { { "x-api-key", DiscordOAuth.igniteUploadKey } }, (responseJSON) =>
			{
				try
				{
					igniteAtlasResponse = JsonConvert.DeserializeObject<AtlasMatchResponse>(responseJSON);
					igniteAtlasDone = true;

				}
				catch (Exception e)
				{
					igniteAtlasDone = true;
					Logger.LogRow(Logger.LogType.Error, $"Can't parse Atlas matches response\n{e}");
				}
			}));

			// once both requests are done....
			Task.Run(() =>
			{
				// wait until both requests are done
				while (!oldAtlasDone || !igniteAtlasDone) Task.Delay(100);

				// if the old atlas request worked
				if (oldAtlasResponse != null && oldAtlasResponse.matches != null)
				{
					// if both worked, add the ignite matches to the old list
					if (igniteAtlasResponse != null && igniteAtlasResponse.matches != null)
					{
						oldAtlasResponse.matches.AddRange(igniteAtlasResponse.matches);
					}
					UpdateUIWithAtlasMatches(oldAtlasResponse.matches);
				}
				// if only the ignite atlas request worked
				else if (igniteAtlasResponse != null && igniteAtlasResponse.matches != null)
				{
					UpdateUIWithAtlasMatches(igniteAtlasResponse.matches);
				}
				// if none worked
				else
				{
					UpdateUIWithAtlasMatches(Array.Empty<AtlasMatch>());
				}
			});
		}

		private void RefreshMatchesClicked(object sender, RoutedEventArgs e)
		{
			GetAtlasMatches();
		}

		private void SurroundWithAngleBracketsChecked(object sender, RoutedEventArgs e)
		{
			if (!initialized) return;

			Settings.Default.atlasLinkUseAngleBrackets = ((CheckBox)sender).IsChecked == true;
			Settings.Default.Save();
			RefreshCurrentLink();
		}



		private void AppendTeamNamesChecked(object sender, RoutedEventArgs e)
		{
			if (!initialized) return;

			Settings.Default.atlasLinkAppendTeamNames = ((CheckBox)sender).IsChecked == true;
			Settings.Default.Save();
			RefreshCurrentLink();
		}

		private void RefreshCurrentLink()
		{
			if (Program.lastFrame != null)
			{
				joinLink.Text = CurrentLink(Program.lastFrame.sessionid);
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
				Logger.LogRow(Logger.LogType.Error, ex.ToString());
			}
		}

		private void IPSourceDropdownChanged(object sender, SelectionChangedEventArgs e)
		{
			// TODO
		}

		private void LinkTypeChanged(object sender, SelectionChangedEventArgs e)
		{
			if (!initialized) return;

			Settings.Default.atlasLinkStyle = ((ComboBox)sender).SelectedIndex;
			Settings.Default.Save();
			RefreshCurrentLink();
		}

		private async void FindQuestIP(object sender, RoutedEventArgs e)
		{
			findQuestStatusLabel.Content = "Searching for Quest on network";
			findQuestStatusLabel.Visibility = Visibility.Visible;
			alternateIPTextBox.IsEnabled = false;
			findQuest.IsEnabled = false;
			resetIP.IsEnabled = false;
			var progress = new Progress<string>(s => findQuestStatusLabel.Content = s);
			await Task.Factory.StartNew(() => Program.echoVRIP = Program.FindQuestIP(progress),
										TaskCreationOptions.None);
			alternateIPTextBox.IsEnabled = true;
			findQuest.IsEnabled = true;
			resetIP.IsEnabled = true;
			if (!Program.overrideEchoVRPort) Program.echoVRPort = 6721;
			alternateIPTextBox.Text = Program.echoVRIP;
			Settings.Default.echoVRIP = Program.echoVRIP;
			if (!Program.overrideEchoVRPort) Settings.Default.echoVRPort = Program.echoVRPort;
			Settings.Default.Save();
		}

		private void SetToLocalIP(object sender, RoutedEventArgs e)
		{
			Program.echoVRIP = "127.0.0.1";
			alternateIPTextBox.Text = Program.echoVRIP;
			Settings.Default.echoVRIP = Program.echoVRIP;
			Settings.Default.Save();
		}

		private void PublicToggled(object sender, RoutedEventArgs e)
		{
			if (!initialized) return;

			OnlyCastersChecked = ((CheckBox)sender).IsChecked == true;
		}

		private void EchoVRIPChanged(object sender, TextChangedEventArgs e)
		{
			if (!initialized) return;
			Program.echoVRIP = ((TextBox)sender).Text;
			Settings.Default.echoVRIP = Program.echoVRIP;
			Settings.Default.Save();
		}
	}
}
