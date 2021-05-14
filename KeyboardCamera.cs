using Newtonsoft.Json;
using Spark.Properties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static Logger;
using static Spark.g_Team;

namespace Spark
{
	class KeyboardCamera
	{
		public KeyboardCamera()
		{
			Program.Goal += (_, _) =>
			{
				if (SparkSettings.instance.toggleMinimapAfterGoals)
				{
					Task.Run(async () =>
					{
						await Task.Delay(500);
						Program.FocusEchoVR();
						await Task.Delay(10);
						Program.FocusEchoVR();
						Keyboard.SendKey(Keyboard.DirectXKeyStrokes.DIK_M, false, Keyboard.InputType.Keyboard);
						await Task.Delay(20);
						Keyboard.SendKey(Keyboard.DirectXKeyStrokes.DIK_M, true, Keyboard.InputType.Keyboard);
						await Task.Delay(20);
						Keyboard.SendKey(Keyboard.DirectXKeyStrokes.DIK_M, false, Keyboard.InputType.Keyboard);
						await Task.Delay(20);
						Keyboard.SendKey(Keyboard.DirectXKeyStrokes.DIK_M, true, Keyboard.InputType.Keyboard);
					});
				}
			};
		}

		public static void SpectatorCamFindPlayer()
		{
			try
			{
				g_Team clientPlayerTeam = Program.lastFrame?.GetTeam(Program.lastFrame.client_name);
				if (clientPlayerTeam == null) return;
				if (clientPlayerTeam.color == TeamColor.spectator) return;
				if (Program.echoVRIP == "127.0.0.1") return;

				Task.Run(async () =>
				{
					// check if we're actually on quest and running pc spectator
					TimeSpan pcSpectatorStartupTime = TimeSpan.FromSeconds(10f);
					bool inPCSpectator = false;
					string result = string.Empty;
					while (!inPCSpectator && pcSpectatorStartupTime > TimeSpan.Zero)
					{
						result = await Program.GetRequestAsync($"http://{Program.echoVRIP}:{Program.echoVRPort}/session", null);
						if (string.IsNullOrEmpty(result))
						{
							continue;
						}

						inPCSpectator = true;
					}

					if (!inPCSpectator)
					{
						new MessageBox("You have chosen to automatically set the camera to follow the player, but you don't have EchoVR running in spectator mode on this pc.").Show();
						return;
					}

					g_Instance frame = JsonConvert.DeserializeObject<g_Instance>(result);
					if (frame == null)
					{
						new MessageBox("Failed to process frame from the local PC").Show();
						return;
					}

					if (frame.sessionid != Program.lastFrame.sessionid || Program.lastFrame.GetPlayer(frame.client_name) == null)
					{
						new MessageBox("Local PC is not in the same match as your Quest. Can't follow player.").Show();
						return;
					}

					// try to find player
					Program.FocusEchoVR();
					Keyboard.SendKey(Keyboard.DirectXKeyStrokes.DIK_P, false, Keyboard.InputType.Keyboard);
					await Task.Delay(20);
					Program.FocusEchoVR();
					Keyboard.SendKey(Keyboard.DirectXKeyStrokes.DIK_P, true, Keyboard.InputType.Keyboard);

					List<Keyboard.DirectXKeyStrokes> numbers = new List<Keyboard.DirectXKeyStrokes>
					{
						Keyboard.DirectXKeyStrokes.DIK_0,
						Keyboard.DirectXKeyStrokes.DIK_1,
						Keyboard.DirectXKeyStrokes.DIK_2,
						Keyboard.DirectXKeyStrokes.DIK_3,
						Keyboard.DirectXKeyStrokes.DIK_4,
						Keyboard.DirectXKeyStrokes.DIK_5,
						Keyboard.DirectXKeyStrokes.DIK_6,
						Keyboard.DirectXKeyStrokes.DIK_7,
						Keyboard.DirectXKeyStrokes.DIK_8,
						Keyboard.DirectXKeyStrokes.DIK_9,
					};

					// loop through all the players twice if we don't find the right one the first time
					int foundTries = 2;
					while (foundTries > 0)
					{
						for (int i = 0; i < numbers.Count; i++)
						{
							Program.FocusEchoVR();
							// press the keys to visit a player
							Keyboard.SendKey(numbers[i], false, Keyboard.InputType.Keyboard);
							await Task.Delay(20);
							Program.FocusEchoVR();
							Keyboard.SendKey(numbers[i], true, Keyboard.InputType.Keyboard);

							// check if this is the right player
							await Task.Delay(50);
							result = await Program.GetRequestAsync($"http://{Program.echoVRIP}:{Program.echoVRPort}/session", null);
							if (string.IsNullOrEmpty(result)) return;
							frame = JsonConvert.DeserializeObject<g_Instance>(result);
							if (frame == null) return;
							List<g_Player> players = frame.GetAllPlayers(false);
							g_Player clientPlayer = frame.GetPlayer(Program.lastFrame.client_name);
							float dist = 0;
							dist = Vector3.Distance(clientPlayer?.head.Position ?? Vector3.Zero, frame.player.vr_position.ToVector3());

							g_Player minPlayer = players.OrderBy(p => Vector3.Distance(p.head.Position, frame.player.vr_position.ToVector3())).First();
							dist = Vector3.Distance(minPlayer.head.Position, frame.player.vr_position.ToVector3());
							LogRow(LogType.Info, $"Player {i} camera distance:\t{dist:N3} m.\tName:\t{minPlayer.name}");

							// if we found the correct player
							if (minPlayer.name == Program.lastFrame.client_name)
							{
								foundTries = 0;
								break;
							}
						}

						Program.FocusEchoVR();
						Keyboard.SendKey(Keyboard.DirectXKeyStrokes.DIK_P, false, Keyboard.InputType.Keyboard);
						await Task.Delay(20);
						Keyboard.SendKey(Keyboard.DirectXKeyStrokes.DIK_P, true, Keyboard.InputType.Keyboard);

						foundTries--;
					}


					switch (SparkSettings.instance.followClientSpectatorCameraMode)
					{
						case 0:
							Program.FocusEchoVR();
							Keyboard.SendKey(Keyboard.DirectXKeyStrokes.DIK_F, false, Keyboard.InputType.Keyboard);
							await Task.Delay(20);
							Program.FocusEchoVR();
							Keyboard.SendKey(Keyboard.DirectXKeyStrokes.DIK_F, true, Keyboard.InputType.Keyboard);
							break;
						case 1:
							Program.FocusEchoVR();
							Keyboard.SendKey(Keyboard.DirectXKeyStrokes.DIK_P, false, Keyboard.InputType.Keyboard);
							await Task.Delay(20);
							Program.FocusEchoVR();
							Keyboard.SendKey(Keyboard.DirectXKeyStrokes.DIK_P, true, Keyboard.InputType.Keyboard);
							break;
					}
				});
			}
			catch (Exception ex)
			{
				LogRow(LogType.Error, $"Error with finding player camera to follow.\n{ex}");
			}
		}
	}
}
