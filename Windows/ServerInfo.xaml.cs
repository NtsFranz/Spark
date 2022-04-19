using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using EchoVRAPI;
using Newtonsoft.Json.Linq;

namespace Spark
{
	public partial class ServerInfo : UserControl
	{
		private readonly Timer outputUpdateTimer = new Timer();

		public ServerInfo()
		{
			InitializeComponent();
		}

		private void OnControlLoaded(object sender, RoutedEventArgs e)
		{
			outputUpdateTimer.Interval = 150;
			outputUpdateTimer.Elapsed += Update;
			outputUpdateTimer.Enabled = true;
			
			Program.JoinedGame += frame =>
			{
				Dispatcher.Invoke(() =>
				{
					// ip stuff
					_ = GetServerLocations(frame.sessionip);
				});
			};
			
		}


		private void Update(object source, ElapsedEventArgs e)
		{
			if (Program.running)
			{
				Dispatcher.Invoke(() =>
				{
					if (Program.InGame && Program.lastFrame != null)
					{
						Dictionary<Team.TeamColor, StringBuilder> playerNames = new Dictionary<Team.TeamColor, StringBuilder>()
						{
							{ Team.TeamColor.blue , new StringBuilder()},
							{ Team.TeamColor.orange , new StringBuilder()},
						};
						Dictionary<Team.TeamColor, StringBuilder> playerPings = new Dictionary<Team.TeamColor, StringBuilder>()
						{
							{ Team.TeamColor.blue , new StringBuilder()},
							{ Team.TeamColor.orange , new StringBuilder()},
						};
						Program.lastFrame.GetAllPlayers().ForEach(p =>
						{
							playerNames[p.team_color].AppendLine(p.name);
							playerPings[p.team_color].AppendLine(p.ping.ToString());
						});
						
						bluePlayerPingsNamesServerInfoTab.Text = playerNames[Team.TeamColor.blue].ToString();
						bluePlayerPingsPingsServerInfoTab.Text = playerPings[Team.TeamColor.blue].ToString();
						orangePlayerPingsNamesServerInfoTab.Text = playerNames[Team.TeamColor.orange].ToString();
						orangePlayerPingsPingsServerInfoTab.Text = playerPings[Team.TeamColor.orange].ToString();

						string playerPingsHeader;

						if (Program.CurrentRound.serverScore > 0)
						{
							playerPingsHeader = $"{Properties.Resources.Player_Pings}   {Properties.Resources.Score_} {Program.CurrentRound.smoothedServerScore:N1}";
						}
						else if (Math.Abs(Program.CurrentRound.serverScore - (-1)) < .1f)
						{
							playerPingsHeader = $"{Properties.Resources.Player_Pings}     >150";
						}
						else
						{
							playerPingsHeader = $"{Properties.Resources.Player_Pings}   {Properties.Resources.Score_} --";
						}
						playerPingsGroupboxServerInfoTab.Header = playerPingsHeader;
						
					}
				});
			}
		}
		
		private async Task GetServerLocations(string ip)
		{
			if (ip != "")
			{
				try
				{
					HttpClient client = new HttpClient();
					List<Task<HttpResponseMessage>> tasks = new List<Task<HttpResponseMessage>>
					{
						client.GetAsync($"http://ip-api.com/json/{ip}"),
						// it's a free key, don't take mine
						client.GetAsync($"https://api.ipdata.co/{ip}?api-key=8c846028401a41778bfaba36d9e8cb4ae81ba56bfc24147454359b3c"), 
					};
					
					HttpResponseMessage[] results = await Task.WhenAll(tasks);
					JObject respObj1 = JObject.Parse(await results[0].Content.ReadAsStringAsync());
					JObject respObj2 = JObject.Parse(await results[1].Content.ReadAsStringAsync());
					
					FullServerLocationTextBox.Text = @$"IP:	{respObj1["query"]}

ip-api.com
City:	{respObj1["city"]}, {respObj1["regionName"]}, {respObj1["country"]}
Org:	{respObj1["org"]}
ISP:	{respObj1["isp"]}

ipdata.co
City:	{respObj2["city"] ?? "?"}, {respObj2["region"] ?? "?"}, {respObj2["country_name"]}
Org:	{respObj2["asn"]?["name"]}
Domain:	{respObj2["asn"]?["domain"]}";

				}
				catch (HttpRequestException)
				{
					Logger.LogRow(Logger.LogType.Error, "Couldn't get city of ip address.");
				}
			}
		}

