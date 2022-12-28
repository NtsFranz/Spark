using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using EchoVRAPI;
using Newtonsoft.Json;

namespace Spark
{
	public class CameraController
	{
		public enum Backend
		{
			Builtin,
			WriteAPI
		}

		public Backend backend;

		public Func<CameraTransform, float, Task> OnUpdate;
		public readonly List<Func<CameraTransform, float, Task>> updateCallbacks = new List<Func<CameraTransform, float, Task>>();
		public readonly CameraTransform cameraTransform;
		public int fps = 60;
		private int fetchFailDelayMS = 500;
		private readonly Stopwatch sw = new Stopwatch();
		private readonly Stopwatch frameTimer = new Stopwatch();

		public CameraController()
		{
			backend = Backend.Builtin;
			cameraTransform = new CameraTransform();

			Task.Run(UpdateThread);
		}

		private async Task UpdateThread()
		{
			while (Program.running)
			{
				sw.Restart();

				// if no listeners, don't bother with other stuff
				if (updateCallbacks.Count == 0)
				{
					Thread.Sleep(fetchFailDelayMS);
					continue;
				}

				// fetch the state from the game
				// if (await GetCameraFromGame()) continue;

				// modify the state 
				Stopwatch sw2 = Stopwatch.StartNew();
				try
				{
					foreach (Func<CameraTransform, float, Task> updateCallback in updateCallbacks)
					{
						await updateCallback(cameraTransform, (float)frameTimer.Elapsed.TotalSeconds);
					}

					// for debugging frame time consistency
					// Debug.WriteLine(frameTimer.ElapsedMilliseconds);
					frameTimer.Restart();
				}
				catch (Exception)
				{
				}

				Debug.WriteLine(sw.ElapsedMilliseconds);

				// write the current state to the game
				switch (backend)
				{
					case Backend.Builtin:
					{
						string resp = await CameraWriteController.SetCameraTransformAsync(cameraTransform);
						if (string.IsNullOrEmpty(resp))
						{
							Thread.Sleep(fetchFailDelayMS);
							continue;
						}

						CameraWriteController.SetCameraMode(CameraWriteController.CameraMode.api);
						break;
					}
					case Backend.WriteAPI:
					{
						string resp = await FetchUtils.PostRequestAsync("http://127.0.0.1:6723/camera_transform", null, JsonConvert.SerializeObject(cameraTransform));
						if (string.IsNullOrEmpty(resp))
						{
							Thread.Sleep(fetchFailDelayMS);
							continue;
						}

						break;
					}
					default:
						throw new ArgumentOutOfRangeException();
				}

				// wait for the interval minus the elapsed time used by this frame
				// this way of iterating guarantees the order of frames, and also prevents overlap of fetch spam when not in a game
				int delay = Math.Clamp((int)(1000f / fps - sw.ElapsedMilliseconds - 3), 0, 1000);
				if (delay > 0)
				{
					Thread.Sleep(delay);
				}
			}
		}

		private async Task<bool> GetCameraFromGame()
		{
			switch (backend)
			{
				case Backend.Builtin:
					string resp = await FetchUtils.GetRequestAsync("http://127.0.0.1:6721/session");
					if (string.IsNullOrEmpty(resp))
					{
						Thread.Sleep(fetchFailDelayMS);
						return true;
					}

					Frame frame = Frame.FromJSON(DateTime.UtcNow, resp, null);
					(Vector3 pos, Quaternion rot) = frame.GetCameraTransform();
					cameraTransform.Position = pos;
					cameraTransform.Rotation = rot;
					break;
				case Backend.WriteAPI:
					string camResp = await FetchUtils.GetRequestAsync("http://127.0.0.1:6723/camera_transform");
					if (string.IsNullOrEmpty(camResp))
					{
						Thread.Sleep(fetchFailDelayMS);
						return true;
					}

					CameraTransform newTransform = JsonConvert.DeserializeObject<CameraTransform>(camResp);
					cameraTransform.Position = newTransform.Position;
					cameraTransform.Rotation = newTransform.Rotation;
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}

			return false;
		}
	}
}