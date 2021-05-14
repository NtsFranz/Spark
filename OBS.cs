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

			if (SparkSettings.instance.obsAutoconnect)
			{
				Task.Run(() =>
				{
					try
					{
						instance.Connect(SparkSettings.instance.obsIP, SparkSettings.instance.obsPassword);
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
			string scene = SparkSettings.instance.obsBetweenGameScene;
			if (string.IsNullOrEmpty(scene) || scene == "Do Not Switch") return;
			instance.SetCurrentScene(scene);
		}

		private void JoinedGame(g_Instance obj)
		{
			string scene = SparkSettings.instance.obsInGameScene;
			if (string.IsNullOrEmpty(scene) || scene == "Do Not Switch") return;
			instance.SetCurrentScene(scene);
		}

		private void Save(g_Instance frame, g_Team team, g_Player player)
		{
			SaveClip(SparkSettings.instance.obsClipSave, player.name, frame);
		}

		private void Goal(g_Instance frame, GoalData goalData)
		{
			SaveClip(SparkSettings.instance.obsClipGoal, frame.last_score.person_scored, frame);
		}

		private void PlayspaceAbuse(g_Instance frame, g_Team team, g_Player player, Vector3 arg4)
		{
			SaveClip(SparkSettings.instance.obsClipPlayspace, player.name, frame);
		}

		private void Assist(g_Instance frame, GoalData goal)
		{
			SaveClip(SparkSettings.instance.obsClipAssist, frame.last_score.assist_scored, frame);
		}

		private void Interception(g_Instance frame, g_Team team, g_Player throwPlayer, g_Player catchPlayer)
		{
			SaveClip(SparkSettings.instance.obsClipInterception, catchPlayer.name, frame);
		}

		private void SaveClip(bool setting, string player_name, g_Instance frame)
		{
			if (!instance.IsConnected) return;
			if (!setting) return;
			if (!IsPlayerScopeEnabled(player_name, frame)) return;
			Task.Delay((int)(SparkSettings.instance.obsClipSecondsAfter * 1000)).ContinueWith(_ => instance.SaveReplayBuffer());
		}


		private static bool IsPlayerScopeEnabled(string player_name, g_Instance frame)
		{
			try
			{
				if (string.IsNullOrEmpty(player_name) || frame.teams == null) return false;

				// if in spectator and record-all-in-spectator is checked
				if (SparkSettings.instance.obsSpectatorRecord && frame.client_name == player_name)
				{
					return true;
				}

				switch (SparkSettings.instance.obsPlayerScope)
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
			if (!SparkSettings.instance.obsAutostartReplayBuffer) return;
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