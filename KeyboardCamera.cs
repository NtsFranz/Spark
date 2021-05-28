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
				Task.Run(async () =>
				{
					// try to find player
					Program.FocusEchoVR();
					Keyboard.SendKey(Keyboard.DirectXKeyStrokes.DIK_P, false, Keyboard.InputType.Keyboard);
					await Task.Delay(20);
					Program.FocusEchoVR();
					Keyboard.SendKey(Keyboard.DirectXKeyStrokes.DIK_P, true, Keyboard.InputType.Keyboard);

					await Task.Delay(50);
					bool found = false;
					found = await CheckPOVCamIsCorrect();

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
					while (foundTries > 0 && !found)
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
							found = await CheckPOVCamIsCorrect(i);

							// if we found the correct player
							if (found) break;
						}

						Program.FocusEchoVR();
						Keyboard.SendKey(Keyboard.DirectXKeyStrokes.DIK_P, false, Keyboard.InputType.Keyboard);
						await Task.Delay(20);
						Keyboard.SendKey(Keyboard.DirectXKeyStrokes.DIK_P, true, Keyboard.InputType.Keyboard);

						foundTries--;
					}

					if (found)
					{
						switch (SparkSettings.instance.followClientSpectatorCameraMode)
						{
							// Follow
							case 0:
								Program.FocusEchoVR();
								Keyboard.SendKey(Keyboard.DirectXKeyStrokes.DIK_F, false, Keyboard.InputType.Keyboard);
								await Task.Delay(20);
								Program.FocusEchoVR();
								Keyboard.SendKey(Keyboard.DirectXKeyStrokes.DIK_F, true, Keyboard.InputType.Keyboard);
								break;
							// POV
							case 1:
								Program.FocusEchoVR();
								Keyboard.SendKey(Keyboard.DirectXKeyStrokes.DIK_P, false, Keyboard.InputType.Keyboard);
								await Task.Delay(20);
								Program.FocusEchoVR();
								Keyboard.SendKey(Keyboard.DirectXKeyStrokes.DIK_P, true, Keyboard.InputType.Keyboard);
								break;
						}
					}
					else
					{
						LogRow(LogType.Error, $"Failed to find player, switching to auto instead.");
						Program.FocusEchoVR();
						Keyboard.SendKey(Keyboard.DirectXKeyStrokes.DIK_A, false, Keyboard.InputType.Keyboard);
						await Task.Delay(20);
						Program.FocusEchoVR();
						Keyboard.SendKey(Keyboard.DirectXKeyStrokes.DIK_A, true, Keyboard.InputType.Keyboard);
					}
				});
			}
			catch (Exception ex)
			{
				LogRow(LogType.Error, $"Error with finding player camera to follow.\n{ex}");
			}
		}

		private static async Task<bool> CheckPOVCamIsCorrect(int i = -1)
		{
			string result = await Program.GetRequestAsync($"http://{Program.echoVRIP}:{Program.echoVRPort}/session", null);
			if (string.IsNullOrEmpty(result)) return false;
			g_Instance frame = JsonConvert.DeserializeObject<g_Instance>(result);
			if (frame == null) return false;
			List<g_Player> players = frame.GetAllPlayers(false);
			g_Player clientPlayer = frame.GetPlayer(Program.lastFrame.client_name);
			float dist = 0;
			dist = Vector3.Distance(clientPlayer?.head.Position ?? Vector3.Zero, frame.player.vr_position.ToVector3());

			g_Player minPlayer = players.OrderBy(p => Vector3.Distance(p.head.Position, frame.player.vr_position.ToVector3())).First();
			dist = Vector3.Distance(minPlayer.head.Position, frame.player.vr_position.ToVector3());
			LogRow(LogType.Info, $"Player {i} camera distance:\t{dist:N3} m.\tName:\t{minPlayer.name}");

			return minPlayer.name == Program.lastFrame.client_name;
		}
	}
}
