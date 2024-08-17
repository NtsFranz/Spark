using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using DiscordRPC;
using DiscordRPC.Logging;
using DiscordRPC.Message;
using EchoVRAPI;
using Spark.Properties;
using static Logger;

namespace Spark
{
	internal static class DiscordRichPresence
	{
		private static DiscordRpcClient discordClient;

		// Generic timer variable to be used as Elapsed time on state that don't have an end time
		private static DateTime initialStateTime;

		// All the states the game can be in, used to properly set timers and monitor state changes
		private enum GlobalGameState
		{
			Transitioning,
			InGame,
			InLobby,
			Generic,
			Disconnected
		}

		// Bool to detect if the game status was changed
		private static bool statusChanged;

		// Setting last* variables to "Unknown" to force a status change when the Frame gets a valid status
		private static string lastStatus = "Unknown";
		private static string lastPausedState = "Unknown";

		// Setting the state as Disconnected by default
		private static GlobalGameState globalGameState = GlobalGameState.Disconnected;

		// Used to easily convert game state from the API values to a pretty text for the Rich Presence
		private static readonly Dictionary<string, string> prettyGameStatus = new Dictionary<string, string>()
		{
			{ "pre_match", "Pre-match" },
			{ "playing", "In Progress" },
			{ "score", "Score" },
			{ "round_start", "Round Start" },
			{ "pre_sudden_death", "Pre-Overtime" },
			{ "sudden_death", "Overtime" },
			{ "post_sudden_death", "Post-Match" },
			{ "round_over", "Post-Round" },
			{ "post_match", "Post-Match" },
			{ "Unknown", "Unknown" }
		};

		// Used to easily convert combat map name from the API values to a pretty text for the Rich Presence
		private static readonly Dictionary<string, string> prettyCombatMapName = new Dictionary<string, string>()
		{
			{ "mpl_combat_dyson", "Dyson" },
			{ "mpl_combat_combustion", "Combustion" },
			{ "mpl_combat_fission", "Fission" },
			{ "mpl_combat_gauss", "Surge" }
		};

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

		private static void DiscordThread()
		{
			InitializeDiscord();

			while (Program.running)
			{
				ProcessDiscordPresence(Program.InGame ? Program.lastFrame : null);

				Thread.Sleep(1000);
			}
		}

		private static void InitializeDiscord()
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

		// These 2 next function are mostly to avoid repeating code between Arena and Combat when building the details since it should be consistent between each game mode 

		/// <summary>
		/// Function that returns a string containing if the match is Public or Private and set the secrets if private
		/// </summary>
		private static string GetPrivateDetailsString(Frame frame, RichPresence rp)
		{
			if (frame.private_match)
			{
				rp.WithSecrets(new Secrets
				{
					JoinSecret = "spark://c/" + frame.sessionid,
					SpectateSecret = "spark://s/" + frame.sessionid,
				});
				return "Private ";
			}

			rp.WithSecrets(new Secrets
			{
				SpectateSecret = "spark://s/" + frame.sessionid,
			});
			return "Public ";
		}

		/// <summary>
		/// Function that returns a string corresponding to if the user is spectating or not
		/// </summary>
		private static string GetSpectatingDetailsString(Frame frame)
		{
			if (frame.GetPlayer(frame.client_name).team_color == Team.TeamColor.spectator)
			{
				return "Spectating ";
			}

			return "Playing (" + frame.ClientTeamColor + ") ";
		}

		private static void RemoveRichPresence()
		{
			try
			{
				discordClient.SetPresence(null);
			}
			catch (Exception)
			{
				LogRow(LogType.Error, "Discord RP client error when setting null presence.");
			}
		}

		private static void DisposeDiscord()
		{
			if (discordClient != null && !discordClient.IsDisposed)
			{
				discordClient.Dispose();
			}
		}

