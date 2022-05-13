using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using Vosk;
using NAudio.Wave;
using Newtonsoft.Json;
using System.Net;
using System.IO.Compression;

namespace Spark
{
	public class SpeechRecognition
	{
		public float micLevel = 0;
		public float speakerLevel = 0;

		private bool capturing;
		private WaveInEvent micCapture;
		private WasapiLoopbackCapture speakerCapture;
		private VoskRecognizer voskRecMic;
		private VoskRecognizer voskRecSpeaker;

		public bool Enabled
		{
			get => capturing;
			set
			{
				if (value != capturing)
				{
					try
					{
						if (value)
						{
							// speechRecognizer.ContinuousRecognitionSession.StartAsync();
						}
						else
						{
							// speechRecognizer.ContinuousRecognitionSession.StopAsync();
						}
					}
					catch (Exception e)
					{
						Logger.LogRow(Logger.LogType.Error, "Error starting/stopping voice rec.\n" + e);
					}
				}

				capturing = value;
			}
		}

		public SpeechRecognition()
		{
			try
			{
				Vosk.Vosk.SetLogLevel(0);

				_ = Task.Run(DownloadVoskModel);
			}
			catch (Exception e)
			{
				Logger.LogRow(Logger.LogType.Error, "Error starting voice rec.\n" + e);
			}
		}

