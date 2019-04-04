using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Newtonsoft.Json.Linq;

namespace RoomSample
{
    public static class DeviceActor
    {
        [FunctionName("DeviceActor")]
        public static async Task Run([ActorTrigger] IDurableActorContext ctx)
        {
            var state = ctx.GetState<Device>();

            switch (ctx.OperationName)
            {
                // ----  device management operations

                case "create-device":
                    {
                        var input = ctx.GetOperationContent<(string deviceType, JObject parameters, ActorId roomActor)>();

                        // create the device object of the specified type
                        var deviceType = Type.GetType(input.deviceType);
                        var deviceId = Guid.Parse(ctx.Key);
                        state.Value = (Device)Activator.CreateInstance(deviceType, ctx.Key, input.parameters);

                        // connect to the room
                        ctx.SignalActor(input.roomActor, "connect-device", ctx.Self);
                        state.Value.RoomActor = input.roomActor;

                        break;
                    }

                case "delete-device":
                    {
                        // disconnect from the room
                        ctx.SignalActor(state.Value.RoomActor, "disconnect-device", ctx.Self);

                        // don't destruct the actor yet until we get a device-disconnected signal from the room
                        // otherwise the actor gets re-constructed when the room sends more signals

                        break;
                    }

                // ----- notification messages received from the room

                case "device-connected":
                    {
                        var properties = ctx.GetOperationContent<JObject>();

                        // perform device-specific actions
                        await state.Value.OnConnected(ctx, properties);

                        break;
                    }

                case "properties-updated":
                    {
                        var input = ctx.GetOperationContent<(ActorId roomActor, JObject properties, ActorId source)>();

                        // perform device-specific actions
                        await state.Value.OnPropertiesUpdated(ctx, input.properties, input.source);

                        break;
                    }

                case "device-disconnected":
                    {
                        // this is the last signal we are receiving
                        ctx.DestructOnExit();

                        break;
                    }

                // ----- device-specific messages

                default:
                    {
                        // handle device-specific messages
                        await state.Value.OnDeviceSpecificOperation(ctx);

                        break;
                    }
            }
        }
    }
}
