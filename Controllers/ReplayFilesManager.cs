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
		public bool zipping;
		public bool replayThreadActive;
		public bool splitting;

		public ConcurrentQueue<string> lastJSONQueue = new ConcurrentQueue<string>();
		public ConcurrentQueue<string> lastDateTimeStringQueue = new ConcurrentQueue<string>();
		public ConcurrentStack<Frame> milkFramesToSave = new ConcurrentStack<Frame>();
		public ConcurrentQueue<Frame> butterFramesToSave = new ConcurrentQueue<Frame>();
		public ConcurrentQueue<DateTime> replayBufferTimestamps = new ConcurrentQueue<DateTime>();
		public ConcurrentQueue<string> replayBufferJSON = new ConcurrentQueue<string>();
		public ConcurrentQueue<string> replayBufferJSONBones = new ConcurrentQueue<string>();

		private const string echoreplayDateFormat = "yyyy/MM/dd HH:mm:ss.fff";
		private const string fileNameFormat = "rec_yyyy-MM-dd_HH-mm-ss";

		private int lastButterNumChunks;
		private ulong lastButterFetchFrameIndex = 0;

		/// <summary>
		/// For replay file saving in batches
		/// </summary>
		private readonly ConcurrentQueue<DateTime> dataCacheTimestamps = new ConcurrentQueue<DateTime>();

		private readonly ConcurrentQueue<string> dataCacheLines = new ConcurrentQueue<string>();


		private static readonly List<float> fullDeltaTimes = new List<float> { 33.3333333f, 66.666666f, 100 };
		private static int FrameInterval => Math.Clamp((int)(fullDeltaTimes[SparkSettings.instance.targetDeltaTimeIndexFull] / Program.StatsIntervalMs), 1, 10000);
		private int frameIndex;

		public ReplayFilesManager()
		{
			butter = new ButterFile(compressionFormat: SparkSettings.instance.butterCompressionFormat);

			// creates a new filename
			Split();

			Program.NewFrame += AddButterFrame;
			// Program.NewFrame += AddMilkFrame;
			Program.FrameFetched += AddEchoreplayFrame;

			Program.JoinedGame += _ =>
			{
				lock (fileWritingLock)
				{
					fileName = DateTime.Now.ToString(fileNameFormat);
				}
			};
			Program.LeftGame += _ =>
			{
				Task.Run(async () =>
				{
					await Task.Delay(100);
					Split();
				});
			};
			Program.NewRound += _ =>
			{
				Split();
			};
			Program.SparkClosing += () =>
			{
				Task.Run(Split);
			};

			Task.Run(ReplayProcessingThread);
		}

		private async Task ReplayProcessingThread()
		{
			replayThreadActive = true;
			while (Program.running)
			{
				// delay first so that when the program quits, we still add the last frames
				await Task.Delay(100);
				while (butterFramesToSave.TryDequeue(out Frame f))
				{
					butter.AddFrame(f);

					// write the whole file every chunk
					// at 300 frames/chunk this is every 10 seconds
					if (butter.NumChunks() != lastButterNumChunks)
					{
						WriteOutButterFile();
						lastButterNumChunks = butter.NumChunks();
					}
				}
			}
			replayThreadActive = false;
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
				// if this is not an error api frame
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
							WriteEchoreplayLine(timestamp.ToString(echoreplayDateFormat) + "\t" + session + "\t" + bones);
						}
						else
						{
							WriteEchoreplayLine(timestamp.ToString(echoreplayDateFormat) + "\t" + session);
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


		private void AddButterFrame(Frame f)
		{
			if (!SparkSettings.instance.enableFullLogging) return;
			if (!SparkSettings.instance.saveButterFiles) return;

			if (frameIndex % FrameInterval != 0) return;
			
			butterFramesToSave.Enqueue(f);
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
		/// Writes the echoreplay data to the file
		/// </summary>
		/// <param name="data">The data to write</param>
		private void WriteEchoreplayLine(string data)
		{
			// this time is just for when to save batches. It isn't saved to the file.
			dataCacheTimestamps.Enqueue(DateTime.UtcNow);
			dataCacheLines.Enqueue(data);

			// if the time elapsed since last write is less than cutoff
			if (!SparkSettings.instance.batchWrites || DateTime.UtcNow - dataCacheTimestamps.First() >= TimeSpan.FromSeconds(5))
			{
				WriteOutEchoreplayFile();
			}
		}

		private void WriteOutEchoreplayFile()
		{
			lock (fileWritingLock)
			{
				if (dataCacheLines.IsEmpty) return;

				// Fail if the folder doesn't even exist
				if (!Directory.Exists(SparkSettings.instance.saveFolder))
				{
					LogRow(LogType.Error, "Replay directory doesn't exist.");
					return;
				}

				string directoryPath = SparkSettings.instance.saveFolder;
				string filePath = Path.Combine(directoryPath, fileName + ".echoreplay");

				StreamWriter streamWriter = new StreamWriter(filePath, true);

				foreach (string row in dataCacheLines)
				{
					streamWriter.WriteLine(row);
				}

				dataCacheLines.Clear();
				dataCacheTimestamps.Clear();

				streamWriter.Close();
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
					zipping = true;
					string tempDir = Path.Combine(SparkSettings.instance.saveFolder, "temp_zip");
					Directory.CreateDirectory(tempDir);
					File.Move(filePath,
						Path.Combine(tempDir, $"{fullFileName}.echoreplay"));
					ZipFile.CreateFromDirectory(tempDir, filePath);
					Directory.Delete(tempDir, true);
					zipping = false;
				}
			}
		}


		/// <summary>
		/// Generates a new filename from the current time.
		/// </summary>
		public void Split()
		{
			splitting = true;
			WriteOutEchoreplayFile();

			lock (butterWritingLock)
			{
				WriteOutButterFile();
				butter = new ButterFile(compressionFormat: SparkSettings.instance.butterCompressionFormat);
				lastButterNumChunks = 0;
			}


			lock (fileWritingLock)
			{
				string lastFilename = fileName;
				fileName = DateTime.Now.ToString(fileNameFormat);

				// compress the file
				if (SparkSettings.instance.useCompression)
				{
					if (File.Exists(Path.Combine(SparkSettings.instance.saveFolder, lastFilename + ".echoreplay")))
					{
						zipping = true;
						string tempDir = Path.Combine(SparkSettings.instance.saveFolder, "temp_zip");
						Directory.CreateDirectory(tempDir);
						File.Move(
							Path.Combine(SparkSettings.instance.saveFolder, lastFilename + ".echoreplay"),
							Path.Combine(SparkSettings.instance.saveFolder, "temp_zip", lastFilename + ".echoreplay")
						);
						ZipFile.CreateFromDirectory(tempDir, Path.Combine(SparkSettings.instance.saveFolder, lastFilename + ".echoreplay"));
						Directory.Delete(tempDir, true);
						zipping = false;
					}
				}
			}


			// reset the replay buffer
			replayBufferTimestamps.Clear();
			replayBufferJSON.Clear();
			replayBufferJSONBones.Clear();
			
			splitting = false;
		}

	}
}