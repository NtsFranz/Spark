using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Navigation;
using Frame = EchoVRAPI.Frame;
using Quaternion = System.Numerics.Quaternion;
using Timer = System.Timers.Timer;

namespace Spark
{
	public partial class CameraWrite : UserControl
	{
		private LiveWindow liveWindow;

		public const string sessionUrl = "http://127.0.0.1:6721/session";

		public string Duration
		{
			set => float.TryParse(value, out CurrentAnimation.duration);
			get => CurrentAnimation?.duration.ToString(CultureInfo.InvariantCulture) ?? "5";
		}

		public bool EaseIn
		{
			set => CurrentAnimation.easeIn = value;
			get => CurrentAnimation?.easeIn ?? false;
		}

		public bool EaseOut
		{
			set => CurrentAnimation.easeOut = value;
			get => CurrentAnimation?.easeOut ?? false;
		}


		public bool PauseWhenClockNotRunning
		{
			set => CurrentAnimation.pauseWhenClockNotRunning = value;
			get => CurrentAnimation?.pauseWhenClockNotRunning ?? false;
		}

		public static float lastSetFov = 1;
		public CameraTransform manualTransform = new CameraTransform();

		#region Properties

		public float xPos
		{
			get => manualTransform.px ?? 0;
			set
			{
				manualTransform.px = value;
				if (sliderListenersActivated)
				{
					WriteXYZ();

					// reenable if the Echo supports setting axes individually
					// CameraTransform transform = new CameraTransform()
					// {
					// 	px = xPos
					// };
					//
					// SetCamera(transform);
				}
			}
		}

		public float yPos
		{
			get => manualTransform.py ?? 0;
			set
			{
				manualTransform.py = value;
				if (sliderListenersActivated)
				{
					WriteXYZ();

					// reenable if the Echo supports setting axes individually
					// CameraTransform transform = new CameraTransform()
					// {
					// 	py = yPos
					// };
					//
					// SetCamera(transform);
				}
			}
		}

		public float zPos
		{
			get => manualTransform.pz ?? 0;
			set
			{
				manualTransform.pz = value;
				if (sliderListenersActivated)
				{
					WriteXYZ();

					// reenable if the Echo supports setting axes individually
					// CameraTransform transform = new CameraTransform()
					// {
					// 	pz = zPos
					// };
					//
					// SetCamera(transform);
				}
			}
		}

		public float xRot
		{
			//get => QuaternionToEuler(manualTransform.rotation).X;
			get => manualTransform.qx ?? 0;
			set
			{
				//manualTransform.rotation = Quaternion.CreateFromYawPitchRoll(
				//	value * CameraWriteController.Deg2Rad, yRot * CameraWriteController.Deg2Rad, zRot * CameraWriteController.Deg2Rad
				//);
				manualTransform.qx = value;
				if (sliderListenersActivated)
				{
					WriteXYZ();

					// reenable if the Echo supports setting axes individually
					// CameraTransform transform = new CameraTransform()
					// {
					// 	qx = xRot, qy = yRot, qz = zRot, qw = wRot,
					// };
					//
					// SetCamera(transform);
				}
			}
		}

		public float yRot
		{
			//get => QuaternionToEuler(manualTransform.rotation).Y;
			get => manualTransform.qy ?? 0;
			set
			{
				//manualTransform.rotation = Quaternion.CreateFromYawPitchRoll(
				//	xRot * CameraWriteController.Deg2Rad, value * CameraWriteController.Deg2Rad, zRot * CameraWriteController.Deg2Rad
				//);
				manualTransform.qy = value;
				if (sliderListenersActivated)
				{
					WriteXYZ();

					// reenable if the Echo supports setting axes individually
					// CameraTransform transform = new CameraTransform()
					// {
					// 	qx = xRot, qy = yRot, qz = zRot, qw = wRot,
					// };
					//
					// SetCamera(transform);
				}
			}
		}

		public float zRot
		{
			//get => QuaternionToEuler(manualTransform.rotation).Z;
			get => manualTransform.qz ?? 0;
			set
			{
				//manualTransform.rotation = Quaternion.CreateFromYawPitchRoll(
				//	xRot * CameraWriteController.Deg2Rad, yRot * CameraWriteController.Deg2Rad, value * CameraWriteController.Deg2Rad
				//);
				manualTransform.qz = value;
				if (sliderListenersActivated)
				{
					WriteXYZ();

					// reenable if the Echo supports setting axes individually
					// CameraTransform transform = new CameraTransform()
					// {
					// 	qx = xRot, qy = yRot, qz = zRot, qw = wRot,
					// };
					//
					// SetCamera(transform);
				}
			}
		}

		public float wRot
		{
			get => manualTransform.qw ?? 1;
			set
			{
				manualTransform.qw = value;
				if (sliderListenersActivated)
				{
					WriteXYZ();

					// reenable if the Echo supports setting axes individually
					// CameraTransform transform = new CameraTransform()
					// {
					// 	qx = xRot, qy = yRot, qz = zRot, qw = wRot,
					// };
					//
					// SetCamera(transform);
				}
			}
		}

