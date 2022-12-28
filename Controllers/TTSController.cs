using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Mime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using Newtonsoft.Json;
using Spark.Properties;

namespace Spark
{
	/// <summary>
	/// 📖➡🔊 An abstraction layer for whatever TTS engine is being used
	/// </summary>
	public class TTSController
	{
		private readonly string[,,] voiceTypes =
		{
			{ { "en-US-Wavenet-D", "en-US-Wavenet-C" }, { "ja-JP-Wavenet-D", "ja-JP-Wavenet-B" } },
			{ { "en-US-Standard-D", "en-US-Standard-C" }, { "ja-JP-Standard-D", "ja-JP-Standard-B" } }
		};

		private readonly string[] languages = { "en-US", "ja-JP" }; // 🌎

		private bool playing = true;
		private readonly Thread ttsThread;
		private readonly Queue<DateTime> rateLimiterQueue = new Queue<DateTime>();
		private const float rateLimitPerSecond = 15;
		private bool ttsDisabled = false;
		private string[] blacklistedNames = Array.Empty<string>();

		/// <summary>
		/// Queue of filenames to read
		/// </summary>
		private readonly ConcurrentQueue<string> ttsQueue = new ConcurrentQueue<string>();

		public static string CacheFolder => Path.Combine(Path.GetTempPath(), "SparkTTSCache");
		
		private readonly Stopwatch lastRulesChangedTimer = Stopwatch.StartNew();

