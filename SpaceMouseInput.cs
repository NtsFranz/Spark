using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using HidSharp;

namespace Spark
{
	public class ConnexionState
	{
		public bool leftClick;
		public bool rightClick;

		public float xPos;
		public float yPos;
		public float zPos;
		public float xRot;
		public float yRot;
		public float zRot;

		public override string ToString()
		{
			return $"{leftClick}\t{rightClick}\t{xPos:N2}\t{yPos:N2}\t{zPos:N2}\t{xRot:N2}\t{yRot:N2}\t{zRot:N2}";
		}
	}

	public class SpaceMouseInput
	{
		private const int CONNEXION_VENDOR = 0x46d;
		private const int CONNEXION_PRODUCT = 0xc626;

		private static HidDevice device;
		private static readonly ConnexionState state = new ConnexionState();
		public static Action<ConnexionState> OnChanged;
		public static bool Running { get; private set; }

		public static void Start()
		{
			Running = true;
			Thread spaceMouseThread = new Thread(InputThread);
			spaceMouseThread.Start();
		}

		public static void Stop()
		{
			Running = false;
		}

		private static void InputThread()
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
						// Console.WriteLine(string.Join("\t", bytes));
						if (bytes[0] == 3)
						{
							state.leftClick = (bytes[1] & 1) != 0;
							state.rightClick = (bytes[1] & 2) != 0;
						}
						else if (bytes[0] == 1)
						{
							state.xPos = (short) ((bytes[2] << 8) | bytes[1]) / 350f;
							state.yPos = (short) ((bytes[4] << 8) | bytes[3]) / 350f;
							state.zPos = (short) ((bytes[6] << 8) | bytes[5]) / 350f;
						}
						else if (bytes[0] == 2)
						{
							state.xRot = (short) ((bytes[2] << 8) | bytes[1]) / 350f;
							state.yRot = (short) ((bytes[4] << 8) | bytes[3]) / 350f;
							state.zRot = (short) ((bytes[6] << 8) | bytes[5]) / 350f;
						}
						OnChanged?.Invoke(state);
					}
				}
			}
		}
	}
}