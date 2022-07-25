using System;
using System.Windows;
using EchoVRAPI;
using Spark.Properties;

namespace Spark
{
	public class SpectateMeController
	{
		public bool spectateMe;
		private string lastSpectatedSessionId;
		public const int SPECTATEME_PORT = 6720;

		public SpectateMeController()
		{
			Program.NewMatch += OnNewRound;
			Program.LeftGame += OnLeftGame;
			Program.JoinedGame += OnJoinedGame;
		}

		private void OnJoinedGame(Frame obj)
		{
			Program.liveWindow.SetSpectateMeSubtitle("In Game!");
		}

		private void OnNewRound(Frame frame)
		{
			if (string.IsNullOrEmpty(SparkSettings.instance.echoVRPath))
			{
				Program.GetEchoVRProcess();
			}

			if (spectateMe)
			{
				try
				{
					Program.KillEchoVR($"-httpport {SPECTATEME_PORT}");
					Program.StartEchoVR(Program.JoinType.Spectator, SPECTATEME_PORT, SparkSettings.instance.useAnonymousSpectateMe, frame.sessionid);
					lastSpectatedSessionId = frame.sessionid;

					Program.liveWindow.SetSpectateMeSubtitle(Resources.Waiting_for_EchoVR_to_start);
				}
				catch (Exception e)
				{
					Logger.LogRow(Logger.LogType.Error, $"Broke something in the spectator follow system.\n{e}");
				}
			}

			Program.WaitUntilLocalGameLaunched(CameraWriteController.UseCameraControlKeys, port: SPECTATEME_PORT);
		}
		
		
		private void OnLeftGame(Frame obj)
		{
			if (spectateMe)
			{
				try
				{
					Program.KillEchoVR($"-httpport {SPECTATEME_PORT}");
					lastSpectatedSessionId = string.Empty;
				}
				catch (Exception e)
				{
					Logger.LogRow(Logger.LogType.Error, $"Broke something in the spectator follow system.\n{e}");
				}
			}
		}

		
		public (string, string) ToggleSpectateMe()
		{
			spectateMe = !spectateMe;
			string labelText = "";
			string subtitleText = "";
			try
			{
				if (spectateMe)
				{
					if (Program.InGame && Program.lastFrame != null && !Program.lastFrame.InLobby)
					{
						Program.KillEchoVR($"-httpport {SPECTATEME_PORT}");
						Program.StartEchoVR(
							Program.JoinType.Spectator,
							port: SPECTATEME_PORT,
							noovr: SparkSettings.instance.useAnonymousSpectateMe,
							session_id: Program.lastFrame.sessionid);
						Program.WaitUntilLocalGameLaunched(CameraWriteController.UseCameraControlKeys, port: SPECTATEME_PORT);
						subtitleText = Resources.Waiting_for_EchoVR_to_start;
					}
					else
					{
						subtitleText = Resources.Waiting_until_you_join_a_game;
					}
					labelText = Resources.Stop_Spectating_Me;
				}
				else
				{
					Program.KillEchoVR($"-httpport {SPECTATEME_PORT}");
					labelText = Resources.Spectate_Me;
					subtitleText = Resources.Not_active;
				}
			}
			catch (Exception ex)
			{
				Logger.LogRow(Logger.LogType.Error, $"Broke something in the spectator follow system.\n{ex}");
			}

			return (labelText, subtitleText);
		}
	}
}