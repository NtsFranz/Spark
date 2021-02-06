using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IgniteBot.Data_Containers.ZMQ_Messages
{
    interface ZMQMessage
    {
        string ToJsonString();
    }
}
