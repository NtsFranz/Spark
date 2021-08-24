using Newtonsoft.Json;
using Spark.Properties;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Threading;
using System.Windows;

namespace Spark
{
	/// <summary>
	/// Based on the example from Graic's WriteAPI
	/// </summary>
	public partial class CameraWrite : Window
	{
		public const string url = "http://127.0.0.1:6723/";
		public string Duration { set { float.TryParse(value, out duration); } get => duration.ToString(); }
		public static float duration = 5;

		public class CameraTransform
		{
			public CameraTransform() { }

			public CameraTransform(Vector3 pos, Quaternion rot)
			{
				position = pos;
				rotation = rot;
			}

			public Vector3 position;
			public Quaternion rotation;
		}

		public static CameraTransform start;
		public static CameraTransform end;

		public bool isAnimating;
		public Thread animationThread;

		public CameraWrite()
		{
			InitializeComponent();
		}

		private void SetStart(object sender, RoutedEventArgs e)
		{
			try
			{
				Program.GetRequestCallback(url, null, response =>
				{
					start = JsonConvert.DeserializeObject<CameraTransform>(response);
				});
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Can't get camera position\n{ex}");
			}
		}

		private void SetEnd(object sender, RoutedEventArgs e)
		{
			try
			{
				Program.GetRequestCallback(url, null, response =>
				{
					end = JsonConvert.DeserializeObject<CameraTransform>(response);
				});
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Can't get camera position\n{ex}");
			}
		}

		private void StartAnimation(object sender, RoutedEventArgs e)
		{
			animationThread = new Thread(AnimationThread);
			animationThread.Start();
		}

		private static void AnimationThread()
		{
			DateTime startTime = DateTime.Now;
			DateTime currentTime;
			float t = 0;
			while (t < 1)
			{
				currentTime = DateTime.Now;
				float elapsed = (currentTime.Ticks - startTime.Ticks) / 10000000f;
				t = elapsed / duration;

				Vector3 newPos = Vector3.Lerp(start.position, end.position, t);
				Quaternion newRot = Quaternion.Slerp(start.rotation, end.rotation, t);

				CameraTransform newTransform = new CameraTransform(newPos, newRot);

				string data = JsonConvert.SerializeObject(newTransform);

				Program.PostRequestCallback(url, null, data, null);

				Thread.Sleep(10);
			}
		}
	}
}
