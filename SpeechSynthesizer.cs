using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using Google.Cloud.TextToSpeech.V1;

namespace IgniteBot
{
	/// <summary>
	/// 📖➡🔊 An abstraction layer for whatever TTS engine is being used
	/// </summary>
	class SpeechSynthesizer
	{
		private readonly TextToSpeechClient client;
		private readonly VoiceSelectionParams voice;
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

			// Build the voice request, select the language code ("en-US"),
			// and the SSML voice gender ("neutral").
			voice = new VoiceSelectionParams
			{
				LanguageCode = "en-US",
				Name = "en-US-Wavenet-D"
			};


			ttsThread = new Thread(TTSThread);
			ttsThread.IsBackground = true;
			ttsThread.Start();

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
				Voice = voice,
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
