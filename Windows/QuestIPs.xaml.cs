using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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
			Task.Run(() => { Dispatcher.Invoke(async () => { await FindQuestIPs(); }); });
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
				Progress<List<IPAddress>> progress = new Progress<List<IPAddress>>(ips =>
				{
					QuestIPsBox.Text = string.Join('\n', ips.Select(ip => ip.ToString()));
					LoadingLabel.Content = "Scanning Network...";
				});
				List<IPAddress> ips = await QuestIPFetching.FindAllQuestIPs(progress);

				LoadingLabel.Content = "Fetching EchoVR API for each Quest...";

				List<(IPAddress, string)> responses = await QuestIPFetching.PingEchoVRAPIAsync(ips);

				List<(IPAddress, Frame)> frames = responses.Select(r =>
					r.Item2 != null && !r.Item2.StartsWith("<html>")
						? (r.Item1, JsonConvert.DeserializeObject<Frame>(r.Item2))
						: (r.Item1, null)).ToList();

				string output = string.Join('\n', frames.Select(ip =>
				{
					(IPAddress ipAddress, Frame f) = ip;
					string status = f != null ? f.err_code == -6 ? "Lobby" : Program.CurrentSparkLink(f?.sessionid) + "\t" + f?.game_status + "\t" + "Players: " + f?.GetAllPlayers().Count : "";
					return ipAddress + "\t" + f?.client_name + " \t" + status;
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