		private async Task DownloadVoskModel()
		{
			string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IgniteVR", "Spark", "vosk-model-small-en-us-0.15");
			if (Directory.Exists(path))
			{
				AfterDownload(path);
			}
			else
			{
				Logger.LogRow(Logger.LogType.Error, "Vosk model not found. Downloading.");
				WebClient webClient = new WebClient();
				webClient.Headers.Add("Accept: text/html, application/xhtml+xml, */*");
				webClient.Headers.Add("User-Agent: Spark");
				string zipFile = Path.Combine(Path.GetTempPath(), "vosk_model.zip");
				await webClient.DownloadFileTaskAsync(new Uri("https://alphacephei.com/vosk/models/vosk-model-small-en-us-0.15.zip"), zipFile);
				ZipFile.ExtractToDirectory(zipFile, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IgniteVR", "Spark"));
				if (Directory.Exists(path))
				{
					AfterDownload(path);
				}
				else
				{
					Logger.LogRow(Logger.LogType.Error, "Vosk model failed to download.");
				}
			}
		}

		private void AfterDownload(string path)
		{
			Model model = new Model(path);

			voskRecMic = new VoskRecognizer(model, 16000f);
			voskRecMic.SetMaxAlternatives(10);
			voskRecMic.SetWords(true);

			micCapture = new WaveInEvent();
			micCapture.WaveFormat = new WaveFormat(16000, 1);
			micCapture.DeviceNumber = GetMicByName(SparkSettings.instance.microphone);
			micCapture.DataAvailable += MicDataAvailable;
			micCapture.StartRecording();

			// voskRecSpeaker = new VoskRecognizer(model, 16000f);
			// voskRecSpeaker.SetMaxAlternatives(10);
			// voskRecSpeaker.SetWords(true);

			// speakerCapture = new WasapiLoopbackCapture(GetSpeakerByName(SparkSettings.instance.speaker));
			// speakerCapture.DataAvailable += SpeakerDataAvailable;
			// speakerCapture.StartRecording();
		}

		private void SpeakerDataAvailable(object sender, WaveInEventArgs e)
		{
			if (!SparkSettings.instance.enableVoiceRecognition) return;

			speakerLevel = 0;

			float[] floats = new float[e.BytesRecorded / 4];

			MemoryStream mem = new MemoryStream(e.Buffer);
			BinaryReader reader = new BinaryReader(mem);
			for (int index = 0; index < e.BytesRecorded / 4; index++)
			{
				float sample = reader.ReadSingle();

				// absolute value 
				if (sample < 0) sample = -sample;
				// is this the max value?
				if (sample > speakerLevel) speakerLevel = sample;

				floats[index / 4] = sample;
			}

			if (voskRecSpeaker.AcceptWaveform(floats, floats.Length))
			{
				HandleResult(voskRecSpeaker.Result());
			}
		}

		private void MicDataAvailable(object sender, WaveInEventArgs e)
		{
			if (!SparkSettings.instance.enableVoiceRecognition) return;

			micLevel = 0;
			// interpret as 16 bit audio
			for (int index = 0; index < e.BytesRecorded; index += 2)
			{
				short sample = (short)((e.Buffer[index + 1] << 8) | e.Buffer[index + 0]);
				// to floating point
				float sample32 = sample / 32768f;
				// absolute value 
				if (sample32 < 0) sample32 = -sample32;
				// is this the max value?
				if (sample32 > micLevel) micLevel = sample32;
			}

			if (voskRecMic.AcceptWaveform(e.Buffer, e.BytesRecorded))
			{
				HandleResult(voskRecMic.Result());
			}
		}

		private static void HandleResult(string result)
		{
			try
			{
				List<string> clipTerms = new List<string>();

				if (SparkSettings.instance.clipThatDetectionNVHighlights || SparkSettings.instance.clipThatDetectionMedal)
				{
					clipTerms.AddRange(new string[] {
						"clip that",
						"quebec",
						"hope that",
						"could that",
						"cop that",
						"say cheese",
					});
				}
				if (SparkSettings.instance.badWordDetectionNVHighlights || SparkSettings.instance.badWordDetectionMedal)
				{
					clipTerms.AddRange(new string[] {
						// downloaded bad words 
					});
				}

				Dictionary<string, List<Dictionary<string, object>>> r = JsonConvert.DeserializeObject<Dictionary<string, List<Dictionary<string, object>>>>(result);
				if (r == null) return;
				foreach (Dictionary<string, object> alt in r["alternatives"])
				{
					if (string.IsNullOrWhiteSpace(alt["text"].ToString())) continue;

					Debug.WriteLine(alt["text"].ToString());


					foreach (string clipTerm in clipTerms)
					{
						if (alt["text"].ToString()?.Contains(clipTerm) ?? false)
						{
							Program.ManualClip?.Invoke();

							if (SparkSettings.instance.clipThatDetectionMedal)
							{
								Medal.ClipNow();
							}
							if (SparkSettings.instance.clipThatDetectionNVHighlights)
							{
								HighlightsHelper.SaveHighlight("PERSONAL_HIGHLIGHT_GROUP", "MANUAL", true);
							}

							Program.synth.SpeakAsync("Clip Saved!");
							return;
						}
					}
				}
			}
			catch (Exception e)
			{
				Logger.LogRow(Logger.LogType.Error, "Error handling voice result: " + e);
			}
		}

		private static int GetMicByName(string name)
		{
			for (int deviceId = 0; deviceId < WaveIn.DeviceCount; deviceId++)
			{
				WaveInCapabilities deviceInfo = WaveIn.GetCapabilities(deviceId);
				if (deviceInfo.ProductName == name)
				{
					return deviceId;
				}
			}

			return 0;
		}

		private static MMDevice GetSpeakerByName(string name)
		{
			MMDeviceEnumerator enumerator = new MMDeviceEnumerator();
			List<MMDevice> devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).ToList();
			int index = devices.Select(d => d.FriendlyName).ToList().IndexOf(name);
			if (index == -1) index = 0;
			return devices[index];
		}


		public float GetMicLevel()
		{
			return micLevel;
		}

		public float GetSpeakerLevel()
		{
			return speakerLevel;
		}

		public async Task ReloadMic()
		{
			micCapture.StopRecording();
			micCapture.DeviceNumber = GetMicByName(SparkSettings.instance.microphone);
			await Task.Delay(100);
			micCapture.StartRecording();
		}

		public async Task ReloadSpeaker()
		{
			speakerCapture.StopRecording();
			await Task.Delay(100);
			speakerCapture = new WasapiLoopbackCapture(GetSpeakerByName(SparkSettings.instance.speaker));
			speakerCapture.DataAvailable += SpeakerDataAvailable;
			speakerCapture.StartRecording();
		}
	}
}