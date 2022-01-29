using System;
using System.Collections.Generic;
using Fleck;
using Newtonsoft.Json;

namespace Spark
{
	public class WebSocketServerManager
	{
		private readonly WebSocketServer server;
		private readonly List<IWebSocketConnection> allSockets;
		private readonly Dictionary<EventContainer.EventType, List<IWebSocketConnection>> subscriberMapping = new Dictionary<EventContainer.EventType, List<IWebSocketConnection>>();

		public WebSocketServerManager()
		{
			server = new WebSocketServer("ws://0.0.0.0:6725");

			allSockets = new List<IWebSocketConnection>();
			server.RestartAfterListenError = true;
			server.Start(socket =>
			{
				socket.OnOpen = () =>
				{
					Console.WriteLine("Websocket Open!");
					allSockets.Add(socket);
				};
				socket.OnClose = () =>
				{
					Console.WriteLine("Websocket Close!");
					allSockets.Remove(socket);
					foreach ((EventContainer.EventType key, List<IWebSocketConnection> value) in subscriberMapping)
					{
						value.RemoveAll(v => v == socket);
					}
				};

				// send the message back
				socket.OnMessage = message =>
				{
					string[] parts = message.Split(':');
					if (parts.Length == 2)
					{
						switch (parts[0])
						{
							case "subscribe":
								if (Enum.TryParse(parts[1], out EventContainer.EventType type))
								{
									if (!subscriberMapping.ContainsKey(type))
									{
										subscriberMapping[type] = new List<IWebSocketConnection>();
									}

									subscriberMapping[type].Add(socket);

									socket.Send(message);
									
									// send back the current state if it's an event that requires it
									if (type == EventContainer.EventType.team_names)
									{
										SendData(EventContainer.EventType.team_names, OverlayConfig.ToDict());
									}
								}
								else
								{
									socket.Send("failed:" + message);
								}

								break;
						}
					}
				};
			});


			Program.Stun += (frame, stunEvent) => { SendData(EventContainer.EventType.stun, stunEvent.ToDict(true)); };
			Program.Goal += (frame, goalData) => { SendData(EventContainer.EventType.goal, goalData.ToDict(true)); };
			Program.Steal += (frame, eventData) => { SendData(EventContainer.EventType.steal, eventData.ToDict(true)); };
			Program.Save += (frame, eventData) => { SendData(EventContainer.EventType.save, eventData.ToDict(true)); };
			Program.JoustEvent += (frame, eventData) => { SendData(EventContainer.EventType.joust, eventData.ToDict(true)); };
			Program.GamePaused += (frame) => { SendData(EventContainer.EventType.pause, JsonConvert.SerializeObject(frame.pause)); };
			Program.PauseRequest += (frame) => { SendData(EventContainer.EventType.pause, JsonConvert.SerializeObject(frame.pause)); };
			Program.GameUnpaused += (frame) => { SendData(EventContainer.EventType.pause, JsonConvert.SerializeObject(frame.pause)); };
			Program.TeamNameLogoChanged += () => { SendData(EventContainer.EventType.team_names, OverlayConfig.ToDict()); };
		}

		~WebSocketServerManager()
		{
			allSockets.ForEach(s => { s.Close(); });
			server.Dispose();
		}

		/// <summary>
		/// Sends a message to connected clients
		/// </summary>
		/// <param name="subscriberGroup">Set this to null to send message to all clients</param>
		/// <param name="message">Message content</param>
		public void SendData(EventContainer.EventType? subscriberGroup, string message)
		{
			if (subscriberGroup == null)
			{
				// just send it to all
				allSockets.ForEach(s => { s.Send(":" + message); });
			}
			else
			{
				if (subscriberMapping.ContainsKey((EventContainer.EventType)subscriberGroup))
				{
					subscriberMapping[(EventContainer.EventType)subscriberGroup].ForEach(s => { s.Send((EventContainer.EventType)subscriberGroup + ":" + message); });
				}
			}
		}

		public void SendData(EventContainer.EventType subscriberGroup, Dictionary<string, object> dict)
		{
			SendData(subscriberGroup, JsonConvert.SerializeObject(dict));
		}
	}
}