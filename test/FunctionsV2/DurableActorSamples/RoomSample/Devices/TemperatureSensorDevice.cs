using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Newtonsoft.Json.Linq;

namespace RoomSample
{
    /// <summary>
    /// A device that measures temperature, and sets the temperature room property.
    /// </summary>
    public class TemperatureSensorDevice : Device
    {
        public TemperatureSensorDevice(Guid deviceId, JObject parameters)
            : base(deviceId)
        {
        }

        public override Task OnDeviceSpecificOperation(IDurableActorContext context)
        {
            switch (context.OperationName)
            {
                case "device-data":

                    // here we would parse content of data
                    var value = 68.0;

                    context.SignalActor(
                        this.RoomActor,
                        "udpate-properties",
                        (new JObject().SetTemperatureProperty(value), context.Self));

                    break;

                default:
                    throw new InvalidOperationException("no such operation");
            }

            return Task.CompletedTask;
        }
    }
}
