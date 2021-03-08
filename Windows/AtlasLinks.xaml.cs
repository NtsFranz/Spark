using IgniteBot.Properties;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace IgniteBot
{
	/// <summary>
	/// Interaction logic for AtlasLinks.xaml
	/// </summary>
	public partial class AtlasLinks : Window
	{
		private readonly bool initialized;

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

			if (!string.IsNullOrEmpty(Program.hostedAtlasSessionId))
			{
				hostingMatchCheckbox.IsChecked = true;
				hostingMatchLabel.Content = "Stop Hosting";
			}


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
					hostMatchButton.IsEnabled = Program.lastFrame != null && Program.lastFrame.private_match == true;

					if (Program.lastFrame != null)
					{
						joinLink.Text = CurrentLink(Program.lastFrame.sessionid);
					}
				});
			}
		}

		private static string CurrentLink(string sessionid)
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

		private void GetLinks(object sender, RoutedEventArgs e)
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
				catch (Exception ex)
				{
					Logger.LogRow(Logger.LogType.Error, $"Can't parse response\n{ex}");
				}
			}));
		}

		//public int HostingVisibilityDropdown {
		//	get => Settings.Default.atlasHostingVisibility;
		//	set {
		//		Settings.Default.atlasHostingVisibility = value;
		//		Settings.Default.Save();
		//	}
		//}

		private void CloseButton_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}

		private void HostMatchClicked(object sender, RoutedEventArgs e)
		{
			if (string.IsNullOrEmpty(Program.hostedAtlasSessionId))
			{
				Program.atlasHostingThread = new Thread(AtlasHostingThread);
				Program.atlasHostingThread.IsBackground = true;
				Program.atlasHostingThread.Start();
				hostingMatchCheckbox.IsChecked = true;
				hostingMatchLabel.Content = "Stop Hosting";
			}
			else
			{
				Program.hostedAtlasSessionId = "";
				hostingMatchCheckbox.IsChecked = false;
				hostingMatchLabel.Content = "Host Match";
			}
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
			[Obsolete("Use matchid instead")]
			public string session_id;
			/// <summary>
			/// Session id. This could be empty if the match isn't available to join
			/// </summary>
			public string matchid;
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
			/// If this is true, users with the caster login in the IgniteBot can see this match
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
					Dictionary<string, object> values = new()
					{
						{ "matchid", matchid },
						{ "username", username },
						{ "blue_team", blue_team },
						{ "orange_team", orange_team },
						{ "is_protected", is_protected },
						{ "visible_to_casters", visible_to_casters },
						{ "server_location", server_location },
						{ "server_score", server_score },
						{ "private_match", private_match },
						{ "whitelist", whitelist },
						{ "blue_points", blue_points },
						{ "orange_points", orange_points },
						{ "slots", slots },
						{ "allow_spectators", allow_spectators },
						{ "game_status", game_status },
						{ "game_clock", game_clock },
					};
					return values;
				}
				catch (Exception e)
				{
					Logger.LogRow(Logger.LogType.Error, $"Can't serialize atlas match data.\n{e.Message}\n{e.StackTrace}");
					return new Dictionary<string, object>
					{
						{"none", 0}
					};
				}
			}
		}

		public class AtlasWhitelist
		{
			public class AtlasTeam
			{
				public string teamName;
				public List<string> players = new();

				public AtlasTeam(string teamName)
				{
					this.teamName = teamName;
				}
			}

			public List<AtlasTeam> teams = new();
			public List<string> players = new();

			public List<string> TeamNames => teams.Select(t => t.teamName).ToList();
			public List<string> AllPlayers {
				get {
					List<string> allPlayers = new List<string>(players);
					foreach (AtlasTeam team in teams)
					{
						allPlayers.AddRange(team.players);
					}

					return allPlayers;
				}
			}
		}

		private void UpdateUIWithAtlasMatches(IEnumerable<AtlasMatch> matches)
		{
			try
			{
				Dispatcher.Invoke(() =>
				{
					// remove all the old children
					MatchesBox.Children.RemoveRange(0, MatchesBox.Children.Count);

					foreach (AtlasMatch match in matches)
					{
						Grid content = new Grid();
						StackPanel header = new StackPanel
						{
							Orientation = Orientation.Horizontal,
							VerticalAlignment = VerticalAlignment.Top,
							HorizontalAlignment = HorizontalAlignment.Right,
							Margin = new Thickness(0, 0, 10, 0)
						};
						header.Children.Add(new Label
						{
							Content = match.is_protected ? (match.visible_to_casters ? "Casters Only" : "Private") : "Public"
						});

						byte buttonColor = 70;
						Button copyLinkButton = new Button
						{
							Content = "Copy Atlas Link",
							Margin = new Thickness(50, 0, 0, 0),
							Padding = new Thickness(10, 0, 10, 0),
							Background = new SolidColorBrush(Color.FromRgb(buttonColor, buttonColor, buttonColor)),
						};
						copyLinkButton.Click += (_, _) =>
						{
							Clipboard.SetText(CurrentLink(match.matchid));
						};
						header.Children.Add(copyLinkButton);
						Button joinButton = new Button
						{
							Content = "Join",
							Margin = new Thickness(20, 0, 0, 0),
							Padding = new Thickness(10, 0, 10, 0),
							Background = new SolidColorBrush(Color.FromRgb(buttonColor, buttonColor, buttonColor)),
						};
						joinButton.Click += (_, _) =>
						{
							Process.Start(new ProcessStartInfo
							{
								FileName = "ignitebot://choose/" + match.matchid,
								UseShellExecute = true
							});
						};
						header.Children.Add(joinButton);

						if (!string.IsNullOrEmpty(match.title) && match.title != "Default Lobby Name")
						{
							header.Children.Add(new Label
							{
								Content = match.title
							});
						} else if (!string.IsNullOrEmpty(match.server_location))
						{
							header.Children.Add(new Label
							{
								Content = match.server_location
							});
						}

						content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
						content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
						content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
						content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });

						content.ShowGridLines = true;

						Image blueLogo = new Image
						{
							Width = 100,
							Height = 100
						};
						if (match.blue_team_info?.team_logo != string.Empty)
						{
							blueLogo.Source = string.IsNullOrEmpty(match.blue_team_info?.team_logo) ? null : (new BitmapImage(new Uri(match.blue_team_info.team_logo)));
						}
						StackPanel blueLogoBox = new StackPanel
						{
							Orientation = Orientation.Vertical,
							Margin = new Thickness(5, 10, 5, 10)
						};
						blueLogoBox.SetValue(Grid.ColumnProperty, 0);
						blueLogoBox.Children.Add(blueLogo);
						blueLogoBox.Children.Add(new Label
						{
							Content = match.blue_team_info?.team_name,
							HorizontalAlignment = HorizontalAlignment.Center

						});


						Image orangeLogo = new Image
						{
							Width = 100,
							Height = 100
						};
						if (match.orange_team_info?.team_logo != string.Empty)
						{
							orangeLogo.Source = string.IsNullOrEmpty(match.orange_team_info?.team_logo) ? null : (new BitmapImage(new Uri(match.orange_team_info.team_logo)));
						}
						StackPanel orangeLogoBox = new StackPanel
						{
							Orientation = Orientation.Vertical,
							Margin = new Thickness(5, 10, 5, 10)
						};
						orangeLogoBox.SetValue(Grid.ColumnProperty, 3);
						orangeLogoBox.Children.Add(orangeLogo);
						orangeLogoBox.Children.Add(new Label
						{
							Content = match.orange_team_info?.team_name,
							HorizontalAlignment = HorizontalAlignment.Center
						});

						TextBlock bluePlayers = new TextBlock
						{
							Text = string.Join('\n', match.blue_team),
							Margin = new Thickness(10, 10, 10, 10),
							TextAlignment = TextAlignment.Right
						};
						bluePlayers.SetValue(Grid.ColumnProperty, 1);
						TextBlock orangePlayers = new TextBlock
						{
							Text = string.Join('\n', match.orange_team),
							Margin = new Thickness(10, 10, 10, 10)
						};
						orangePlayers.SetValue(Grid.ColumnProperty, 2);
						Label sessionIdTextBox = new Label
						{
							Content = match.matchid
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

		private void AtlasHostingThread()
		{
			const string hostURL = Program.APIURL + "host_atlas_match_v2";
			const string unhostURL = Program.APIURL + "unhost_atlas_match_v2";

			// TODO show error message instead of just quitting
			if (Program.lastFrame == null || Program.lastFrame.teams == null) return;

			Program.hostedAtlasSessionId = Program.lastFrame.sessionid;

			AtlasMatch match = new AtlasMatch
			{
				matchid = Program.lastFrame.sessionid,
				blue_team = Program.lastFrame.teams[0].player_names.ToArray(),
				orange_team = Program.lastFrame.teams[1].player_names.ToArray(),
				is_protected = (Settings.Default.atlasHostingVisibility > 0),
				visible_to_casters = (Settings.Default.atlasHostingVisibility == 1),
				server_location = Program.matchData.ServerLocation,
				server_score = Program.matchData.ServerScore,
				private_match = Program.lastFrame.private_match,
				username = Program.lastFrame.client_name,
				whitelist = Program.atlasWhitelist.AllPlayers.ToArray(),
			};
			bool firstHost = true;

			while (Program.running &&
				   Program.inGame &&
				   Program.lastFrame != null &&
				   Program.lastFrame.teams != null &&
				   Program.hostedAtlasSessionId == Program.lastFrame.sessionid)
			{
				bool diff =
					firstHost || 
					match.blue_team.Length != Program.lastFrame.teams[0].players.Count ||
					match.orange_team.Length != Program.lastFrame.teams[1].players.Count ||
					(Program.lastFrame.teams[0].stats != null && match.blue_points != Program.lastFrame.teams[0].stats.points) ||
					(Program.lastFrame.teams[1].stats != null && match.orange_points != Program.lastFrame.teams[1].stats.points) ||
					match.is_protected != (Settings.Default.atlasHostingVisibility > 0) ||
					match.visible_to_casters != (Settings.Default.atlasHostingVisibility == 1) ||
					match.whitelist.Length != Program.atlasWhitelist.AllPlayers.Count;

				if (diff)
				{
					// actually update values
					match.blue_team = Program.lastFrame.teams[0].player_names.ToArray();
					match.orange_team = Program.lastFrame.teams[1].player_names.ToArray();
					match.blue_points = Program.lastFrame.teams[0].stats != null ? Program.lastFrame.teams[0].stats.points : 0;
					match.orange_points = Program.lastFrame.teams[1].stats != null ? Program.lastFrame.teams[1].stats.points : 0;
					match.is_protected = (Settings.Default.atlasHostingVisibility > 0);
					match.visible_to_casters = (Settings.Default.atlasHostingVisibility == 1);
					match.server_score = Program.matchData.ServerScore;
					match.username = Program.lastFrame.client_name;
					match.whitelist = Program.atlasWhitelist.AllPlayers.ToArray();
					match.slots = Program.lastFrame.GetAllPlayers().Count;

					string data = JsonConvert.SerializeObject(match.ToDict());
					firstHost = false;

					// post new data, then fetch the updated list
					Task.Run(() => Program.PostAsync(hostURL, new Dictionary<string, string>() { { "x-api-key", DiscordOAuth.igniteUploadKey } }, data, (responseJSON) =>
					{
						GetAtlasMatches();
					}));
				}

				Thread.Sleep(100);
			}

			// post new data, then fetch the updated list
			string matchInfo = JsonConvert.SerializeObject(match.ToDict());
			Task.Run(() => Program.PostAsync(unhostURL, new Dictionary<string, string>() { { "x-api-key", DiscordOAuth.igniteUploadKey } }, matchInfo, (responseJSON) =>
			{
				Program.hostedAtlasSessionId = string.Empty;
				Dispatcher.Invoke(() =>
				{
					hostingMatchCheckbox.IsChecked = false;
					hostingMatchLabel.Content = "Host Match";
				});
				Thread.Sleep(10);
				GetAtlasMatches();
			}));
		}

		private void GetAtlasMatches()
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


			string matchesAPIURL = Program.APIURL + "atlas_matches_v2/" + Settings.Default.client_name;
			Task.Run(() => Program.GetAsync(
				matchesAPIURL,
				new Dictionary<string, string>() {
					{ "x-api-key", DiscordOAuth.igniteUploadKey },
					{ "access_code", DiscordOAuth.AccessCode }
				},
				(responseJSON) =>
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
				}
			 ));

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

		private void WhitelistButtonClicked(object sender, RoutedEventArgs e)
		{
			Program.ToggleWindow(typeof(AtlasWhitelistWindow), "Atlas Whitelist", this);
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

		private void EchoVRIPChanged(object sender, TextChangedEventArgs e)
		{
			if (!initialized) return;
			Program.echoVRIP = ((TextBox)sender).Text;
			Settings.Default.echoVRIP = Program.echoVRIP;
			Settings.Default.Save();
		}
	}
}
