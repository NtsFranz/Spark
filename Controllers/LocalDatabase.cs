using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace Spark
{
	public class LocalDatabase
	{
		private const string Version = "2.6";
		private readonly string dbName;

		public LocalDatabase()
		{
			string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Spark", "Database");
			dbName = Path.Combine(folder, $"spark_v{Version}.db");
			Program.OnEvent += AddEvent;
		}

		private async Task CreateDb()
		{
			if (File.Exists(dbName))
			{
				Logger.Error("Tried to create database, but one already exists.");
				return;
			}

			if (!Directory.Exists(Path.GetDirectoryName(dbName)))
			{
				Directory.CreateDirectory(Path.GetDirectoryName(dbName) ?? "");
			}

			await using SqliteConnection connection = new SqliteConnection("DataSource=" + dbName);
			try
			{
				await connection.OpenAsync();

				await using SqliteCommand command = connection.CreateCommand();
				command.CommandText =
					@"
CREATE TABLE `Event` (
    `session_id` TEXT NOT NULL,
    `match_time` DATETIME NOT NULL,
    `game_clock` NUMERIC NOT NULL,
    `player_id` INTEGER NOT NULL,
    `player_name` TEXT NOT NULL,
    `event_type` TEXT NOT NULL,
    `other_player_id` INTEGER DEFAULT NULL,
    `other_player_name` TEXT DEFAULT NULL,
    `pos_x` NUMERIC NOT NULL,
    `pos_y` NUMERIC NOT NULL,
    `pos_z` NUMERIC NOT NULL,
    `x2` NUMERIC,
    `y2` NUMERIC,
    `z2` NUMERIC
);";
				await command.ExecuteNonQueryAsync();

				command.CommandText =
					@"
CREATE TABLE `QuestIP` (
    `timestamp` TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    `ip` TEXT NOT NULL,
    `mac_address` TEXT NOT NULL,
    `client_name` TEXT
);";
				await command.ExecuteNonQueryAsync();
			}
			finally
			{
				await connection.CloseAsync();
			}
		}

		public void AddEvent(EventData e)
		{
			Task.Run(async () =>
			{
				if (!File.Exists(dbName))
				{
					await CreateDb();
				}

				await using SqliteConnection connection = new SqliteConnection("DataSource=" + dbName);
				try
				{
					await connection.OpenAsync();

					SqliteCommand command = connection.CreateCommand();
					command.CommandText =
						@"
INSERT INTO `Event`(
        `session_id`,
        `match_time`,
        `game_clock`,
        `player_id`,
        `player_name`,
        `event_type`,
        `other_player_id`,
        `other_player_name`,
        `pos_x`,
        `pos_y`,
        `pos_z`,
        `x2`,
        `y2`,
        `z2`
    )
    VALUES(
        @session_id,
        @match_time,
        @game_clock,
        @player_id,
        @player_name,
        @event_type,
        @other_player_id,
        @other_player_name,
        @pos_x,
        @pos_y,
        @pos_z,
        @x2,
        @y2,
        @z2
    )";
					Dictionary<string, object> d = e.ToDict();
					command.Parameters.AddWithValue("@session_id", d["session_id"]);
					command.Parameters.AddWithValue("@match_time", d["match_time"]);
					command.Parameters.AddWithValue("@game_clock", d["game_clock"]);
					command.Parameters.AddWithValue("@player_id", d["player_id"]);
					command.Parameters.AddWithValue("@player_name", d["player_name"]);
					command.Parameters.AddWithValue("@event_type", d["event_type"]);
					command.Parameters.AddWithValue("@other_player_id", d["other_player_id"] ?? 0);
					command.Parameters.AddWithValue("@other_player_name", d["other_player_name"] ?? "");
					command.Parameters.AddWithValue("@pos_x", d["pos_x"]);
					command.Parameters.AddWithValue("@pos_y", d["pos_y"]);
					command.Parameters.AddWithValue("@pos_z", d["pos_z"]);
					command.Parameters.AddWithValue("@x2", d["x2"] ?? 0);
					command.Parameters.AddWithValue("@y2", d["y2"] ?? 0);
					command.Parameters.AddWithValue("@z2", d["z2"] ?? 0);
					await command.PrepareAsync();
					await command.ExecuteNonQueryAsync();
				}
				catch (Exception ex)
				{
					Logger.Error(ex.ToString());
					Debug.WriteLine(ex);
				}
				finally
				{
					await connection.CloseAsync();
				}
			});
		}

		public void AddGoal(GoalData g)
		{
		}

		public void AddThrow(ThrowData t)
		{
		}

		public void AddMatch(AccumulatedFrame m)
		{
		}

		#region Queries

		public List<Dictionary<string, object>> GetJousts(int limit = 1000, bool includeNeutral = true, bool includeDefensive = true)
		{
			using SqliteConnection connection = new SqliteConnection("DataSource=" + dbName);
			connection.Open();

			SqliteCommand command = connection.CreateCommand();
			command.CommandText = $"SELECT * FROM `Event` WHERE `event_type` = 'joust_speed' OR `event_type` = 'defensive_joust' ORDER BY`match_time` DESC, `game_clock` ASC LIMIT {limit};";

			using SqliteDataReader reader = command.ExecuteReader();
			List<Dictionary<string, object>> list = new List<Dictionary<string, object>>();
			while (reader.Read())
			{
				list.Add(ReadEvent(reader));
			}

			return list;
		}

		public List<Dictionary<string, object>> GetEvents(int limit = 1000)
		{
			using SqliteConnection connection = new SqliteConnection("DataSource=" + dbName);
			connection.Open();

			SqliteCommand command = connection.CreateCommand();
			command.CommandText = $"SELECT * FROM `Event` ORDER BY`match_time` DESC, `game_clock` ASC LIMIT {limit};";

			using SqliteDataReader reader = command.ExecuteReader();
			List<Dictionary<string, object>> list = new List<Dictionary<string, object>>();
			while (reader.Read())
			{
				list.Add(ReadEvent(reader));
			}

			return list;
		}

		private static Dictionary<string, object> ReadEvent(SqliteDataReader reader)
		{
			return new Dictionary<string, object>
			{
				{ "session_id", reader.GetString(0) },
				{ "match_time", reader.GetString(1) },
				{ "game_clock", reader.GetFloat(2) },
				{ "player_id", reader.GetInt64(3) },
				{ "player_name", reader.GetString(4) },
				{ "event_type", reader.GetString(5) },
				{ "other_player_id", reader.GetInt64(6) },
				{ "other_player_name", reader.GetString(7) },
				{ "pos_x", reader.GetFloat(8) },
				{ "pos_y", reader.GetFloat(9) },
				{ "pos_z", reader.GetFloat(10) },
				{ "x2", reader.GetFloat(11) },
				{ "y2", reader.GetFloat(12) },
				{ "z2", reader.GetFloat(13) }
			};
		}

		#endregion

		#region QuestIP Finding

		public void AddQuestIP(string ip, string macAddress, string clientName)
		{
			Task.Run(async () =>
			{
				// Create DB if not exists
				if (!File.Exists(dbName))
				{
					await CreateDb();
				}

				await using SqliteConnection connection = new SqliteConnection("DataSource=" + dbName);
				try
				{
					await connection.OpenAsync();

					SqliteCommand command = connection.CreateCommand();
					command.CommandText =
						@"
INSERT INTO `QuestIP`(
        `ip`,
        `mac_address`,
        `client_name`
    )
    VALUES(
        @ip,
        @mac_address,
        @client_name
    )";
					command.Parameters.AddWithValue("@ip", ip);
					command.Parameters.AddWithValue("@mac_address", macAddress);
					command.Parameters.AddWithValue("@client_name", clientName);
					await command.PrepareAsync();
					await command.ExecuteNonQueryAsync();
				}
				finally
				{
					await connection.CloseAsync();
				}
			});
		}

		public List<string> GetClientNamesFromMacAddress(string macAddress)
		{
			if (macAddress == null) return new List<string>();
			using SqliteConnection connection = new SqliteConnection("DataSource=" + dbName);
			connection.Open();

			SqliteCommand command = connection.CreateCommand();
			command.CommandText = @"
SELECT `client_name`
FROM `QuestIP`
WHERE `client_name` IS NOT NULL AND `mac_address` = @mac_address
GROUP BY `client_name`
ORDER BY `timestamp` DESC;";

			command.Parameters.AddWithValue("@mac_address", macAddress);
			command.Prepare();

			using SqliteDataReader reader = command.ExecuteReader();
			List<string> names = new List<string>();
			while (reader.Read())
			{
				names.Add(reader.GetString(0));
			}

			return names;
		}

		#endregion
	}
}