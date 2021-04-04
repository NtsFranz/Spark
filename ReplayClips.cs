using System.Threading.Tasks;
using Spark.Properties;

namespace Spark
{
	public class ReplayClips
	{
		private const int DelayAfter = 5;

		public ReplayClips()
		{
			Program.PlayspaceAbuse += (frame, team, player, offset) =>
			{
				if (Settings.Default.replayClipPlayspace)
				{
					Task.Delay(DelayAfter * 1000).ContinueWith(t => Program.SaveReplayClip($"{player.name}_abuse"));
				}
			};

			Program.Goal += (frame, goalData) =>
			{
				if (Settings.Default.replayClipGoal)
				{
					Task.Delay(DelayAfter * 1000).ContinueWith(t => Program.SaveReplayClip($"{frame.last_score.person_scored}_goal"));
				}
			};

			Program.Save += (frame, team, player) =>
			{
				if (Settings.Default.replayClipSave)
				{
					Task.Delay(DelayAfter * 1000).ContinueWith(t => Program.SaveReplayClip($"{player.name}_save"));
				}
			};
		}
	}
}