using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Newtonsoft.Json.Linq;

namespace RoomSample
{
    public static class RoomActor
    {
        [FunctionName("RoomActor")]
        public static Task Run([ActorTrigger] IDurableActorContext context)
        {
            var state = context.GetState<Room>();

            switch (context.OperationName)
            {
                // properties can be accessed by users directly and/or by devices

                case "get-properties":
                    {
                        context.Return(state.Value.RoomProperties);
                        break;
                    }

                case "update-properties":
                    {
                        var input = context.GetOperationContent<(JObject properties, ActorId source)>();
                        state.Value.UpdateProperties(context, input.properties, input.source);
                        break;
                    }

                // devices talk to the room using the following protocol
                //
                //  device ---> room     (connect-device)
                //  device <--- room     (device-connected)
                //  device <--- room     (properties-updated)
                //  device <--- room     (properties-updated)
                //  ...
                //  device ---> room     (disconnect-device)
                //  device <--- room     (device-disconnected)

                case "connect-device":
                    {
                        var deviceActor = context.GetOperationContent<ActorId>();
                        state.Value.ConnectDevice(context, deviceActor);
                        break;
                    }

                case "disconnect-device":
                    {
                        var deviceActor = context.GetOperationContent<ActorId>();
                        state.Value.DisconnectDevice(context, deviceActor);
                        break;
                    }

                default:
                    throw new InvalidOperationException("no such operation");
            }

            return Task.CompletedTask;
        }
    }
}
