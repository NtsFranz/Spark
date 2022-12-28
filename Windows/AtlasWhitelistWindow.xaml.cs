using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Newtonsoft.Json.Linq;
using Button = System.Windows.Controls.Button;
using Label = System.Windows.Controls.Label;
using Orientation = System.Windows.Controls.Orientation;

namespace Spark
{
	/// <summary>
	/// Interaction logic for AtlasWhitelistWindow.xaml
	/// </summary>
	public partial class AtlasWhitelistWindow
	{
		public AtlasWhitelistWindow()
		{
			InitializeComponent();

			RefreshWhitelistUI();
		}

		private void CloseButtonEvent(object sender, RoutedEventArgs e)
		{
			SparkSettings.instance.Save();
			Close();
		}

		private void AddPlayerKeyPress(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Enter)
			{
				AddPlayerClicked(null,null);
			}
		}
		
		private void AddTeamKeyPress(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Enter)
			{
				AddTeamClicked(null,null);
			}
		}

		private void AddTeamClicked(object sender, RoutedEventArgs e)
		{
			string teamName = teamNameInput.Text;
			// if there was no name in the box
			if (string.IsNullOrEmpty(teamName)) return;

			teamNameInput.Text = string.Empty;

			// if the team name was already added
			if (Program.atlasWhitelist.TeamNames.Contains(teamName)) return;

			LiveWindow.AtlasWhitelist.AtlasTeam team = new(teamName);
			Program.atlasWhitelist.teams.Add(team);

			FetchUtils.GetRequestCallback(
				$"{Program.APIURL}/vrml/get_players_on_team/" + teamName,
				new Dictionary<string, string>(),
				(response) =>
				{
					try
					{
						JObject data = JObject.Parse(response);
						List<Dictionary<string, string>> players = data["players"]?.ToObject<List<Dictionary<string, string>>>();
						if (players == null) return;
						
						foreach (Dictionary<string, string> player in players)
						{
							team.players.Add(player["player_name"]);
						}

						Dispatcher.Invoke(RefreshWhitelistUI);
					}
					catch (Exception ex)
					{
						Logger.LogRow(Logger.LogType.Error, $"Error getting player list from team\n{ex}");
					}
				});

			RefreshWhitelistUI();
		}

		private void AddPlayerClicked(object sender, RoutedEventArgs e)
		{
			string playerName = playerNameInput.Text;
			// if there was no name in the box
			if (string.IsNullOrEmpty(playerName)) return;

			playerNameInput.Text = string.Empty;

			// if the player is already in the whitelist (maybe in a team)
			// TODO add a warning message
			if (Program.atlasWhitelist.players.Contains(playerName)) return;
			Program.atlasWhitelist.players.Add(playerName);


			// Add the player to the list in the GUI
			RefreshWhitelistUI();
		}

		/// <summary>
		/// Updates the UI from Program.atlasWhitelist
		/// </summary>
		private void RefreshWhitelistUI()
		{
			// player list
			playerList.Children.Clear();
			foreach (string playerName in Program.atlasWhitelist.players)
			{
				StackPanel row = new()
				{
					Orientation = Orientation.Horizontal
				};
				Label name = new()
				{
					Content = playerName
				};
				Button remove = new()
				{
					Content = "X",
					Width = 20,
					Height = 20,
					FontSize = 12,
				};
				remove.Click += (_, _) => { RemovePlayer(playerName); };
				row.Children.Add(name);
				row.Children.Add(remove);
				playerList.Children.Add(row);
			}


			// team list
			teamList.Children.Clear();
			foreach (LiveWindow.AtlasWhitelist.AtlasTeam team in Program.atlasWhitelist.teams)
			{
				StackPanel row = new()
				{
					Orientation = Orientation.Horizontal
				};
				Label name = new()
				{
					Content = team.teamName
				};
				Button remove = new()
				{
					Content = "X",
					Width = 20,
					Height = 20,
					FontSize = 12,
				};
				remove.Click += (_, _) => { RemoveTeam(team.teamName); };
				row.Children.Add(name);
				row.Children.Add(remove);
				teamList.Children.Add(row);

				foreach (string teamPlayer in team.players)
				{
					StackPanel playerRow = new()
					{
						Orientation = Orientation.Horizontal
					};
					Label playerName = new()
					{
						Content = teamPlayer,
						Margin = new Thickness(10, -8, 0, 0)
					};
					playerRow.Children.Add(playerName);
					teamList.Children.Add(playerRow);
				}
			}
		}

		/// <summary>
		/// Removes the player from the whitelist. The UI elements have to be removed separately.
		/// </summary>
		/// <param name="playerName"></param>
		private void RemovePlayer(string playerName)
		{
			Program.atlasWhitelist.players.Remove(playerName);
			RefreshWhitelistUI();
		}

		/// <summary>
		/// Removes the team from the whitelist. The UI elements have to be removed separately.
		/// </summary>
		/// <param name="teamName">The name of the team</param>
		private void RemoveTeam(string teamName)
		{
			Program.atlasWhitelist.teams.RemoveAll(t => t.teamName == teamName);
			RefreshWhitelistUI();
		}
	}
}