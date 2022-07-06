using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using EchoVRAPI;
using Newtonsoft.Json;
using Spark.Properties;

namespace Spark
{
	public class QuestIPFetching
	{
		private static Thread IPSearchthread1;
		private static Thread IPSearchthread2;

		// The max number of physical addresses.
		const int MAXLEN_PHYSADDR = 8;

		// Define the MIB_IPNETROW structure.
		[StructLayout(LayoutKind.Sequential)]
		struct MIB_IPNETROW
		{
			[MarshalAs(UnmanagedType.U4)] public int dwIndex;
			[MarshalAs(UnmanagedType.U4)] public int dwPhysAddrLen;
			[MarshalAs(UnmanagedType.U1)] public byte mac0;
			[MarshalAs(UnmanagedType.U1)] public byte mac1;
			[MarshalAs(UnmanagedType.U1)] public byte mac2;
			[MarshalAs(UnmanagedType.U1)] public byte mac3;
			[MarshalAs(UnmanagedType.U1)] public byte mac4;
			[MarshalAs(UnmanagedType.U1)] public byte mac5;
			[MarshalAs(UnmanagedType.U1)] public byte mac6;
			[MarshalAs(UnmanagedType.U1)] public byte mac7;
			[MarshalAs(UnmanagedType.U4)] public int dwAddr;
			[MarshalAs(UnmanagedType.U4)] public int dwType;
		}

		[DllImport("iphlpapi.dll", ExactSpelling = true)]
		public static extern int SendARP(int DestIP, int SrcIP, [Out] byte[] pMacAddr, ref int PhyAddrLen);

		public static IPAddress QuestIP = null;
		public static bool IPPingThread1Done = false;
		public static bool IPPingThread2Done = false;


		// Declare the GetIpNetTable function.
		[DllImport("IpHlpApi.dll")]
		[return: MarshalAs(UnmanagedType.U4)]
		static extern int GetIpNetTable(
			IntPtr pIpNetTable,
			[MarshalAs(UnmanagedType.U4)] ref int pdwSize,
			bool bOrder);

		[DllImport("IpHlpApi.dll", SetLastError = true, CharSet = CharSet.Auto)]
		internal static extern int FreeMibTable(IntPtr plpNetTable);

		// The insufficient buffer error.
		const int ERROR_INSUFFICIENT_BUFFER = 122;

		public static string GetLocalIP()
		{
			using Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
			socket.Connect("8.8.8.8", 65530);
			IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
			return endPoint != null ? endPoint.Address.ToString() : "";
		}

		
		public static List<IPAddress> GetLocalIPAddresses()
		{
			IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
			return host.AddressList.Where(ip => ip.AddressFamily == AddressFamily.InterNetwork).ToList();
		}
		
		
		public static List<IPAddress> GetPossibleLocalIPs()
		{
			List<IPAddress> myIps = GetLocalIPAddresses();
			List<IPAddress> ips = new List<IPAddress>();

			foreach (IPAddress ip in myIps)
			{
				for (byte i = 0; i < 255; i++)
				{
					List<byte> orig = ip.GetAddressBytes().SkipLast(1).ToList();
					orig.Add(i);
					ips.Add(new IPAddress(orig.ToArray()));
				}
			}

			return ips;
		}
		
		public static void GetCurrentIPAndPingNetwork()
		{
			foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces().Where(ni => ni.OperationalStatus == OperationalStatus.Up && (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 || ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet)))
			{
				GatewayIPAddressInformation? addr = adapter.GetIPProperties().GatewayAddresses.FirstOrDefault();
				if (addr != null && !addr.Address.ToString().Equals("0.0.0.0"))
				{
					foreach (UnicastIPAddressInformation unicastIPAddressInformation in adapter.GetIPProperties().UnicastAddresses)
					{
						if (unicastIPAddressInformation.Address.AddressFamily == AddressFamily.InterNetwork)
						{
							Console.WriteLine("PC IP Address: " + unicastIPAddressInformation.Address);
							Console.Write("PC Subnet Mask: " + unicastIPAddressInformation.IPv4Mask + "\n Searching for Quest on network...");
							PingNetworkIPs(unicastIPAddressInformation.Address, unicastIPAddressInformation.IPv4Mask);
						}
					}
				}
			}
		}

