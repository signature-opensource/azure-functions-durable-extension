using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Azure.WebJobs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RoomSample
{
    /// <summary>
    /// A room has global properties that are read and modified by the connected devices.
    /// </summary>
    public class Room
    {
        /// <summary>
        /// The room properties.
        /// </summary>
        public JObject RoomProperties { get; set; }

        /// <summary>
        /// The connected devices, represented by their actor ids.
        /// </summary>
        public HashSet<ActorId> ConnectedDevices { get; set; }

        public void ConnectDevice(IDurableActorContext ctx, ActorId deviceActor)
        {
            if (!this.ConnectedDevices.Contains(deviceActor))
            {
                this.ConnectedDevices.Add(deviceActor);

                // send a signal to the device which starts the connection.
                ctx.SignalActor(
                    deviceActor,
                    "device-connected",
                    this.RoomProperties);
            }
        }

        public void DisconnectDevice(IDurableActorContext ctx, ActorId deviceActor)
        {
            if (this.ConnectedDevices.Contains(deviceActor))
            {
                this.ConnectedDevices.Remove(deviceActor);
            }

            // send a signal to the device to notify that disconnection is complete.
            ctx.SignalActor(
                deviceActor,
                "device-disconnected");
        }

        public void UpdateProperties(IDurableActorContext ctx, JObject properties, ActorId source)
        {
            foreach (var property in properties.Properties())
            {
                this.RoomProperties[property.Name] = property.Value;
            }

            // send a signal to all connected devices, indicating the changed properties
            foreach (var deviceActor in this.ConnectedDevices)
            {
                ctx.SignalActor(
                    deviceActor,
                    "properties-updated",
                    (properties, source));
            }
        }
    }
}
