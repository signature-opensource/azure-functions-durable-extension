using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Newtonsoft.Json.Linq;

namespace RoomSample
{
    /// <summary>
    /// The base class for all devices and agents.
    /// </summary>
    public abstract class Device
    {
        public Device(Guid deviceId)
        {
            this.DeviceId = deviceId;
        }

        /// <summary>
        /// A unique identifier for this device.
        /// </summary>
        public Guid DeviceId { get; private set; }

        /// <summary>
        /// The room this device is connected to, or null if not connected.
        /// </summary>
        public ActorId RoomActor { get; set; }

        /// <summary>
        /// Device-specific behavior to perform after connection to a room has been established.
        /// </summary>
        public virtual Task OnConnected(IDurableActorContext context, JObject roomProperties)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Device-specific behavior to perform when room properties have been updated.
        /// </summary>
        public virtual Task OnPropertiesUpdated(IDurableActorContext context, JObject updatedRoomProperties, ActorId source)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Handle device-specific operations.
        /// </summary>
        public virtual Task OnDeviceSpecificOperation(IDurableActorContext context)
        {
            throw new InvalidOperationException("no such operation");
        }
    }
}
