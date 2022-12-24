using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using HidSharp;

namespace Spark
{
	public class ConnexionState
	{
		public bool leftClick;
		public bool rightClick;

		public Vector3 position;

		public Vector3 rotation;
		//public float xPos;
		//public float yPos;
		//public float zPos;
		//public float xRot;
		//public float yRot;
		//public float zRot;

		public override string ToString()
		{
			return $"{leftClick}\t{rightClick}\t{position.X:N2}\t{position.Y:N2}\t{position.Z:N2}\t{rotation.X:N2}\t{rotation.Y:N2}\t{rotation.Z:N2}";
		}
	}

	public class SpaceMouseInput
	{
		struct Mouse
		{
			public string name;
			public int vendor;
			public int product;
		}

		private static readonly Mouse[] mouseVendorProducts =
		{
			new Mouse { name = "SpaceNavigator", vendor = 0x46d, product = 0xc626 },
			new Mouse { name = "SpaceMouse Compact", vendor = 0x256F, product = 0xc635 },
		};

		private HidDevice device;
		private readonly ConnexionState state = new ConnexionState();
		public Action<ConnexionState> OnChanged;
		public bool Running { get; private set; }

		public void Start()
		{
			Running = true;
			Thread spaceMouseThread = new Thread(InputThread);
			spaceMouseThread.Start();
		}

		public void Stop()
		{
			Running = false;
		}

		private void InputThread()
		{
			List<HidDevice> deviceList = DeviceList.Local.GetHidDevices().ToList();
			foreach (HidDevice d in deviceList)
			{
				foreach (Mouse m in mouseVendorProducts)
				{
					if (d.VendorID == m.vendor && d.ProductID == m.product)
					{
						device = d;
					}
				}
			}

			if (device == null) return;
			if (!device.TryOpen(out HidStream hidStream)) return;

			hidStream.ReadTimeout = Timeout.Infinite;

			using HidStream stream = hidStream;
			while (Running)
			{
				byte[] bytes = hidStream.Read();
				switch (bytes[0])
				{
					case 1:
						state.position = new Vector3(
							(short)((bytes[2] << 8) | bytes[1]) / 350f,
							(short)((bytes[4] << 8) | bytes[3]) / 350f,
							(short)((bytes[6] << 8) | bytes[5]) / 350f
						);
						break;
					case 2:
						state.rotation = new Vector3(
							(short)((bytes[2] << 8) | bytes[1]) / 350f,
							(short)((bytes[4] << 8) | bytes[3]) / 350f,
							(short)((bytes[6] << 8) | bytes[5]) / 350f
						);
						break;
					// buttons
					case 3:
						state.leftClick = (bytes[1] & 1) != 0;
						state.rightClick = (bytes[1] & 2) != 0;
						break;
				}

				OnChanged?.Invoke(state);
			}
		}
	}
}