using System;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Spark
{
	public class UploadController
	{
		public UploadController()
		{
			Program.RoundOver += (frame, reason) =>
			{
				// if end of match upload
				// this excludes resets
				if (reason != AccumulatedFrame.FinishReason.reset)
				{
					UploadMatchBatch(Program.CurrentRound, true);
				}
			};

			Program.Goal += (frame, goalData) =>
			{
				// if during-match upload
				if (!DiscordOAuth.Personal && DiscordOAuth.AccessCode.series_name != "ignitevr")
				{
					UploadMatchBatch(Program.CurrentRound, false);
				}
			};
		}
		
		public void UploadMatchBatch(AccumulatedFrame round, bool final = false)
		{
			if (!SparkSettings.instance.uploadToIgniteDB)
			{
				Console.WriteLine("Won't upload right now.");
			}

			BatchOutputFormat data = new BatchOutputFormat
			{
				final = final,
				match_data = round.ToDict()
			};
			round.players.Values.ToList().ForEach(e =>
			{
				if (e.Name != "anonymous") data.match_players.Add(e.ToDict());
			});

			round.events.ToList().ForEach(e =>
			{
				if (!e.inDB) data.events.Add(e.ToDict());
				e.inDB = true;
			});
			round.goals.ToList().ForEach(e =>
			{
				if (!e.inDB) data.goals.Add(e.ToDict());
				e.inDB = true;
			});
			round.throws.ToList().ForEach(e =>
			{
				if (!e.inDB) data.throws.Add(e.ToDict());
				e.inDB = true;
			});

			string dataString = JsonConvert.SerializeObject(data);
			string hash;
			using (SHA256 sha = SHA256.Create())
			{
				byte[] rawHash = sha.ComputeHash(Encoding.ASCII.GetBytes(dataString + round.frame.client_name));

				// Convert the byte array to hexadecimal string
				StringBuilder sb = new StringBuilder();
				foreach (byte b in rawHash)
				{
					sb.Append(b.ToString("X2"));
				}

				hash = sb.ToString().ToLower();
			}

			if (SparkSettings.instance.uploadToIgniteDB || DiscordOAuth.AccessCode.series_name.Contains("vrml"))
			{
				_ = DoUploadMatchBatchIgniteDB(dataString, hash, round.frame.client_name);
			}
			
			// upload tablet stats as well
			if (round.frame?.private_match == false && final) Program.AutoUploadTabletStats();
		}

		static async Task DoUploadMatchBatchIgniteDB(string data, string hash, string client_name)
		{
			FetchUtils.client.DefaultRequestHeaders.Remove("x-api-key");
			FetchUtils.client.DefaultRequestHeaders.Add("x-api-key", DiscordOAuth.igniteUploadKey);
			FetchUtils.client.DefaultRequestHeaders.Remove("access-code");
			FetchUtils.client.DefaultRequestHeaders.Add("access-code", DiscordOAuth.AccessCode.series_name);

			StringContent content = new StringContent(data, Encoding.UTF8, "application/json");

			try
			{
				HttpResponseMessage response = await FetchUtils.client.PostAsync("/add_data?hashkey=" + hash + "&client_name=" + client_name, content);
				Logger.LogRow(Logger.LogType.Info, "[DB][Response] " + response.Content.ReadAsStringAsync().Result);
			}
			catch
			{
				Logger.LogRow(Logger.LogType.Error, "Can't connect to the DB server");
			}
		}
	}
}