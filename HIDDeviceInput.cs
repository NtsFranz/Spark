using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using HidSharp;

namespace Spark
{
	public class HIDDeviceInput
	{
		public struct Device
		{
			public string name;
			public int vendor;
			public int product;
		}

		public class ConnexionState
		{
			public bool leftClick;
			public bool rightClick;

			public Vector3 position;
			public Vector3 rotation;

			public override string ToString()
			{
				return $"{leftClick}\t{rightClick}\t{position.X:N2}\t{position.Y:N2}\t{position.Z:N2}\t{rotation.X:N2}\t{rotation.Y:N2}\t{rotation.Z:N2}";
			}
		}

		private List<Device> possibleDevices = new List<Device>();
		private HidDevice device;
		public Action<byte[]> OnChanged;
		public bool Running { get; private set; }

		public HIDDeviceInput(int vendor, int product)
		{
			possibleDevices.Add(new Device
			{
				vendor = vendor,
				product = product,
			});
		}

		public HIDDeviceInput(IEnumerable<Device> devices)
		{
			possibleDevices.AddRange(devices);
		}

		public void Start()
		{
			Running = true;
			Thread devicePollingThread = new Thread(InputThread);
			devicePollingThread.Start();
		}

		public void Stop()
		{
			Running = false;
		}

		private void InputThread()
		{
			IEnumerable<HidDevice> deviceList = DeviceList.Local.GetHidDevices();

			foreach (HidDevice d in deviceList)
			{
				Logger.Error($"Device: {d}");
				foreach (Device m in possibleDevices)
				{
					if (d.VendorID == m.vendor && d.ProductID == m.product)
					{
						Logger.Error($"Found device: {m}\t{d}");
						device = d;
					}
				}
			}

			if (device == null)
			{
				Logger.Error($"Didn't find device");
				return;
			}

			if (!device.TryOpen(out HidStream hidStream))
			{
				Logger.Error($"Couldn't open device stream.");
				return;
			}

			hidStream.ReadTimeout = Timeout.Infinite;

			using HidStream stream = hidStream;
			while (Running)
			{
				byte[] bytes = hidStream.Read();
				OnChanged?.Invoke(bytes);
			}
		}
	}
}