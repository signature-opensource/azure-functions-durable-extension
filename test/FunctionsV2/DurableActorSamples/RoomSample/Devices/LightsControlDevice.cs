using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Newtonsoft.Json.Linq;

namespace RoomSample
{
    /// <summary>
    /// A device that controls the light switch, and keeps it in sync with the lightsOn room property.
    /// </summary>
    public class LightsControlDevice : Device
    {
        public LightsControlDevice(Guid deviceId, JObject parameters)
            : base(deviceId)
        {
        }

        private string Lights { get; set; }

        public override async Task OnConnected(IDurableActorContext context, JObject roomProperties)
        {
            await this.SyncDeviceState(context);
        }

        public override async Task OnPropertiesUpdated(IDurableActorContext context, JObject updatedRoomProperties, ActorId source)
        {
            // if some other actor modified the lights property, issue a command to the device
            if (!source.Equals(context.Self)
                && updatedRoomProperties.TryGetLightsProperty(out var newLights))
            {
                if (newLights != this.Lights)
                {
                    await this.SyncDeviceState(context, newLights);
                }
            }
        }

        private async Task SyncDeviceState(IDurableActorContext context, string setToValue = null)
        {
            // perform I/O within an activity
            var reportedState = await context.CallActivityAsync<string>(nameof(SendRestRequestToLightsControlDevice), setToValue);

            if (reportedState != this.Lights)
            {
                this.Lights = reportedState;
                context.SignalActor(
                    this.RoomActor,
                    "update-properties",
                    (new JObject().SetLightsProperty(reportedState), context.Self));
            }
        }

        public static Task<string> SendRestRequestToLightsControlDevice([ActivityTrigger] IDurableActivityContext context)
        {
            var setToValue = context.GetInput<string>();
            string reportedState = null;

            try
            {
                // send http REST request here
                // to set the light status (if setToValue != null)
                // and to inquire about the current status
                reportedState = RoomProperties.LightsOn;
            }
            catch (Exception)
            {
            }

            return Task.FromResult(reportedState);
        }

    }
}
