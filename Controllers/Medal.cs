using OBSWebsocketDotNet;
using System;
using System.Numerics;
using System.Threading.Tasks;
using EchoVRAPI;

namespace Spark
{
	public class Medal
	{
		public Medal()
		{
			Program.PlayspaceAbuse += PlayspaceAbuse;
			Program.Goal += Goal;
			Program.Save += Save;
			Program.Assist += Assist;
			Program.Interception += Interception;
		}


		private void Save(Frame frame, EventData eventData)
		{
			SaveClip(SparkSettings.instance.medalClipSave, eventData.player.name, frame);
		}

		private void Goal(Frame frame, GoalData goalData)
		{
			SaveClip(SparkSettings.instance.medalClipGoal, frame.last_score.person_scored, frame);
		}

		private void PlayspaceAbuse(Frame frame, Team team, Player player, Vector3 arg4)
		{
			SaveClip(SparkSettings.instance.medalClipPlayspace, player.name, frame);
		}

		private void Assist(Frame frame, GoalData goal)
		{
			SaveClip(SparkSettings.instance.medalClipAssist, frame.last_score.assist_scored, frame);
		}

		private void Interception(Frame frame, Team team, Player throwPlayer, Player catchPlayer)
		{
			SaveClip(SparkSettings.instance.medalClipInterception, catchPlayer.name, frame);
		}

		private void SaveClip(bool setting, string player_name, Frame frame)
		{
			if (!setting) return;
			if (!IsPlayerScopeEnabled(player_name, frame)) return;
			Task.Delay((int)(SparkSettings.instance.medalClipSecondsAfter * 1000)).ContinueWith(_ =>
			{
				ClipNow();
			});
		}

		public static void ClipNow()
		{
			Keyboard.SendEchoKey((Keyboard.DirectXKeyStrokes)SparkSettings.instance.medalClipKey, focusEchoVR: false);
		}

		private static bool IsPlayerScopeEnabled(string player_name, Frame frame)
		{
			try
			{
				if (string.IsNullOrEmpty(player_name) || frame.teams == null) return false;

				// if in spectator and record-all-in-spectator is checked
				if (SparkSettings.instance.medalClipSpectatorRecord)
				{
					return true;
				}

				switch (SparkSettings.instance.medalClipPlayerScope)
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
	}
}