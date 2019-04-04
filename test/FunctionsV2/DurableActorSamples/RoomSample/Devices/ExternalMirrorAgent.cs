using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Newtonsoft.Json.Linq;

namespace RoomSample
{
    /// <summary>
    /// A device that mirrors the room properties to external storage, e.g. CosmosDB.
    /// </summary>
    public class StorageMirrorAgent : Device
    {
        public StorageMirrorAgent(Guid deviceId, JObject parameters)
            : base(deviceId)
        {
        }

        private JObject RoomProperties { get; set; } = new JObject();

        public override Task OnConnected(IDurableActorContext context, JObject roomProperties)
        {
            return this.OnPropertiesUpdated(context, roomProperties, this.RoomActor);
        }

        public override async Task OnPropertiesUpdated(IDurableActorContext context, JObject updatedRoomProperties, ActorId source)
        {
            foreach (var property in updatedRoomProperties.Properties())
            {
                this.RoomProperties[property.Name] = property.Value;
            }

            // all I/O has to happen in an activity
            await context.CallActivityAsync(nameof(UpdateExternalMirror), this.RoomProperties);
        }

        public static async Task UpdateExternalMirror([ActivityTrigger] IDurableActivityContext context)
        {
            try
            {
                // update external storage
            }
            catch (Exception)
            {
            }
        }
    }
}