		public TTSController()
		{
			// TTS won't work without Discord auth
			if (DiscordOAuth.firebaseCred == null) return;

			ttsThread = new Thread(TTSThread);
			ttsThread.IsBackground = true;
			ttsThread.Start();

			Task.Run(async () =>
			{
				string blacklistFilename = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IgniteVR", "Spark", "tts_blacklist.txt");
				if (File.Exists(blacklistFilename))
				{
					blacklistedNames = await File.ReadAllLinesAsync(blacklistFilename);
				}
			});

			#region Set up the event listeners to actually use TTS

			Program.PlayerJoined += (frame, team, player) =>
			{
				if (!SparkSettings.instance.playerJoinTTS) return;
				if (blacklistedNames.Contains(player.name)) return;
				SpeakAsync($"{player.name} {Resources.tts_join_1} {team.color} {Resources.tts_join_2}");
			};
			Program.PlayerLeft += (frame, team, player) =>
			{
				if (!SparkSettings.instance.playerLeaveTTS) return;
				if (blacklistedNames.Contains(player.name)) return;
				SpeakAsync($"{player.name} {Resources.tts_leave_1} {team.color} {Resources.tts_leave_2}");
			};
			Program.PlayerSwitchedTeams += (frame, fromTeam, toTeam, player) =>
			{
				if (!SparkSettings.instance.playerSwitchTeamTTS) return;
				if (blacklistedNames.Contains(player.name)) return;

				if (fromTeam != null)
				{
					SpeakAsync($"{player.name} {Resources.tts_switch_1} {fromTeam.color} {Resources.tts_switch_2} {toTeam.color} {Resources.tts_switch_3}");
				}
				else
				{
					SpeakAsync($"{player.name} {Resources.tts_switch_alt_1} {toTeam.color} {Resources.tts_switch_alt_2}");
				}
			};
			Program.PauseRequest += (frame, player, distance) =>
			{
				if (SparkSettings.instance.pausedTTS)
				{
					SpeakAsync($"{frame.pause.paused_requested_team} {Resources.tts_pause_req}");
				}
			};
			Program.GamePaused += (frame, player, distance) =>
			{
				if (SparkSettings.instance.pausedTTS)
				{
					SpeakAsync($"{frame.pause.paused_requested_team} {Resources.tts_paused}");
				}
			};
			Program.GameUnpaused += (frame, player, distance) =>
			{
				if (SparkSettings.instance.pausedTTS)
				{
					SpeakAsync($"{frame.pause.unpaused_team} {Resources.tts_unpause}");
				}
			};
			Program.LocalThrow += (frame) =>
			{
				if (SparkSettings.instance.throwSpeedTTS && frame.last_throw.total_speed > 10)
				{
					SpeakAsync($"{frame.last_throw.total_speed:N1}");
				}
			};
			Program.BigBoost += (frame, team, player, speed, howLongAgo) =>
			{
				if (SparkSettings.instance.maxBoostSpeedTTS && player.name == frame.client_name)
				{
					SpeakAsync($"{speed:N0} {Resources.tts_meters_per_second}");
				}
			};
			Program.PlayspaceAbuse += (frame, team, player, playspacePos) =>
			{
				if (SparkSettings.instance.playspaceTTS)
				{
					SpeakAsync($"{player.name} {Resources.tts_abused}");
				}
			};
			Program.Joust += (frame, team, player, isNeutral, joustTime, maxSpeed, maxTubeExitSpeed) =>
			{
				// only joust time
				if (SparkSettings.instance.joustTimeTTS && !SparkSettings.instance.joustSpeedTTS)
				{
					SpeakAsync($"{team.color} {joustTime:N1}");
				}
				// only joust speed
				else if (!SparkSettings.instance.joustTimeTTS && SparkSettings.instance.joustSpeedTTS)
				{
					SpeakAsync($"{team.color} {maxSpeed:N0} {Resources.tts_meters_per_second}");
				}
				// both
				else if (SparkSettings.instance.joustTimeTTS && SparkSettings.instance.joustSpeedTTS)
				{
					SpeakAsync($"{team.color} {joustTime:N1} {maxSpeed:N0} {Resources.tts_meters_per_second}");
				}
			};
			Program.Goal += (frame, goalEvent) =>
			{
				// combine if both true
				if (SparkSettings.instance.goalDistanceTTS && SparkSettings.instance.goalSpeedTTS)
				{
					SpeakAsync($"{frame.last_score.distance_thrown:N1} {Resources.tts_meters}. {frame.last_score.disc_speed:N1} {Resources.tts_meters_per_second}");
				}
				else if (SparkSettings.instance.goalDistanceTTS)
				{
					SpeakAsync($"{frame.last_score.distance_thrown:N1} {Resources.tts_meters}");
				}
				else if (SparkSettings.instance.goalSpeedTTS)
				{
					SpeakAsync($"{frame.last_score.disc_speed:N1} {Resources.tts_meters_per_second}");
				}
			};
			Program.RulesChanged += frame =>
			{
				if (SparkSettings.instance.rulesChangedTTS && lastRulesChangedTimer.Elapsed.TotalSeconds > 2)
				{
					SpeakAsync($"{frame.rules_changed_by} changed the rules");
				}
				lastRulesChangedTimer.Restart();
			};

			#endregion
		}

		~TTSController() // finalizer
		{
			ttsThread?.Abort();
		}

		// TODO convert to task with cancellation token
		private void TTSThread()
		{
			MediaPlayer mediaPlayer = new MediaPlayer();
			mediaPlayer.MediaEnded += (sender, e) =>
			{
				playing = false;
				Debug.WriteLine("stopped playing");
			};
			while (Program.running)
			{
				if (ttsQueue.TryDequeue(out string result))
				{
					mediaPlayer.Stop();
					mediaPlayer.Open(new Uri(result));

					playing = true;
					mediaPlayer.Play();

					// wait until it's done playing
					Thread.Sleep(100);

					Task.Run(TrimCacheFolder);
					// Task.Run(() => File.Delete(result));
				}
				else
				{
					Thread.Sleep(50);
				}
			}
		}

		public float Rate { get; private set; }

		public void SetRate(int selectedIndex)
		{
			Rate = 1 + ((selectedIndex - 1 /*index of Normal*/) * .4f /*Slope of the speed change*/);
		}

