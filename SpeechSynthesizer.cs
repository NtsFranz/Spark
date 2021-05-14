using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using Google.Cloud.TextToSpeech.V1;
using Spark.Properties;

namespace Spark
{
	/// <summary>
	/// 📖➡🔊 An abstraction layer for whatever TTS engine is being used
	/// </summary>
	class SpeechSynthesizer
	{
		private readonly string[,,] voiceTypes =
		{
			{{"en-US-Wavenet-D", "en-US-Wavenet-C"}, {"ja-JP-Wavenet-D", "ja-JP-Wavenet-B"}},
			{{"en-US-Standard-D", "en-US-Standard-C"}, {"ja-JP-Standard-D", "ja-JP-Standard-B"}}
		};

		private readonly string[] languages = {"en-US", "ja-JP"}; // 🌎

		private readonly TextToSpeechClient client;
		private bool playing = true;
		private readonly Thread ttsThread;

		/// <summary>
		/// Queue of filenames to read
		/// </summary>
		private readonly ConcurrentQueue<string> ttsQueue = new ConcurrentQueue<string>();

		public SpeechSynthesizer()
		{
			// TTS won't work without Discord auth
			if (DiscordOAuth.firebaseCred == null) return;

			// Instantiate a client
			TextToSpeechClientBuilder builder = new TextToSpeechClientBuilder
			{
				JsonCredentials = DiscordOAuth.firebaseCred
			};
			client = builder.Build();

			ttsThread = new Thread(TTSThread);
			ttsThread.IsBackground = true;
			ttsThread.Start();


			#region Set up the event listeners to actually use TTS

			Program.PlayerJoined += (frame, team, player) =>
			{
				if (SparkSettings.instance.playerJoinTTS)
				{
					Program.synth.SpeakAsync($"{player.name} {Resources.tts_join_1} {team.color} {Resources.tts_join_2}");
				}
			};
			Program.PlayerLeft += (frame, team, player) =>
			{
				if (SparkSettings.instance.playerLeaveTTS)
				{
					Program.synth.SpeakAsync($"{player.name} {Resources.tts_leave_1} {team.color} {Resources.tts_leave_2}");
				}
			};
			Program.PlayerSwitchedTeams += (frame, fromTeam, toTeam, player) =>
			{
				if (!SparkSettings.instance.playerSwitchTeamTTS) return;

				if (fromTeam != null)
				{
					Program.synth.SpeakAsync($"{player.name} {Resources.tts_switch_1} {fromTeam.color} {Resources.tts_switch_2} {toTeam.color} {Resources.tts_switch_3}");
				}
				else
				{
					Program.synth.SpeakAsync($"{player.name} {Resources.tts_switch_alt_1} {toTeam.color} {Resources.tts_switch_alt_2}");
				}
			};
			Program.PauseRequest += (frame) =>
			{
				if (SparkSettings.instance.pausedTTS)
				{
					Program.synth.SpeakAsync($"{frame.pause.paused_requested_team} {Resources.tts_pause_req}");
				}
			};
			Program.GamePaused += (frame) =>
			{
				if (SparkSettings.instance.pausedTTS)
				{
					Program.synth.SpeakAsync($"{frame.pause.paused_requested_team} {Resources.tts_paused}");
				}
			};
			Program.GameUnpaused += (frame) =>
			{
				if (SparkSettings.instance.pausedTTS)
				{
					Program.synth.SpeakAsync($"{frame.pause.paused_requested_team} {Resources.tts_unpause}");
				}
			};
			Program.LocalThrow += (frame) =>
			{
				if (SparkSettings.instance.throwSpeedTTS && frame.last_throw.total_speed > 10)
				{
					Program.synth.SpeakAsync($"{frame.last_throw.total_speed:N1}");
				}
			};
			Program.BigBoost += (frame, team, player, speed, howLongAgo) =>
			{
				if (SparkSettings.instance.maxBoostSpeedTTS && player.name == frame.client_name)
				{
					Program.synth.SpeakAsync($"{speed:N0} {Resources.tts_meters_per_second}");
				}
			};
			Program.PlayspaceAbuse += (frame, team, player, playspacePos) =>
			{
				if (SparkSettings.instance.playspaceTTS)
				{
					Program.synth.SpeakAsync($"{player.name} {Resources.tts_abused}");
				}
			};
			Program.Joust += (frame, team, player, isNeutral, joustTime, maxSpeed, maxTubeExitSpeed) =>
			{
				// only joust time
				if (SparkSettings.instance.joustTimeTTS && !SparkSettings.instance.joustSpeedTTS)
				{
					Program.synth.SpeakAsync($"{team.color} {joustTime:N1}");
				}
				// only joust speed
				else if (!SparkSettings.instance.joustTimeTTS && SparkSettings.instance.joustSpeedTTS)
				{
					Program.synth.SpeakAsync($"{team.color} {maxSpeed:N0} {Resources.tts_meters_per_second}");
				}
				// both
				else if (SparkSettings.instance.joustTimeTTS && SparkSettings.instance.joustSpeedTTS)
				{
					Program.synth.SpeakAsync($"{team.color} {joustTime:N1} {maxSpeed:N0} {Resources.tts_meters_per_second}");
				}
			};
			Program.Goal += (frame, goalEvent) =>
			{
				if (SparkSettings.instance.goalDistanceTTS)
				{
					Program.synth.SpeakAsync($"{frame.last_score.distance_thrown:N1} {Resources.tts_meters}");
				}

				if (SparkSettings.instance.goalSpeedTTS)
				{
					Program.synth.SpeakAsync($"{frame.last_score.disc_speed:N1} {Resources.tts_meters_per_second}");
				}
			};

			#endregion
		}

		~SpeechSynthesizer() // finalizer
		{
			ttsThread?.Abort();
		}

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

					// TODO actually wait until the previous one is finished
					//while (!mediaPlayer.NaturalDuration.HasTimeSpan)
					//{
					//	Thread.Sleep(10);
					//}
					//Thread.Sleep((int)mediaPlayer.NaturalDuration.TimeSpan.TotalMilliseconds);
					//while (playing)
					//{
					//	Thread.Sleep(10);
					//}
					Task.Run(() => File.Delete(result));
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
			if (client == null) return;

			// Set the text input to be synthesized.
			SynthesisInput input = new SynthesisInput
			{
				Text = text
			};


			// Select the type of audio file you want returned.
			AudioConfig config = new AudioConfig
			{
				AudioEncoding = AudioEncoding.Mp3,
				// Pitch = -5,
				SpeakingRate = Rate
			};

			// Perform the Text-to-Speech request, passing the text input
			// with the selected voice parameters and audio file type
			SynthesizeSpeechResponse response = client.SynthesizeSpeech(new SynthesizeSpeechRequest
			{
				Input = input,
				Voice = new VoiceSelectionParams
				{
					LanguageCode = languages[SparkSettings.instance.languageIndex],
					Name = voiceTypes[SparkSettings.instance.useWavenetVoices ? 0 : 1, SparkSettings.instance.languageIndex, SparkSettings.instance.ttsVoice]
				},
				AudioConfig = config
			});

			// Write the AudioContent of the response to an MP3 file.
			string s64 = response.AudioContent.ToBase64();
			string filePath = Path.Combine(Path.GetTempPath(), "ttsoutput64_" + DateTime.Now.ToFileTime() + ".mp3");
			File.WriteAllBytes(filePath, Convert.FromBase64String(s64));

			// play the file
			ttsQueue.Enqueue(filePath);
		}
	}
}