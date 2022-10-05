using OBSWebsocketDotNet;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Threading.Tasks;
using EchoVRAPI;
using OBSWebsocketDotNet.Communication;
using OBSWebsocketDotNet.Types;

namespace Spark
{
	public class OBS
	{
		public readonly OBSWebsocket ws;

		public int currentReplay = 0;

		public OutputState? replayBufferState;
		public bool connected;

		public OBS()
		{
			if (SparkSettings.instance.firstTimeOBSv28)
			{
				SparkSettings.instance.obsIP = "ws://127.0.0.1:4455";
				SparkSettings.instance.firstTimeOBSv28 = false;
			}

			ws = new OBSWebsocket();

			ws.Connected += OnConnect;
			ws.Disconnected += OnDisconnect;
			ws.ReplayBufferStateChanged += (_, state) =>
			{
				replayBufferState = state.State;
			};

			Program.EmoteActivated += (frame, team, player) =>
			{
				SaveClip(SparkSettings.instance.obsClipEmote, player.name, frame, "", 0, 0, 0);
			};
			Program.PlayspaceAbuse += PlayspaceAbuse;
			Program.Goal += Goal;
			Program.Assist += Assist;
			Program.Save += Save;
			Program.Interception += Interception;
			Program.JoustEvent += Joust;

			Program.JoinedGame += JoinedGame;
			Program.LeftGame += LeftGame;

			Program.GameStatusChanged += GameStatusChanged;

			if (SparkSettings.instance.obsAutoconnect)
			{
				Task.Run(() =>
				{
					try
					{
						ws.Connect(SparkSettings.instance.obsIP, SparkSettings.instance.obsPassword);
					}
					catch (Exception e)
					{
						Logger.LogRow(Logger.LogType.Error, $"Error when autoconnecting to OBS.\n{e}");
						ws.Disconnect();
					}
				});
			}
		}

		private void Joust(Frame frame, EventData eventData)
		{
			if (eventData.eventType == EventContainer.EventType.joust_speed)
			{
				SaveClip(SparkSettings.instance.obsClipNeutralJoust, eventData.player.name, frame, "", 0, 0, 0);
			}
			else if (eventData.eventType == EventContainer.EventType.defensive_joust)
			{
				SaveClip(SparkSettings.instance.obsClipDefensiveJoust, eventData.player.name, frame, "", 0, 0, 0);
			}
			else
			{
				Logger.Error("Joust that isn't neutral or defensive");
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
					ws.ResumeRecord();
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
					if (ws.IsConnected)
					{
						ws.PauseRecord();
					}
				}
				catch (Exception)
				{
					// pass
				}
			}
		}

		private void LeftGame(Frame frame)
		{
			if (!ws.IsConnected) return;
			string scene = SparkSettings.instance.obsBetweenGameScene;
			if (string.IsNullOrEmpty(scene) || scene == "--- Do Not Switch ---" || scene == "Do Not Switch") return;
			ws.SetCurrentProgramScene(scene);

			if (!SparkSettings.instance.obsPauseRecordingWithGameClock) return;

			try
			{
				ws.PauseRecord();
			}
			catch (Exception)
			{
				// pass
			}
		}

		private void JoinedGame(Frame frame)
		{
			if (!ws.IsConnected) return;
			string scene = SparkSettings.instance.obsInGameScene;
			if (string.IsNullOrEmpty(scene) || scene == "--- Do Not Switch ---" || scene == "Do Not Switch") return;
			ws.SetCurrentProgramScene(scene);

			if (!SparkSettings.instance.obsPauseRecordingWithGameClock) return;
			if (frame.private_match) return;

			if (frame.game_status == "playing")
			{
				try
				{
					ws.ResumeRecord();
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
			if (!ws.IsConnected) return;
			if (!setting) return;
			if (!IsPlayerScopeEnabled(player_name, frame)) return;
			Task.Delay((int)(save_delay * 1000)).ContinueWith(_ =>
			{
				try
				{
					ws.SaveReplayBuffer();
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
			if (!ws.IsConnected) return;
			// the last event takes precednce
			if (string.IsNullOrEmpty(scene) || scene == "--- Do Not Switch ---" || scene == "Do Not Switch") return;
			if (replayNum == currentReplay) ws.SetCurrentProgramScene(scene);
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
			Task.Run(async () =>
			{
				await Task.Delay(100);
				if (!ws.IsConnected) return;
				try
				{
					replayBufferState = ws.GetReplayBufferStatus() ? OutputState.OBS_WEBSOCKET_OUTPUT_STARTED : OutputState.OBS_WEBSOCKET_OUTPUT_STOPPED;

					if (SparkSettings.instance.obsAutostartReplayBuffer)
					{
						ws.StartReplayBuffer();
					}
				}
				catch (Exception exp)
				{
					Debug.WriteLine("Replay buffer not enabled in startup");
				}
			});

			connected = true;
		}

		private void OnDisconnect(object sender, ObsDisconnectionInfo e)
		{
			replayBufferState = null;
			connected = false;
		}
	}
}