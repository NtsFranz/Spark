using System.Collections.Generic;
using System.Numerics;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EchoVRAPI;

namespace Spark
{
	/// <summary>
	/// Interaction logic for Playspace.xaml
	/// </summary>
	public partial class Playspace : Window
	{
		private readonly Timer outputUpdateTimer = new();
		private Vector2 offset = Vector2.Zero;
		private Vector2 lastPosition = Vector2.Zero;
		private const int deltaMillis = 67; // about 15 fps

		public static readonly object playerDropdownLock = new object();

		public Playspace()
		{
			InitializeComponent();

			RefreshPlayers();
			choosePlayerDropdown.SelectedIndex = 0;

			outputUpdateTimer.Interval = deltaMillis;
			outputUpdateTimer.Elapsed += Update;
			outputUpdateTimer.Enabled = true;
		}


		private void Update(object source, ElapsedEventArgs e)
		{
			if (Program.running)
			{
				lock (playerDropdownLock)
				{
					Dispatcher.Invoke(() =>
					{
						if (Program.lastFrame != null && Program.lastFrame.game_status == "playing")
						{
							if (choosePlayerDropdown.SelectedIndex == 0)
							{
								Vector3 pos = Program.lastFrame.player.vr_position.ToVector3();
								SetPosition(pos.X, pos.Z);
							}
							else
							{
								string playerName = choosePlayerDropdown.SelectedValue.ToString();
								Player player = Program.lastFrame.GetPlayer(playerName);
								if (player == null) return;
								MatchPlayer playerData = Program.CurrentRound.GetPlayerData(player);
								if (playerData == null) return;
								Vector3 pos = player.head.Position - playerData.playspaceLocation;
								SetPosition(pos.X, pos.Y);
							}

							playerCircle.Visibility = Visibility.Visible;
						}
						else
						{
							SetPosition(0, 0);
							playerCircle.Visibility = Visibility.Hidden;
						}
					});
				}
			}
		}

		private void SetPosition(float x, float y)
		{
			SetPosition(new Vector2(x, y));
		}

		private void SetPosition(Vector2 pos)
		{
			// 1.7 m = 170 pixels
			if (double.IsNaN(pos.X)) return;
			const float radius = 16;
			const float canvasRadius = 200;
			const float scaleFactor = 150;

			Vector2 newPos = Vector2.Lerp(lastPosition, pos, .2f);
			playerCircle.Margin = new Thickness(
				scaleFactor * newPos.X - radius + canvasRadius,
				scaleFactor * newPos.Y - radius + canvasRadius,
				0, 0);
			lastPosition = newPos;
			rawLabel.Content = $"x: {pos.X:N2}, y: {pos.Y:N2}";
		}

		private void RefreshPlayers()
		{
			lock (playerDropdownLock)
			{
				int lastSelectedIndex = choosePlayerDropdown.SelectedIndex;
				choosePlayerDropdown.Items.Clear();
				choosePlayerDropdown.Items.Add("Local Player");
				if (Program.lastFrame == null) return;
				List<Player> players = Program.lastFrame.GetAllPlayers();
				foreach (Player p in players)
				{
					choosePlayerDropdown.Items.Add(p.name);
				}

				if (lastSelectedIndex <= players.Count)
				{
					choosePlayerDropdown.SelectedIndex = lastSelectedIndex;
				}
				else
				{
					choosePlayerDropdown.SelectedIndex = 0;
				}
			}
		}

		private void CloseButtonClicked(object sender, RoutedEventArgs e)
		{
			Close();
		}

		private void RefreshPlayerList(object sender, RoutedEventArgs e)
		{
			RefreshPlayers();
		}

		private void StreamerModeChecked(object sender, RoutedEventArgs e)
		{
			playspaceBackground.Background = ((CheckBox)sender).IsChecked == true ? Brushes.Green : (Brush)FindResource("ContainerBackground");
		}
	}
}