using OBSWebsocketDotNet;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using EchoVRAPI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
			ws.ReplayBufferStateChanged += (_, state) => { replayBufferState = state.OutputState.State; };

			Program.EmoteActivated += (frame, team, player, isLeft) => { SaveClip(SparkSettings.instance.obsClipEmote, player.name, frame, "", 0, 0, 0); };
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
						ws.ConnectAsync(SparkSettings.instance.obsIP, SparkSettings.instance.obsPassword);
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
					LoggerEvents.Log(Program.lastFrame, "Saving OBS Replay Buffer...");
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

		public void AddSparkSources()
		{
			Task.Run(async () =>
			{
				string sceneFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "obs-studio", "basic", "scenes", "Spark.json");
				if (!File.Exists(sceneFile))
				{
					string defaultCollection = await File.ReadAllTextAsync(Path.Combine(Path.GetDirectoryName(SparkSettings.instance.sparkExeLocation) ?? "", "resources", "obs_scene_collection.json"));
					await File.WriteAllTextAsync(sceneFile, defaultCollection);
				}

				JObject scene = JsonConvert.DeserializeObject<JObject>(await File.ReadAllTextAsync(sceneFile));

				if (scene == null) return;

				string[] urls =
				{
					"http://127.0.0.1:6724/branding",
					"http://127.0.0.1:6724/midmatch_overlay",
					"http://127.0.0.1:6724/scoreboard",
					"http://127.0.0.1:6724/configurable_overlay",
					"http://127.0.0.1:6724/playspace",
					"http://127.0.0.1:6724/speedometer/player",
					"http://127.0.0.1:6724/speedometer/disc",
					"http://127.0.0.1:6724/speedometer/lone_echo_1",
					"http://127.0.0.1:6724/speedometer/lone_echo_2",

					"http://127.0.0.1:6724/components/minimap",
					"http://127.0.0.1:6724/components/compact_minimap",
					"http://127.0.0.1:6724/components/event_log",
					"http://127.0.0.1:6724/components/player_list_blue",
					"http://127.0.0.1:6724/components/player_list_orange",
					"http://127.0.0.1:6724/components/compact_banner",
				};


				List<JToken> sources = scene["sources"].ToList();
				JToken sceneSource = sources.FirstOrDefault(s => s["name"].ToString() == "Spark Sources");

				// use the existing scene sources list
				// List<JToken> sceneSources = sceneSource["settings"]["items"].ToList();
				// reset the existing scene sources list
				List<JToken> sceneSources = new List<JToken>();

				foreach (string url in urls)
				{
					string name = $"Spark:{string.Join('/', url.Split("/").Skip(3))}";

					// add to the sources list
					JToken obj = sources.FirstOrDefault(s => s["name"].ToString() == name);
					if (obj == null)
					{
						obj = JToken.Parse($@"
							{{
								""id"": ""browser_source"",
								""versioned_id"": ""browser_source"",
								""name"": ""{name}"",
								""settings"": {{
									""url"": ""{url}"",
									""width"": 1920,
									""height"": 1080,
									""shutdown"": true
								}}
							}}
						");
						sources.Add(obj);
					}

					// add the sources to the scene
					JToken sceneItem = sceneSources.FirstOrDefault(s => s["name"].ToString() == name);


					if (sceneItem == null)
					{
						sceneSources.Add(JToken.Parse($@"
							{{
								""name"": ""{name}"",
								""private_settings"": {{
									""color"": ""#55ffc105"",
									""color-preset"": 1
								}},
							}}
						"));
					}
				}

				sceneSource["settings"]["items"] = JsonConvert.DeserializeObject<JToken>(JsonConvert.SerializeObject(sceneSources));
				scene["sources"] = JsonConvert.DeserializeObject<JToken>(JsonConvert.SerializeObject(sources));

				await File.WriteAllTextAsync(sceneFile, JsonConvert.SerializeObject(scene));
			});
		}
	}
}