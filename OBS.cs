using OBSWebsocketDotNet;
using Spark.Properties;
using System;
using System.Numerics;
using System.Threading.Tasks;

namespace Spark
{
	public class OBS
	{
		public readonly OBSWebsocket instance;

		public OBS()
		{
			instance = new OBSWebsocket();

			instance.Connected += OnConnect;
			instance.Disconnected += OnDisconnect;

			Program.PlayspaceAbuse += PlayspaceAbuse;
			Program.Goal += Goal;
			Program.Save += Save;
			Program.Assist += Assist;
			Program.Interception += Interception;

			Program.JoinedGame += JoinedGame;
			Program.LeftGame += LeftGame;

			if (Settings.Default.obsAutoconnect)
			{
				Task.Run(() =>
				{
					try
					{
						instance.Connect(Settings.Default.obsIP, Settings.Default.obsPassword);
					}
					catch (Exception e)
					{
						Logger.LogRow(Logger.LogType.Error, $"Error when autoconnecting to OBS.\n{e}");
						instance.Disconnect();
					}
				});
			}
		}

		private void LeftGame(g_Instance obj)
		{
			string scene = Settings.Default.obsBetweenGameScene;
			if (string.IsNullOrEmpty(scene) || scene == "Do Not Switch") return;
			instance.SetCurrentScene(scene);
		}

		private void JoinedGame(g_Instance obj)
		{
			string scene = Settings.Default.obsInGameScene;
			if (string.IsNullOrEmpty(scene) || scene == "Do Not Switch") return;
			instance.SetCurrentScene(scene);
		}

		private void Save(g_Instance frame, g_Team team, g_Player player)
		{
			SaveClip(Settings.Default.obsClipSave, player.name, frame);
		}

		private void Goal(g_Instance frame, GoalData goalData)
		{
			SaveClip(Settings.Default.obsClipGoal, frame.last_score.person_scored, frame);
		}

		private void PlayspaceAbuse(g_Instance frame, g_Team team, g_Player player, Vector3 arg4)
		{
			SaveClip(Settings.Default.obsClipPlayspace, player.name, frame);
		}

		private void Assist(g_Instance frame, GoalData goal)
		{
			SaveClip(Settings.Default.obsClipAssist, frame.last_score.assist_scored, frame);
		}

		private void Interception(g_Instance frame, g_Team team, g_Player throwPlayer, g_Player catchPlayer)
		{
			SaveClip(Settings.Default.obsClipInterception, catchPlayer.name, frame);
		}

		private void SaveClip(bool setting, string player_name, g_Instance frame)
		{
			if (!instance.IsConnected) return;
			if (!setting) return;
			if (!IsPlayerScopeEnabled(player_name, frame)) return;
			Task.Delay((int)(Settings.Default.obsClipSecondsAfter * 1000)).ContinueWith(_ => instance.SaveReplayBuffer());
		}


		private static bool IsPlayerScopeEnabled(string player_name, g_Instance frame)
		{
			try
			{
				if (string.IsNullOrEmpty(player_name) || frame.teams == null) return false;

				// if in spectator and record-all-in-spectator is checked
				if (Settings.Default.obsSpectatorRecord && frame.client_name == player_name)
				{
					return true;
				}

				switch (Settings.Default.obsPlayerScope)
				{
					// only me
					case 0:
						return player_name == frame.client_name;
					// only my team
					case 1:
						return frame.GetPlayer(frame.client_name).team.color == frame.GetPlayer(player_name).team.color;
					// anyone
					case 2:
						return true;
				}
			}
			catch (Exception ex)
			{
				Logger.LogRow(Logger.LogType.Error, $"Something broke while checking if player highlights is enabled\n{ex}");
			}

			return false;
		}

		private void OnConnect(object sender, EventArgs e)
		{
			if (!Settings.Default.obsAutostartReplayBuffer) return;
			try
			{
				instance.StartReplayBuffer();
			}
			catch (Exception exp)
			{
				Logger.LogRow(Logger.LogType.Error, $"Error when autostarting replay buffer in OBS.\n{exp}");
			}
		}

		private void OnDisconnect(object sender, EventArgs e)
		{
		}
	}
}