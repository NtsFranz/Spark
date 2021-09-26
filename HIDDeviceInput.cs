using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading;
using HidSharp;

namespace Spark
{

	public class HIDDeviceInput
	{
		private int vendor = 0x46d;
		private int product = 0xc626;

		private HidDevice device;
		public Action<byte[]> OnChanged;
		public bool Running { get; private set; }

		public HIDDeviceInput(int vendor, int product)
		{
			this.vendor = vendor;
			this.product = product;
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
			IEnumerable<HidDevice> list = DeviceList.Local.GetHidDevices(vendor, product);
			device = list.FirstOrDefault();

			if (device == null) return;
			if (device.TryOpen(out HidStream hidStream))
			{
				hidStream.ReadTimeout = Timeout.Infinite;

				using (hidStream)
				{
					while (Running)
					{
						byte[] bytes = hidStream.Read();
						OnChanged?.Invoke(bytes);
					}
				}
			}
		}
	}
}