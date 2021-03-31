using System.Threading.Tasks;

namespace Spark
{
	public class ReplayClips
	{
		private const int DelayAfter = 5;

		public ReplayClips()
		{
			Program.PlayspaceAbuse += (frame, team, player, offset) =>
			{
				Task.Run(() =>
				{
					Task.Delay(DelayAfter * 1000);
					Program.SaveReplayClip($"{player.name}_abuse");
				});
			};

			Program.Goal += (frame, goalData) =>
			{
				Task.Run(() =>
				{
					Task.Delay(DelayAfter * 1000);
					Program.SaveReplayClip($"{frame.last_score.person_scored}_goal");
				});
			};
		}
	}
}