using OBSWebsocketDotNet;
using System;
using System.Numerics;
using System.Threading.Tasks;
using EchoVRAPI;

namespace Spark
{
	public class OBS
	{
		public readonly OBSWebsocket instance;

		public int currentReplay = 0;

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

			Program.GameStatusChanged += GameStatusChanged;

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

		private void GameStatusChanged(Frame lastFrame, Frame newFrame)
		{
			if (!SparkSettings.instance.obsPauseRecordingWithGameClock) return;
			if (newFrame.private_match) return;

			if (newFrame.game_status == "playing")
			{
				try
				{
					instance.ResumeRecording();
				}
				catch (Exception)
				{
					// pass
				}
			}
			else if (lastFrame.game_status == "playing")
			{
				try
				{
					instance.PauseRecording();
				}
				catch (Exception)
				{
					// pass
				}
			}
		}

		private void LeftGame(Frame frame)
		{
			if (!instance.IsConnected) return;
			string scene = SparkSettings.instance.obsBetweenGameScene;
			if (string.IsNullOrEmpty(scene) || scene == "Do Not Switch") return;
			instance.SetCurrentScene(scene);
			
			if (!SparkSettings.instance.obsPauseRecordingWithGameClock) return;
			
			try
			{
				instance.PauseRecording();
			}
			catch (Exception)
			{
				// pass
			}
		}

		private void JoinedGame(Frame frame)
		{
			if (!instance.IsConnected) return;
			string scene = SparkSettings.instance.obsInGameScene;
			if (string.IsNullOrEmpty(scene) || scene == "Do Not Switch") return;
			instance.SetCurrentScene(scene);
			
			if (!SparkSettings.instance.obsPauseRecordingWithGameClock) return;
			if (frame.private_match) return;

			if (frame.game_status == "playing")
			{
				try
				{
					instance.ResumeRecording();
				}
				catch (Exception)
				{
					// pass
				}
			}
		}

		private void Save(Frame frame, EventData eventData)
		{
			SaveClip(SparkSettings.instance.obsClipSave, eventData.player.name, frame, SparkSettings.instance.obsSaveReplayScene, SparkSettings.instance.obsClipSecondsAfter, SparkSettings.instance.obsSaveSecondsAfter, SparkSettings.instance.obsSaveReplayLength);
		}

		private void Goal(Frame frame, GoalData goalData)
		{
			SaveClip(SparkSettings.instance.obsClipGoal, frame.last_score.person_scored, frame, SparkSettings.instance.obsGoalReplayScene, SparkSettings.instance.obsClipSecondsAfter, SparkSettings.instance.obsGoalSecondsAfter, SparkSettings.instance.obsGoalReplayLength);
		}

		private void PlayspaceAbuse(Frame frame, Team team, Player player, Vector3 arg4)
		{
			SaveClip(SparkSettings.instance.obsClipPlayspace, player.name, frame, "", SparkSettings.instance.obsClipSecondsAfter, 0, 0);
		}

		private void Assist(Frame frame, GoalData goal)
		{
			SaveClip(SparkSettings.instance.obsClipAssist, frame.last_score.assist_scored, frame, "", SparkSettings.instance.obsClipSecondsAfter, 0, 0);
		}

		private void Interception(Frame frame, Team team, Player throwPlayer, Player catchPlayer)
		{
			SaveClip(SparkSettings.instance.obsClipInterception, catchPlayer.name, frame, "", SparkSettings.instance.obsClipSecondsAfter, 0, 0);
		}

		private void SaveClip(bool setting, string player_name, Frame frame, string to_scene, float save_delay, float scene_delay, float scene_length)
		{
			if (!instance.IsConnected) return;
			if (!setting) return;
			if (!IsPlayerScopeEnabled(player_name, frame)) return;
			Task.Delay((int) (save_delay * 1000)).ContinueWith(_ =>
			{
				try
				{
					instance.SaveReplayBuffer();
				}
				catch (Exception)
				{
					// replay buffer probably not active
				}
			});

			currentReplay++;
			Task.Delay((int)(scene_delay * 1000)).ContinueWith(_ => SetSceneIfLastReplay(to_scene, currentReplay));
			Task.Delay((int)((scene_delay + scene_length) * 1000)).ContinueWith(_ => SetSceneIfLastReplay(SparkSettings.instance.obsInGameScene, currentReplay));
		}

		private void SetSceneIfLastReplay(string scene, int replayNum)
		{
			if (!instance.IsConnected) return;
			// the last event takes precednce
			if (string.IsNullOrEmpty(scene) || scene == "Do Not Switch") return;
			if (replayNum == currentReplay) instance.SetCurrentScene(scene);
		}

		private static bool IsPlayerScopeEnabled(string player_name, Frame frame)
		{
			try
			{
				if (string.IsNullOrEmpty(player_name) || frame.teams == null) return false;

				// if in spectator and record-all-in-spectator is checked
				if (SparkSettings.instance.obsSpectatorRecord)
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
						return frame.GetPlayer(frame.client_name).team_color == frame.GetPlayer(player_name).team_color;
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