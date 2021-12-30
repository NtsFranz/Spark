using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using ButterReplays;
using EchoVRAPI;
using Newtonsoft.Json;
using static Logger;

namespace Spark
{
	public class ReplayFilesManager
	{
		
		// public Milk milkData;
		private ButterFile butter;
		
		public string fileName;
		
		private readonly object butterWritingLock = new object();
		private readonly object fileWritingLock = new object();
		
		public ConcurrentQueue<string> lastJSONQueue = new ConcurrentQueue<string>();
		public ConcurrentQueue<string> lastDateTimeStringQueue = new ConcurrentQueue<string>();
		public ConcurrentStack<Frame> milkFramesToSave = new ConcurrentStack<Frame>();
		public ConcurrentQueue<DateTime> replayBufferTimestamps = new ConcurrentQueue<DateTime>();
		public ConcurrentQueue<string> replayBufferJSON = new ConcurrentQueue<string>();
		public ConcurrentQueue<string> replayBufferJSONBones = new ConcurrentQueue<string>();

		private const string echoreplayDateFormat = "yyyy/MM/dd HH:mm:ss.fff";
		
		private int lastButterNumChunks = 0;
		private ulong lastButterFetchFrameIndex = 0;
		
		/// <summary>
		/// For replay file saving in batches
		/// </summary>
		private readonly List<DateTime> dataCacheTimestamps = new List<DateTime>();
		private readonly List<string> dataCacheLines = new List<string>();
		
		
		private static readonly List<float> fullDeltaTimes = new List<float> { 16.6666666f, 33.3333333f, 100 };
		private int FrameInterval => Math.Clamp((int)(fullDeltaTimes[SparkSettings.instance.targetDeltaTimeIndexFull] / Program.StatsHz), 1, 10000);
		private int frameIndex = 0;
		
		public ReplayFilesManager()
		{
			butter = new ButterFile(compressionFormat: SparkSettings.instance.butterCompressionFormat);

			Split();
			
			Program.NewFrame += AddButterFrame;
			// Program.NewFrame += AddMilkFrame;
			Program.FrameFetched += AddEchoreplayFrame;

			Program.JoinedGame += frame =>
			{
				Split();
			};
			Program.LeftGame += frame =>
			{
				Task.Run(async () =>
				{
					await Task.Delay(50);
					Split();
				});
			};
			Program.RoundOver += frame =>
			{
				Split();
			};
			Program.SparkClosing += () =>
			{
				Task.Run(async () =>
				{
					await Task.Delay(50);
					Split();
				});
				Split();
			};
		}
		
