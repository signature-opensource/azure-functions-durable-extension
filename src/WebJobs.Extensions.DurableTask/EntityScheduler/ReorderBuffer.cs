using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.EntityScheduler
{
    [JsonObject(MemberSerialization.OptIn)]
    internal class Deduplicator
    {
        [JsonProperty]
        public Dictionary<string, DateTime> Sent { get; set; }

        [JsonProperty]
        public Dictionary<string, ReceiveBuffer> ReceiveBuffers { get; set; }

        [JsonObject(MemberSerialization.OptOut)]
        public struct ReceiveBuffer
        {
            public DateTime Received;

            public Dictionary<DateTime, RequestMessage> Buffered;
        }

        public void Send(RequestMessage message, DateTime timeStamp)
        {
            if (Sent == null)
            {
                Sent = new Dictionary<string, DateTime>();
            }
            else
            {
                if (Sent.TryGetValue(message.ParentInstanceId, out var last))
                {
                    message.Predecessor = last;

                    // ensure timestamps are monotonic even if system clock is not
                    if (timeStamp <= last)
                    {
                        timeStamp = new DateTime(last.Ticks + 1);
                    }
                }
            }
            message.Timestamp = timeStamp;
        }

        public IEnumerable<RequestMessage> Receive(RequestMessage message)
        {
            ReceiveBuffer receiveBuffer = default(ReceiveBuffer);

            if (ReceiveBuffers == null)
            {
                ReceiveBuffers = new Dictionary<string, ReceiveBuffer>();
            }
            else
            {
                ReceiveBuffers.TryGetValue(message.ParentInstanceId, out receiveBuffer);
            }

            if (message.Timestamp <= receiveBuffer.Received)
            {
                // This message was already delivered, it's a duplicate
                yield break;
            }

            if (message.Predecessor != receiveBuffer.Received)
            {
                // this message is out of order, buffer it

                if (receiveBuffer.Buffered == null)
                {
                    receiveBuffer.Buffered = new Dictionary<DateTime, RequestMessage>();
                }
                receiveBuffer.Buffered[message.Predecessor] = message;

                yield break;
            }

            if (message.Predecessor != default(DateTime)
                && message.Predecessor < receiveBuffer.Received)
            {
                throw new Exception("Nondeterminism detected");
            }

            yield return message;

            receiveBuffer.Received = message.Timestamp;

            if (receiveBuffer.Buffered != null)
            {
                while (receiveBuffer.Buffered.TryGetValue(receiveBuffer.Received, out var next))
                {
                    receiveBuffer.Buffered.Remove(receiveBuffer.Received);

                    yield return next;

                    receiveBuffer.Received = next.Timestamp;
                }
            }

            ReceiveBuffers[message.ParentInstanceId] = receiveBuffer;
        }

        public void Collect(DateTime lingerLimit)
        {
            // all messages with timestamp smaller than the lingerLimit are guaranteed
            // to not be lingering, i.e. "in flight" anywhere anymore
            // (meaning that they have been delivered and will never be delivered again)
            // so we can collect unneeded tracking data

            List<string> expired = null;

            foreach (var kvp in Sent)
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
                    Sent.Remove(t);
                }

                expired.Clear();
            }

            if (Sent.Count == 0)
            {
                Sent = null;
            }

            foreach (var kvp in ReceiveBuffers)
            {
                if (kvp.Value.Buffered == null || kvp.Value.Buffered.Count == 0)
                {
                    if (kvp.Value.Received < lingerLimit)
                    {
                        expired.Add(kvp.Key);
                    }
                }
            }

            if (expired != null)
            {
                foreach (var t in expired)
                {
                    ReceiveBuffers.Remove(t);
                }
            }

            if (ReceiveBuffers.Count == 0)
            {
                Sent = null;
            }
        }
    }
}
