using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Threading.Tasks;
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

		public readonly Dictionary<string, List<TriggerRegion>> checkpoints = new Dictionary<string, List<TriggerRegion>>()
		{
			{
				"mpl_combat_combustion", new List<TriggerRegion>()
				{
					new TriggerRegion()
					{
						position = new Vector3(-69.35f, 0.58f, 18.19f),
						eulerAngles = new Vector3(0, -45f, 0),
						scale = new Vector3(8.210141f, 4.250662f, 4.550486f),
					},
					new TriggerRegion()
					{
						position = new Vector3(-29.556f, 3.3607f, 39.4439f),
						eulerAngles = new Vector3(0, -45f, 0),
						scale = new Vector3(19.08612f, 6.611905f, 1f),
					},
					new TriggerRegion()
					{
						position = new Vector3(42.07f, 1.8442f, 37.98f),
						eulerAngles = new Vector3(0, -45f, 0),
						scale = new Vector3(15.05699f, 11.8406f, 1f),
					},
					new TriggerRegion()
					{
						position = new Vector3(21.05f, -0.952f, -0.26f),
						eulerAngles = new Vector3(0, 45f, 0),
						scale = new Vector3(9.4268f, 4.64905f, 1f),
					},
					new TriggerRegion()
					{
						position = new Vector3(-10.01f, -0.2403f, -11.11f),
						eulerAngles = new Vector3(0, 135f, 0),
						scale = new Vector3(9.4268f, 6.072589f, 1f),
					},
					new TriggerRegion()
					{
						position = new Vector3(-30.19f, 2.96f, 16.55f),
						eulerAngles = new Vector3(0, 90f, 0),
						scale = new Vector3(9.4268f, 6.072589f, 1f),
					},
					new TriggerRegion()
					{
						position = new Vector3(-67.5f, 0.5f, 31.73f),
						eulerAngles = new Vector3(0, 135f, 0),
						scale = new Vector3(9.4268f, 6.072589f, 4f),
					},
				}
			}
		};

		[Serializable]
		public class TriggerRegion
		{
			public Vector3 position;
			public Vector3 eulerAngles;
			public Vector3 scale;
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
			if (!f.InCombat) return;

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
					List<TriggerRegion> checkpointsList = checkpoints[f.map_name];
					if (InTrigger(f, checkpointsList[checkpointIndex]))
					{
						splitTimes.Add((float)stopwatch.Elapsed.TotalSeconds);

						// if last checkpoint
						if (checkpointIndex == checkpointsList.Count - 1)
						{
							stopwatch.Stop();
							Program.synth.SpeakAsync($"Finished! Your time was {stopwatch.Elapsed.TotalSeconds:N1} seconds");
							previousRaces.Add(new RaceResults()
							{
								finalTime = (float)stopwatch.Elapsed.TotalSeconds,
								mapName = f.map_name,
								players = new[] { f.client_name },
								splits = splitTimes.ToArray(),
							});
							checkpointIndex = 0;
						}
						else
						{
							Program.synth.SpeakAsync("Checkpoint " + checkpointIndex);
							checkpointIndex++;
						}
					}

					break;
				}
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		private static bool InTrigger(Frame f, TriggerRegion region)
		{
			// TODO
			return false;
		}

		private void Start()
		{
			stopwatch.Restart();
		}
	}
}