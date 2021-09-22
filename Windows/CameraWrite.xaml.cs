using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
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

		public string Duration
		{
			set { float.TryParse(value, out duration); }
			get => duration.ToString();
		}

		public float duration = 5;

		public bool orbitingDisc = false;
		public float rotSpeed { get; set; } = 30;
		public float orbitRadius { get; set; } = 2;
		public float followSmoothing { get; set; } = 1f;
		public float lagCompDiscFollow { get; set; } = 0f;

		public double spaceMouseMoveSpeed { get; set; } = .7f;
		public double spaceMouseRotateSpeed { get; set; } = .5f;
		public float spaceMouseMoveExponential { get; set; } = 2;
		public float spaceMouseRotateExponential { get; set; } = 2;

		public bool easeIn { get; set; }
		public bool easeOut { get; set; }


		public CameraTransform manualTransform = new CameraTransform();

		public float xPos
		{
			get => manualTransform.position.X;
			set
			{
				manualTransform.position.X = value;
				if (sliderListenersActivated) WriteXYZ();
			}
		}

		public float yPos
		{
			get => manualTransform.position.Y;
			set
			{
				manualTransform.position.Y = value;
				if (sliderListenersActivated) WriteXYZ();
			}
		}

		public float zPos
		{
			get => manualTransform.position.Z;
			set
			{
				manualTransform.position.Z = value;
				if (sliderListenersActivated) WriteXYZ();
			}
		}

		public float xRot
		{
			//get => QuaternionToEuler(manualTransform.rotation).X;
			get => manualTransform.rotation.X;
			set
			{
				//manualTransform.rotation = Quaternion.CreateFromYawPitchRoll(
				//	value * Deg2Rad, yRot * Deg2Rad, zRot * Deg2Rad
				//);
				manualTransform.rotation.X = value;
				if (sliderListenersActivated) WriteXYZ();
			}
		}

		public float yRot
		{
			//get => QuaternionToEuler(manualTransform.rotation).Y;
			get => manualTransform.rotation.Y;
			set
			{
				//manualTransform.rotation = Quaternion.CreateFromYawPitchRoll(
				//	xRot * Deg2Rad, value * Deg2Rad, zRot * Deg2Rad
				//);
				manualTransform.rotation.Y = value;
				if (sliderListenersActivated) WriteXYZ();
			}
		}

		public float zRot
		{
			//get => QuaternionToEuler(manualTransform.rotation).Z;
			get => manualTransform.rotation.Z;
			set
			{
				//manualTransform.rotation = Quaternion.CreateFromYawPitchRoll(
				//	xRot * Deg2Rad, yRot * Deg2Rad, value * Deg2Rad
				//);
				manualTransform.rotation.Z = value;
				if (sliderListenersActivated) WriteXYZ();
			}
		}

		public float wRot
		{
			get => manualTransform.rotation.W;
			set
			{
				manualTransform.rotation.W = value;
				if (sliderListenersActivated) WriteXYZ();
			}
		}


		[Serializable]
		public class CameraTransform
		{
			public CameraTransform()
			{
				position = Vector3.Zero;
				rotation = Quaternion.Identity;
			}

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
		public Thread orbitThread;

		private readonly Timer outputUpdateTimer = new();
		private const int deltaMillis = 67; // about 15 fps

		public const float Deg2Rad = 1 / 57.29578f;
		public const float Rad2Deg = 57.29578f;

		public bool sliderListenersActivated;
		private bool animationsComboBoxListenersActivated;

		public List<CameraTransform> CurrentAnimation;
		//{
		//	get
		//	{
		//		// if the key exists normally
		//		if (CameraWriteSettings.instance.animations.ContainsKey(CameraWriteSettings.instance.activeAnimation))
		//		{
		//			return CameraWriteSettings.instance.animations[CameraWriteSettings.instance.activeAnimation];
		//		}
		//		// if there are still other animations to choose from
		//		else if (CameraWriteSettings.instance.animations.Count > 0)
		//		{
		//			// change the selected animation to one of those
		//			CameraWriteSettings.instance.activeAnimation = CameraWriteSettings.instance.animations.Keys.First();
		//			return CameraWriteSettings.instance.animations[CameraWriteSettings.instance.activeAnimation];
		//		}
		//		// make an empty animation
		//		else
		//		{
		//			return new List<CameraTransform>();
		//		}
		//	}
		//}

		public CameraWrite()
		{
			CameraWriteSettings.Load();

			if (CameraWriteSettings.instance == null)
			{
				new MessageBox(
						$"Error accessing settings.\nTry renaming/deleting the file in C:\\Users\\[USERNAME]\\AppData\\Roaming\\IgniteVR\\Spark\\camerawrite_settings.json")
					.Show();
				return;
			}

			InitializeComponent();

			outputUpdateTimer.Interval = deltaMillis;
			outputUpdateTimer.Elapsed += Update;
			outputUpdateTimer.Enabled = true;


			SetSliderPositions();

			sliderListenersActivated = true;

			installWriteAPIButton.Content = File.Exists(WriteAPIExePath) ? "Launch WriteAPI" : "Install WriteAPI";

			// set the current animation in memory
			//if the key exists normally
			if (CameraWriteSettings.instance.animations.ContainsKey(CameraWriteSettings.instance.activeAnimation))
			{
				CurrentAnimation = CameraWriteSettings.instance.animations[CameraWriteSettings.instance.activeAnimation]
					.ToList();
			}
			// if there are still other animations to choose from
			else if (CameraWriteSettings.instance.animations.Count > 0)
			{
				// change the selected animation to one of those
				CameraWriteSettings.instance.activeAnimation = CameraWriteSettings.instance.animations.Keys.First();
				CurrentAnimation = CameraWriteSettings.instance.animations[CameraWriteSettings.instance.activeAnimation]
					.ToList();
			}
			// make an empty animation
			else
			{
				CurrentAnimation = new List<CameraTransform>();
			}

			RefreshAnimationsComboBoxFromSettings();
			RegenerateWaypointButtons();
			RegenerateKeyframeButtons();
		}

		private void RefreshAnimationsComboBoxFromSettings()
		{
			animationsComboBoxListenersActivated = false;
			AnimationsComboBox.Items.Clear();

			List<string> keys = CameraWriteSettings.instance.animations.Keys.ToList();
			for (int i = 0; i < keys.Count; i++)
			{
				AnimationsComboBox.Items.Add(new ComboBoxItem
				{
					Content = keys[i]
				});
			}

			AnimationsComboBox.SelectedIndex = keys.IndexOf(CameraWriteSettings.instance.activeAnimation);
			AnimationNameTextBox.Text = CameraWriteSettings.instance.activeAnimation;

			animationsComboBoxListenersActivated = true;
		}

		private void Update(object source, ElapsedEventArgs e)
		{
			if (Program.running)
			{
				Dispatcher.Invoke(() => { animationProgressBar.Value = animationProgress; });
			}
		}

		private void SetStart(object sender, RoutedEventArgs e)
		{
			try
			{
				Program.GetRequestCallback(url, null,
					response => { start = JsonConvert.DeserializeObject<CameraTransform>(response); });
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
				Program.GetRequestCallback(url, null,
					response => { end = JsonConvert.DeserializeObject<CameraTransform>(response); });
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Can't get camera position\n{ex}");
			}
		}

		private void StartAnimation(object sender, RoutedEventArgs e)
		{
			if (start == null || end == null) return;

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

		private void StartKeyframeAnimation(object sender, RoutedEventArgs e)
		{
			if (animationThread is {IsAlive: true})
			{
				isAnimating = false;
				startButton.Content = "Start";
			}
			else if (CameraWriteSettings.instance.animations.Count > 0)
			{
				isAnimating = true;
				animationThread = new Thread(KeyframeAnimationThread);
				animationThread.Start();
				startButton.Content = "Stop";
			}
		}


		private void KeyframeAnimationThread()
		{
			BezierSpline spline = new BezierSpline(CurrentAnimation);

			DateTime startTime = DateTime.Now;
			CameraTransform lastTransform = new CameraTransform();
			Stopwatch sw = new Stopwatch();
			sw.Start();
			while (animationProgress < 1 && isAnimating)
			{
				DateTime currentTime = DateTime.Now;
				float elapsed = (currentTime.Ticks - startTime.Ticks) / 10000000f;
				animationProgress = elapsed / duration;

				// Change t to use easing in and out
				float t = animationProgress;
				if (easeIn && easeOut)
				{
					t = EaseInOut(animationProgress);
				}
				else if (easeIn)
				{
					t = EaseIn(animationProgress);
				}
				else if (easeOut)
				{
					t = EaseOut(animationProgress);
				}


				Vector3 newPos = spline.GetPoint(t);
				//Quaternion newRot = QuaternionLookRotation(spline.GetDirection(t), Vector3.UnitY);
				Quaternion newRot = spline.GetRotation(t);

				CameraTransform newTransform = new(newPos, newRot);

				string distance = Vector3.Distance(newPos, lastTransform.position) / sw.Elapsed.TotalSeconds + "\t" +
				                  spline.GetCurve(t).Item1?.keyframes[0].position.X;
				sw.Restart();
				Debug.WriteLine(distance);

				string data = JsonConvert.SerializeObject(newTransform);

				Program.PostRequestCallback(url, null, data, null);

				lastTransform = newTransform;

				Thread.Sleep(8);
			}

			// write out the last frame
			// the if check is to avoid doing so if the stop button was clicked
			if (animationProgress >= 1)
			{
				Program.PostRequestCallback(url, null,
					JsonConvert.SerializeObject(CurrentAnimation.Last()), null);
			}

			Dispatcher.Invoke(() => { startButton.Content = "Start"; });
			animationProgress = 0;
			isAnimating = false;
		}

		private void AnimationThread()
		{
			DateTime startTime = DateTime.Now;
			while (animationProgress < 1 && isAnimating)
			{
				DateTime currentTime = DateTime.Now;
				float elapsed = (currentTime.Ticks - startTime.Ticks) / 10000000f;
				animationProgress = elapsed / duration;

				float t = animationProgress;
				if (easeIn && easeOut)
				{
					t = EaseInOut(animationProgress);
				}
				else if (easeIn)
				{
					t = EaseIn(animationProgress);
				}
				else if (easeOut)
				{
					t = EaseOut(animationProgress);
				}

				Vector3 newPos = Vector3.Lerp(start.position, end.position, t);
				Quaternion newRot = Quaternion.Slerp(start.rotation, end.rotation, t);

				CameraTransform newTransform = new(newPos, newRot);

				string data = JsonConvert.SerializeObject(newTransform);

				Program.PostRequestCallback(url, null, data, null);

				Thread.Sleep(8);
			}

			Program.PostRequestCallback(url, null, JsonConvert.SerializeObject(end), null);

			Dispatcher.Invoke(() => { startButton.Content = "Start"; });
			animationProgress = 0;
			isAnimating = false;
		}

		/// <summary>
		/// Lerps t from 0-1 only
		/// </summary>
		private static float EaseIn(float t)
		{
			return MathF.Pow(t, 3);
		}

		/// <summary>
		/// Lerps t from 0-1 only
		/// </summary>
		private static float EaseOut(float t)
		{
			t = 1 - t;
			return 1 - MathF.Pow(t, 3);
		}


		/// <summary>
		/// Lerps t from 0-1 only
		/// </summary>
		private static float EaseInOut(float t)
		{
			return EaseIn(t) + (EaseOut(t) - EaseIn(t)) * t;
		}

		private void ReadXYZ(object sender, RoutedEventArgs e)
		{
			Program.GetRequestCallback(url, null, response =>
			{
				CameraTransform data = JsonConvert.DeserializeObject<CameraTransform>(response);
				manualTransform = data;


				Dispatcher.Invoke(() => { SetSliderPositions(); });
			});
		}

		private void SetSliderPositions()
		{
			sliderListenersActivated = false;

			try
			{
				xSlider.Value = manualTransform.position.X;
				ySlider.Value = manualTransform.position.Y;
				zSlider.Value = manualTransform.position.Z;

				//Vector3 rot = QuaternionToEuler(manualTransform.rotation);
				Quaternion rot = manualTransform.rotation;
				xRotSlider.Value = rot.X;
				yRotSlider.Value = rot.Y;
				zRotSlider.Value = rot.Z;
				wRotSlider.Value = rot.W;
			}
			finally
			{
				sliderListenersActivated = true;
			}
		}

		private void WriteXYZ(object sender, RoutedEventArgs e)
		{
			WriteXYZ();
		}

		private void WriteXYZ()
		{
			try
			{
				CameraTransform transform = new CameraTransform(
					new Vector3(xPos, yPos, zPos),
					new Quaternion(xRot, yRot, zRot, wRot)
					//Quaternion.CreateFromYawPitchRoll(xRot * Deg2Rad, yRot * Deg2Rad, zRot * Deg2Rad)
				);

				Program.PostRequestCallback(url, null, JsonConvert.SerializeObject(transform), null);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Can't parse camera input values\n{ex}");
				new MessageBox("Can't parse position/rotation values. Make sure they are all numbers.").Show();
			}
		}

		/// <summary>
		/// Output is in degrees
		/// </summary>
		private static Vector3 QuaternionToEuler(Quaternion q)
		{
			Vector3 euler;

			// if the input quaternion is normalized, this is exactly one. Otherwise, this acts as a correction factor for the quaternion's not-normalizedness
			float unit = (q.X * q.X) + (q.Y * q.Y) + (q.Z * q.Z) + (q.W * q.W);

			// this will have a magnitude of 0.5 or greater if and only if this is a singularity case
			float test = q.X * q.W - q.Y * q.Z;

			if (test > 0.4995f * unit) // singularity at north pole
			{
				euler.X = (float) Math.PI / 2;
				euler.Y = 2f * (float) Math.Atan2(q.Y, q.X);
				euler.Z = 0;
			}
			else if (test < -0.4995f * unit) // singularity at south pole
			{
				euler.X = -(float) Math.PI / 2;
				euler.Y = -2f * (float) Math.Atan2(q.Y, q.X);
				euler.Z = 0;
			}
			else // no singularity - this is the majority of cases
			{
				euler.X = (float) Math.Asin(2f * (q.W * q.X - q.Y * q.Z));
				euler.Y = (float) Math.Atan2(2f * q.W * q.Y + 2f * q.Z * q.X, 1 - 2f * (q.X * q.X + q.Y * q.Y));
				euler.Z = (float) Math.Atan2(2f * q.W * q.Z + 2f * q.X * q.Y, 1 - 2f * (q.Z * q.Z + q.X * q.X));
			}

			// all the math so far has been done in radians. Before returning, we convert to degrees...
			euler *= 57.29578f;

			//...and then ensure the degree values are between 0 and 360
			euler.X %= 360;
			euler.Y %= 360;
			euler.Z %= 360;

			return euler;
		}

		private void RegenerateWaypointButtons()
		{
			WaypointsPanel.Children.Clear();

			foreach (var wp in CameraWriteSettings.instance.waypoints)
			{
				Grid row = new Grid
				{
					Margin = new Thickness(5, 0, 0, 0)
				};
				row.ColumnDefinitions.Add(new ColumnDefinition
				{
					Width = new GridLength(1, GridUnitType.Star)
				});
				row.ColumnDefinitions.Add(new ColumnDefinition
				{
					Width = new GridLength(70)
				});
				row.ColumnDefinitions.Add(new ColumnDefinition
				{
					Width = new GridLength(40)
				});
				Label name = new Label
				{
					Content = wp.Key,
				};
				Button goTo = new Button
				{
					Content = "Go To",
					Margin = new Thickness(5, 0, 0, 0),
				};
				goTo.Click += (_, _) =>
				{
					Program.PostRequestCallback(url, null, JsonConvert.SerializeObject(wp.Value), null);
					RegenerateWaypointButtons();
				};
				Button delete = new Button
				{
					Content = "X",
					Margin = new Thickness(5, 0, 5, 0),
				};
				delete.Click += (_, _) =>
				{
					CameraWriteSettings.instance.waypoints.Remove(wp.Key);
					RegenerateWaypointButtons();
				};

				Grid.SetColumn(name, 0);
				Grid.SetColumn(goTo, 1);
				Grid.SetColumn(delete, 2);

				row.Children.Add(name);
				row.Children.Add(goTo);
				row.Children.Add(delete);
				WaypointsPanel.Children.Add(row);
			}
		}

		private void RegenerateKeyframeButtons()
		{
			KeyframesList.Children.Clear();

			for (int i = 0; i < CurrentAnimation.Count; i++)
			{
				CameraTransform wp = CurrentAnimation[i];

				Grid row = new Grid
				{
					Margin = new Thickness(5, 0, 0, 0)
				};
				row.ColumnDefinitions.Add(new ColumnDefinition
				{
					Width = new GridLength(1, GridUnitType.Star)
				});
				row.ColumnDefinitions.Add(new ColumnDefinition
				{
					Width = new GridLength(70)
				});
				row.ColumnDefinitions.Add(new ColumnDefinition
				{
					Width = new GridLength(70)
				});
				row.ColumnDefinitions.Add(new ColumnDefinition
				{
					Width = new GridLength(40)
				});
				Label name = new Label
				{
					Content = i + 1,
				};
				Button replace = new Button
				{
					Content = "Replace",
					Margin = new Thickness(5, 0, 0, 0),
				};
				replace.Click += (_, _) =>
				{
					Program.GetRequestCallback(url, null, response =>
					{
						CameraTransform data = JsonConvert.DeserializeObject<CameraTransform>(response);

						if (data == null) return;

						CurrentAnimation[CurrentAnimation.FindIndex(val => val == wp)] = data;

						// update the UI with the new waypoint
						Dispatcher.Invoke(RegenerateKeyframeButtons);
					});
				};
				Button goTo = new Button
				{
					Content = "Go To",
					Margin = new Thickness(5, 0, 0, 0),
				};
				goTo.Click += (_, _) =>
				{
					Program.PostRequestCallback(url, null, JsonConvert.SerializeObject(wp), null);
					RegenerateKeyframeButtons();
				};
				Button delete = new Button
				{
					Content = "X",
					Margin = new Thickness(5, 0, 5, 0),
				};
				delete.Click += (_, _) =>
				{
					CurrentAnimation.Remove(wp);
					RegenerateKeyframeButtons();
				};

				Grid.SetColumn(name, 0);
				Grid.SetColumn(replace, 1);
				Grid.SetColumn(goTo, 2);
				Grid.SetColumn(delete, 3);

				row.Children.Add(name);
				row.Children.Add(replace);
				row.Children.Add(goTo);
				row.Children.Add(delete);
				KeyframesList.Children.Add(row);
			}
		}

		private void SaveWaypoint(object sender, RoutedEventArgs e)
		{
			string keyName = NewWaypointName.Text;
			if (string.IsNullOrEmpty(keyName))
			{
				return;
			}

			Program.GetRequestCallback(url, null, response =>
			{
				CameraTransform data = JsonConvert.DeserializeObject<CameraTransform>(response);

				CameraWriteSettings.instance.waypoints[keyName] = data;

				// update the UI with the new waypoint
				Dispatcher.Invoke(RegenerateWaypointButtons);

				CameraWriteSettings.instance.Save();
			});
		}

		private void WindowClosed(object sender, EventArgs e)
		{
			CameraWriteSettings.instance.Save();

			orbitingDisc = false;
			isAnimating = false;
			SpaceMouseInput.Start();
		}

		private void AddKeyframe(object sender, RoutedEventArgs e)
		{
			Program.GetRequestCallback(url, null, response =>
			{
				CameraTransform data = JsonConvert.DeserializeObject<CameraTransform>(response);

				if (data == null) return;

				CurrentAnimation.Add(data);

				// update the UI with the new waypoint
				Dispatcher.Invoke(() => { RegenerateKeyframeButtons(); });
			});
		}

		private void ClearKeyframes(object sender, RoutedEventArgs e)
		{
			CurrentAnimation.Clear();
			RegenerateKeyframeButtons();
		}

		private void ToggleOrbitDisc(object sender, RoutedEventArgs e)
		{
			if (orbitThread?.IsAlive == true)
			{
				orbitingDisc = false;
				IsOrbitingCheckbox.IsChecked = false;
			}
			else
			{
				orbitingDisc = true;
				IsOrbitingCheckbox.IsChecked = true;
				orbitThread = new Thread(OrbitDiscThread);
				orbitThread.Start();
			}
		}

		private void OrbitDiscThread()
		{
			DateTime startTime = DateTime.Now;

			const int avgCount = 5;

			List<CameraTransform> lastTransforms = new List<CameraTransform>();

			Vector3 smoothDiscPos = Vector3.Zero;

			while (orbitingDisc)
			{
				DateTime currentTime = DateTime.Now;
				double elapsed = (currentTime - startTime).TotalSeconds;

				double angle = elapsed * rotSpeed * Deg2Rad;

				Vector3 diff = Program.lastFrame.disc.position.ToVector3() -
				               Program.lastLastFrame.disc.position.ToVector3();
				Vector3 discVel = Program.lastFrame.disc.velocity.ToVector3();
				Vector3 lastDiscVel = Program.lastLastFrame.disc.velocity.ToVector3();
				if (discVel != Vector3.Zero)
				{
					diff = discVel;
				}

				Quaternion vel;
				Vector3 offset;
				if (diff == Vector3.Zero)
				{
					vel = Quaternion.Identity;
					offset = Vector3.UnitZ;
				}
				else
				{
					vel = QuaternionLookRotation(diff.Normalized(), Vector3.UnitY);
					offset = diff.Normalized();
				}

				offset = new Vector3((float) Math.Cos(angle), 0, (float) Math.Sin(angle)) * orbitRadius;


				Vector3 discPos = Program.lastFrame.disc.position.ToVector3();
				Vector3 lastDiscPos = Program.lastLastFrame.disc.position.ToVector3();

				// add lag comp
				discPos += discVel * lagCompDiscFollow;
				lastDiscPos += lastDiscVel * lagCompDiscFollow;

				//discPos = Vector3.Lerp(lastDiscPos, discPos, (float)(DateTime.Now - Program.lastDataTime).TotalSeconds/1);
				smoothDiscPos = Vector3.Lerp(smoothDiscPos, discPos, followSmoothing);
				discPos = smoothDiscPos;

				Vector3 pos = discPos - offset;

				Quaternion lookDir = QuaternionLookRotation(discPos - pos, Vector3.UnitY);

				CameraTransform newTransform = new(pos, lookDir);
				lastTransforms.Add(newTransform);
				if (lastTransforms.Count > avgCount)
				{
					lastTransforms.RemoveAt(0);
				}

				CameraTransform avg = lastTransforms[0];

				foreach (CameraTransform t in lastTransforms)
				{
					//avg.position = Vector3.Lerp(lastTransforms[i].position, avg.position, .5f);
					avg.position += t.position;
					avg.rotation = Quaternion.Lerp(t.rotation, avg.rotation, .5f);
				}

				avg.position /= avgCount;

				avg.rotation = new Quaternion(avg.rotation.X / avgCount, avg.rotation.Y / avgCount,
					avg.rotation.Z / avgCount, avg.rotation.W / avgCount);
				//avg.rotation /= (float)avgCount;


				string data = JsonConvert.SerializeObject(newTransform);

				Program.PostRequestCallback(url, null, data, null);

				Thread.Sleep(2);
			}

			Dispatcher.Invoke(() => { IsOrbitingCheckbox.IsChecked = false; });
		}

		private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
		{
			try
			{
				Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) {UseShellExecute = true});
				e.Handled = true;
			}
			catch (Exception ex)
			{
				Logger.LogRow(Logger.LogType.Error, ex.ToString());
			}
		}

		private static Quaternion QuaternionLookRotation(Vector3 forward, Vector3 up)
		{
			forward /= forward.Length();

			Vector3 vector = Vector3.Normalize(forward);
			Vector3 vector2 = Vector3.Normalize(Vector3.Cross(up, vector));
			Vector3 vector3 = Vector3.Cross(vector, vector2);
			var m00 = vector2.X;
			var m01 = vector2.Y;
			var m02 = vector2.Z;
			var m10 = vector3.X;
			var m11 = vector3.Y;
			var m12 = vector3.Z;
			var m20 = vector.X;
			var m21 = vector.Y;
			var m22 = vector.Z;


			float num8 = (m00 + m11) + m22;
			var quaternion = new Quaternion();
			if (num8 > 0f)
			{
				var num = (float) Math.Sqrt(num8 + 1f);
				quaternion.W = num * 0.5f;
				num = 0.5f / num;
				quaternion.X = (m12 - m21) * num;
				quaternion.Y = (m20 - m02) * num;
				quaternion.Z = (m01 - m10) * num;
				return quaternion;
			}

			if ((m00 >= m11) && (m00 >= m22))
			{
				var num7 = (float) Math.Sqrt(((1f + m00) - m11) - m22);
				var num4 = 0.5f / num7;
				quaternion.X = 0.5f * num7;
				quaternion.Y = (m01 + m10) * num4;
				quaternion.Z = (m02 + m20) * num4;
				quaternion.W = (m12 - m21) * num4;
				return quaternion;
			}

			if (m11 > m22)
			{
				var num6 = (float) Math.Sqrt(((1f + m11) - m00) - m22);
				var num3 = 0.5f / num6;
				quaternion.X = (m10 + m01) * num3;
				quaternion.Y = 0.5f * num6;
				quaternion.Z = (m21 + m12) * num3;
				quaternion.W = (m20 - m02) * num3;
				return quaternion;
			}

			var num5 = (float) Math.Sqrt(((1f + m22) - m00) - m11);
			var num2 = 0.5f / num5;
			quaternion.X = (m20 + m02) * num2;
			quaternion.Y = (m21 + m12) * num2;
			quaternion.Z = 0.5f * num5;
			quaternion.W = (m01 - m10) * num2;
			return quaternion;
		}


		private string WriteAPIFolder =>
			Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "WriteAPI");

		private string WriteAPIExePath => Path.Combine(WriteAPIFolder, "WriteAPI.exe");
		private string WriteAPIZipPath => Path.Combine(WriteAPIFolder, "WriteAPI.zip");


		private void InstallLaunchWriteAPI(object sender, RoutedEventArgs e)
		{
			if (File.Exists(WriteAPIExePath))
			{
				try
				{
					Process.Start(new ProcessStartInfo(WriteAPIExePath) {UseShellExecute = true});
				}
				catch (Exception ex)
				{
					Logger.LogRow(Logger.LogType.Error, ex.ToString());
				}
			}
			else
			{
				try
				{
					installWriteAPIButton.Content = "Installing...";
					Task.Run(() =>
					{
						try
						{
							using WebClient webClient = new WebClient();

							if (!Directory.Exists(WriteAPIFolder))
							{
								Directory.CreateDirectory(WriteAPIFolder);
							}

							webClient.DownloadFile(
								"https://github.com/Graicc/WriteAPI/releases/download/v1.0.0/WriteAPI.zip",
								WriteAPIZipPath);

							ZipFile.ExtractToDirectory(WriteAPIZipPath, WriteAPIFolder);

							Dispatcher.Invoke(() => { installWriteAPIButton.Content = "Launch WriteAPI"; });
						}
						catch (Exception ex)
						{
							Logger.LogRow(Logger.LogType.Error, ex.ToString());
						}
					});
				}
				catch (Exception ex)
				{
					Logger.LogRow(Logger.LogType.Error, ex.ToString());
				}
			}
		}

		private void AnimationsComboBoxChanged(object sender, SelectionChangedEventArgs e)
		{
			if (animationsComboBoxListenersActivated)
			{
				CameraWriteSettings.instance.activeAnimation =
					((ComboBoxItem) AnimationsComboBox.SelectedItem).Content.ToString();
				AnimationNameTextBox.Text = CameraWriteSettings.instance.activeAnimation;

				if (!CameraWriteSettings.instance.animations.ContainsKey(CameraWriteSettings.instance.activeAnimation))
				{
					new MessageBox("Error 4853: Report this to NtsFranz immediately, or else. >:|").Show();
					return;
				}

				CurrentAnimation = CameraWriteSettings.instance.animations[CameraWriteSettings.instance.activeAnimation]
					.ToList();
				RegenerateKeyframeButtons();
			}
		}

		private void AnimationSaveClicked(object sender, RoutedEventArgs e)
		{
			string newName = AnimationNameTextBox.Text;

			if (string.IsNullOrEmpty(newName))
			{
				new MessageBox("Please enter a name first.").Show();
			}
			else
			{
				CameraWriteSettings.instance.activeAnimation = newName;
				CameraWriteSettings.instance.animations[newName] = CurrentAnimation;

				RefreshAnimationsComboBoxFromSettings();

				CameraWriteSettings.instance.Save();
			}
		}

		private void AnimationDeleteClicked(object sender, RoutedEventArgs e)
		{
			string name = AnimationNameTextBox.Text;
			CameraWriteSettings.instance.animations.Remove(name);
			if (CameraWriteSettings.instance.animations.Count > 0)
			{
				CameraWriteSettings.instance.activeAnimation = CameraWriteSettings.instance.animations.Keys.First();
				CurrentAnimation = CameraWriteSettings.instance.animations[CameraWriteSettings.instance.activeAnimation]
					.ToList();
			}

			RefreshAnimationsComboBoxFromSettings();
			RegenerateKeyframeButtons();
		}


		private void Toggle3DMouse(object sender, RoutedEventArgs e)
		{
			if (SpaceMouseInput.Running)
			{
				SpaceMouseInput.Stop();
				SpaceMouseCheckBox.IsChecked = false;
			}
			else
			{
				SpaceMouseInput.OnChanged += OnSpaceMouseChanged;
				SpaceMouseInput.Start();
				SpaceMouseCheckBox.IsChecked = true;
			}
		}

		private CameraTransform lastTransform = null;
		private DateTime lastTransformTime = DateTime.MinValue;
		private void OnSpaceMouseChanged(ConnexionState state)
		{
			float Exponential(float value, float expo)
			{
				if (value < 0)
				{
					return -MathF.Pow(-value, expo);
				}
				else
				{
					return MathF.Pow(value, expo);
				}
			}

			Program.GetRequestCallback(url, null, response =>
			{
				if (DateTime.Now - lastTransformTime > TimeSpan.FromSeconds(1))
				{
					Debug.WriteLine("Reloading from WriteAPI");
					lastTransform = null;
				}
				CameraTransform camPos = lastTransform ?? JsonConvert.DeserializeObject<CameraTransform>(response);
				if (camPos == null) return;

				CameraTransform output = new CameraTransform();


				Dispatcher.Invoke(() =>
				{
					inputPosX.Value = state.position.X;
					inputPosY.Value = state.position.Y;
					inputPosZ.Value = state.position.Z;

					inputRotX.Value = state.rotation.X;
					inputRotY.Value = state.rotation.Y;
					inputRotZ.Value = state.rotation.Z;
				});

				state.position *= (float) spaceMouseMoveSpeed;
				state.rotation *= (float) spaceMouseRotateSpeed;

				Vector3 inputPosition = new Vector3(
					Exponential(-state.position.X, spaceMouseMoveExponential),
					Exponential(-state.position.Z, spaceMouseMoveExponential),
					Exponential(-state.position.Y, spaceMouseMoveExponential)
				);
				Quaternion rotate = Quaternion.CreateFromYawPitchRoll(
					Exponential(-state.rotation.Z, spaceMouseRotateExponential),
					Exponential(-state.rotation.X, spaceMouseRotateExponential),
					Exponential(-state.rotation.Y, spaceMouseRotateExponential)
				);

				Matrix4x4 camPosMatrix = Matrix4x4.CreateFromQuaternion(camPos.rotation);
				Matrix4x4 transformMatrix = Matrix4x4.CreateFromQuaternion(rotate);
				
				output.position = camPos.position + Vector3.Transform(inputPosition, camPosMatrix);
				// output.rotation = Quaternion.CreateFromRotationMatrix(transformMatrix * camPosMatrix);
				output.rotation = Quaternion.Multiply(camPos.rotation, rotate);


				lastTransform = output;
				lastTransformTime = DateTime.Now;
				
				// do manual serialization to get more precision
				string serializedOutput = $@"{{
					""position"": {{
						""X"": {output.position.X},
						""Y"": {output.position.Y},
						""Z"": {output.position.Z}
					}},
					""rotation"": {{
						""X"": {output.rotation.X},
						""Y"": {output.rotation.Y},
						""Z"": {output.rotation.Z},
						""W"": {output.rotation.W}
					}}
				}}";

				Program.PostRequestCallback(url, null, serializedOutput, null);
			});
		}
	}
}