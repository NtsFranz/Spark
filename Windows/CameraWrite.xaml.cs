using Newtonsoft.Json;
using Spark.Properties;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Threading;
using System.Timers;
using System.Windows;
using System.Windows.Threading;
using Timer = System.Timers.Timer;

namespace Spark
{
	/// <summary>
	/// Based on the example from Graic's WriteAPI
	/// </summary>
	public partial class CameraWrite : Window
	{
		public const string url = "http://127.0.0.1:6723/";
		public string Duration { set { float.TryParse(value, out duration); } get => duration.ToString(); }
		public float duration = 5;

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

		public CameraTransform start;
		public CameraTransform end;

		public bool isAnimating;
		private float animationProgress = 0;
		public Thread animationThread;
		
		private readonly Timer outputUpdateTimer = new();
		private const int deltaMillis = 67; // about 15 fps

		public CameraWrite()
		{
			InitializeComponent();
			
			outputUpdateTimer.Interval = deltaMillis;
			outputUpdateTimer.Elapsed += Update;
			outputUpdateTimer.Enabled = true;
		}


		private void Update(object source, ElapsedEventArgs e)
		{
			if (Program.running)
			{

				Dispatcher.Invoke(() =>
				{
					animationProgressBar.Value = animationProgress;
				});
			}
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
			if (animationThread is {IsAlive: true})
			{
				isAnimating = false;
				startButton.Content = "Start";
			}
			else
			{
				isAnimating = true;
				animationThread = new Thread(AnimationThread);
				animationThread.Start();
				startButton.Content = "Stop";	
			}
		}

		private void AnimationThread()
		{
			DateTime startTime = DateTime.Now;
			while (animationProgress < 1 && isAnimating)
			{
				DateTime currentTime = DateTime.Now;
				float elapsed = (currentTime.Ticks - startTime.Ticks) / 10000000f;
				animationProgress = elapsed / duration;

				Vector3 newPos = Vector3.Lerp(start.position, end.position, animationProgress);
				Quaternion newRot = Quaternion.Slerp(start.rotation, end.rotation, animationProgress);

				CameraTransform newTransform = new(newPos, newRot);

				string data = JsonConvert.SerializeObject(newTransform);

				Program.PostRequestCallback(url, null, data, null);

				Thread.Sleep(8);
			}
			Program.PostRequestCallback(url, null, JsonConvert.SerializeObject(end), null);

			Dispatcher.Invoke(() =>
			{
				startButton.Content = "Start";
			});
			animationProgress = 0;
			isAnimating = false;
		}

        private void ReadXYZ(object sender, RoutedEventArgs e)
        {
			Program.GetRequestCallback(url, null, response =>
			{
                CameraTransform data = JsonConvert.DeserializeObject<CameraTransform>(response);
				Dispatcher.Invoke(() =>
				{
					x.Text = $"{data.position.X:N1}";
					y.Text = $"{data.position.Y:N1}";
					z.Text = $"{data.position.Z:N1}";

					Vector3 yawPitchRoll = QuaternionToEuler(data.rotation);
					yaw.Text = $"{yawPitchRoll.X:N1}";
					pitch.Text = $"{yawPitchRoll.Y:N1}";
					roll.Text = $"{yawPitchRoll.Z:N1}";
				});
			});
		}

		private void WriteXYZ(object sender, RoutedEventArgs e)
		{
			try
			{
				CameraTransform transform = new CameraTransform(
					new Vector3(
						float.Parse(x.Text),
						float.Parse(y.Text),
						float.Parse(z.Text)
					),
					Quaternion.CreateFromYawPitchRoll(
						float.Parse(yaw.Text),
						float.Parse(pitch.Text),
						float.Parse(roll.Text)
					));

				Program.PostRequestCallback(url, null, JsonConvert.SerializeObject(transform), null);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Can't parse camera input values\n{ex}");
				new MessageBox("Can't parse position/rotation values. Make sure they are all numbers.").Show();
			}
		}

		private static Vector3 QuaternionToEuler(Quaternion q)
		{
			Vector3 euler;

			// if the input quaternion is normalized, this is exactly one. Otherwise, this acts as a correction factor for the quaternion's not-normalizedness
			float unit = (q.X * q.X) + (q.Y * q.Y) + (q.Z * q.Z) + (q.W * q.W);

			// this will have a magnitude of 0.5 or greater if and only if this is a singularity case
			float test = q.X * q.W - q.Y * q.Z;

			if (test > 0.4995f * unit) // singularity at north pole
			{
				euler.X = (float)Math.PI / 2;
				euler.Y = 2f * (float)Math.Atan2(q.Y, q.X);
				euler.Z = 0;
			}
			else if (test < -0.4995f * unit) // singularity at south pole
			{
				euler.X = -(float)Math.PI / 2;
				euler.Y = -2f * (float)Math.Atan2(q.Y, q.X);
				euler.Z = 0;
			}
			else // no singularity - this is the majority of cases
			{
				euler.X = (float)Math.Asin(2f * (q.W * q.X - q.Y * q.Z));
				euler.Y = (float)Math.Atan2(2f * q.W * q.Y + 2f * q.Z * q.X, 1 - 2f * (q.X * q.X + q.Y * q.Y));
				euler.Z = (float)Math.Atan2(2f * q.W * q.Z + 2f * q.X * q.Y, 1 - 2f * (q.Z * q.Z + q.X * q.X));
			}

			// all the math so far has been done in radians. Before returning, we convert to degrees...
			euler *= 57.29578f;

			//...and then ensure the degree values are between 0 and 360
			euler.X %= 360;
			euler.Y %= 360;
			euler.Z %= 360;

			return euler;
		}
	}
}
