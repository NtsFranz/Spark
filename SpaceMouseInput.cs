using System;
using System.Collections.Generic;
using System.Diagnostics;
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
		private const int CONNEXION_VENDOR = 0x46d;
		private const int CONNEXION_PRODUCT = 0xc626;

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
			IEnumerable<HidDevice> list = DeviceList.Local.GetHidDevices(CONNEXION_VENDOR, CONNEXION_PRODUCT);
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
						if (bytes[0] == 3)
						{
							state.leftClick = (bytes[1] & 1) != 0;
							state.rightClick = (bytes[1] & 2) != 0;
						}
						else if (bytes[0] == 1)
						{
							state.position = new Vector3(
								(short)((bytes[2] << 8) | bytes[1]) / 350f,
								(short)((bytes[4] << 8) | bytes[3]) / 350f,
								(short)((bytes[6] << 8) | bytes[5]) / 350f
							);
						}
						else if (bytes[0] == 2)
						{
							state.rotation = new Vector3(
								(short)((bytes[2] << 8) | bytes[1]) / 350f,
								(short)((bytes[4] << 8) | bytes[3]) / 350f,
								(short)((bytes[6] << 8) | bytes[5]) / 350f
							);
						}
						OnChanged?.Invoke(state);
					}
				}
			}
		}
	}
}