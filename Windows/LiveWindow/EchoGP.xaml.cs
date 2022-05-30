using System;
using System.Linq;
using System.Timers;
using System.Windows;
using System.Windows.Controls;

namespace Spark
{
	public partial class EchoGP
	{
		private readonly Timer outputUpdateTimer = new Timer();

		public EchoGP()
		{
			InitializeComponent();
		}

		private void OnControlLoaded(object sender, RoutedEventArgs e)
		{
			outputUpdateTimer.Interval = 150;
			outputUpdateTimer.Elapsed += Update;
			outputUpdateTimer.Enabled = true;
		}


		private void Update(object source, ElapsedEventArgs e)
		{
			if (Program.running)
			{
				Dispatcher.Invoke(() =>
				{
					if (Program.InGame && Program.lastFrame != null)
					{
						switch (Program.echoGPController.state)
						{
							case EchoGPController.State.NotReady:
								ActivateEchoGPSubtitle.Text = "Not Ready";
								break;
							case EchoGPController.State.NotInStartingArea:
								ActivateEchoGPSubtitle.Text = "Move to the starting area.";
								break;
							case EchoGPController.State.InStartingArea:
								ActivateEchoGPSubtitle.Text = "Ready to go!";
								break;
							case EchoGPController.State.Racing:
								ActivateEchoGPSubtitle.Text = "Racing!";
								break;
							default:
								throw new ArgumentOutOfRangeException();
						}
					}

					PreviousRaces.Text = Program.echoGPController.previousRaces
						.Select(r => $"{r.mapName} {r.finalTime:N2}")
						.Aggregate(string.Empty, (r1, r2) => r1 + "\n" + r2);

					SplitsText.Text = string.Join('\n', Program.echoGPController.splitTimes.Select(f => f.ToString("N2")));
				});
			}
		}

		private void ActivateEchoGP(object sender, RoutedEventArgs e)
		{
			Program.echoGPController.active = !Program.echoGPController.active;
			if (Program.echoGPController.active)
			{
				ActivateEchoGPTitle.Content = "Active";
			}
			else
			{
				ActivateEchoGPTitle.Content = "Not Active";
				Program.echoGPController.Cancel();
			}
		}
	}
}