		private void RefreshTraceroute(object sender, RoutedEventArgs e)
		{
			try
			{
				if (string.IsNullOrEmpty(Program.lastFrame?.sessionip)) return;

				Task.Run(() =>
				{
					Tracert(Program.lastFrame?.sessionip, OnProgressCallback: (entries, done) =>
					{
						Dispatcher.Invoke(() =>
						{
							TracerouteTextBox.Text = entries.Aggregate("ID\tPing\tIP\t\tHostname", (current, entry) => current + "\n" + entry);
							if (done)
							{
								TracerouteTextBox.Text += "\nDONE";
							}
						});
					});
				});
			}
			catch (Exception ex)
			{
				Logger.LogRow(Logger.LogType.Error, $"Traceroute failed.{ex}");
			}
		}

		public class TracertEntry
		{
			public int HopID;
			public string Address;
			public string Hostname;
			public long ReplyTime;
			public IPStatus ReplyStatus;

			public override string ToString()
			{
				return $"{HopID}\t{ReplyTime}\t{Address}\t{Hostname}";
			}
		}

		/// <summary>
		/// Traces the route which data have to travel through in order to reach an IP address.
		/// </summary>
		/// <param name="ipAddress">The IP address of the destination.</param>
		/// <param name="maxHops">Max hops to be returned.</param>
		public static IEnumerable<TracertEntry> Tracert(string ipAddress, int maxHops = 30, int timeout = 10000, Action<IEnumerable<TracertEntry>, bool> OnProgressCallback = null)
		{
			List<TracertEntry> entries = new List<TracertEntry>();

			// Ensure that the argument address is valid.
			if (!IPAddress.TryParse(ipAddress, out IPAddress address))
				throw new ArgumentException($"{ipAddress} is not a valid IP address.");

			// Max hops should be at least one or else there won't be any data to return.
			if (maxHops < 1)
				throw new ArgumentException("Max hops can't be lower than 1.");

			// Ensure that the timeout is not set to 0 or a negative number.
			if (timeout < 1) throw new ArgumentException("Timeout value must be higher than 0.");
			Ping ping = new Ping();
			PingOptions pingOptions = new PingOptions(1, true);
			Stopwatch pingReplyTime = new Stopwatch();
			PingReply reply;
			do
			{
				pingReplyTime.Start();
				reply = ping.Send(address, timeout, new byte[] { 0 }, pingOptions);
				pingReplyTime.Stop();
				string hostname = string.Empty;
				if (reply?.Address != null)
				{
					try
					{
						IPHostEntry ipHostInfo = Dns.GetHostEntry(reply.Address);
						hostname = ipHostInfo.HostName;
						//IPAddress ipA = ipHostInfo.AddressList.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
						//var ame = Dns.GetHostByAddress(reply.Address);    // Retrieve the hostname for the replied address.
					}
					catch (SocketException)
					{
						/* No host available for that address. */
					}
				}

				// Return out TracertEntry object with all the information about the hop.
				if (reply != null)
					entries.Add(new TracertEntry
					{
						HopID = pingOptions.Ttl,
						Address = reply.Address.ToString(),
						Hostname = hostname,
						ReplyTime = pingReplyTime.ElapsedMilliseconds,
						ReplyStatus = reply.Status
					});

				OnProgressCallback?.Invoke(entries, false);

				pingOptions.Ttl++;
				pingReplyTime.Reset();
			} while (reply != null && reply.Status != IPStatus.Success && pingOptions.Ttl <= maxHops);


			OnProgressCallback?.Invoke(entries, true);

			return entries;
		}
	}
}