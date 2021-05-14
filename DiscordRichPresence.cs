using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using DiscordRPC;
using DiscordRPC.Logging;
using DiscordRPC.Message;
using Spark.Properties;
using static Logger;

namespace Spark
{
	class DiscordRichPresence
	{
		public static DiscordRpcClient discordClient;
		public static DateTime initializationTime;
		public static DateTime lastDiscordPresenceTime;
		private static DateTime lobbyEntryTime;
		private static bool inLobby;

		private static Thread thread;

		/// <summary>
		/// Start the discord thread.
		/// </summary>
		public static void Start()
		{
			thread = new Thread(DiscordThread);
			thread.Start();
		}

		/// <summary>
		/// Emergency Stop for some reason
		/// </summary>
		public static void Stop()
		{
			thread.Abort();
			DisposeDiscord();
		}

		public static void DiscordThread()
		{
			initializationTime = DateTime.UtcNow;
			InitializeDiscord();

			while (Program.running)
			{
				if (Program.inGame)
				{
					ProcessDiscordPresence(Program.lastFrame);
				}
				else
				{
					ProcessDiscordPresence(null);
				}

				Thread.Sleep(1000);
			}
		}

		public static void InitializeDiscord()
		{
			// Create a Discord client
			discordClient = new DiscordRpcClient(SecretKeys.discordRPCClientID);
			discordClient.RegisterUriScheme();

			// Set the logger
			discordClient.Logger = new ConsoleLogger { Level = LogLevel.Warning };

			// Subscribe to events
			discordClient.OnJoin += OnJoin;
			discordClient.OnSpectate += OnSpectate;
			discordClient.OnJoinRequested += OnJoinRequested;

			discordClient.SetSubscription(EventType.Join | EventType.Spectate | EventType.JoinRequest);

			lobbyEntryTime = DateTime.UtcNow;

			// Connect to the RPC
			discordClient.Initialize();
		}

		private static void OnJoin(object sender, JoinMessage args)
		{
			Process.Start(new ProcessStartInfo
			{
				FileName = args.Secret,
				UseShellExecute = true
			});
		}

		private static void OnJoinRequested(object sender, JoinRequestMessage args)
		{
			Program.synth.SpeakAsync(args.User.Username + " requested to join using Discord.");
		}

		private static void OnSpectate(object sender, SpectateMessage args)
		{
			Process.Start(new ProcessStartInfo
			{
				FileName = args.Secret,
				UseShellExecute = true
			});
		}

		public static void DisposeDiscord()
		{
			if (discordClient != null && !discordClient.IsDisposed)
				discordClient.Dispose();
		}

		public static void ProcessDiscordPresence(g_Instance frame)
		{
			lastDiscordPresenceTime = DateTime.Now;

			if (SparkSettings.instance.discordRichPresence)
			{
				if (discordClient == null || discordClient.IsDisposed)
				{
					//InitializeDiscord();
					LogRow(LogType.Error, "Discord RP client disposed while in normal thread.");
				}

				RichPresence rp = new RichPresence();

				if (frame == null)
				{
					discordClient.SetPresence(null);
					return;
				}

				StringBuilder details = new StringBuilder();
				if (frame.map_name == "mpl_arena_a")
				{
					if (frame.teams[2].players.Find(p => p.name == frame.client_name) != null)
					{
						details.Append("Spectating ");
					}
					else
					{
						details.Append("Playing ");
					}

					details.Append("Arena ");

					if (frame.private_match)
					{
						details.Append("pvt.");

						rp.WithSecrets(new Secrets
						{
							JoinSecret = "spark://c/" + frame.sessionid,
							SpectateSecret = "spark://s/" + frame.sessionid,
						});
					}
					else
					{
						details.Append("pub.");
					}

					rp.State = "Score: " + frame.orange_points + " - " + frame.blue_points;
					rp.Timestamps = new Timestamps
					{
						End = frame.game_status == "post_match" ? DateTime.UtcNow : DateTime.UtcNow.AddSeconds(frame.game_clock)
					};
					rp.WithParty(new Party
					{
						ID = frame.sessionid,
						Size = frame.GetAllPlayers().Count,
						Max = frame.private_match ? 15 : 8
					});

				}
				else if (frame.map_name == "mpl_lobby_b2")
				{
					details.Append("in EchoVR Lobby");

					// how long have we been in the lobby?
					if (!inLobby)
					{
						inLobby = true;
						lobbyEntryTime = DateTime.UtcNow;
					}
					rp.Timestamps = new Timestamps
					{
						Start = lobbyEntryTime
					};
				}
				else // if (frame.map_name == "whatever combat is")
				{
					details.Append("Playing Combat");
				}

				rp.Details = details.ToString();
				rp.Assets = new Assets
				{
					LargeImageKey = "echo_arena_store_icon",
					LargeImageText = "Rich presence from Spark"
				};


				discordClient.SetPresence(rp);
			}
			else
			{
				if (discordClient != null && !discordClient.IsDisposed)
					discordClient.Dispose();
			}
		}
	}
}
