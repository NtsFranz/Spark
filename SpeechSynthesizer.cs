using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using Google.Cloud.TextToSpeech.V1;
using IgniteBot.Properties;

namespace IgniteBot
{
	/// <summary>
	/// 📖➡🔊 An abstraction layer for whatever TTS engine is being used
	/// </summary>
	class SpeechSynthesizer
	{
		private string[,] voiceTypes = { { "en-US-Wavenet-D", "en-US-Wavenet-C" }, { "ja-JP-Wavenet-D", "ja-JP-Wavenet-B" } };
		public string[] languages = { "en-US", "ja-JP" };

		private readonly TextToSpeechClient client;
		bool playing = true;
		Thread ttsThread;

		/// <summary>
		/// Queue of filenames to read
		/// </summary>
		ConcurrentQueue<string> ttsQueue = new ConcurrentQueue<string>();

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
				if (Settings.Default.playerJoinTTS)
				{
					Program.synth.SpeakAsync($"{player.name} {Resources.tts_join_1} {team.color} {Resources.tts_join_2}");
				}
			};
			Program.PlayerLeft += (frame, team, player) =>
			{
				if (Settings.Default.playerLeaveTTS)
				{
					Program.synth.SpeakAsync($"{player.name} {Resources.tts_leave_1} {team.color} {Resources.tts_leave_2}");
				}
			};
			Program.PlayerSwitchedTeams += (frame, fromTeam, toTeam, player) =>
			{
				if (Settings.Default.playerSwitchTeamTTS)
				{
					Program.synth.SpeakAsync($"{player.name} {Resources.tts_switch_1} {fromTeam.color} {Resources.tts_switch_2} {toTeam.color} {Resources.tts_switch_3}");
				}
			};
			Program.PauseRequest += (frame) =>
			{
				if (Settings.Default.pausedTTS)
				{
					Program.synth.SpeakAsync($"{frame.pause.paused_requested_team} {Resources.tts_pause_req}");
				}
			};
			Program.GamePaused += (frame) =>
			{
				if (Settings.Default.pausedTTS)
				{
					Program.synth.SpeakAsync($"{frame.pause.paused_requested_team} {Resources.tts_paused}");
				}
			};
			Program.GameUnpaused += (frame) =>
			{
				if (Settings.Default.pausedTTS)
				{
					Program.synth.SpeakAsync($"{frame.pause.paused_requested_team} {Resources.tts_unpause}");
				}
			};
			Program.LocalThrow += (frame) =>
			{
				if (Settings.Default.throwSpeedTTS)
				{
					Program.synth.SpeakAsync($"{frame.last_throw.total_speed}");
				}
			};
			Program.BigBoost += (frame, team, player, speed, howLongAgo) =>
			{
				if (player.name == frame.client_name)
				{
					if (Settings.Default.maxBoostSpeedTTS)
					{
						Program.synth.SpeakAsync($"{speed:N0} {Resources.tts_meters_per_second}");
					}
				}
			};
			Program.PlayspaceAbuse += (frame, team, player, playspacePos) =>
			{
				if (Settings.Default.playspaceTTS)
				{
					Program.synth.SpeakAsync($"{player.name} {Resources.tts_abused}");
				}
			};
			Program.Joust += (frame, team, player, isNeutral, joustTime, maxSpeed, maxTubeExitSpeed) =>
			{
				// only joust time
				if (Settings.Default.joustTimeTTS && !Settings.Default.joustSpeedTTS)
				{
					Program.synth.SpeakAsync($"{team.color} {joustTime:N1}");
				}
				// only joust speed
				else if (!Settings.Default.joustTimeTTS && Settings.Default.joustSpeedTTS)
				{
					Program.synth.SpeakAsync($"{team.color} {maxSpeed:N0} {Resources.tts_meters_per_second}");
				}
				// both
				else if (Settings.Default.joustTimeTTS && Settings.Default.joustSpeedTTS)
				{
					Program.synth.SpeakAsync($"{team.color} {joustTime:N1} {maxSpeed:N0} {Resources.tts_meters_per_second}");
				}
			};
			Program.Goal += (frame, goalEvent) =>
			{
				if (Settings.Default.goalDistanceTTS)
				{
					Program.synth.SpeakAsync($"{frame.last_score.distance_thrown:N1} {Resources.tts_meters}");
				}

				if (Settings.Default.goalSpeedTTS)
				{
					Program.synth.SpeakAsync($"{frame.last_score.disc_speed:N1} {Resources.tts_meters_per_second}");
				}
			};
			#endregion

		}

		~SpeechSynthesizer()  // finalizer
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
			Rate = 1 + ((selectedIndex - 1 /*index of Normal*/ ) * .4f /*Slope of the speed change*/);
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
				Pitch = -5,
				SpeakingRate = Rate
			};

			// Perform the Text-to-Speech request, passing the text input
			// with the selected voice parameters and audio file type
			var response = client.SynthesizeSpeech(new SynthesizeSpeechRequest
			{
				Input = input,
				Voice = new VoiceSelectionParams
				{
					LanguageCode = languages[Settings.Default.language],
					Name = voiceTypes[Settings.Default.language, Settings.Default.ttsVoice]
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