		public static async Task GetCurrentIPAndPingNetworkAsync()
		{
			foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces().Where(ni => ni.OperationalStatus == OperationalStatus.Up && (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 || ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet)))
			{
				GatewayIPAddressInformation? addr = adapter.GetIPProperties().GatewayAddresses.FirstOrDefault();
				if (addr != null && !addr.Address.ToString().Equals("0.0.0.0"))
				{
					foreach (UnicastIPAddressInformation unicastIPAddressInformation in adapter.GetIPProperties().UnicastAddresses)
					{
						if (unicastIPAddressInformation.Address.AddressFamily == AddressFamily.InterNetwork)
						{
							Console.WriteLine("PC IP Address: " + unicastIPAddressInformation.Address);
							Console.Write("PC Subnet Mask: " + unicastIPAddressInformation.IPv4Mask + "\n Searching for Quest on network...");

							uint ipAddress = BitConverter.ToUInt32(unicastIPAddressInformation.Address.GetAddressBytes(), 0);
							uint ipMaskV4 = BitConverter.ToUInt32(unicastIPAddressInformation.IPv4Mask.GetAddressBytes(), 0);
							uint broadCastIpAddress = ipAddress | ~ipMaskV4;

							IPAddress start = new IPAddress(BitConverter.GetBytes(broadCastIpAddress));

							byte leastSigByte = unicastIPAddressInformation.Address.GetAddressBytes().Last();
							int range = 255 - leastSigByte;

							List<IPAddress> pingReplyTasks = Enumerable.Range(0, range)
								.Select(x =>
								{
									byte[] bb = start.GetAddressBytes();
									bb[3] = (byte)x;
									IPAddress destIp = new IPAddress(bb);
									return destIp;
								})
								.ToList();
							IEnumerable<Task<PingReply>> tasks = pingReplyTasks.Select(ip => new Ping().SendPingAsync(ip, 4000));
							PingReply[] results = await Task.WhenAll(tasks);
						}
					}
				}
			}
		}

		public static async void PingIPList(List<IPAddress> IPs, int threadID)
		{
			IEnumerable<Task<PingReply>> tasks = IPs.Select(ip => new Ping().SendPingAsync(ip, 4000));
			PingReply[] results = await Task.WhenAll(tasks);
			switch (threadID)
			{
				case 1:
					IPPingThread1Done = true;
					break;
				case 2:
					IPPingThread2Done = true;
					break;
				default:
					break;
			}
		}

		public static void PingNetworkIPs(IPAddress address, IPAddress mask)
		{
			uint ipAddress = BitConverter.ToUInt32(address.GetAddressBytes(), 0);
			uint ipMaskV4 = BitConverter.ToUInt32(mask.GetAddressBytes(), 0);
			uint broadCastIpAddress = ipAddress | ~ipMaskV4;

			IPAddress start = new IPAddress(BitConverter.GetBytes(broadCastIpAddress));

			byte leastSigByte = address.GetAddressBytes().Last();
			int range = 255 - leastSigByte;

			List<IPAddress> pingReplyTasks = Enumerable.Range(leastSigByte, range)
				.Select(x =>
				{
					byte[] bb = start.GetAddressBytes();
					bb[3] = (byte)x;
					IPAddress destIp = new IPAddress(bb);
					return destIp;
				})
				.ToList();
			List<IPAddress> pingReplyTasks2 = Enumerable.Range(0, leastSigByte - 1)
				.Select(x =>
				{
					byte[] bb = start.GetAddressBytes();
					bb[3] = (byte)x;
					IPAddress destIp = new IPAddress(bb);
					return destIp;
				})
				.ToList();
			IPSearchthread1 = new Thread(() => PingIPList(pingReplyTasks, 1));
			IPSearchthread2 = new Thread(() => PingIPList(pingReplyTasks2, 2));
			IPPingThread1Done = false;
			IPPingThread2Done = false;
			IPSearchthread1.Start();
			IPSearchthread2.Start();
		}


		public static async Task PingNetworkIPsAsync(IPAddress address, IPAddress mask)
		{
		}


		public static void CheckARPTable()
		{
			int bytesNeeded = 0;

			// The result from the API call.
			int result = GetIpNetTable(IntPtr.Zero, ref bytesNeeded, false);

			// Call the function, expecting an insufficient buffer.
			if (result != ERROR_INSUFFICIENT_BUFFER)
			{
				// Throw an exception.
				throw new Exception();
			}

			// Allocate the memory, do it in a try/finally block, to ensure
			// that it is released.
			IntPtr buffer = IntPtr.Zero;
			// Allocate the memory.
			buffer = Marshal.AllocCoTaskMem(bytesNeeded);

			// Make the call again. If it did not succeed, then
			// raise an error.
			result = GetIpNetTable(buffer, ref bytesNeeded, false);

			// If the result is not 0 (no error), then throw an exception.
			if (result != 0)
			{
				// Throw an exception.
				throw new Exception();
			}

			// Now we have the buffer, we have to marshal it. We can read
			// the first 4 bytes to get the length of the buffer.
			int entries = Marshal.ReadInt32(buffer);

			// Increment the memory pointer by the size of the int.
			IntPtr currentBuffer = new IntPtr(buffer.ToInt64() +
			                                  Marshal.SizeOf(typeof(int)));

			// Allocate an array of entries.
			MIB_IPNETROW[] table = new MIB_IPNETROW[entries];

			// Cycle through the entries.
			for (int index = 0; index < entries; index++)
			{
				// Call PtrToStructure, getting the structure information.
				table[index] = (MIB_IPNETROW)Marshal.PtrToStructure(new IntPtr(currentBuffer.ToInt64() + (index * Marshal.SizeOf(typeof(MIB_IPNETROW)))), typeof(MIB_IPNETROW));
			}

			for (int index = 0; index < entries; index++)
			{
				MIB_IPNETROW row = table[index];

				if (row.mac0 == 0x2C && row.mac1 == 0x26 && row.mac2 == 0x17)
				{
					QuestIP = new IPAddress(BitConverter.GetBytes(row.dwAddr));
					break;
				}
			}

			// Release the memory.
			FreeMibTable(buffer);
		}

		public static async Task<List<IPAddress>> CheckARPTableAsync(bool onlyQuests = true)
		{
			int bytesNeeded = 0;

			// The result from the API call.
			int result = GetIpNetTable(IntPtr.Zero, ref bytesNeeded, false);

			// Call the function, expecting an insufficient buffer.
			if (result != ERROR_INSUFFICIENT_BUFFER)
			{
				// Throw an exception.
				throw new Exception();
			}

			// Allocate the memory, do it in a try/finally block, to ensure
			// that it is released.
			// Allocate the memory.
			IntPtr buffer = Marshal.AllocCoTaskMem(bytesNeeded);

			// Make the call again. If it did not succeed, then
			// raise an error.
			result = GetIpNetTable(buffer, ref bytesNeeded, false);

			// If the result is not 0 (no error), then throw an exception.
			if (result != 0)
			{
				// Throw an exception.
				throw new Exception();
			}

			// Now we have the buffer, we have to marshal it. We can read
			// the first 4 bytes to get the length of the buffer.
			int entries = Marshal.ReadInt32(buffer);

			// Increment the memory pointer by the size of the int.
			IntPtr currentBuffer = new IntPtr(buffer.ToInt64() + Marshal.SizeOf(typeof(int)));

			// Allocate an array of entries.
			MIB_IPNETROW[] table = new MIB_IPNETROW[entries];

			// Cycle through the entries.
			for (int index = 0; index < entries; index++)
			{
				// Call PtrToStructure, getting the structure information.
				table[index] = (MIB_IPNETROW)Marshal.PtrToStructure(new IntPtr(currentBuffer.ToInt64() + (index * Marshal.SizeOf(typeof(MIB_IPNETROW)))), typeof(MIB_IPNETROW));
			}

			List<IPAddress> ips = new List<IPAddress>();
			for (int index = 0; index < entries; index++)
			{
				MIB_IPNETROW row = table[index];

				if (!onlyQuests || (row.mac0 == 0x2C && row.mac1 == 0x26 && row.mac2 == 0x17))
				{
					IPAddress ip = new IPAddress(BitConverter.GetBytes(row.dwAddr));
					if (!ips.Contains(ip)) ips.Add(ip);
				}
			}

			// Release the memory.
			FreeMibTable(buffer);

			return ips;
		}


		/// <summary>
		/// https://stackoverflow.com/a/31492250
		/// </summary>
		private static Task<int> RunProcessAsync(Process process)
		{
			TaskCompletionSource<int> tcs = new TaskCompletionSource<int>();

			process.Exited += (s, ea) => tcs.SetResult(process.ExitCode);
			process.OutputDataReceived += (s, ea) => Console.WriteLine(ea.Data);
			process.ErrorDataReceived += (s, ea) => Console.WriteLine("ERR: " + ea.Data);

			bool started = process.Start();
			if (!started)
			{
				//you may allow for the process to be re-used (started = false) 
				//but I'm not sure about the guarantees of the Exited event in such a case
				throw new InvalidOperationException("Could not start process: " + process);
			}

			process.BeginOutputReadLine();
			process.BeginErrorReadLine();

			return tcs.Task;
		}


		public static void ClearARPCache()
		{
			try
			{
				Process process = new Process();
				ProcessStartInfo startInfo = new ProcessStartInfo
				{
					WindowStyle = ProcessWindowStyle.Hidden,
					FileName = "cmd.exe",
					Arguments = "/C netsh interface ip delete arpcache",
					Verb = "runas",
					UseShellExecute = true
				};
				process.StartInfo = startInfo;
				process.Start();
				process.WaitForExit(500);
				Thread.Sleep(20);
			}
			catch
			{
				// ignored
			}
		}


		public static async Task ClearARPCacheAsync()
		{
			try
			{
				Process process = new Process();
				ProcessStartInfo startInfo = new ProcessStartInfo
				{
					WindowStyle = ProcessWindowStyle.Hidden,
					FileName = "cmd.exe",
					Arguments = "/C netsh interface ip delete arpcache",
					Verb = "runas",
					UseShellExecute = true
				};
				process.StartInfo = startInfo;
				process.Start();
				await process.WaitForExitAsync();
			}
			catch
			{
				// ignored
			}
		}

		// /// <summary>
		// /// Waits asynchronously for the process to exit.
		// /// </summary>
		// /// <param name="process">The process to wait for cancellation.</param>
		// /// <param name="cancellationToken">A cancellation token. If invoked, the task will return 
		// /// immediately as canceled.</param>
		// /// <returns>A Task representing waiting for the process to end.</returns>
		// public static Task WaitForExitAsync(this Process process, 
		// 	CancellationToken cancellationToken = default(CancellationToken))
		// {
		// 	if (process.HasExited) return Task.CompletedTask;
		//
		// 	var tcs = new TaskCompletionSource<object>();
		// 	process.EnableRaisingEvents = true;
		// 	process.Exited += (sender, args) => tcs.TrySetResult(null);
		// 	if(cancellationToken != default(CancellationToken))
		// 		cancellationToken.Register(() => tcs.SetCanceled());
		//
		// 	return process.HasExited ? Task.CompletedTask : tcs.Task;
		// }

		
		public static async Task<List<(IPAddress, string)>> PingEchoVRAPIAsync(IReadOnlyCollection<IPAddress> ips, int maxConcurrency = 1000, IProgress<float> progress = null)
		{
			HttpClient client = new HttpClient();
			client.Timeout = TimeSpan.FromSeconds(2);

			int count = ips.Count;
			int finished = 0;
			
			SemaphoreSlim throttler = new SemaphoreSlim(initialCount: maxConcurrency);
			IEnumerable<Task<string>> tasks = ips.Select(async ip =>
			{
				// do an async wait until we can schedule again
				await throttler.WaitAsync();
				
				string s = null;
				try
				{
					HttpResponseMessage response = await client.GetAsync($"http://{ip}:6721/session");
					s = await response.Content.ReadAsStringAsync();
				}
				catch (Exception)
				{
					// ignored
				}

				finished += 1;
				progress?.Report((float)finished/count);

				throttler.Release();

				return s;
			});

			string[] results = await Task.WhenAll(tasks);

			return ips.Zip(results).ToList();
		}

		/// <summary>
		/// Finds a Quest local IP address on the same network
		/// </summary>
		/// <returns>The IP address</returns>
		public static string FindQuestIP(IProgress<string> progress)
		{
			try
			{
				string QuestStatusLabel = Resources.Searching_for_Quest_on_network;
				QuestIP = null;
				ClearARPCache();
				CheckARPTable();
				int count = 0;
				string statusDots = "";
				if (QuestIP == null)
				{
					GetCurrentIPAndPingNetwork();
					while (QuestIP == null && (!IPPingThread1Done || !IPPingThread2Done))
					{
						if (count % 16 == 0)
						{
							statusDots = "";
						}
						else if (count % 4 == 0)
						{
							statusDots += ".";
						}

						count++;
						progress.Report(QuestStatusLabel + statusDots);
						Thread.Sleep(50);
						CheckARPTable();
					}

					IPSearchthread1 = null;
					IPSearchthread2 = null;
					if (QuestIP != null)
					{
						progress.Report(Resources.QuestIPFetching_FindQuestIP_Found_Quest_on_network_);
					}
					else
					{
						Thread.Sleep(1000);
						CheckARPTable();
						if (QuestIP != null)
						{
							progress.Report(Resources.QuestIPFetching_FindQuestIP_Found_Quest_on_network_);
						}
						else
						{
							progress.Report(Resources.Failed_to_find_Quest_on_network_);
						}
					}
				}
				else
				{
					progress.Report(Resources.QuestIPFetching_FindQuestIP_Found_Quest_on_network_);
				}
			}
			finally
			{
			}

			Thread.Sleep(500);
			return QuestIP == null ? "127.0.0.1" : QuestIP.ToString();
			// TODO set Program.echoVRIP
		}

		public static async Task<List<IPAddress>> FindAllQuestIPs(IProgress<List<IPAddress>> progress)
		{
			// await ClearARPCacheAsync();
			List<IPAddress> ips = await CheckARPTableAsync();
			progress.Report(ips);
			await GetCurrentIPAndPingNetworkAsync();
			ips.AddRange(await CheckARPTableAsync());
			ips = ips.Distinct().ToList();
			progress.Report(ips);

			// IEnumerable<SimpleFrame> frames = results.Select(s=> s != null ? JsonConvert.DeserializeObject<SimpleFrame>(s) : null);

			return ips;
		}
	}
}