		public float fov
		{
			get => manualTransform.fovy ?? 1;
			set
			{
				manualTransform.fovy = value;
				if (sliderListenersActivated)
				{
					WriteXYZ();

					// reenable if the Echo supports setting axes individually
					// CameraTransform transform = new CameraTransform()
					// {
					// 	fovy = fov
					// };
					//
					// SetCamera(transform);
				}
			}
		}

		#endregion

		#region Camera Modules

		private ControllableSideline controllableSideline = new ControllableSideline();
		private DiscOrbit discOrbit = new DiscOrbit();
		private SpaceMouseInput spaceMouseInput = new SpaceMouseInput();

		#endregion

		public CameraTransform start;
		public CameraTransform end;

		public bool isAnimating;
		public bool animationSliderManipulating;
		private float animationProgress;
		public Thread animationThread;
		public Thread orbitThread;

		private readonly Timer outputUpdateTimer = new();
		private const int deltaMillis = 67; // about 15 fps


		public bool sliderListenersActivated;
		private bool animationsComboBoxListenersActivated;

		private BezierSpline spline;
		private AnimationKeyframes currentAnimation;
		public AnimationKeyframes CurrentAnimation
		{
			get => currentAnimation;
			set
			{
				currentAnimation = value;
				spline = new BezierSpline(currentAnimation);
			}	
		}
		//{
		//	get
		//	{
		//		// if the key exists normally
		//		if (CameraWriteSettings.animations.ContainsKey(CameraWriteSettings.instance.activeAnimation))
		//		{
		//			return CameraWriteSettings.animations[CameraWriteSettings.instance.activeAnimation];
		//		}
		//		// if there are still other animations to choose from
		//		else if (CameraWriteSettings.animations.Count > 0)
		//		{
		//			// change the selected animation to one of those
		//			CameraWriteSettings.instance.activeAnimation = CameraWriteSettings.animations.Keys.First();
		//			return CameraWriteSettings.animations[CameraWriteSettings.instance.activeAnimation];
		//		}
		//		// make an empty animation
		//		else
		//		{
		//			return new List<CameraTransform>();
		//		}
		//	}
		//}

		private GlobalHotKey[] numPadHotKeys;


		string WriteApiExePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "WriteAPI", "WriteAPI.exe");

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

			Program.cameraWriteWindow = this;


			SetSliderPositions(manualTransform);

			sliderListenersActivated = true;

			InstallWriteApiButton.Content = File.Exists(WriteApiExePath) ? "Launch WriteAPI" : "Install WriteAPI";

			// set the current animation in memory
			//if the key exists normally
			if (CameraWriteSettings.animations.ContainsKey(CameraWriteSettings.instance.activeAnimation))
			{
				CurrentAnimation = CameraWriteSettings.animations[CameraWriteSettings.instance.activeAnimation];
			}
			// if there are still other animations to choose from
			else if (CameraWriteSettings.animations.Count > 0)
			{
				// change the selected animation to one of those
				CameraWriteSettings.instance.activeAnimation = CameraWriteSettings.animations.Keys.First();
				CurrentAnimation = CameraWriteSettings.animations[CameraWriteSettings.instance.activeAnimation];
			}
			// make an empty animation
			else
			{
				CurrentAnimation = new AnimationKeyframes();
			}

			EnableHotKeys(CameraWriteSettings.instance.enableHotKeys);

			RefreshAnimationsComboBoxFromSettings();
			RegenerateWaypointButtons();
			RegenerateKeyframeButtons();

