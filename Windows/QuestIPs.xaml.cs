using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Frame = EchoVRAPI.Frame;

namespace Spark
{
	public partial class QuestIPs : Window
	{
		public Dictionary<string, QuestIPLine> data = new Dictionary<string, QuestIPLine>();

		public QuestIPs()
		{
			InitializeComponent();
			Task.Run(() => { Dispatcher.Invoke(async () => { await FindQuestIPs(); }); });
		}


		[Serializable]
		public class QuestIPLine
		{
			public IPAddress ip;
			public string mac;
			public Frame frame;

			public UIElement GetRow()
			{
				string status = "";
				if (frame != null)
				{
					status = frame.err_code switch
					{
						-2 => $"API not enabled",
						-6 => $"Lobby\t(last client name: {Program.localDatabase.GetClientNamesFromMacAddress(mac)?.LastOrDefault() ?? "---"})",
						-10 => $"Loading screen\t(last client name: {Program.localDatabase.GetClientNamesFromMacAddress(mac)?.LastOrDefault() ?? "---"})",
						_ => $"{frame.client_name} \t{frame.game_status}\t Players: {frame.GetAllPlayers().Count}"
					};
				}

				StackPanel panel = new StackPanel()
				{
					Orientation = Orientation.Horizontal,
					Children =
					{
						new Label()
						{
							Content = $"{ip}\t{status}"
						}
					},
					Margin = new Thickness(0, 0, 0, 2),
				};

				if (!string.IsNullOrEmpty(frame?.sessionid))
				{
					Button button = new Button
					{
						Content = "Copy Link",
						Padding = new Thickness(4, 0, 4, 0),
						Margin = new Thickness(4, 0, 4, 0),
					};
					button.Click += (_, _) => { Clipboard.SetText(Program.CurrentSparkLink(frame.sessionid)); };
					panel.Children.Add(button);
				}

				// send player to match
				if ((frame?.err_code == -6 || frame?.sessionid != null) && Program.InGame && ip.ToString() != SparkSettings.instance.echoVRIP)
				{
					Button orangeButton = new Button
					{
						Content = "Send Player To My Match",
						Padding = new Thickness(4, 0, 4, 0),
						Margin = new Thickness(4, 0, 4, 0),
						Background = new SolidColorBrush(Color.FromArgb(0xff, 0x8c, 0x5c, 0x2a))
					};
					orangeButton.Click += async (_, _) => { await Program.APIJoin(Program.lastFrame.sessionid, 1, overrideIP: ip.ToString()); };
					panel.Children.Add(orangeButton);

					Button blueButton = new Button
					{
						Content = "Send Player To My Match",
						Padding = new Thickness(4, 0, 4, 0),
						Margin = new Thickness(4, 0, 4, 0),
						Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x31, 0x50, 0x7a))
					};
					blueButton.Click += async (_, _) => { await Program.APIJoin(Program.lastFrame.sessionid, 0, overrideIP: ip.ToString()); };
					panel.Children.Add(blueButton);
				}

				return panel;
			}
		}


		private void CloseButtonClicked(object sender, RoutedEventArgs e)
		{
			Close();
		}

		private async void RefreshClicked(object sender, RoutedEventArgs e)
		{
			await RefreshExistingIPs();
		}

		private async void FindIPsClicked(object sender, RoutedEventArgs e)
		{
			await FindQuestIPs();
		}

		private async Task FindQuestIPs()
		{
			try
			{
				data.Clear();
				RefreshChildren();


				LoadingLabel.Content = "Scanning Network...";

				// iterate through all the subnet ips
				List<IPAddress> ips = QuestIPFetching.GetPossibleLocalIPs();

				// add the ones we found in the arp table
				LoadingLabel.Content = "Adding IPs from ARP Table...";
				Dictionary<string, string> pairs = GetAllMacAddressesAndIpPairs();
				foreach (KeyValuePair<string, string> kvp in pairs)
				{
					if (ips.Find(ip => ip.ToString() == kvp.Key) == null)
					{
						ips.Add(IPAddress.Parse(kvp.Key));
					}
				}

				ips.Insert(0, IPAddress.Parse("127.0.0.1"));

				// create UI from the IP list
				ips.ForEach(ip =>
				{
					data.Add(ip.ToString(), new QuestIPLine()
					{
						ip = ip
					});
				});

				RefreshChildren();

				await RefreshExistingIPs();
			}
			catch (Exception e)
			{
				LoadingLabel.Content = "Error";
				Logger.LogRow(Logger.LogType.Error, $"Error fetching Quest IPs\n{e}");
			}
		}

		private async Task RefreshExistingIPs()
		{
			LoadingLabel.Content = "Fetching EchoVR API for each IP...";
			List<IPAddress> ips = data.Select(l => l.Value.ip).ToList();
			List<(IPAddress, string)> responses = await QuestIPFetching.PingEchoVRAPIAsync(ips);

			// check the game state for ips with frames
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

			Dictionary<string, string> pairs = GetAllMacAddressesAndIpPairs();

			// add the addresses to the database
			frames.ForEach(f =>
			{
				Program.localDatabase.AddQuestIP(
					f.Item1.ToString(),
					pairs.ContainsKey(f.Item1.ToString()) ? pairs[f.Item1.ToString()] : null,
					f.Item2?.client_name
				);
			});


			data.Clear();
			frames.ForEach(f =>
			{
				data.Add(f.Item1.ToString(), new QuestIPLine()
				{
					ip = f.Item1,
					frame = f.Item2,
					mac = pairs.ContainsKey(f.Item1.ToString()) ? pairs[f.Item1.ToString()] : null,
				});
			});

			RefreshChildren();
			LoadingLabel.Content = "";
		}

		private void RefreshChildren()
		{
			ListBox.Children.Clear();
			foreach (KeyValuePair<string, QuestIPLine> kvp in data)
			{
				ListBox.Children.Add(kvp.Value.GetRow());
			}
		}

		/// <summary>
		/// https://stackoverflow.com/a/19244196
		/// </summary>
		/// <returns>IP, MacAddress</returns>
		public Dictionary<string, string> GetAllMacAddressesAndIpPairs()
		{
			Dictionary<string, string> mip = new Dictionary<string, string>();
			Process pProcess = new Process();
			pProcess.StartInfo.FileName = "arp";
			pProcess.StartInfo.Arguments = "-a ";
			pProcess.StartInfo.UseShellExecute = false;
			pProcess.StartInfo.RedirectStandardOutput = true;
			pProcess.StartInfo.CreateNoWindow = true;
			pProcess.Start();
			string cmdOutput = pProcess.StandardOutput.ReadToEnd();
			const string pattern = @"(?<ip>([0-9]{1,3}\.?){4})\s*(?<mac>([a-f0-9]{2}-?){6})";

			foreach (Match m in Regex.Matches(cmdOutput, pattern, RegexOptions.IgnoreCase))
			{
				if (!mip.ContainsKey(m.Groups["ip"].Value))
				{
					mip.Add(m.Groups["ip"].Value, m.Groups["mac"].Value);
				}
				else
				{
					Console.WriteLine($"Duplicate IP: {m.Groups["ip"].Value}");
				}
			}

			return mip;
		}

	}
}