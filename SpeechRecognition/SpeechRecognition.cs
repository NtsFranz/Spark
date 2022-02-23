using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Documents;
using Windows.Media.SpeechRecognition;

namespace Spark
{
	public class SpeechRecognition
	{
		private readonly SpeechRecognizer speechRecognizer;
		private bool capturing;

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
							speechRecognizer.ContinuousRecognitionSession.StartAsync();
						}
						else
						{
							speechRecognizer.ContinuousRecognitionSession.StopAsync();
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
				speechRecognizer = new SpeechRecognizer();
				List<string> constraints = new List<string>
				{
					"clip that",
				};
				speechRecognizer.Constraints.Add(new SpeechRecognitionListConstraint(constraints));
				speechRecognizer.CompileConstraintsAsync();
				speechRecognizer.ContinuousRecognitionSession.ResultGenerated += ContinuousRecognitionSession_ResultGenerated;
				if (SparkSettings.instance.enableVoiceRecognition)
				{
					speechRecognizer.ContinuousRecognitionSession.StartAsync();
					capturing = true;
				}
			}
			catch (Exception e)
			{
				Logger.LogRow(Logger.LogType.Error, "Error starting voice rec.\n" + e);
			}
		}

		private static void ContinuousRecognitionSession_ResultGenerated(SpeechContinuousRecognitionSession sender, SpeechContinuousRecognitionResultGeneratedEventArgs args)
		{
			Debug.WriteLine(args.Result.Text);
			switch (args.Result.Text)
			{
				case "clip that":
				{
					Program.ManualClip?.Invoke();
					HighlightsHelper.SaveHighlight("PERSONAL_HIGHLIGHT_GROUP", "MANUAL", true);
					Program.synth.SpeakAsync("Clip saved!");
					break;
				}
			}
		}
	}
}