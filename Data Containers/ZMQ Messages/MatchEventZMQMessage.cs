using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IgniteBot.Data_Containers.ZMQ_Messages
{
    [System.Serializable]
    public class MatchEventZMQMessage : ZMQMessage
    {
        public string EventTypeName { get; set; }
        public List<EventDataZMQ> Data { get; set; }

        public MatchEventZMQMessage(string eventTypeName, string key, string value)
        {
            this.EventTypeName = eventTypeName;
            this.Data = new List<EventDataZMQ>();
            this.Data.Add(new EventDataZMQ(key, value));
        }
        public string ToJsonString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }

    [System.Serializable]
    public class EventDataZMQ
    {
        public string Key { get; set; }
        public string Value { get; set; }

        public EventDataZMQ(string key, string value)
        {
            this.Key = key;
            this.Value = value;
        }
    }

}
