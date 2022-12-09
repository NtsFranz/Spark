using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Fleck;
using Newtonsoft.Json;

namespace Spark
{
	public class WebSocketServerManager
	{
		private readonly WebSocketServer server;
		private readonly List<IWebSocketConnection> allSockets;
		private readonly ConcurrentDictionary<EventContainer.EventType, List<IWebSocketConnection>> subscriberMapping = new ConcurrentDictionary<EventContainer.EventType, List<IWebSocketConnection>>();
		private readonly object subscriberLock = new object();

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
					lock (subscriberLock)
					{
						allSockets.Add(socket);
					}
				};
				socket.OnClose = () =>
				{
					Console.WriteLine("Websocket Close!");
					lock (subscriberLock)
					{
						allSockets.Remove(socket);
						foreach ((EventContainer.EventType key, List<IWebSocketConnection> value) in subscriberMapping)
						{
							value.RemoveAll(v => v == socket);
						}
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
							{
								if (Enum.TryParse(parts[1], out EventContainer.EventType type))
								{
									if (!subscriberMapping.ContainsKey(type))
									{
										subscriberMapping[type] = new List<IWebSocketConnection>();
									}

									lock (subscriberLock)
									{
										subscriberMapping[type].Add(socket);
									}

									socket.Send(message);

									// send back the current state if it's an event that requires it
									if (type == EventContainer.EventType.overlay_config)
									{
										SendData(EventContainer.EventType.overlay_config, OverlayConfig.ToDict());
									}
								}
								else
								{
									socket.Send("failed:" + message);
								}
								break;
							}
							case "unsubscribe":
							{
								if (Enum.TryParse(parts[1], out EventContainer.EventType type))
								{
									if (subscriberMapping.ContainsKey(type))
									{
										lock (subscriberLock)
										{
											subscriberMapping[type].Remove(socket);
										}
									}

									socket.Send(message);
								}
								else
								{
									socket.Send("failed:" + message);
								}
								break;
							}
						}
					}
				};
			});

			Program.Goal += (frame, goalData) =>
			{
				SendData(EventContainer.EventType.goal, goalData.ToDict(true));
			};
			Program.Stun += (frame, stunEvent) =>
			{
				SendData(EventContainer.EventType.stun, stunEvent.ToDict(true));
			};
			Program.Steal += (frame, eventData) =>
			{
				SendData(EventContainer.EventType.steal, eventData.ToDict(true));
			};
			Program.Save += (frame, eventData) =>
			{
				SendData(EventContainer.EventType.save, eventData.ToDict(true));
			};
			Program.JoustEvent += (frame, eventData) =>
			{
				SendData(EventContainer.EventType.joust, eventData.ToDict(true));
			};
			Program.GamePaused += (frame, player, distance) =>
			{
				SendData(EventContainer.EventType.pause, JsonConvert.SerializeObject(frame.pause));
			};
			// Program.PauseRequest += (frame) => { SendData(EventContainer.EventType.pause, JsonConvert.SerializeObject(frame.pause)); };
			Program.GameUnpaused += (frame, player, distance) =>
			{
				SendData(EventContainer.EventType.pause, JsonConvert.SerializeObject(frame.pause));
			};
			Program.OverlayConfigChanged += () =>
			{
				SendData(EventContainer.EventType.overlay_config, OverlayConfig.ToDict());
			};
			Program.JoinedGame += (frame) =>
			{
				SendData(EventContainer.EventType.joined_game, JsonConvert.SerializeObject(frame));
			};
			Program.LeftGame += (frame) =>
			{
				SendData(EventContainer.EventType.left_game, JsonConvert.SerializeObject(frame));
			};
			Program.EventLog += (msg) =>
			{
				SendData(EventContainer.EventType.event_log, new Dictionary<string, object>() { { "message", msg } });
			};


			DateTime lastSent30Hz = DateTime.UtcNow;
			DateTime lastSent10Hz = DateTime.UtcNow;
			DateTime lastSent1Hz = DateTime.UtcNow;
			Program.FrameFetched += (_, session, _) =>
			{
				if (DateTime.UtcNow - lastSent1Hz > TimeSpan.FromSeconds(1f))
				{
					SendData(EventContainer.EventType.frame_1hz, session);
					lastSent1Hz = DateTime.UtcNow;
				}

				if (DateTime.UtcNow - lastSent10Hz > TimeSpan.FromSeconds(.1f))
				{
					SendData(EventContainer.EventType.frame_10hz, session);
					lastSent10Hz = DateTime.UtcNow;
				}

				if (DateTime.UtcNow - lastSent30Hz > TimeSpan.FromSeconds(.0333f))
				{
					SendData(EventContainer.EventType.frame_30hz, session);
					lastSent30Hz = DateTime.UtcNow;
				}
			};

			// DateTime lastSentSimpleFrame30Hz = DateTime.UtcNow;
			// DateTime lastSentSimpleFrame10Hz = DateTime.UtcNow;
			// DateTime lastSentSimpleFrame1Hz = DateTime.UtcNow;
			// Program.NewFrame += (frame) =>
			// {
			// 	if (DateTime.UtcNow - lastSentSimpleFrame1Hz > TimeSpan.FromSeconds(1f))
			// 	{
			// 		SendData(EventContainer.EventType.frame_1hz, new Dictionary<string, object>()
			// 		{
			// 			""
			// 		});
			// 		lastSentSimpleFrame1Hz = DateTime.UtcNow;
			// 	}
			// 	if (DateTime.UtcNow - lastSentSimpleFrame10Hz > TimeSpan.FromSeconds(.1f))
			// 	{
			// 		SendData(EventContainer.EventType.frame_10hz, session);
			// 		lastSentSimpleFrame10Hz = DateTime.UtcNow;
			// 	}
			// 	if (DateTime.UtcNow - lastSentSimpleFrame30Hz > TimeSpan.FromSeconds(.0333f))
			// 	{
			// 		SendData(EventContainer.EventType.frame_30hz, session);
			// 		lastSentSimpleFrame30Hz = DateTime.UtcNow;
			// 	}
			// };
		}

		~WebSocketServerManager()
		{
			allSockets.ForEach(s =>
			{
				s.Close();
			});
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
				allSockets.ForEach(s =>
				{
					s.Send(":" + message);
				});
			}
			else
			{
				if (subscriberMapping.ContainsKey((EventContainer.EventType)subscriberGroup))
				{
					List<IWebSocketConnection> copy;
					lock (subscriberLock)
					{
						copy = subscriberMapping[(EventContainer.EventType)subscriberGroup].ToList();
					}

					copy.ForEach(s =>
					{
						s.Send((EventContainer.EventType)subscriberGroup + ":" + message);
					});
				}
			}
		}

		public void SendData(EventContainer.EventType subscriberGroup, Dictionary<string, object> dict)
		{
			SendData(subscriberGroup, JsonConvert.SerializeObject(dict));
		}
	}
}