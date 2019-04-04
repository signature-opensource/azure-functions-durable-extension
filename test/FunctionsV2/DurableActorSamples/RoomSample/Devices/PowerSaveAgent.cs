using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Newtonsoft.Json.Linq;

namespace RoomSample
{
    /// <summary>
    /// A device that monitors the occupancy sensor and controls the powerSaveMode property.
    /// </summary>
    public class PowerSaveAgent : Device
    {
        public PowerSaveAgent(Guid deviceId, JObject parameters)
            : base(deviceId)
        {
        }

        private string CurrentOccupancy { get; set; }

        private DateTime? LastChangeToOccupancy { get; set; }


        public override Task OnConnected(IDurableActorContext context, JObject roomProperties)
        {
            return this.OnPropertiesUpdated(context, roomProperties, this.RoomActor);
        }

        public override Task OnPropertiesUpdated(IDurableActorContext context, JObject updatedRoomProperties, ActorId source)
        {
            // check for occupancy changes and take appropriate action

            if (updatedRoomProperties.TryGetOccupancyProperty(out var newOccupancy))
            {
                if (this.CurrentOccupancy != newOccupancy)
                {
                    this.CurrentOccupancy = newOccupancy;
                    this.LastChangeToOccupancy = context.CurrentUtcDateTime;

                    if (newOccupancy == RoomProperties.OccupancyOccupied)
                    {
                        this.ExitPowerSaveMode(context);
                    }
                    else
                    {
                        // enter power save mode on in 30 seconds from now, if room is still empty
                        context.ScheduleOperation(TimeSpan.FromSeconds(30), "check-if-room-still-empty", this.LastChangeToOccupancy.ToString());
                    }
                }
            }

            return Task.CompletedTask;
        }

        public override Task OnDeviceSpecificOperation(IDurableActorContext context)
        {
            switch (context.OperationName)
            {
                case "check-if-room-still-empty":
                    var timestamp = context.GetOperationContent<DateTime>();
                    if (this.LastChangeToOccupancy == timestamp)
                    {
                        this.EnterPowerSaveMode(context);
                    }

                    break;

                default:
                    throw new InvalidOperationException("no such operation");
            }

            return Task.CompletedTask;
        }

        private void EnterPowerSaveMode(IDurableActorContext context)
        {
            var properties = new JObject()
                .SetPowerSaveModeProperty(RoomProperties.PowerSaveModeOn)
                .SetLightsProperty(RoomProperties.LightsOff)
                .SetDesiredTemperatureProperty(null);

            context.SignalActor(
                this.RoomActor,
                "update-properties",
                (properties, context.Self));
        }

        private void ExitPowerSaveMode(IDurableActorContext context)
        {
            var properties = new JObject()
                .SetPowerSaveModeProperty(RoomProperties.PowerSaveModeOff)
                .SetLightsProperty(RoomProperties.LightsOn)
                .SetDesiredTemperatureProperty(68.0);

            context.SignalActor(
                this.RoomActor,
                "update-properties",
                (properties, context.Self));
        }
    }
}
