using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Threading.Tasks;
using System.Windows.Media;
using EchoVRAPI;

namespace Spark
{
	public class EchoGPController
	{
		/// <summary>
		/// Enables or disables this whole feature
		/// </summary>
		public bool active;

		public readonly Stopwatch stopwatch = new Stopwatch();
		public readonly List<float> splitTimes = new List<float>();
		public readonly List<RaceResults> previousRaces = new List<RaceResults>();

		public enum State
		{
			NotReady, // not in the correct map or not connected
			NotInStartingArea, // in a correct map, but not racing and not in starting area
			InStartingArea, // just waiting to cross the start line
			Racing, // timer going
		}

		public State state = State.NotReady;
		public int checkpointIndex = -1;

		public readonly Dictionary<string, List<TriggerSphere>> checkpoints = new Dictionary<string, List<TriggerSphere>>()
		{
			// {
			// 	"mpl_combat_combustion_boxes", new List<TriggerRegion>()
			// 	{
			// 		new TriggerRegion()
			// 		{
			// 			position = new Vector3(-69.35f, 0.58f, 18.19f),
			// 			eulerAngles = new Vector3(0, -45f, 0),
			// 			scale = new Vector3(8.210141f, 4.250662f, 4.550486f),
			// 		},
			// 		new TriggerRegion()
			// 		{
			// 			position = new Vector3(-29.556f, 3.3607f, 39.4439f),
			// 			eulerAngles = new Vector3(0, -45f, 0),
			// 			scale = new Vector3(19.08612f, 6.611905f, 1f),
			// 		},
			// 		new TriggerRegion()
			// 		{
			// 			position = new Vector3(42.07f, 1.8442f, 37.98f),
			// 			eulerAngles = new Vector3(0, -45f, 0),
			// 			scale = new Vector3(15.05699f, 11.8406f, 1f),
			// 		},
			// 		new TriggerRegion()
			// 		{
			// 			position = new Vector3(21.05f, -0.952f, -0.26f),
			// 			eulerAngles = new Vector3(0, 45f, 0),
			// 			scale = new Vector3(9.4268f, 4.64905f, 1f),
			// 		},
			// 		new TriggerRegion()
			// 		{
			// 			position = new Vector3(-10.01f, -0.2403f, -11.11f),
			// 			eulerAngles = new Vector3(0, 135f, 0),
			// 			scale = new Vector3(9.4268f, 6.072589f, 1f),
			// 		},
			// 		new TriggerRegion()
			// 		{
			// 			position = new Vector3(-30.19f, 2.96f, 16.55f),
			// 			eulerAngles = new Vector3(0, 90f, 0),
			// 			scale = new Vector3(9.4268f, 6.072589f, 1f),
			// 		},
			// 		new TriggerRegion()
			// 		{
			// 			position = new Vector3(-67.8064f, 0.4292f, 31.4236f),
			// 			eulerAngles = new Vector3(0, 135f, 0),
			// 			scale = new Vector3(4.021927f, 4.383803f, 4f),
			// 		},
			// 	}
			// },
			{
				"mpl_combat_combustion", new List<TriggerSphere>()
				{
					new TriggerSphere()
					{
						position = new Vector3(-69.35f, 0.58f, 18.19f),
						radius = 8.210141f
					},
					new TriggerSphere()
					{
						position = new Vector3(-28.4f, 3.36f, 38.3f),
						radius = 19.08612f
					},
					new TriggerSphere()
					{
						position = new Vector3(43.6f, 1.8442f, 36.45f),
						radius = 15
					},
					new TriggerSphere()
					{
						position = new Vector3(21.05f, -0.952f, -0.26f),
						radius = 9.4268f
					},
					new TriggerSphere()
					{
						position = new Vector3(-10.01f, -0.2403f, -11.11f),
						radius = 9.4268f,
					},
					new TriggerSphere()
					{
						position = new Vector3(-30.19f, 2.96f, 16.55f),
						radius = 9.4268f,
					},
					new TriggerSphere()
					{
						position = new Vector3(-67.8064f, 0.4292f, 31.4236f),
						radius = 4
					},
				}
			},
			{
				"mpl_arena_a", new List<TriggerSphere>()
				{
					new TriggerSphere()
					{
						position = new Vector3(91f, 0, 0),
						radius = 100f
					},

					new TriggerSphere()
					{
						position = new Vector3(-50f, 0, 0),
						radius = 25
					},
					new TriggerSphere()
					{
						position = new Vector3(50f, 0, 0),
						radius = 25
					},

					new TriggerSphere()
					{
						position = new Vector3(-50f, 0, 0),
						radius = 25
					},
					new TriggerSphere()
					{
						position = new Vector3(50f, 0, 0),
						radius = 25
					},
				}
			}
		};

