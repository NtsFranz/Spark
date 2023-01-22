using System;
using System.Threading.Tasks;
using EchoVRAPI;

namespace Spark
{
	public class ReplayClips
	{
		public ReplayClips()
		{
			Program.EmoteActivated += (frame, _, player, isLeft) =>
			{
				if (player.name == frame.client_name)
				{
					SaveClip(SparkSettings.instance.replayClipEmote, player.name, frame, $"{player.name}_{(isLeft ? "left" : "right")}_emote");
				}
			};
			Program.PlayspaceAbuse += (frame, _, player, _) => { SaveClip(SparkSettings.instance.replayClipPlayspace, player.name, frame, $"{player.name}_abuse"); };
			Program.Goal += (frame, _) => { SaveClip(SparkSettings.instance.replayClipGoal, frame.last_score.person_scored, frame, $"{frame.last_score.person_scored}_goal"); };
			Program.Save += (frame, eventData) => { SaveClip(SparkSettings.instance.replayClipSave, eventData.player.name, frame, $"{eventData.player.name}_save"); };
			Program.Assist += (frame, _) => { SaveClip(SparkSettings.instance.replayClipAssist, frame.last_score.assist_scored, frame, $"{frame.last_score.assist_scored}_assist"); };
			Program.Interception += (frame, _, _, catchPlayer) => { SaveClip(SparkSettings.instance.replayClipInterception, catchPlayer.name, frame, $"{catchPlayer.name}_interception"); };
			Program.Joust += (frame, _, player, neutral, _, _, _) =>
			{
				if (neutral)
				{
					SaveClip(SparkSettings.instance.replayClipNeutralJoust, player.name, frame, $"{player.name}_neutral_joust");
				}
				else
				{
					SaveClip(SparkSettings.instance.replayClipDefensiveJoust, player.name, frame, $"{player.name}_defensive_joust");
				}
			};
		}

		private static void SaveClip(bool setting, string player_name, Frame frame, string clip_name)
		{
			if (!setting) return;
			if (!IsPlayerScopeEnabled(player_name, frame)) return;
			Task.Delay((int) (SparkSettings.instance.replayClipSecondsAfter * 1000)).ContinueWith(_ => Program.replayFilesManager.SaveReplayClip(clip_name));
		}

		private static bool IsPlayerScopeEnabled(string player_name, Frame frame)
		{
			try
			{
				if (string.IsNullOrEmpty(player_name) || frame.teams == null) return false;

				// if in spectator and record-all-in-spectator is checked
				if (SparkSettings.instance.replayClipSpectatorRecord && frame.client_name == player_name)
				{
					return true;
				}

				switch (SparkSettings.instance.replayClipPlayerScope)
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