		public void SetOutputToDefaultAudioDevice()
		{
		}

		public void SpeakAsync(string text)
		{
			Task.Run(() => Speak(text));
		}

		private void Speak(string text)
		{
			// rate limiting
			// the length of the queue counts how many recent requests there are
			rateLimiterQueue.Enqueue(DateTime.UtcNow);
			
			// remove old items from the queue
			while ((DateTime.UtcNow - rateLimiterQueue.Peek()).TotalSeconds > 1)
			{
				rateLimiterQueue.Dequeue();
			}

			if (rateLimiterQueue.Count > rateLimitPerSecond)
			{
				//Speak("Rate Limit hit. TTS disabled. Please report this to NtsFranz.");
				ttsDisabled = true;
				Logger.LogRow(Logger.LogType.Error, "Rate Limit hit. " + text);
			}

			if (ttsDisabled) return;

			if (!Directory.Exists(CacheFolder))
			{
				Directory.CreateDirectory(CacheFolder);
			}

			string filePath = Path.Combine(CacheFolder, $"{Rate}_{SparkSettings.instance.languageIndex}_{SparkSettings.instance.useWavenetVoices}_{SparkSettings.instance.ttsVoice}_{text.Replace(" ", "_")}.mp3");

			// play from cache
			if (File.Exists(filePath))
			{
				// play the file
				ttsQueue.Enqueue(filePath);
				return;
			}
			
			_ = Task.Run(async () =>
			{
				string json = JsonConvert.SerializeObject(new Dictionary<string, object>
				{
					{"text", text},
					{"language_code", voiceTypes[SparkSettings.instance.useWavenetVoices ? 0 : 1, SparkSettings.instance.languageIndex, SparkSettings.instance.ttsVoice]},
					{"voice_name", voiceTypes[SparkSettings.instance.useWavenetVoices ? 0 : 1, SparkSettings.instance.languageIndex, SparkSettings.instance.ttsVoice]},
					{"speaking_rate", Rate},
				});
				HttpRequestMessage request = new HttpRequestMessage
				{
					Method = HttpMethod.Post,
					RequestUri = new Uri($"{Program.APIURL}/tts"),
					Content = new StringContent(json, Encoding.UTF8, MediaTypeNames.Application.Json),
				};
				HttpResponseMessage response = await FetchUtils.client.SendAsync(request);
				byte[] bytes = await response.Content.ReadAsByteArrayAsync();
			
				// Write the audio content of the response to an MP3 file.
				if (bytes.Length > 0)
				{
					await File.WriteAllBytesAsync(filePath, bytes);
				}

				// play the file
				ttsQueue.Enqueue(filePath);
			});
		}

		public static void ClearCacheFolder()
		{
			try
			{
				if (Directory.Exists(CacheFolder))
				{
					DirectoryInfo di = new DirectoryInfo(CacheFolder);

					foreach (FileInfo file in di.GetFiles())
					{
						file.Delete();
					}
				}
			}
			catch (Exception e)
			{
				Logger.LogRow(Logger.LogType.Error, "Failed clearing TTS cache\n" + e);
			}
		}

		public static void TrimCacheFolder()
		{
			try
			{
				if (Directory.Exists(CacheFolder))
				{
					DirectoryInfo di = new DirectoryInfo(CacheFolder);

					List<FileInfo> files = di.GetFiles().ToList();
					files.Sort((f1, f2) => f1.LastAccessTime.CompareTo(f2.LastAccessTime));
					long size = files.Sum(file => file.Length);

					// delete files over the cache size limit
					while (size > SparkSettings.instance.ttsCacheSizeBytes)
					{
						FileInfo removed = files[0];
						files.RemoveAt(0);
						size -= removed.Length;
						removed.Delete();
					}
				}
			}
			catch (Exception e)
			{
				Logger.LogRow(Logger.LogType.Error, "Failed deleting files from cache\n" + e);
			}
		}
	}
}