		[Serializable]
		[Obsolete]
		public class TriggerRegion
		{
			public Vector3 position;
			public Vector3 eulerAngles;
			public Vector3 scale;
		}

		[Serializable]
		public class TriggerSphere
		{
			public Vector3 position;
			public float radius;
		}

		[Serializable]
		public class RaceResults
		{
			public float finalTime;
			public string mapName;
			public string[] players;
			public float[] splits;
		}

		public EchoGPController()
		{
			_ = Task.Run(() =>
			{
				Program.NewFrame += ProcessFrame;
				Program.LeftGame += LeftGame;
				Program.JoinedGame += JoinedGame;
			});
		}

		private void JoinedGame(Frame f)
		{
			state = State.NotReady;
			if (checkpoints.ContainsKey(f.map_name))
			{
				state = State.NotInStartingArea;
			}
		}

		private void LeftGame(Frame obj)
		{
			// cancel
			Cancel();
		}

		public void Cancel()
		{
			state = State.NotReady;
			stopwatch.Stop();
			stopwatch.Reset();
		}

		private void ProcessFrame(Frame f)
		{
			if (!active)
			{
				state = State.NotReady;
				return;
			}

			// if we don't have a checkpoints list for this map
			if (!checkpoints.ContainsKey(f.map_name))
			{
				state = State.NotReady;
				return;
			}

			switch (state)
			{
				case State.NotReady:
				{
					if (checkpoints.ContainsKey(f.map_name))
					{
						state = State.NotInStartingArea;
					}

					break;
				}
				case State.NotInStartingArea:
				{
					// moved into starting area
					if (InTrigger(f, checkpoints[f.map_name][0]))
					{
						state = State.InStartingArea;
						Program.synth.SpeakAsync("Ready to start");
					}

					break;
				}
				case State.InStartingArea:
				{
					// if we left the starting area, start the timer
					if (!InTrigger(f, checkpoints[f.map_name][0]))
					{
						Program.synth.SpeakAsync("GO GO GO!");
						state = State.Racing;
						stopwatch.Restart();
						splitTimes.Clear();
						checkpointIndex = 1;
					}

					break;
				}
				case State.Racing:
				{
					List<TriggerSphere> checkpointsList = checkpoints[f.map_name];

					// if in the starting area
					if (InTrigger(f, checkpoints[f.map_name][0]))
					{
						state = State.InStartingArea;
						Program.synth.SpeakAsync("Cancelled Race. Ready in starting area.");
					}

					if (InTrigger(f, checkpointsList[checkpointIndex]))
					{
						splitTimes.Add((float)stopwatch.Elapsed.TotalSeconds);

						// if last checkpoint
						if (checkpointIndex == checkpointsList.Count - 1)
						{
							stopwatch.Stop();
							Program.synth.SpeakAsync($"Finished! Your time was {stopwatch.Elapsed.TotalSeconds:N1} seconds");
							Debug.WriteLine($"Finished! Your time was {stopwatch.Elapsed.TotalSeconds:N1} seconds");
							previousRaces.Add(new RaceResults()
							{
								finalTime = (float)stopwatch.Elapsed.TotalSeconds,
								mapName = f.map_name,
								players = new[] { f.client_name },
								splits = splitTimes.ToArray(),
							});
							state = State.NotInStartingArea;
							checkpointIndex = 0;
						}
						else
						{
							Program.synth.SpeakAsync("Checkpoint " + checkpointIndex);
							Debug.WriteLine("Checkpoint " + checkpointIndex);
							checkpointIndex++;
						}
					}

					break;
				}
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		private static bool InTrigger(Frame f, TriggerSphere region)
		{
			Player clientPlayer = f.GetPlayer(f.client_name);
			Vector3 pos = clientPlayer.head.Position;
			#if DEBUG
			if (clientPlayer.team_color == Team.TeamColor.spectator)
			{
				(pos, _) = f.GetCameraTransform();
			}
			#endif
			

			(pos.X, pos.Z) = (pos.Z, pos.X);

			float distance = Vector3.Distance(region.position, pos);
			return distance < region.radius / 2f;
		}

		private static bool InTrigger(Frame f, TriggerRegion region)
		{
			(Vector3 pos, _) = f.GetCameraTransform();

			(pos.X, pos.Z) = (pos.Z, pos.X);

			float distance = Vector3.Distance(region.position, pos);
			float maxRadius = MathF.Max(MathF.Max(region.scale.X, region.scale.Y), region.scale.Z);

			return distance < maxRadius;
		}

		private static bool InTrigger3(Frame f, TriggerRegion region)
		{
			// https://stackoverflow.com/a/53559963

			Vector3 I = region.position;
			(Vector3 P, Quaternion rotation) = f.GetCameraTransform();

			// var xLocal = 

			// b1,b2,b3,b4,t1,t2,t3,t4 = cube3d
			//
			// dir1 = (t1-b1)
			// size1 = np.linalg.norm(dir1)
			// dir1 = dir1 / size1
			//
			// dir2 = (b2-b1)
			// size2 = np.linalg.norm(dir2)
			// dir2 = dir2 / size2
			//
			// dir3 = (b4-b1)
			// size3 = np.linalg.norm(dir3)
			// dir3 = dir3 / size3
			//
			// cube3d_center = (b1 + t3)/2.0
			//
			// dir_vec = points - cube3d_center
			//
			// res1 = np.where( (np.absolute(np.dot(dir_vec, dir1)) * 2) > size1 )[0]
			// res2 = np.where( (np.absolute(np.dot(dir_vec, dir2)) * 2) > size2 )[0]
			// res3 = np.where( (np.absolute(np.dot(dir_vec, dir3)) * 2) > size3 )[0]
			//
			// return list( set().union(res1, res2, res3) )
			return false;
		}

		private static bool InTrigger2(Frame f, TriggerRegion region)
		{
			(Vector3 pos, Quaternion rot) = f.GetCameraTransform();

			(pos.X, pos.Z) = (pos.Z, pos.X);


			Vector3 localSpace = InverseTransformPoint(region.position, region.eulerAngles, Vector3.One, pos);

			// Matrix4x4 rotMatrix = GetRotationMatrix(region.eulerAngles);
			// Matrix4x4.Invert(rotMatrix, out Matrix4x4 inverse);
			// Vector3 localSpace = Vector3.Transform(pos - region.position, inverse);

			return MathF.Abs(localSpace.X) < region.scale.X &&
			       MathF.Abs(localSpace.Y) < region.scale.Y &&
			       MathF.Abs(localSpace.Z) < region.scale.Z;

			// return MathF.Abs(localSpace.X) < 1 &&
			//        MathF.Abs(localSpace.Y) < 1 &&
			//        MathF.Abs(localSpace.Z) < 1;
		}

		private void Start()
		{
			stopwatch.Restart();
		}

		private static Vector3 InverseTransformPoint(Vector3 transformPos, Vector3 transformRotation, Vector3 transformScale, Vector3 pos)
		{
			Matrix4x4 trs = Get_TRS_Matrix(transformPos, transformRotation, transformScale);
			Matrix4x4.Invert(trs, out Matrix4x4 inverse);
			return Vector3.Transform(pos, inverse);
		}


		private static float ConvertDegToRad(float degrees)
		{
			return ((float)Math.PI / (float)180) * degrees;
		}

		private static Matrix4x4 GetTranslationMatrix(Vector3 position)
		{
			return new Matrix4x4(
				1, 0, 0, 0,
				0, 1, 0, 0,
				0, 0, 1, 0,
				position.X, position.Y, position.Z, 1
			);
		}

		private static Matrix4x4 GetRotationMatrix(Vector3 anglesDeg)
		{
			anglesDeg = new Vector3(ConvertDegToRad(anglesDeg.X), ConvertDegToRad(anglesDeg.Y), ConvertDegToRad(anglesDeg.Z));

			Matrix4x4 rotationX = new Matrix4x4(
				1, 0, 0, 0,
				0, MathF.Cos(anglesDeg.X), MathF.Sin(anglesDeg.X), 0,
				0, -MathF.Sin(anglesDeg.X), MathF.Cos(anglesDeg.X), 0,
				0, 0, 0, 1);

			Matrix4x4 rotationY = new Matrix4x4(
				MathF.Cos(anglesDeg.Y), 0, -MathF.Sin(anglesDeg.Y), 0,
				0, 1, 0, 0,
				MathF.Sin(anglesDeg.Y), 0, MathF.Cos(anglesDeg.Y), 0,
				0, 0, 0, 1);

			Matrix4x4 rotationZ = new Matrix4x4(
				MathF.Cos(anglesDeg.Z), MathF.Sin(anglesDeg.Z), 0, 0,
				-MathF.Sin(anglesDeg.Z), MathF.Cos(anglesDeg.Z), 0, 0,
				0, 0, 1, 0,
				0, 0, 0, 1);

			return rotationX * rotationY * rotationZ;
		}

		private static Matrix4x4 GetScaleMatrix(Vector3 scale)
		{
			return new Matrix4x4(
				scale.X, 0, 0, 0,
				0, scale.Y, 0, 0,
				0, 0, scale.Z, 0,
				0, 0, 0, 1
			);
		}

		private static Matrix4x4 Get_TRS_Matrix(Vector3 position, Vector3 rotationAngles, Vector3 scale)
		{
			return GetTranslationMatrix(position) * GetRotationMatrix(rotationAngles) * GetScaleMatrix(scale);
		}
	}
}