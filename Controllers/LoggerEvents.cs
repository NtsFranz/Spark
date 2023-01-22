using EchoVRAPI;
using static Logger;

namespace Spark
{
	public class LoggerEvents
	{
		public LoggerEvents()
		{
			Program.LocalThrow += frame =>
			{
				if (SparkSettings.instance.eventLog.localThrows)
				{
					Log(frame, $"Total speed: {frame.last_throw.total_speed}  Arm: {frame.last_throw.speed_from_arm}  Wrist: {frame.last_throw.speed_from_wrist}  Movement: {frame.last_throw.speed_from_movement}");
				}
			};
			Program.PlayerJoined += (frame, team, player) =>
			{
				if (SparkSettings.instance.eventLog.playerJoins)
				{
					Log(frame, $"Player Joined: {player.name}");
				}
			};
			Program.PlayerLeft += (frame, team, player) =>
			{
				if (SparkSettings.instance.eventLog.playerLeaves)
				{
					Log(frame, $"Player Left: {player.name}");
				}
			};
			Program.PlayerSwitchedTeams += (frame, fromTeam, toTeam, player) =>
			{
				if (SparkSettings.instance.eventLog.playerSwitchedTeams)
				{
					Log(frame, $"Player switched to {toTeam.color} team: {player.name}");
				}
			};
			Program.GamePaused += (frame, player, distance) =>
			{
				if (SparkSettings.instance.eventLog.pauseEvents)
				{
					Log(frame, $"{frame.pause.paused_requested_team} team paused the game ({player?.name}, {distance:N2} m)");
				}
			};
			Program.PauseRequest += (frame, player, distance) =>
			{
				if (SparkSettings.instance.eventLog.pauseRequests)
				{
					Log(frame, $"{frame.pause.paused_requested_team} team requested a pause ({player?.name}, {distance:N2} m)");
				}
			};
			Program.GameUnpaused += (frame, player, distance) =>
			{
				if (SparkSettings.instance.eventLog.unPauseRequests)
				{
					Log(frame, $"{frame.pause.unpaused_team} team unpaused the game ({player?.name}, {distance:N2} m)");
				}
			};
			Program.BigBoost += (frame, team, player, boostSpeed, howLongAgoBoost) =>
			{
				if (SparkSettings.instance.eventLog.bigBoosts)
				{
					Log(frame, $"{player.name} boosted to {boostSpeed:N1} m/s");
				}
			};
			Program.PlayspaceAbuse += (frame, team, player, location) =>
			{
				if (SparkSettings.instance.eventLog.playspaceAbuses)
				{
					Log(frame, $"{player.name} abused their playspace");
				}
			};
			Program.Save += (frame, data) =>
			{
				if (SparkSettings.instance.eventLog.saves)
				{
					Log(frame, $"{data.player.name} made a save");
				}
			};
			Program.Stun += (frame, data) =>
			{
				if (SparkSettings.instance.eventLog.stuns)
				{
					Log(frame, $"{data.player.name} stunned {data.otherPlayer.name}");
				}
			};
			Program.Turnover += (frame, team, throwPlayer, catchPlayer) =>
			{
				if (SparkSettings.instance.eventLog.turnovers)
				{
					Log(frame, $"{throwPlayer.name} turned over the disk to {catchPlayer.name}");
				}
			};
			Program.Pass += (frame, team, throwPlayer, catchPlayer) =>
			{
				if (SparkSettings.instance.eventLog.passes)
				{
					Log(frame, $"{catchPlayer.name} received a pass from {throwPlayer.name}");
				}
			};
			Program.Catch += (frame, team, player) =>
			{
				if (SparkSettings.instance.eventLog.catches)
				{
					Log(frame, $"{player.name} made a catch");
				}
			};
			Program.Interception += (frame, team, throwPlayer, catchPlayer) =>
			{
				if (SparkSettings.instance.eventLog.interceptions)
				{
					Log(frame, $"{catchPlayer.name} intercepted a throw from {throwPlayer.name}");
				}
			};
			Program.ShotTaken += (frame, team, player) =>
			{
				if (SparkSettings.instance.eventLog.shotAttempts)
				{
					Log(frame, $"{player.name} took a shot");
				}
			};
			Program.LargePing += (frame, team, player) =>
			{
				if (SparkSettings.instance.eventLog.largePings)
				{
					Log(frame, $"{player.name} ping went above 150");
				}
			};
			Program.RestartRequest += (frame, color, player, distance) =>
			{
				if (SparkSettings.instance.eventLog.restartRequests)
				{
					Log(frame, $"{color} team restart request");
				}
			};
			Program.JoustEvent += (frame, data) =>
			{
				if ((SparkSettings.instance.eventLog.neutralJousts && data.eventType == EventContainer.EventType.joust_speed) || 
				    (SparkSettings.instance.eventLog.defensiveJousts && data.eventType == EventContainer.EventType.defensive_joust))
				{
					Log(frame, $"{data.team.color} team joust time{(data.eventType == EventContainer.EventType.defensive_joust ? " (defensive)" : "")}: {(data.vec2.Z):N2} s, Max speed: {data.vec2.X:N2} m/s, Tube Exit Speed: {data.vec2.Y:N2} m/s");
				}
			};
			Program.Throw += (frame, team, player, leftHanded, underhandedness) =>
			{
				if (SparkSettings.instance.eventLog.throws)
				{
					Log(frame, $"{player.name} threw the disk at {frame.disc.velocity.ToVector3().Length():N2} m/s with their {(leftHanded ? "left" : "right")} hand");
				}
			};
			Program.Goal += (frame, data) =>
			{
				if (SparkSettings.instance.eventLog.goals)
				{
					Log(frame, $"{frame.last_score.person_scored} scored at {frame.last_score.disc_speed:N2} m/s from {frame.last_score.distance_thrown:N2} m away{(frame.last_score.assist_scored == "[INVALID]" ? "!" : (", assisted by " + frame.last_score.assist_scored + "!"))}");
					Log(frame, $"Goal angle: {data.GoalAngle:N2} deg, from {(data.Backboard ? "behind" : "the front")}");
					Log(frame, $"ORANGE: {frame.orange_points}  BLUE: {frame.blue_points}");
				}
			};
			Program.JoinedGame += frame =>
			{
				Log(frame, $"Joined game");
			};
			Program.LeftGame += frame =>
			{
				Log(frame, $"Left game");
			};
			Program.EmoteActivated += (frame, _, player, isLeft) =>
			{
				Log(frame, $"{player.name} used the {(isLeft ? "left" : "right")} emote");
			};
			Program.RulesChanged += frame =>
			{
				Log(frame, $"{frame.rules_changed_by} changed the private match rules");
			};
		}

		public static void Log(Frame frame, string msg)
		{
			LogRow(LogType.File, frame.sessionid, $"{frame.game_clock_display} - {msg}");
		}
	}
}