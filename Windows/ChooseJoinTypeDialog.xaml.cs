using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Newtonsoft.Json;
using Spark.Properties;

namespace Spark
{
	/// <summary>
	/// Interaction logic for ChooseJoinTypeDialog.xaml
	/// </summary>
	public partial class ChooseJoinTypeDialog
	{
		private readonly string sessionId;
		private bool? sessionDataFound;

		public ChooseJoinTypeDialog(string session_id)
		{
			InitializeComponent();

			sessionId = session_id;

			RefreshConnectionStatus();
		}

		private void RefreshConnectionStatus()
		{
			sessionDataFound = null;
			UpdateStatusLabel();
			_ = Task.Run(async () =>
			{
				string resp = null;
				try
				{
					FetchUtils.client.Timeout = TimeSpan.FromSeconds(2);
					HttpResponseMessage response = await FetchUtils.client.GetAsync($"http://{SparkSettings.instance.echoVRIP}:{SparkSettings.instance.echoVRPort}/session");
					resp = await response.Content.ReadAsStringAsync();
				}
				catch (Exception)
				{
					// ignored
				}

				if (!string.IsNullOrEmpty(resp) && resp.StartsWith("{"))
				{
					sessionDataFound = true;
					Dispatcher.Invoke(UpdateStatusLabel);
				}
				else
				{
					sessionDataFound = false;
					Dispatcher.Invoke(UpdateStatusLabel);
				}
			});
		}

		private void UpdateStatusLabel()
		{
			if (SparkSettings.instance.sparkLinkForceLaunchNewInstance)
			{
				EchoVRDetectedLabel.Text = "A new instance of Echo VR will open.";
				EchoVRDetectedLabel.Foreground = Brushes.Gray;
			}
			else
			{
				switch (sessionDataFound)
				{
					case true:
						EchoVRDetectedLabel.Text = "Echo VR detected. Your game will be switched to the new match.";
						EchoVRDetectedLabel.Foreground = new SolidColorBrush(Color.FromRgb(41, 135, 55));
						break;
					case false:
						EchoVRDetectedLabel.Text = "Echo VR not detected. A new instance of Echo VR will open on your PC.";
						EchoVRDetectedLabel.Foreground = new SolidColorBrush(Color.FromRgb(209, 82, 73));
						break;
					default:
						EchoVRDetectedLabel.Text = "Detecting Echo VR...";
						EchoVRDetectedLabel.Foreground = new SolidColorBrush(Color.FromRgb(196, 186, 77));
						break;
				}
			}
		}

		private async Task Join(int teamIndex = -1)
		{
			if (sessionDataFound == true)
			{
				bool success = await Program.APIJoin(sessionId, teamIndex);
				if (success)
				{
					Dispatcher.Invoke(() =>
					{
						SparkSettings.instance.Save();
						Close();
						Program.Quit();
					});
				}
				else
				{
					Dispatcher.Invoke(() =>
					{
						new MessageBox("Failed to send match id to the game. Try again or launch a new instance instead.", "Error", Program.Quit).Show();
					});
				}
			}
			else
			{
				if (string.IsNullOrEmpty(SparkSettings.instance.echoVRPath)) return;
				if (teamIndex == 2)
				{
					Program.StartEchoVR(Program.JoinType.Spectator, session_id: sessionId, noovr: SparkSettings.instance.sparkLinkNoOVR);
				}
				else
				{
					Program.StartEchoVR(Program.JoinType.Player, session_id: sessionId, teamIndex: teamIndex);
				}

				SparkSettings.instance.Save();
				// Close();
				Program.Quit();
			}
		}


		private void CloseButtonClicked(object sender, EventArgs e)
		{
			Program.Quit();
		}

		private void ForceLaunchChecked(object sender, RoutedEventArgs e)
		{
			UpdateStatusLabel();
		}

		private async void JoinBlueTeam(object sender, RoutedEventArgs e)
		{
			await Join(0);
		}

		private async void JoinRandomTeam(object sender, RoutedEventArgs e)
		{
			await Join();
		}

		private async void JoinOrangeTeam(object sender, RoutedEventArgs e)
		{
			await Join(1);
		}

		private async void JoinAsSpectatorClicked(object sender, RoutedEventArgs e)
		{
			await Join(2);
		}

		private void Hyperlink_OnClick(object sender, RoutedEventArgs e)
		{
			QProJoiner window = new QProJoiner(sessionId)
			{
				Owner = this
			};
			window.Show();
		}
	}
}