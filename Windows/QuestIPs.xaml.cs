using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using EchoVRAPI;
using Newtonsoft.Json;

namespace Spark
{
	public partial class QuestIPs : Window
	{
		public QuestIPs()
		{
			InitializeComponent();
			Task.Run(() =>
			{
				Dispatcher.Invoke(async () =>
				{
					await FindQuestIPs();
				});
			});
		}


		private void CloseButtonClicked(object sender, RoutedEventArgs e)
		{
			Close();
		}

		private async void RefreshClicked(object sender, RoutedEventArgs e)
		{
			await FindQuestIPs();
		}


		private async Task FindQuestIPs()
		{
			try
			{
				Stopwatch sw = Stopwatch.StartNew();
				Progress<List<IPAddress>> progress = new Progress<List<IPAddress>>(ips =>
				{
					QuestIPsBox.Text = string.Join('\n', ips.Select(ip => ip.ToString()));
					LoadingLabel.Content = "Scanning Network...";
				});
				// List<IPAddress> ips = await QuestIPFetching.FindAllQuestIPs(progress);

				List<IPAddress> ips = QuestIPFetching.GetPossibleLocalIPs();


				QuestIPsBox.Text = string.Join('\n', ips.Select(ip => ip.ToString()));
				Debug.WriteLine($"Adding additional ips: {sw.Elapsed.TotalSeconds}");
				sw.Restart();

				LoadingLabel.Content = "Fetching EchoVR API for each IP...";

				List<(IPAddress, string)> responses = await QuestIPFetching.PingEchoVRAPIAsync(ips);
				
				Debug.WriteLine($"Pinging ips: {sw.Elapsed.TotalSeconds}");
				sw.Restart();

				List<(IPAddress, Frame)> frames = responses.Select(r =>
				{
					if (r.Item2 != null)
					{
						// main menu
						if (r.Item2.StartsWith("<html>"))
						{
							return (r.Item1, new Frame { err_code = -10 });
						}
						else
						{
							return (r.Item1, Frame.FromJSON(DateTime.UtcNow, r.Item2, null));
						}
					}
					else
					{
						return (r.Item1, null);
					}
				}).Where(r => r.Item2 != null).ToList();

				string output = string.Join('\n', frames.Select(ip =>
				{
					(IPAddress ipAddress, Frame f) = ip;
					string status = "";
					if (f != null)
					{
						status = f.err_code switch
						{
							-2 => "API not enabled",
							-6 => "Lobby",
							-10 => "Loading screen",
							_ => $"{Program.CurrentSparkLink(f.sessionid)}\t{f.client_name}\t{f.game_status}\tPlayers: {f.GetAllPlayers().Count}"
						};
					}

					return $"{ipAddress}\t{status}";
				}));
				QuestIPsBox.Text = output;
				LoadingLabel.Content = "";
				
			}
			catch (Exception e)
			{
				Logger.LogRow(Logger.LogType.Error, $"Error fetching Quest IPs\n{e}");
			}
		}

	}
}