using OBSWebsocketDotNet;
using Spark.Properties;
using System;
using System.Numerics;
using System.Threading.Tasks;

namespace Spark
{
	public class OBS
	{
		public OBSWebsocket instance;

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
					}
				});

			}
		}

		private void Save(g_Instance arg1, g_Team arg2, g_Player arg3)
		{
			if (!instance.IsConnected) return;

			if (Settings.Default.obsClipSave)
			{
				Task.Delay((int)(Settings.Default.obsClipSecondsAfter * 1000)).ContinueWith(t => instance.SaveReplayBuffer());
			}
		}

		private void Goal(g_Instance arg1, GoalData arg2)
		{
			if (!instance.IsConnected) return;

			if (Settings.Default.obsClipGoal)
			{
				Task.Delay((int)(Settings.Default.obsClipSecondsAfter * 1000)).ContinueWith(t => instance.SaveReplayBuffer());
			}
		}

		private void PlayspaceAbuse(g_Instance arg1, g_Team arg2, g_Player arg3, Vector3 arg4)
		{
			if (!instance.IsConnected) return;

			if (Settings.Default.obsClipPlayspace)
			{
				Task.Delay((int)(Settings.Default.obsClipSecondsAfter * 1000)).ContinueWith(t => instance.SaveReplayBuffer());
			}
		}

		private void Assist(g_Instance arg1, GoalData goal)
		{
			if (!instance.IsConnected) return;

			if (Settings.Default.obsClipAssist)
			{
				Task.Delay((int)(Settings.Default.obsClipSecondsAfter * 1000)).ContinueWith(t => instance.SaveReplayBuffer());
			}
		}

		private void Interception(g_Instance frame, g_Team team, g_Player throwPlayer, g_Player catchPlayer)
		{
			if (!instance.IsConnected) return;

			if (Settings.Default.obsClipInterception)
			{
				Task.Delay((int)(Settings.Default.obsClipSecondsAfter * 1000)).ContinueWith(t => instance.SaveReplayBuffer());
			}
		}

		private void OnConnect(object sender, EventArgs e)
		{
			if (Settings.Default.obsAutostartReplayBuffer)
			{
				try
				{
					instance.StartReplayBuffer();
				}
				catch (Exception exp)
				{
					Logger.LogRow(Logger.LogType.Error, $"Error when autostarting replay buffer in OBS.\n{exp}");
				}
			}

			//txtServerIP.Enabled = false;
			//txtServerPassword.Enabled = false;
			//btnConnect.Text = "Disconnect";

			//gbControls.Enabled = true;

			//var versionInfo = _obs.GetVersion();
			//tbPluginVersion.Text = versionInfo.PluginVersion;
			//tbOBSVersion.Text = versionInfo.OBSStudioVersion;

			//btnListScenes.PerformClick();
			//btnGetCurrentScene.PerformClick();

			//btnListSceneCol.PerformClick();
			//btnGetCurrentSceneCol.PerformClick();

			//btnListProfiles.PerformClick();
			//btnGetCurrentProfile.PerformClick();

			//btnListTransitions.PerformClick();
			//btnGetCurrentTransition.PerformClick();

			//btnGetTransitionDuration.PerformClick();

			//var streamStatus = _obs.GetStreamingStatus();
			//if (streamStatus.IsStreaming)
			//	onStreamingStateChange(_obs, OutputState.Started);
			//else
			//	onStreamingStateChange(_obs, OutputState.Stopped);

			//if (streamStatus.IsRecording)
			//	onRecordingStateChange(_obs, OutputState.Started);
			//else
			//	onRecordingStateChange(_obs, OutputState.Stopped);
		}

		private void OnDisconnect(object sender, EventArgs e)
		{
			//BeginInvoke((MethodInvoker)(() => {
			//	gbControls.Enabled = false;

			//	txtServerIP.Enabled = true;
			//	txtServerPassword.Enabled = true;
			//	btnConnect.Text = "Connect";
			//}));
		}
	}
}
