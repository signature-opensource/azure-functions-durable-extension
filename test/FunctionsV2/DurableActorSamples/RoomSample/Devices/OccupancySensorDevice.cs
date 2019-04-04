using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Newtonsoft.Json.Linq;

namespace RoomSample
{
    /// <summary>
    /// A device that measures occupancy, and sets the occupancy room property.
    /// </summary>
    public class OccupancySensorDevice : Device
    {
        public OccupancySensorDevice(Guid deviceId, JObject parameters)
            : base(deviceId)
        {
        }

        public override Task OnDeviceSpecificOperation(IDurableActorContext context)
        {
            switch (context.OperationName)
            {
                case "device-data":

                    // here we would parse content of data
                    var value = RoomProperties.OccupancyOccupied;

                    context.SignalActor(
                        this.RoomActor,
                        "udpate-properties",
                        (new JObject().SetOccupancyProperty(value), context.Self));

                    break;

                default:
                    throw new InvalidOperationException("no such operation");
            }

            return Task.CompletedTask;
        }
    }
}