		private void AddEchoreplayFrame(DateTime timestamp, string session, string bones)
		{
			frameIndex++;
			
			if (!SparkSettings.instance.enableReplayBuffer)
			{
				if (!SparkSettings.instance.enableFullLogging) return;
				if (!SparkSettings.instance.saveEchoreplayFiles) return;
			}

			if (frameIndex % FrameInterval != 0) return;

			try
			{
				// if this is not a lobby api frame
				if (session.Length <= 800) return;
				
				if (SparkSettings.instance.enableFullLogging)
				{
					bool log = false;
					if (SparkSettings.instance.onlyRecordPrivateMatches)
					{
						SimpleFrame obj = JsonConvert.DeserializeObject<SimpleFrame>(session);
						if (obj?.private_match == true)
						{
							log = true;
						}
					}
					else
					{
						log = true;
					}

					if (log)
					{
						if (bones != null)
						{
							WriteToFile(timestamp.ToString(echoreplayDateFormat) + "\t" + session + "\t" + bones);
						}
						else
						{
							WriteToFile(timestamp.ToString(echoreplayDateFormat) + "\t" + session);	
						}
					}
				}


				if (SparkSettings.instance.enableReplayBuffer)
				{
					// add to replay buffer
					replayBufferTimestamps.Enqueue(timestamp);
					replayBufferJSON.Enqueue(session);
					replayBufferJSONBones.Enqueue(bones);

					// shorten the buffer to match the desired length
					while (timestamp - replayBufferTimestamps.First() > TimeSpan.FromSeconds(SparkSettings.instance.replayBufferLength))
					{
						replayBufferTimestamps.TryDequeue(out _);
						replayBufferJSON.TryDequeue(out _);
						replayBufferJSONBones.TryDequeue(out _);
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine("Error in adding .echoreplay frame " + ex);
			}
		}
		
		// private void MilkThread()
		// {
		// 	Thread.Sleep(2000);
		// 	int frameCount = 0;
		// 	// Session pull loop.
		// 	while (running)
		// 	{
		// 		if (milkFramesToSave.TryPop(out Frame frame))
		// 		{
		// 			if (milkData == null)
		// 			{
		// 				milkData = new Milk(frame);
		// 			}
		// 			else
		// 			{
		// 				milkData.AddFrame(frame);
		// 			}
		//
		// 			frameCount++;
		// 		}
		//
		// 		// only save every once in a while
		// 		if (frameCount > 200)
		// 		{
		// 			frameCount = 0;
		// 			string filePath = Path.Combine(SparkSettings.instance.saveFolder, fileName + ".milk");
		// 			File.WriteAllBytes(filePath, milkData.GetBytes());
		// 		}
		//
		// 		Thread.Sleep(fullDeltaTimes[SparkSettings.instance.targetDeltaTimeIndexFull]);
		// 	}
		// }


		private void AddButterFrame(Frame f)
		{
			if (!SparkSettings.instance.enableFullLogging) return;
			if (!SparkSettings.instance.saveButterFiles) return;
			
			if (frameIndex % FrameInterval != 0) return;
			
			butter.AddFrame(f);
			if (lastButterNumChunks != butter.NumChunks())
			{
				WriteOutButterFile();
			}
			lastButterNumChunks = butter.NumChunks();
		}

		private void WriteOutButterFile()
		{
			lock (butterWritingLock)
			{
				byte[] butterBytes = butter?.GetBytes();
				if (butterBytes != null && butterBytes.Length > 0)
				{
					File.WriteAllBytes(Path.Combine(SparkSettings.instance.saveFolder, fileName + ".butter"), butterBytes);
				}
			}
		}

		/// <summary>
		/// Writes the data to the file
		/// </summary>
		/// <param name="data">The data to write</param>
		private void WriteToFile(string data)
		{
			lock (fileWritingLock)
			{
				if (SparkSettings.instance.batchWrites)
				{
					dataCacheTimestamps.Add(DateTime.UtcNow);
					dataCacheLines.Add(data);

					// if the time elapsed since last write is less than cutoff
					if (DateTime.UtcNow - dataCacheTimestamps[0] < TimeSpan.FromSeconds(5))
					{
						return;
					}
				}

				// Fail if the folder doesn't even exist
				if (!Directory.Exists(SparkSettings.instance.saveFolder))
				{
					return;
				}

				string filePath, directoryPath;

				// could combine with some other data path, such as AppData
				directoryPath = SparkSettings.instance.saveFolder;

				filePath = Path.Combine(directoryPath, fileName + ".echoreplay");

				lock (fileWritingLock)
				{
					StreamWriter streamWriter = new StreamWriter(filePath, true);

					if (SparkSettings.instance.batchWrites)
					{
						foreach (string row in dataCacheLines)
						{
							streamWriter.WriteLine(row);
						}

						dataCacheLines.Clear();
						dataCacheTimestamps.Clear();
					}
					else
					{
						streamWriter.WriteLine(data);
					}

					streamWriter.Close();
				}
			}
		}

		public void SaveReplayClip(string filename)
		{
			string[] frames = replayBufferJSON.ToArray();
			DateTime[] timestamps = replayBufferTimestamps.ToArray();

			if (frames.Length != timestamps.Length)
			{
				LogRow(LogType.Error, "Something went wrong in the replay buffer saving.");
				return;
			}

			string fullFileName = $"{DateTime.Now:clip_yyyy-MM-dd_HH-mm-ss}_{filename}";
			string filePath = Path.Combine(SparkSettings.instance.saveFolder, $"{fullFileName}.echoreplay");

			lock (fileWritingLock)
			{
				StreamWriter streamWriter = new StreamWriter(filePath, false);

				for (int i = 0; i < frames.Length; i++)
				{
					streamWriter.WriteLine(timestamps[i].ToString(echoreplayDateFormat) + "\t" + frames[i]);
				}

				streamWriter.Close();

				// compress the file
				if (SparkSettings.instance.useCompression)
				{
					string tempDir = Path.Combine(SparkSettings.instance.saveFolder, "temp_zip");
					Directory.CreateDirectory(tempDir);
					File.Move(filePath,
						Path.Combine(tempDir, $"{fullFileName}.echoreplay"));
					ZipFile.CreateFromDirectory(tempDir, filePath);
					Directory.Delete(tempDir, true);
				}
			}
		}
		

		/// <summary>
		/// Generates a new filename from the current time.
		/// </summary>
		public void Split()
		{
			lock (fileWritingLock)
			{
				string lastFilename = fileName;
				fileName = DateTime.Now.ToString("rec_yyyy-MM-dd_HH-mm-ss");

				// compress the file
				if (SparkSettings.instance.useCompression)
				{
					if (File.Exists(Path.Combine(SparkSettings.instance.saveFolder, lastFilename + ".echoreplay")))
					{
						string tempDir = Path.Combine(SparkSettings.instance.saveFolder, "temp_zip");
						Directory.CreateDirectory(tempDir);
						File.Move(Path.Combine(SparkSettings.instance.saveFolder, lastFilename + ".echoreplay"),
							Path.Combine(SparkSettings.instance.saveFolder, "temp_zip",
								lastFilename + ".echoreplay")); // TODO can fail because in use
						ZipFile.CreateFromDirectory(tempDir, Path.Combine(SparkSettings.instance.saveFolder, lastFilename + ".echoreplay"));
						Directory.Delete(tempDir, true);
					}
				}
			}

			lock (butterWritingLock)
			{
				WriteOutButterFile();
				butter = new ButterFile(compressionFormat: SparkSettings.instance.butterCompressionFormat);
			}

			// reset the replay buffer
			replayBufferTimestamps.Clear();
			replayBufferJSON.Clear();
			replayBufferJSONBones.Clear();
		}
	}
}