using System;
using EchoVRAPI;
using NetMQ;
using NetMQ.Sockets;
using Spark.Data_Containers.ZMQ_Messages;

namespace Spark
{
	public class NetMQEvents
	{
		private readonly PublisherSocket pubSocket;
		
		public NetMQEvents()
		{
			
			try
			{
				AsyncIO.ForceDotNet.Force();
				NetMQConfig.Cleanup();
				pubSocket = new PublisherSocket();
				pubSocket.Options.SendHighWatermark = 1000;
				pubSocket.Bind("tcp://*:12345");
			}
			catch (Exception e)
			{
				Logger.LogRow(Logger.LogType.Error, $"Error setting up pub/sub system: {e}");
			}

			Program.FrameFetched += (time, session, bones) =>
			{
				pubSocket.SendMoreFrame("RawFrame").SendFrame(session);
			};
			Program.NewFrame += frame =>
			{

			};
			Program.NewArenaFrame += frame =>
			{
				pubSocket.SendMoreFrame("TimeAndScore").SendFrame($"{frame.game_clock:0.00} Orange: {frame.orange_points} Blue: {frame.blue_points}");
			};
			Program.JoinedGame += frame =>
			{
				MatchEventZMQMessage msg = new MatchEventZMQMessage("NewMatch", "sessionid", frame.sessionid);
				pubSocket.SendMoreFrame("MatchEvent").SendFrame(msg.ToJsonString());
			};
			Program.LeftGame += frame =>
			{
				MatchEventZMQMessage msg = new MatchEventZMQMessage("LeaveMatch", "sessionid", frame.sessionid);
				pubSocket.SendMoreFrame("MatchEvent").SendFrame(msg.ToJsonString());
			};
			
			Program.GoalImmediate += frame => {
				bool shouldPlayHorn = frame.ClientTeamColor == Team.TeamColor.spectator || frame.ClientTeamColor.ToString() == frame.last_score.team;
				MatchEventZMQMessage msg = new MatchEventZMQMessage("GoalScored", "isClientTeam", shouldPlayHorn.ToString());
				pubSocket.SendMoreFrame("MatchEvent").SendFrame(msg.ToJsonString());
			};
		}

		public void CloseApp()
		{	
			if (pubSocket != null)
			{
				pubSocket.SendMoreFrame("CloseApp").SendFrame("");
			}
		}
	}
}