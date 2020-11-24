using Google.Cloud.TextToSpeech.V1;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;

namespace IgniteBot2
{
	/// <summary>
	/// 📖➡🔊 An abstraction layer for whatever TTS engine is being used
	/// </summary>
	class SpeechSynthesizer
	{
		private readonly TextToSpeechClient client;
		private readonly VoiceSelectionParams voice;
		bool playing = true;

		/// <summary>
		/// Queue of filenames to read
		/// </summary>
		ConcurrentQueue<string> ttsQueue = new ConcurrentQueue<string>();

		public SpeechSynthesizer()
		{

			// Instantiate a client
			TextToSpeechClientBuilder builder = new TextToSpeechClientBuilder
			{
				JsonCredentials = SecretKeys.firebaseJSONCredentials
			};
			client = builder.Build();

			// Build the voice request, select the language code ("en-US"),
			// and the SSML voice gender ("neutral").
			voice = new VoiceSelectionParams
			{
				LanguageCode = "en-US",
				Name = "en-US-Wavenet-D"
			};


			Thread ttsThread = new Thread(TTSThread);
			ttsThread.IsBackground = true;
			ttsThread.Start();

		}

		private void TTSThread()
		{
			MediaPlayer mediaPlayer = new MediaPlayer();
			mediaPlayer.MediaEnded += (sender, e) =>
			{
				playing = false;
			};
			while (Program.running)
			{
				if (ttsQueue.TryDequeue(out string result))
				{
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

					File.Delete(result);
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

		private async Task Speak(string text)
		{
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
