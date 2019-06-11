using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask
{
    [JsonObject(MemberSerialization.OptIn)]
    internal class Deduplicator
    {
        public string Id { get; set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public Dictionary<string, DateTime> Sent { get; set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public Dictionary<string, ReceiveBuffer> Received { get; set; }

        [JsonObject(MemberSerialization.OptOut)]
        public struct ReceiveBuffer
        {
            public DateTime Last;

            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
            public SortedDictionary<DateTime, RequestMessage> Buffered;
        }

        public void Send(RequestMessage message, string destination, DateTime now)
        {
            DateTime timestamp = now;

            if (this.Sent == null)
            {
                this.Sent = new Dictionary<string, DateTime>();
            }
            else if (this.Sent.TryGetValue(destination, out var last))
            {
                message.Predecessor = last;

                // ensure timestamps are monotonic even if system clock is not
                if (timestamp <= last)
                {
                    timestamp = new DateTime(last.Ticks + 1);
                }
            }

            message.ParentInstanceId = this.Id;
            message.Timestamp = timestamp;
            this.Sent[destination] = timestamp;
        }

        public IEnumerable<RequestMessage> Receive(RequestMessage message)
        {
            ReceiveBuffer receiveBuffer = default(ReceiveBuffer);

            if (this.Received == null)
            {
                this.Received = new Dictionary<string, ReceiveBuffer>();
            }
            else
            {
                this.Received.TryGetValue(message.ParentInstanceId, out receiveBuffer);
            }

            if (message.Timestamp <= receiveBuffer.Last)
            {
                // This message was already delivered, it's a duplicate
                yield break;
            }

            if (message.Predecessor != receiveBuffer.Last)
            {
                // this message is out of order, buffer it
                if (receiveBuffer.Buffered == null)
                {
                    receiveBuffer.Buffered = new SortedDictionary<DateTime, RequestMessage>();
                }

                receiveBuffer.Buffered[message.Timestamp] = message;

                yield break;
            }

            if (message.Predecessor != default(DateTime)
                && message.Predecessor < receiveBuffer.Last)
            {
                throw new Exception("Nondeterminism detected"); // TODO only warn
            }

            yield return message;

            receiveBuffer.Last = message.Timestamp;

            while (SuccessorIsInBuffer(receiveBuffer, out var successor))
            {
                receiveBuffer.Buffered.Remove(successor.Timestamp);

                receiveBuffer.Last = successor.Timestamp;

                yield return successor;
            }

            this.Received[message.ParentInstanceId] = receiveBuffer;
        }

        private static bool SuccessorIsInBuffer(ReceiveBuffer buffer, out RequestMessage message)
        {
            if (buffer.Buffered != null)
            {
                using (var e = buffer.Buffered.GetEnumerator())
                {
                    if (e.MoveNext())
                    {
                        if (e.Current.Key < buffer.Last)
                        {
                            throw new Exception("Nondeterminism detected"); // TODO only warn

                            // message = e.Current.Value;
                            // return true;
                        }

                        if (e.Current.Value.Predecessor == buffer.Last)
                        {
                            message = e.Current.Value;
                            return true;
                        }
                    }
                }
            }

            message = null;
            return false;
        }

        public void Collect(DateTime lingerLimit)
        {
            // all messages with timestamp smaller than the lingerLimit are guaranteed
            // to not be lingering, i.e. "in flight" anywhere anymore
            // (meaning that they have been delivered and will never be delivered again)
            // so we can collect unneeded tracking data

            List<string> expired = null;

            foreach (var kvp in this.Sent)
            {
                if (kvp.Value < lingerLimit)
                {
                    (expired ?? (expired = new List<string>())).Add(kvp.Key);
                }
            }

            if (expired != null)
            {
                foreach (var t in expired)
                {
                    this.Sent.Remove(t);
                }

                expired.Clear();
            }

            if (this.Sent.Count == 0)
            {
                this.Sent = null;
            }

            foreach (var kvp in this.Received)
            {
                if (kvp.Value.Buffered == null || kvp.Value.Buffered.Count == 0)
                {
                    if (kvp.Value.Last < lingerLimit)
                    {
                        (expired ?? (expired = new List<string>())).Add(kvp.Key);
                    }
                }
            }

            if (expired != null)
            {
                foreach (var t in expired)
                {
                    this.Received.Remove(t);
                }
            }

            if (this.Received.Count == 0)
            {
                this.Received = null;
            }
        }
    }
}
