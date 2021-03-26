using System.Collections.Generic;

namespace Spark
{
	class BatchOutputFormat
	{
		public bool final;
		public Dictionary<string, object> match_data;
		public List<Dictionary<string, object>> match_players;
		public List<Dictionary<string, object>> events;
		public List<Dictionary<string, object>> goals;
		public List<Dictionary<string, object>> throws;

		public BatchOutputFormat()
		{
			match_players = new List<Dictionary<string, object>>();
			events = new List<Dictionary<string, object>>();
			goals = new List<Dictionary<string, object>>();
			throws = new List<Dictionary<string, object>>();
		}
	}
}
