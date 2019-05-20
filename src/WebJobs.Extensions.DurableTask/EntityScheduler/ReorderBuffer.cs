using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.EntityScheduler
{
    internal class Deduplicator
    {
        // identity of this sender
        [JsonIgnore]
        public string InstanceId;

        [JsonProperty("lastSentTimestamp")]
        public DateTime LastSentTimestamp;

        [JsonProperty("receiveBuffers")]
        public Dictionary<string, ReceiveBuffer> ReceiveBuffers;

        [JsonProperty("receiveBuffers")]
        private SortedList<DateTime, RequestMessage> reorderBuffer

        public struct ReceiveBuffer
        {
            [JsonProperty("lastTimestamp")]
            public DateTime LastTimestamp;

            [JsonProperty("lastId")]
            public DateTime LastId;

            [JsonProperty("lastId")]
            public 

        }

    }
}