			Program.JoinedGame += frame => { SetSliderLimits(frame); };
		}

		private void SetSliderLimits(Frame frame)
		{
			Dispatcher.Invoke(() =>
			{
				if (frame.InArena)
				{
					xSlider.Minimum = -24;
					xSlider.Maximum = 24;

					ySlider.Minimum = -15;
					ySlider.Maximum = 15;

					zSlider.Minimum = -80;
					zSlider.Maximum = 80;
				}
				else
				{
					xSlider.Minimum = -200;
					xSlider.Maximum = 200;

					ySlider.Minimum = -200;
					ySlider.Maximum = 200;

					zSlider.Minimum = -200;
					zSlider.Maximum = 200;
				}
			});
		}

		private void EnableHotKeys(bool enable)
		{
			if (enable)
			{
				numPadHotKeys = new GlobalHotKey[]
				{
					new GlobalHotKey(Key.NumPad1, KeyModifier.None, (_) => { TryGoToWaypoint(0); }),
					new GlobalHotKey(Key.NumPad2, KeyModifier.None, (_) => { TryGoToWaypoint(1); }),
					new GlobalHotKey(Key.NumPad3, KeyModifier.None, (_) => { TryGoToWaypoint(2); }),
					new GlobalHotKey(Key.NumPad4, KeyModifier.None, (_) => { TryPlayAnim(0); }),
					new GlobalHotKey(Key.NumPad5, KeyModifier.None, (_) => { TryPlayAnim(1); }),
					new GlobalHotKey(Key.NumPad6, KeyModifier.None, (_) => { TryPlayAnim(2); }),
					new GlobalHotKey(Key.NumPad7, KeyModifier.None, (_) => { TryPlayAnim(3); }),
					new GlobalHotKey(Key.NumPad8, KeyModifier.None, (_) => { TryPlayAnim(4); }),
					new GlobalHotKey(Key.NumPad9, KeyModifier.None, (_) => { TryPlayAnim(5); })
				};
			}
			else
			{
				numPadHotKeys = null;
			}
		}

		// get a reference to main windows when it is available.
		// The Loaded Event is set in the XAML code above.
		private void OnControlLoaded(object sender, RoutedEventArgs e)
		{
			liveWindow = Window.GetWindow(this) as LiveWindow;
		}

		public static void TryGoToWaypoint(int index)
		{
			CameraTransform[] waypoints = CameraWriteSettings.instance.waypoints.Values.ToArray();
			if (waypoints.Length <= index) return;
			SetCamera(waypoints[index]);
		}

		public bool TryGoToWaypoint(string name)
		{
			CameraTransform[] waypoints = CameraWriteSettings.instance.waypoints.Values.ToArray();
			if (CameraWriteSettings.instance.waypoints.ContainsKey(name))
			{
				SetCamera(CameraWriteSettings.instance.waypoints[name]);
				return true;
			}
			else
			{
				return false;
			}
		}

		public void TryPlayAnim(int index)
		{
			if (AnimationsComboBox.Items.Count <= index) return;
			Dispatcher.Invoke(() =>
			{
				AnimationsComboBox.SelectedIndex = index;
				StartKeyframeAnimation(null, null);
			});
		}

		public void TryPlayAnim(string name)
		{
			Dispatcher.Invoke(() =>
			{
				for (int i = 0; i < AnimationsComboBox.Items.Count; i++)
				{
					var test = ((ComboBoxItem)AnimationsComboBox.Items[i]).Content.ToString();
					if (((ComboBoxItem)AnimationsComboBox.Items[i]).Content.ToString() == name)
					{
						AnimationsComboBox.SelectedIndex = i;
						StartKeyframeAnimation(null, null);
						break;
					}
				}
			});
		}

		private void RefreshAnimationsComboBoxFromSettings()
		{
			animationsComboBoxListenersActivated = false;
			AnimationsComboBox.Items.Clear();

			List<string> keys = CameraWriteSettings.animations.Keys.ToList();
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
				Dispatcher.Invoke(() =>
				{
					animationProgressBar.Value = animationProgress;
					AnimationInputSlider.Value = animationProgress;

					// spacemouse
					if (spaceMouseInput.Enabled)
					{
						inputPosX.Value = spaceMouseInput.lastMouseState.position.X;
						inputPosY.Value = spaceMouseInput.lastMouseState.position.Y;
						inputPosZ.Value = spaceMouseInput.lastMouseState.position.Z;
						inputRotX.Value = spaceMouseInput.lastMouseState.rotation.X;
						inputRotY.Value = spaceMouseInput.lastMouseState.rotation.Y;
						inputRotZ.Value = spaceMouseInput.lastMouseState.rotation.Z;
					}
				});
			}
		}

		private void SetStart(object sender, RoutedEventArgs e)
		{
			if (Program.lastFrame == null) return;
			(Vector3 p, Quaternion q) = Program.lastFrame.GetCameraTransform();
			start = new CameraTransform(p, q, lastSetFov);
		}

		private void SetEnd(object sender, RoutedEventArgs e)
		{
			if (Program.lastFrame == null) return;
			(Vector3 p, Quaternion q) = Program.lastFrame.GetCameraTransform();
			end = new CameraTransform(p, q, lastSetFov);
		}

		private void StartAnimation(object sender, RoutedEventArgs e)
		{
			if (start == null || end == null) return;

			if (animationThread is { IsAlive: true })
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
			if (animationThread is { IsAlive: true })
			{
				isAnimating = false;
				startButton.Content = "Start";
			}
			else if (CameraWriteSettings.animations.Count > 0)
			{
				isAnimating = true;
				animationThread = new Thread(KeyframeAnimationThread);
				animationThread.Start();
				startButton.Content = "Stop";
			}
		}


		private void KeyframeAnimationThread()
		{
			DateTime startTime = DateTime.Now;
			// CameraTransform lastTransform = new CameraTransform();
			Stopwatch sw = new Stopwatch();
			sw.Start();
			while (animationProgress < 1 && isAnimating)
			{
				if ((!CurrentAnimation.pauseWhenClockNotRunning || Program.lastFrame?.private_match == true || Program.lastFrame?.game_status == "playing") &&
				    !animationSliderManipulating)
				{
					DateTime currentTime = DateTime.Now;
					float elapsed = (currentTime.Ticks - startTime.Ticks) / 10000000f;
					animationProgress = elapsed / CurrentAnimation.duration;
				}

				animationSliderManipulating = false;

				UpdateCameraForAnimation();
				sw.Restart();

				// lastTransform = newTransform;

				Thread.Sleep(4);
			}

			// write out the last frame
			// the if check is to avoid doing so if the stop button was clicked
			if (animationProgress >= 1)
			{
				SetCamera(CurrentAnimation.keyframes.LastOrDefault());
			}

			Dispatcher.Invoke(() => { startButton.Content = "Start"; });
			animationProgress = 0;
			isAnimating = false;
		}

		private void UpdateCameraForAnimation()
		{
			// Change t to use easing in and out
			float t = animationProgress;
			if (CurrentAnimation.easeIn && CurrentAnimation.easeOut)
			{
				t = EaseInOut(animationProgress);
			}
			else if (CurrentAnimation.easeIn)
			{
				t = DoEaseIn(animationProgress);
			}
			else if (CurrentAnimation.easeOut)
			{
				t = DoEaseOut(animationProgress);
			}


			Vector3 newPos = spline.GetPoint(t);
			//Quaternion newRot = QuaternionLookRotation(spline.GetDirection(t), Vector3.UnitY);
			Quaternion newRot = spline.GetRotation(t);
			float newFov = spline.GetFov(t);

			CameraTransform newTransform = new CameraTransform(newPos, newRot, newFov);

			// string distance = Vector3.Distance(newPos, lastTransform.Position) / sw.Elapsed.TotalSeconds + "\t" +
			//                   spline.GetCurve(t).Item1?.keyframes[0].px;

			// Debug.WriteLine(distance);

			SetCamera(newTransform);
		}

		private void AnimationThread()
		{
			DateTime startTime = DateTime.Now;
			while (animationProgress < 1 && isAnimating)
			{
				if (!Program.InGame)
				{
					Thread.Sleep(100);
					continue;
				}

				DateTime currentTime = DateTime.Now;
				float elapsed = (currentTime.Ticks - startTime.Ticks) / 10000000f;
				animationProgress = elapsed / CurrentAnimation.duration;

				float t = animationProgress;
				if (CurrentAnimation.easeIn && CurrentAnimation.easeOut)
				{
					t = EaseInOut(animationProgress);
				}
				else if (CurrentAnimation.easeIn)
				{
					t = DoEaseIn(animationProgress);
				}
				else if (CurrentAnimation.easeOut)
				{
					t = DoEaseOut(animationProgress);
				}

				Vector3 newPos = Vector3.Lerp(start.Position, end.Position, t);
				Quaternion newRot = Quaternion.Slerp(start.Rotation, end.Rotation, t);
				float newFov = Lerp(start.fovy ?? 1, end.fovy ?? 1, t);

				CameraTransform newTransform = new CameraTransform(newPos, newRot, newFov);

				SetCamera(newTransform);

				Thread.Sleep(8);
			}

			SetCamera(end);

			Dispatcher.Invoke(() => { startButton.Content = "Start"; });
			animationProgress = 0;
			isAnimating = false;
		}


		private static float Lerp(float p0, float p1, float t)
		{
			return p0 + t * (p1 - p0);
		}

		/// <summary>
		/// Lerps t from 0-1 only
		/// </summary>
		private static float DoEaseIn(float t)
		{
			return MathF.Pow(t, 3);
		}

		/// <summary>
		/// Lerps t from 0-1 only
		/// </summary>
		private static float DoEaseOut(float t)
		{
			t = 1 - t;
			return 1 - MathF.Pow(t, 3);
		}


		/// <summary>
		/// Lerps t from 0-1 only
		/// </summary>
		private static float EaseInOut(float t)
		{
			return DoEaseIn(t) + (DoEaseOut(t) - DoEaseIn(t)) * t;
		}

		private void ReadXYZ(object sender, RoutedEventArgs e)
		{
			if (Program.lastFrame == null) return;

			(Vector3 p, Quaternion q) = Program.lastFrame.GetCameraTransform();
			manualTransform = new CameraTransform(p, q, lastSetFov);
			SetSliderPositions(manualTransform);
		}

		private void SetSliderPositions(CameraTransform t)
		{
			if (t == null) return;

			sliderListenersActivated = false;

			try
			{
				xSlider.Value = t.px ?? 0;
				ySlider.Value = t.py ?? 0;
				zSlider.Value = t.pz ?? 0;

				xRotSlider.Value = t.qx ?? 0;
				yRotSlider.Value = t.qy ?? 0;
				zRotSlider.Value = t.qz ?? 0;
				wRotSlider.Value = t.qw ?? 1;

				// this can't be gotten from the API
				// fovSlider.Value = manualTransform.fovy ?? 1;
				fovSlider.Value = lastSetFov;
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
				CameraTransform transform = new CameraTransform()
				{
					px = xPos,
					py = yPos,
					pz = zPos,
					qx = xRot,
					qy = yRot,
					qz = zRot,
					qw = wRot,
					fovy = fov,
				};

				SetCamera(transform);
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
					SetCamera(wp.Value);
					RegenerateWaypointButtons();
					SetSliderPositions(wp.Value);
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
			durationInput.Text = CurrentAnimation.duration.ToString(CultureInfo.InvariantCulture);
			EaseInCheckbox.IsChecked = CurrentAnimation.easeIn;
			EaseOutCheckbox.IsChecked = CurrentAnimation.easeOut;

			KeyframesList.Children.Clear();
			for (int i = 0; i < CurrentAnimation.keyframes.Count; i++)
			{
				CameraTransform wp = CurrentAnimation.keyframes[i];

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
					if (Program.lastFrame == null) return;

					(Vector3 p, Quaternion q) = Program.lastFrame.GetCameraTransform();
					CurrentAnimation.keyframes[CurrentAnimation.keyframes.FindIndex(val => val == wp)] =
						new CameraTransform(p, q, lastSetFov);

					// update the UI with the new waypoint
					RegenerateKeyframeButtons();
				};
				Button goTo = new Button
				{
					Content = "Go To",
					Margin = new Thickness(5, 0, 0, 0),
				};
				goTo.Click += (_, _) =>
				{
					SetCamera(wp);
					RegenerateKeyframeButtons();
					SetSliderPositions(wp);
				};
				Button delete = new Button
				{
					Content = "X",
					Margin = new Thickness(5, 0, 5, 0),
				};
				delete.Click += (_, _) =>
				{
					CurrentAnimation.keyframes.Remove(wp);
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

			if (Program.lastFrame == null) return;

			(Vector3 p, Quaternion q) = Program.lastFrame.GetCameraTransform();
			CameraWriteSettings.instance.waypoints[keyName] = new CameraTransform(p, q, lastSetFov);

			// update the UI with the new waypoint
			RegenerateWaypointButtons();

			CameraWriteSettings.instance.Save();
		}


		private void WindowClosed(object sender, EventArgs e)
		{
			CameraWriteSettings.instance.Save();

			isAnimating = false;
			joystickDevice.Stop();
		}

		private void AddKeyframe(object sender, RoutedEventArgs e)
		{
			if (Program.lastFrame == null) return;

			(Vector3 p, Quaternion q) = Program.lastFrame.GetCameraTransform();
			CurrentAnimation.keyframes.Add(new CameraTransform(p, q, lastSetFov));

			// update the UI with the new waypoint
			RegenerateKeyframeButtons();
		}

		private void ClearKeyframes(object sender, RoutedEventArgs e)
		{
			CurrentAnimation.keyframes.Clear();
			RegenerateKeyframeButtons();
		}

		private void ToggleOrbitDisc(object sender, RoutedEventArgs e)
		{
			OrbitDisc(!discOrbit.Enabled);
		}

		public void OrbitDisc(bool enabled)
		{
			// just in case this is called from the api.
			Dispatcher.Invoke(() =>
			{
				discOrbit.Enabled = enabled;
				IsOrbitingCheckbox.IsChecked = discOrbit.Enabled;
			});
		}

		private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
		{
			try
			{
				Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
				e.Handled = true;
			}
			catch (Exception ex)
			{
				Logger.LogRow(Logger.LogType.Error, ex.ToString());
			}
		}


		private void InstallLaunchWriteAPI(object sender, RoutedEventArgs e)
		{
			if (!File.Exists(WriteApiExePath))
			{
				try
				{
					InstallWriteApiButton.Content = "Installing...";
					Task.Run(async () =>
					{
						HttpResponseMessage response = await FetchUtils.client.GetAsync("https://github.com/NtsFranz/WriteAPI/releases/download/v1.1.1/WriteAPI.zip");
						string fileName = Path.Combine(Path.GetTempPath(), "WriteAPI.zip");
						await using (FileStream fs = new FileStream(fileName, FileMode.Create))
						{
							await response.Content.CopyToAsync(fs);
						}

						string installFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "WriteAPI");
						if (!Directory.Exists(installFolder))
						{
							Directory.CreateDirectory(installFolder);
						}

						await Task.Run(() => Directory.Delete(installFolder, true));
						await Task.Run(() => ZipFile.ExtractToDirectory(fileName, installFolder));


						Dispatcher.Invoke(() => { InstallWriteApiButton.Content = "Launch WriteAPI"; });
					});
				}
				catch (Exception ex)
				{
					Logger.LogRow(Logger.LogType.Error, ex.ToString());
				}
			}


			if (File.Exists(WriteApiExePath))
			{
				try
				{
					Process.Start(new ProcessStartInfo(WriteApiExePath) { UseShellExecute = true });
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
					((ComboBoxItem)AnimationsComboBox.SelectedItem).Content.ToString();
				AnimationNameTextBox.Text = CameraWriteSettings.instance.activeAnimation;

				if (!CameraWriteSettings.animations.ContainsKey(CameraWriteSettings.instance.activeAnimation))
				{
					new MessageBox("Error 4853: Report this to NtsFranz immediately, or else. >:|").Show();
					return;
				}

				CurrentAnimation = CameraWriteSettings.animations[CameraWriteSettings.instance.activeAnimation];
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
				CameraWriteSettings.animations[newName] = CurrentAnimation;

				RefreshAnimationsComboBoxFromSettings();

				CameraWriteSettings.instance.Save();
			}
		}

		private void AnimationDeleteClicked(object sender, RoutedEventArgs e)
		{
			string name = AnimationNameTextBox.Text;
			CameraWriteSettings.animations.Remove(name);
			if (CameraWriteSettings.animations.Count > 0)
			{
				CameraWriteSettings.instance.activeAnimation = CameraWriteSettings.animations.Keys.First();
				CurrentAnimation = CameraWriteSettings.animations[CameraWriteSettings.instance.activeAnimation];
			}

			RefreshAnimationsComboBoxFromSettings();
			RegenerateKeyframeButtons();
		}

		private void NewAnimationClicked(object sender, RoutedEventArgs e)
		{
			AnimationNameTextBox.Text = "Untitled Animation";
			string newName = AnimationNameTextBox.Text;


			CurrentAnimation = new AnimationKeyframes();
			CameraWriteSettings.instance.activeAnimation = newName;
			CameraWriteSettings.animations[newName] = CurrentAnimation;

			RefreshAnimationsComboBoxFromSettings();
			RegenerateKeyframeButtons();

			CameraWriteSettings.instance.Save();
		}

		private void Toggle3DMouse(object sender, RoutedEventArgs e)
		{
			spaceMouseInput.Enabled = !spaceMouseInput.Enabled;
			SpaceMouseCheckBox.IsChecked = spaceMouseInput.Enabled;
		}

		public static void SetCamera(CameraTransform output, bool mirror = false)
		{
			if (output == null) return;
			if (output.qx != null)
			{
				output.Rotation = Quaternion.Normalize(output.Rotation);
				if (mirror)
				{
					output.qy *= -1;
					output.qz *= -1;
				}
			}

			if (output.fovy != null)
			{
				lastSetFov = output.fovy ?? 1;
			}

			CameraWriteController.SetCameraMode(CameraWriteController.CameraMode.api);
			CameraWriteController.SetCameraTransform(output);
		}

		private bool xPlaneInputActive;
		private AircraftState aircraftState = new AircraftState();
		private Vector3 aircraftOrigin;

		private void ToggleXPlaneCamera(object sender, RoutedEventArgs e)
		{
			if (xPlaneInputActive)
			{
				xPlaneInputActive = false;
				XPlaneCameraCheckBox.IsChecked = false;
			}
			else
			{
				xPlaneInputActive = true;
				Thread xPlaneInput = new Thread(XPlaneInputThread);
				xPlaneInput.Start();
				XPlaneCameraCheckBox.IsChecked = true;
			}
		}

		private void XPlaneInputThread()
		{
			UdpClient client = new UdpClient(49003);
			while (xPlaneInputActive)
			{
				try
				{
					// Receive Bytes
					IPEndPoint connectionIP = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 49003);
					byte[] bytes = client.Receive(ref connectionIP);

					string packetType = Encoding.UTF8.GetString(bytes.Take(4).ToArray());

					if (packetType == "DATA")
					{
						Debug.WriteLine("Got DATA");
						GetData(bytes.Skip(5).ToArray());
						SetCamera(new CameraTransform(
							(aircraftState.GetXYZPosition(Vector3.Zero) - aircraftOrigin) * CameraWriteSettings.instance.xPlanePosMultiplier,
							aircraftState.Rotation));
					}
					else if (packetType == "RPOS")
					{
						Debug.WriteLine("Got DATA");
						GetRPOS(bytes.Skip(5).ToArray());
						SetCamera(new CameraTransform(
							(aircraftState.GetXYZPosition(Vector3.Zero) - aircraftOrigin) * CameraWriteSettings.instance.xPlanePosMultiplier,
							aircraftState.Rotation));
					}
				}
				catch (Exception)
				{
					//
				}
			}
		}

		/// <summary>
		/// Process the data from a "RPOS" field.
		/// </summary>
		/// <param name="bytes">The data not including label and null.</param>
		private void GetRPOS(byte[] bytes)
		{
			if (bytes.Length < 64)
			{
				return;
			}

			aircraftState.longitude = BitConverter.ToDouble(bytes, 0);
			aircraftState.latitude = BitConverter.ToDouble(bytes, 8);
			aircraftState.altitude = BitConverter.ToDouble(bytes, 16);
			aircraftState.pitch = BitConverter.ToSingle(bytes, 28);
			aircraftState.heading = BitConverter.ToSingle(bytes, 32);
			aircraftState.roll = BitConverter.ToSingle(bytes, 36);
			aircraftState.velocity = new Vector3(
				BitConverter.ToSingle(bytes, 48),
				BitConverter.ToSingle(bytes, 44),
				BitConverter.ToSingle(bytes, 40));
			aircraftState.angVelocity = new Vector3(
				BitConverter.ToSingle(bytes, 60),
				BitConverter.ToSingle(bytes, 56),
				BitConverter.ToSingle(bytes, 52));
		}

		/// <summary>
		/// Process the data from a "DATA" field.
		/// </summary>
		/// <param name="bytes">The data not including label and null.</param>
		private void GetData(byte[] bytes)
		{
			for (int i = 0; i < bytes.Length / 36; i++)
			{
				int index = BitConverter.ToInt32(bytes, 36 * i);
				float[] floats = new float[8];
				for (int j = 0; j < 8; j++)
				{
					floats[j] = BitConverter.ToSingle(bytes, (36 * i) + 4 + (j * 4));
				}

				switch (index)
				{
					// pitch, roll, heading
					case 17:
						aircraftState.pitch = floats[0];
						aircraftState.roll = floats[1];
						aircraftState.heading = floats[2];
						break;
					// lat, long, alt
					case 20:
						aircraftState.latitude = floats[0];
						aircraftState.longitude = floats[1];
						aircraftState.altitude = floats[2];
						break;
					// loc, vel, dist traveled
					case 21:
						aircraftState.position = new Vector3(-floats[0], floats[1], floats[2]);
						aircraftState.velocity = new Vector3(-floats[3], floats[4], floats[5]);
						if (MathF.Abs(aircraftState.velocity.X) < .001)
						{
							aircraftState.velocity.X = 0;
						}

						if (MathF.Abs(aircraftState.velocity.Y) < .001)
						{
							aircraftState.velocity.Y = 0;
						}

						if (MathF.Abs(aircraftState.velocity.Z) < .001)
						{
							aircraftState.velocity.Z = 0;
						}

						break;
					// angular velocity
					case 16:
						aircraftState.angVelocity = new Vector3(floats[0], floats[2], floats[1]);
						break;
				}
			}
		}

		public static class GeoUtils
		{
			public const double lat2Y = -110918.3;
			public const double long2X = -92439.5;
			public const double feet2Meters = 3.28084;
			public const double lon2tile_factor = 16 / 0.087948;
			public const double lat2tile_factor = 16 / 0.072836;
			public const float metersPerSecondToKnots = 1.94384f;
			public const float metersPerSecondToFtPerMin = 196.85f;
			public const float metersToFt = 3.28084f;
			public const float secondsToFtTimesKnots = .4975f;

			public static int lon2tile(double lon)
			{
				float rawVal = (float)(lon * lon2tile_factor + 32750.1010597);
				return (int)MathF.Round(rawVal / 16) * 16;
			}

			public static int lat2tile(double lat)
			{
				float rawVal = (float)(lat * lat2tile_factor + 18711.6072272);
				return (int)MathF.Round(rawVal / 16) * 16;
			}

			// lat
			// 33.943364
			// 34.016200
			// avg = 33.979782
			// tile = 26176

			// lon
			//-83.320292
			//-83.408240
			// avg = -83.364266
			// tile = 17584

			public static Vector3 LatLon2M(Vector3 origin, Vector3 latlon)
			{
				return new Vector3(
					(float)((latlon.X - origin.Z) * long2X),
					(float)((latlon.Y - origin.Y) / feet2Meters),
					(float)((latlon.Z - origin.X) * lat2Y));
			}

			public static Vector2 LatLon2M(Vector2 origin, Vector2 latlon)
			{
				return new Vector2(
					(float)((latlon.X - origin.X) * long2X),
					(float)((latlon.Y - origin.Y) * lat2Y));
			}
		}

		/// <summary>
		/// Saves the values for the current aircraft state in conventional units for easier use
		/// </summary>
		public class AircraftState
		{
			/// <summary>
			/// raw latitude from X-Plane
			/// </summary>
			public double latitude;

			/// <summary>
			/// raw latitude from X-Plane
			/// </summary>
			public double longitude;

			/// <summary>
			/// Elevation in m (above ground in Unity)
			/// </summary>
			public double altitude;

			public float pitch;
			public float roll;
			public float heading;
			public float airspeed;
			public Vector3 velocity;
			public Vector3 position;
			public Vector3 angVelocity;

			// contains the number of times the ground must be moved away from the plane
			public Vector3 centeringMult;

			public Quaternion Rotation
			{
				get { return Quaternion.CreateFromYawPitchRoll(-heading * CameraWriteController.Deg2Rad, -pitch * CameraWriteController.Deg2Rad, roll * CameraWriteController.Deg2Rad); }
			}

			public Vector3 Position
			{
				get { return new Vector3((float)longitude, (float)altitude, (float)latitude); }
			}

			// assumes origin is in lat, lon, alt format
			public Vector3 GetLatLongPosition(double lat, double lon, double alt)
			{
				return new Vector3(
					(float)((longitude - lon) * GeoUtils.long2X),
					(float)((altitude - alt)),
					(float)((latitude - lat) * GeoUtils.lat2Y));
			}

			public Vector3 GetXYZPosition(Vector3 origin)
			{
				// pos is in lon, alt, lat
				// origin is in lat, lon, alt
				Vector3 pos = origin;
				pos.Z = origin.X;
				pos.Y = origin.Z;
				pos.X = origin.Y;
				pos.X += (float)longitude;
				pos.Z -= (float)latitude;
				pos.Y += (float)altitude;
				pos.Z *= (float)GeoUtils.lat2Y;
				pos.X *= (float)GeoUtils.long2X;

				return pos;
				//return new Vector3(pos.X, pos.Y, pos.X);

				//Vector3 pos = position - origin;

				//centeringMult.x = -(int)(pos.x / 1000);
				//centeringMult.y = -(int)(pos.y / 1000);
				//centeringMult.z = -(int)(pos.z / 1000);

				//pos.x %= 1000;
				//pos.y %= 1000;
				//pos.z %= 1000;

				//return pos;
			}
		}

		private void ResetXPlanePosition(object sender, RoutedEventArgs e)
		{
			aircraftOrigin = aircraftState.GetXYZPosition(Vector3.Zero);
		}

		private HIDDeviceInput joystickDevice = new HIDDeviceInput(1103, 45322);

		private struct JoystickValues
		{
			public float x;
			public float y;
			public float spin;
			public float slider;
		}

		private JoystickValues joystickValues;
		private CameraTransform joystickCameraTransform = new CameraTransform();

		private void ToggleJoystickInput(object sender, RoutedEventArgs e)
		{
			if (joystickDevice.Running)
			{
				JoystickInputCheckBox.IsChecked = false;

				joystickDevice?.Stop();
			}
			else
			{
				joystickDevice.OnChanged += bytes =>
				{
					joystickValues.x = (short)((((bytes[5] << 8) | bytes[4]) >> 2) - 2048) / 2048f;
					joystickValues.y = (short)((((bytes[7] << 8) | bytes[6]) >> 2) - 2048) / 2048f;
					joystickValues.spin = -(bytes[8] / 255f) + .5f;
					joystickValues.slider = -(bytes[9] / 127f) + 1f;


					Dispatcher.Invoke(() =>
					{
						joyInputPosX.Value = joystickValues.x;
						joyInputPosY.Value = joystickValues.y;
						joyInputPosZ.Value = joystickValues.spin;
						joyInputRotX.Value = joystickValues.slider;
					});
				};
				joystickDevice.Start();
				JoystickInputCheckBox.IsChecked = true;
				Thread joystickInputThread = new Thread(JoystickInputThread);
				joystickInputThread.Start();

				FetchUtils.GetRequestCallback(sessionUrl, null, response =>
				{
					if (response == null) return;
					Frame frame = JsonConvert.DeserializeObject<Frame>(response);
					if (frame == null) return;
					(Vector3 p, Quaternion q) = frame.GetCameraTransform();
					joystickCameraTransform = new CameraTransform(p, q, lastSetFov);
				});
			}
		}

		private void JoystickInputThread()
		{
			while (joystickDevice.Running)
			{
				float x = CameraWriteController.Exponential(joystickValues.x * -CameraWriteSettings.instance.joystickMoveSpeed, CameraWriteSettings.instance.joystickMoveExponential);
				float y = CameraWriteController.Exponential(joystickValues.y * -CameraWriteSettings.instance.joystickMoveSpeed, CameraWriteSettings.instance.joystickMoveExponential);
				float spin = CameraWriteController.Exponential(joystickValues.spin * CameraWriteSettings.instance.joystickRotateSpeed, CameraWriteSettings.instance.joystickRotateExponential);

				Vector3 translateBy = new Vector3(x, 0, y);
				Quaternion rotateBy = Quaternion.CreateFromYawPitchRoll(spin, 0, 0);

				Matrix4x4 camPosMatrix = Matrix4x4.CreateFromQuaternion(joystickCameraTransform.Rotation);

				joystickCameraTransform.Position += Vector3.Transform(translateBy, camPosMatrix);
				joystickCameraTransform.py = joystickValues.slider * 10;
				joystickCameraTransform.Rotation = Quaternion.Multiply(joystickCameraTransform.Rotation, rotateBy);

				SetCamera(joystickCameraTransform);
				Thread.Sleep(8);
			}
		}


		private void ToggleKeyboardShortcuts(object sender, RoutedEventArgs e)
		{
			CameraWriteSettings.instance.enableHotKeys = !CameraWriteSettings.instance.enableHotKeys;
			EnableKeyboardShortcutsCheckbox.IsChecked = CameraWriteSettings.instance.enableHotKeys;
			EnableHotKeys(CameraWriteSettings.instance.enableHotKeys);
		}

		private void DischolderPOVSelectionChanged(object sender, SelectionChangedEventArgs e)
		{
		}

		private void ToggleControllableSideline(object sender, RoutedEventArgs e)
		{
			controllableSideline.Enabled = !controllableSideline.Enabled;
			ControllableSidelineCheckBox.IsChecked = controllableSideline.Enabled;
		}

		private void AnimationSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
		{
			animationSliderManipulating = true;
			animationProgress = (float)e.NewValue;

			UpdateCameraForAnimation();
		}
	}

	public class CameraSettingBindingExtension : Binding
	{
		public CameraSettingBindingExtension()
		{
			Initialize();
		}

		public CameraSettingBindingExtension(string path) : base(path)
		{
			Initialize();
		}

		private void Initialize()
		{
			Source = CameraWriteSettings.instance;
			Mode = BindingMode.TwoWay;
		}
	}
}