		private static void ProcessDiscordPresence(Frame frame)
		{
			// Check if the rich presence setting is enabled in Spark
			if (SparkSettings.instance.discordRichPresence)
			{
				if (discordClient == null || discordClient.IsDisposed)
				{
					//InitializeDiscord();
					LogRow(LogType.Error, "Discord RP client disposed while in normal thread.");
					return;
				}

				// Check if the frame is null and if Echo is closed
				if (frame == null && Program.connectionState == Program.ConnectionState.NotConnected)
				{
					// Setting match state variables back to default and the global state as Disconnected since Echo is no longer running
					lastStatus = lastPausedState = "Unknown";
					globalGameState = GlobalGameState.Disconnected;

					// Removing rich presence since Echo is no longer running
					RemoveRichPresence();
					return;
				}

				// Initializing the variables needed to make the rich presence status
				RichPresence rp = new RichPresence();
				StringBuilder details = new StringBuilder();
				StringBuilder state = new StringBuilder();

				// Process the frame if it's not null
				if (frame != null)
				{
					// If player is spectating as a moderator, don't update rich presence.
                    			if (frame.GetPlayer(frame.client_name) == null)
                    			{
                        			RemoveRichPresence();
                        			return;
                    			}
					
					// The user has requested to disable rich presence while spectating, disabling rich presence accordingly
					if (!string.IsNullOrEmpty(frame.teams[2].ToString()) && frame.teams[2].players.Find(p => p.name == frame.client_name) != null && SparkSettings.instance.discordRichPresenceSpectator)
					{
						RemoveRichPresence();
						return;
					}

					// Check which map the user is in
					switch (frame.map_name)
					{
						case "mpl_arena_a":
						{
							// User is in a level, setting globalGameState to InGame
							globalGameState = GlobalGameState.InGame;

							// Bulding the details section

							// Start with the game mode name
							details.Append("Arena ");

							// Check if the arena is Public or Private and set the status according to the result
							details.Append(GetPrivateDetailsString(frame, rp));

							// Adding the number of players per team in the team order as shown in game (Blue then Orange)
							details.Append("(" + frame.teams[0].players.Count + " v " + frame.teams[1].players.Count + ")");

							// Adding the score in the team order as shown in game (Blue then Orange)
							details.Append(": " + frame.blue_points + " - " + frame.orange_points);

							// Check if the user is spectating or playing the match and add set the proper status
							state.Append(GetSpectatingDetailsString(frame));


							// Building the State section

							// Checking if the match was paused
							if (frame.private_match && frame.pause.paused_state != "unpaused" && frame.pause.paused_state != "paused_requested")
							{
								// Check if the match is not already paused
								if (lastPausedState != frame.pause.paused_state)
								{
									// Set the lastPausedState to the current pause state since it just changed
									lastPausedState = frame.pause.paused_state;

									// Making sure to put the paused time based on the info from the game API so it shows accurate time if someone backfills a match while it's paused
									initialStateTime = DateTime.UtcNow.AddSeconds(-frame.pause.paused_timer);
								}

								// Set the status to the pause state and making the first letter uppercase so it looks prettier
								state.Append(" - " + char.ToUpper(frame.pause.paused_state[0]) + frame.pause.paused_state[1..]);

								rp.Timestamps = new Timestamps
								{
									Start = initialStateTime
								};
							}
							else
							{
								// If the frame return an empty or null game_status string, set it to the last knonwn status
								// This is done only if the game is not already paused or unpausing as the API returns an empty string for the game_status when the match is paused or unpausing
								if (string.IsNullOrEmpty(frame.game_status)) frame.game_status = lastStatus;

								// Check if the status has changed
								statusChanged = lastStatus != frame.game_status;
								lastStatus = frame.game_status;

								// If the user is in any pre-match state, set an elapsed timer instead of the remaining game time since it won't change until the match starts
								if (frame.game_status == "pre_match" || frame.game_status == "pre_sudden_death")
								{
									// Check if the status has changed and set the initialStateTime to the current time if it did
									if (statusChanged)
									{
										// Making sure the status is no longer changed to avoid resetting the timer
										statusChanged = false;
										initialStateTime = DateTime.UtcNow;
									}

									rp.Timestamps = new Timestamps
									{
										Start = initialStateTime
									};
								}
								// User is in a game that is not paused or in any pre-match state, set the remaining time to the one given by the game_clock value
								else
								{
									rp.Timestamps = new Timestamps
									{
										// If the game is in the post-match state, set the end time to now, otherwise set it based on the game_clock value
										End = frame.game_status == "post_match" ? DateTime.UtcNow : DateTime.UtcNow.AddSeconds(frame.game_clock)
									};
								}

								// Put the game status at the end of the state
								state.Append(" - " + prettyGameStatus[frame.game_status]);
							}

							break;
						}
						// Check if the user is in a Combat match
						case "mpl_combat_dyson":
						case "mpl_combat_combustion":
						case "mpl_combat_fission":
						case "mpl_combat_gauss":
						{
							// Setting up a generic timer for the elapsed time since the API doesn't return any time or match status reference in Combat
							if (globalGameState != GlobalGameState.InGame)
							{
								// User is in a level, setting globalGameState to InGame
								globalGameState = GlobalGameState.InGame;
								initialStateTime = DateTime.UtcNow;
							}

							rp.Timestamps = new Timestamps
							{
								Start = initialStateTime
							};


							// Bulding the details section 

							// Setting the map name
							details.Append(prettyCombatMapName[frame.map_name] + " ");

							//Check if the Combat is Public or Private and set the status according to the result
							details.Append(GetPrivateDetailsString(frame, rp));

							// Adding the number of players per team in the team order shown in game (Blue then Orange)
							details.Append("(" + frame.teams[0].players.Count + " v " + frame.teams[1].players.Count + ")");


							// Building the State section

							// Check if the user is spectating or playing the match
							state.Append(GetSpectatingDetailsString(frame));


							// Todo : Add actual stats when the new API drops
							break;
						}
						// User is not in a valid map, defaulting status to generic status
						default:
							// Setting game states back to default as the user is no longer considered in a game
							lastStatus = lastPausedState = "Unknown";

							// Setting the details to generic status message
							//details.Append("Playing Echo VR");

							// Use a generic timer of how long the user has been playing Echo
							if (globalGameState != GlobalGameState.Generic)
							{
								globalGameState = GlobalGameState.Generic;
								initialStateTime = DateTime.UtcNow;
							}

							rp.Timestamps = new Timestamps
							{
								Start = initialStateTime
							};
							break;
					}

					// Setting the state if it exists
					if (!string.IsNullOrEmpty(state.ToString())) rp.State = state.ToString();


					// Set the party info if the game state is valid
					if (globalGameState != GlobalGameState.Generic)
					{
						rp.WithParty(new Party
						{
							ID = frame.sessionid,
							Size = frame.GetAllPlayers().Count,
							Max = frame.private_match ? 15 : 8
						});
					}
				}
				else
				{
					// frame is null, set status based on ConnectionState
					switch (Program.connectionState)
					{
						// Looking at the connection state to determine if the user is in a Lobby as a workaround since /session is restricted in the lobby
						case Program.ConnectionState.InLobby:
						{
							//  Resetting the match status to default since the user is in a Lobby
							lastStatus = lastPausedState = "Unknown";

							// Set the status to only "in EchoVR Lobby" since the API doesn't give any informations in Lobbys
							details.Append("in EchoVR Lobby");

							// If the user just entered the Lobby, set initialStateTime to current time
							if (globalGameState != GlobalGameState.InLobby)
							{
								// Properly set the state to InLobby to avoid the timer being resetted while the user is still in the Lobby
								globalGameState = GlobalGameState.InLobby;
								initialStateTime = DateTime.UtcNow;
							}

							rp.Timestamps = new Timestamps
							{
								Start = initialStateTime
							};
							break;
						}
						// User is in a Transition
						case Program.ConnectionState.Menu:
						{
							// Ignore transition if using the generic status to preserve the elapsed time
							if (globalGameState == GlobalGameState.Generic) return;

							// Resetting the match status to default since the user is in a transition
							lastStatus = lastPausedState = "Unknown";

							// Set the status to just "In Transition" since the API doesn't give any information during a transition
							details.Append("In Transition");

							// If the user just entered the transition, set initialStateTime to current time and then set the status to Transitioning
							if (globalGameState != GlobalGameState.Transitioning)
							{
								globalGameState = GlobalGameState.Transitioning;
								initialStateTime = DateTime.UtcNow;
							}

							rp.Timestamps = new Timestamps
							{
								Start = initialStateTime
							};
							break;
						}
						// Api is not enabled, setting a generic "Playing Echo VR" status
						case Program.ConnectionState.NoAPI:
						{
							// API is no longer enabled, setting status to default values
							lastStatus = lastPausedState = "Unknown";

							// Use a generic timer of how long the user has been playing Echo
							if (globalGameState != GlobalGameState.Generic)
							{
								globalGameState = GlobalGameState.Generic;
								initialStateTime = DateTime.UtcNow;
							}

							rp.Timestamps = new Timestamps
							{
								Start = initialStateTime
							};
							break;
						}
					}
				}

				// Adding the details and assets to the RichPresence
				if (!string.IsNullOrEmpty(details.ToString())) rp.Details = details.ToString();

				rp.Assets = new Assets
				{
					//ToDo: Possibly make or find a Combat icon?
					LargeImageKey = "echo_arena_store_icon",
					LargeImageText = SparkSettings.instance.discordRichPresenceServerLocation &&
					                 !string.IsNullOrEmpty(Program.CurrentRound?.serverLocation)
						? Program.CurrentRound.serverLocation
						: Resources.Rich_presence_from_Spark
				};

				// Setting the Rich presence
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
