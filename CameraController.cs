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
	class CameraController
	{
		public static string BaseUrl = "http://localhost:6721/";

		public CameraController()
		{
			Program.Goal += (_, _) =>
			{
				if (SparkSettings.instance.toggleMinimapAfterGoals)
				{
					Task.Run(async () =>
					{
						await Task.Delay(500);

						SetMinimapVisibility(false);
						await Task.Delay(20);
						SetMinimapVisibility(true);
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
					SetCameraMode(CameraMode.pov);

					await Task.Delay(50);
					bool found = false;
					found = await CheckPOVCamIsCorrect(playerName);
					int foundIndex = 0;

					// loop through all the players twice if we don't find the right one the first time
					int foundTries = 1;
					while (foundTries > 0 && !found)
					{
						for (int i = 0; i < Keyboard.numbers.Length; i++)
						{
							// press the keys to visit a player
							SetCameraMode(CameraMode.pov, i);

							// check if this is the right player
							await Task.Delay(50);
							found = await CheckPOVCamIsCorrect(playerName, i);

							// if we found the correct player
							if (found)
							{
								foundIndex = i;
								break;
							}
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
								SetCameraMode(CameraMode.follow, foundIndex);
								break;
							// POV
							case 1:
								SetCameraMode(CameraMode.pov, foundIndex);
								break;
						}
					}
					else
					{
						LogRow(LogType.File, Program.lastFrame.sessionid,
							"Failed to find player, switching to auto instead.");

						SetCameraMode(CameraMode.side);
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
			string result = await Program.GetRequestAsync($"http://127.0.0.1:6721/session", null);
			if (string.IsNullOrEmpty(result)) return false;
			g_Instance frame = JsonConvert.DeserializeObject<g_Instance>(result);
			if (frame == null) return false;
			List<g_Player> players = frame.GetAllPlayers(false);
			g_Player targetPlayer = frame.GetPlayer(playerName);

			List<g_Player> sortedList = players
				.OrderBy(p => Vector3.Distance(p.head.Position, frame.player.vr_position.ToVector3())).ToList();

			// debug all player distances
			//sortedList.ForEach(p => LogRow(LogType.File, frame.sessionid, $"{Vector3.Distance(p.head.Position, frame.player.vr_position.ToVector3())}\t{p.name}"));

			g_Player minPlayer = sortedList.First();
			float dist = Vector3.Distance(minPlayer.head.Position, frame.player.vr_position.ToVector3());

			LogRow(LogType.File, frame.sessionid, $"Player {i} camera distance: {dist:N3} m.  Name: {minPlayer.name}");

			return minPlayer.name == playerName;
		}

		public static void SetUIVisibility(bool visible)
		{
			Dictionary<string, bool> data = new Dictionary<string, bool>()
			{
				{"enabled", visible}
			};
			Program.PostRequestCallback(BaseUrl + "ui_visibility", null, JsonConvert.SerializeObject(data), null);
		}

		public static void SetNameplatesVisibility(bool visible)
		{
			Dictionary<string, object> data = new Dictionary<string, object>()
			{
				{"enabled", visible}
			};
			Program.PostRequestCallback(BaseUrl + "nameplates_visibility", null, JsonConvert.SerializeObject(data), null);
		}

		public static void SetMinimapVisibility(bool visible)
		{
			Dictionary<string, object> data = new Dictionary<string, object>()
			{
				{"enabled", visible}
			};
			Program.PostRequestCallback(BaseUrl + "minimap_visibility", null, JsonConvert.SerializeObject(data), null);
		}

		public static void SetTeamsMuted(bool blueTeamMuted, bool orangeTeamMuted)
		{
			Dictionary<string, bool> data = new Dictionary<string, bool>()
			{
				{"blue_team_muted", blueTeamMuted},
				{"orange_team_muted", orangeTeamMuted},
			};
			Program.PostRequestCallback(BaseUrl + "team_muted", null, JsonConvert.SerializeObject(data), null);
		}

		public enum CameraMode
		{
			pov,
			level,
			follow,
			side,
			free,
			api
		}

		public static void SetCameraMode(CameraMode mode)
		{
			Dictionary<string, object> data = new Dictionary<string, object>()
			{
				{"mode", mode.ToString()},
			};
			Program.PostRequestCallback(BaseUrl + "camera_mode", null, JsonConvert.SerializeObject(data), null);
		}

		public static void SetCameraMode(int playerId)
		{
			Dictionary<string, object> data = new Dictionary<string, object>()
			{
				{"num", playerId},
			};
			Program.PostRequestCallback(BaseUrl + "camera_mode", null, JsonConvert.SerializeObject(data), null);
		}

		public static void SetCameraMode(CameraMode mode, int playerId)
		{
			Dictionary<string, object> data = new Dictionary<string, object>()
			{
				{"mode", mode.ToString()},
				{"num", playerId},
			};
			Program.PostRequestCallback(BaseUrl + "camera_mode", null, JsonConvert.SerializeObject(data), null);
		}
	}
}