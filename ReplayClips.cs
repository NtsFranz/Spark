using System;
using System.Threading.Tasks;
using Spark.Properties;

namespace Spark
{
	public class ReplayClips
	{
		public ReplayClips()
		{
			Program.PlayspaceAbuse += (frame, _, player, _) => { SaveClip(Settings.Default.replayClipPlayspace, player.name, frame, $"{player.name}_abuse"); };
			Program.Goal += (frame, _) => { SaveClip(Settings.Default.replayClipGoal, frame.last_score.person_scored, frame, $"{frame.last_score.person_scored}_goal"); };
			Program.Save += (frame, _, player) => { SaveClip(Settings.Default.replayClipSave, player.name, frame, $"{player.name}_save"); };
			Program.Assist += (frame, _) => { SaveClip(Settings.Default.replayClipAssist, frame.last_score.assist_scored, frame, $"{frame.last_score.assist_scored}_assist"); };
			Program.Interception += (frame, _, _, catchPlayer) => { SaveClip(Settings.Default.replayClipInterception, catchPlayer.name, frame, $"{catchPlayer.name}_interception"); };
		}

		private static void SaveClip(bool setting, string player_name, g_Instance frame, string clip_name)
		{
			if (!setting) return;
			if (!IsPlayerScopeEnabled(player_name, frame)) return;
			Task.Delay((int) (Settings.Default.replayClipSecondsAfter * 1000)).ContinueWith(_ => Program.SaveReplayClip(clip_name));
		}

		private static bool IsPlayerScopeEnabled(string player_name, g_Instance frame)
		{
			try
			{
				if (string.IsNullOrEmpty(player_name) || frame.teams == null) return false;

				// if in spectator and record-all-in-spectator is checked
				if (Settings.Default.replayClipSpectatorRecord && frame.client_name == player_name)
				{
					return true;
				}

				switch (Settings.Default.replayClipPlayerScope)
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
	}
}