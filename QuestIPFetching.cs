using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
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
			[MarshalAs(UnmanagedType.U4)]
			public int dwIndex;
			[MarshalAs(UnmanagedType.U4)]
			public int dwPhysAddrLen;
			[MarshalAs(UnmanagedType.U1)]
			public byte mac0;
			[MarshalAs(UnmanagedType.U1)]
			public byte mac1;
			[MarshalAs(UnmanagedType.U1)]
			public byte mac2;
			[MarshalAs(UnmanagedType.U1)]
			public byte mac3;
			[MarshalAs(UnmanagedType.U1)]
			public byte mac4;
			[MarshalAs(UnmanagedType.U1)]
			public byte mac5;
			[MarshalAs(UnmanagedType.U1)]
			public byte mac6;
			[MarshalAs(UnmanagedType.U1)]
			public byte mac7;
			[MarshalAs(UnmanagedType.U4)]
			public int dwAddr;
			[MarshalAs(UnmanagedType.U4)]
			public int dwType;
		}
		[DllImport("iphlpapi.dll", ExactSpelling = true)]
		public static extern int SendARP(int DestIP, int SrcIP, [Out] byte[] pMacAddr, ref int PhyAddrLen);

		public static IPAddress QuestIP = null;
		public static bool IPPingThread1Done = false;
		public static bool IPPingThread2Done = false;

		public static void GetCurrentIPAndPingNetwork()
		{
			foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces().Where(ni => ni.OperationalStatus == OperationalStatus.Up && (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 || ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet)))
			{
				var addr = adapter.GetIPProperties().GatewayAddresses.FirstOrDefault();
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
		
		public static async void PingIPList(List<IPAddress> IPs, int threadID)
		{
			var tasks = IPs.Select(ip => new Ping().SendPingAsync(ip, 4000));
			var results = await Task.WhenAll(tasks);
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

			var bytes = start.GetAddressBytes();
			var leastSigByte = address.GetAddressBytes().Last();
			var range = 255 - leastSigByte;

			var pingReplyTasks = Enumerable.Range(leastSigByte, range)
				.Select(x =>
				{
					var bb = start.GetAddressBytes();
					bb[3] = (byte)x;
					var destIp = new IPAddress(bb);
					return destIp;
				})
				.ToList();
			var pingReplyTasks2 = Enumerable.Range(0, leastSigByte - 1)
				.Select(x =>
				{

					var bb = start.GetAddressBytes();
					bb[3] = (byte)x;
					var destIp = new IPAddress(bb);
					return destIp;
				})
				.ToList();
			IPSearchthread1 = new Thread(new ThreadStart(() => PingIPList(pingReplyTasks, 1)));
			IPSearchthread2 = new Thread(new ThreadStart(() => PingIPList(pingReplyTasks2, 2)));
			IPPingThread1Done = false;
			IPPingThread2Done = false;
			IPSearchthread1.Start();
			IPSearchthread2.Start();
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
			buffer = IntPtr.Zero;
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
				table[index] = (MIB_IPNETROW)Marshal.PtrToStructure(new
					IntPtr(currentBuffer.ToInt64() + (index *
					Marshal.SizeOf(typeof(MIB_IPNETROW)))), typeof(MIB_IPNETROW));
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
		}
		
		public static async Task<List<IPAddress>> CheckARPTableAsync()
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
			buffer = IntPtr.Zero;
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

				if (row.mac0 == 0x2C && row.mac1 == 0x26 && row.mac2 == 0x17)
				{
					ips.Add(new IPAddress(BitConverter.GetBytes(row.dwAddr)));
				}

			}

			return ips;
		}
		
		// Declare the GetIpNetTable function.
		[DllImport("IpHlpApi.dll")]
		[return: MarshalAs(UnmanagedType.U4)]
		static extern int GetIpNetTable(
			IntPtr pIpNetTable,
			[MarshalAs(UnmanagedType.U4)]
			ref int pdwSize,
			bool bOrder);

		[DllImport("IpHlpApi.dll", SetLastError = true, CharSet = CharSet.Auto)]
		internal static extern int FreeMibTable(IntPtr plpNetTable);

		// The insufficient buffer error.
		const int ERROR_INSUFFICIENT_BUFFER = 122;
		static IntPtr buffer;
		
		
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
						progress.Report("Found Quest on network!");
					}
					else
					{
						Thread.Sleep(1000);
						CheckARPTable();
						if (QuestIP != null)
						{
							progress.Report("Found Quest on network!");
						}
						else
						{
							progress.Report("Failed to find Quest on network!");
						}
					}
				}
				else
				{
					progress.Report("Found Quest on network!");
				}

			}
			finally
			{
				// Release the memory.
				FreeMibTable(buffer);
			}
			Thread.Sleep(500);
			return QuestIP == null ? "127.0.0.1" : QuestIP.ToString();
			// TODO set Program.echoVRIP
		}

		public static async Task<List<IPAddress>> FindAllQuestIPs()
		{
			// await ClearARPCacheAsync();
			List<IPAddress> ips = await CheckARPTableAsync();

			return ips;
		}
	}
}