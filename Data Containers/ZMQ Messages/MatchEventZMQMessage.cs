using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Spark.Data_Containers.ZMQ_Messages
{
	[Serializable]
	public class MatchEventZMQMessage : ZMQMessage
	{
		public string EventTypeName { get; set; }
		public List<EventDataZMQ> Data { get; set; }

		public MatchEventZMQMessage(string eventTypeName, string key, string value)
		{
			EventTypeName = eventTypeName;
			Data = new List<EventDataZMQ>
			{
				new EventDataZMQ(key, value)
			};
		}
		public string ToJsonString()
		{
			return JsonConvert.SerializeObject(this);
		}
	}

	[Serializable]
	public class EventDataZMQ
	{
		public string Key { get; set; }
		public string Value { get; set; }

		public EventDataZMQ(string key, string value)
		{
			Key = key;
			Value = value;
		}
	}

}
