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

		public static void SpectatorCamFindPlayer(string playerName = null)
		{
			if (Program.lastFrame == null) return;

			if (playerName == null) playerName = Program.lastFrame.client_name;

			if (Program.lastFrame.GetPlayer(playerName) == null)
			{
				LogRow(LogType.File, Program.lastFrame.sessionid, "Requested follow player not in the game.");
				return;
			}

			try
			{
				Task.Run(async () =>
				{
					await Task.Delay(500);

					// try to find player
					Program.FocusEchoVR();
					Keyboard.SendKey(Keyboard.DirectXKeyStrokes.DIK_P, false, Keyboard.InputType.Keyboard);
					await Task.Delay(20);
					Program.FocusEchoVR();
					Keyboard.SendKey(Keyboard.DirectXKeyStrokes.DIK_P, true, Keyboard.InputType.Keyboard);

					await Task.Delay(50);
					bool found = false;
					found = await CheckPOVCamIsCorrect(playerName);

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
					int foundTries = 1;
					while (foundTries > 0 && !found)
					{
						Program.FocusEchoVR();
						Keyboard.SendKey(Keyboard.DirectXKeyStrokes.DIK_P, false, Keyboard.InputType.Keyboard);
						await Task.Delay(20);
						Keyboard.SendKey(Keyboard.DirectXKeyStrokes.DIK_P, true, Keyboard.InputType.Keyboard);
						await Task.Delay(20);

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
							found = await CheckPOVCamIsCorrect(playerName, i);

							// if we found the correct player
							if (found) break;
						}

						foundTries--;
					}

					if (found)
					{
						LogRow(LogType.File, Program.lastFrame.sessionid, "Correct player found.");
						switch (SparkSettings.instance.followPlayerCameraMode)
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
								// don't press P again, since this will switch to a different player
								// we are already in POV mode.

								//Program.FocusEchoVR();
								//Keyboard.SendKey(Keyboard.DirectXKeyStrokes.DIK_P, false, Keyboard.InputType.Keyboard);
								//await Task.Delay(20);
								//Program.FocusEchoVR();
								//Keyboard.SendKey(Keyboard.DirectXKeyStrokes.DIK_P, true, Keyboard.InputType.Keyboard);
								break;
						}
					}
					else
					{
						LogRow(LogType.File, Program.lastFrame.sessionid, "Failed to find player, switching to auto instead.");
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

		private static async Task<bool> CheckPOVCamIsCorrect(string playerName, int i = -1)
		{
			string result = await Program.GetRequestAsync($"http://{Program.echoVRIP}:{Program.echoVRPort}/session", null);
			if (string.IsNullOrEmpty(result)) return false;
			g_Instance frame = JsonConvert.DeserializeObject<g_Instance>(result);
			if (frame == null) return false;
			List<g_Player> players = frame.GetAllPlayers(false);
			g_Player targetPlayer = frame.GetPlayer(playerName);

			var sortedList = players.OrderBy(p => Vector3.Distance(p.head.Position, frame.player.vr_position.ToVector3())).ToList();

			// debug all player distances
			//sortedList.ForEach(p => LogRow(LogType.File, frame.sessionid, $"{Vector3.Distance(p.head.Position, frame.player.vr_position.ToVector3())}\t{p.name}"));
			
			g_Player minPlayer = sortedList.First();
			float dist = Vector3.Distance(minPlayer.head.Position, frame.player.vr_position.ToVector3());

			LogRow(LogType.File, frame.sessionid, $"Player {i} camera distance: {dist:N3} m.  Name: {minPlayer.name}");

			return minPlayer.name == playerName;
		}
	}
}
