using System.Threading.Tasks;
using Spark.Properties;

namespace Spark
{
	public class ReplayClips
	{

		public ReplayClips()
		{
			Program.PlayspaceAbuse += (frame, team, player, offset) =>
			{
				if (Settings.Default.replayClipPlayspace)
				{
					Task.Delay((int)(Settings.Default.replayClipSecondsAfter * 1000)).ContinueWith(t => Program.SaveReplayClip($"{player.name}_abuse"));
				}
			};

			Program.Goal += (frame, goalData) =>
			{
				if (Settings.Default.replayClipGoal)
				{
					Task.Delay((int)(Settings.Default.replayClipSecondsAfter * 1000)).ContinueWith(t => Program.SaveReplayClip($"{frame.last_score.person_scored}_goal"));
				}
			};

			Program.Save += (frame, team, player) =>
			{
				if (Settings.Default.replayClipSave)
				{
					Task.Delay((int)(Settings.Default.replayClipSecondsAfter * 1000)).ContinueWith(t => Program.SaveReplayClip($"{player.name}_save"));
				}
			};

			Program.Assist += (frame, goalData) =>
			{
				if (Settings.Default.replayClipAssist)
				{
					Task.Delay((int)(Settings.Default.replayClipSecondsAfter * 1000)).ContinueWith(t => Program.SaveReplayClip($"{frame.last_score.assist_scored}_assist"));
				}
			};

			Program.Interception += (frame, team, throwPlayer, catchPlayer) =>
			{
				if (Settings.Default.replayClipInterception)
				{
					Task.Delay((int)(Settings.Default.replayClipSecondsAfter * 1000)).ContinueWith(t => Program.SaveReplayClip($"{catchPlayer.name}_interception"));
				}
			};
		}
	}
}