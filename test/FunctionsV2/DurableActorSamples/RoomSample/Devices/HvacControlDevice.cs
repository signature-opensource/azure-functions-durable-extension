using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Newtonsoft.Json.Linq;

namespace RoomSample
{
    /// <summary>
    /// A device that controls the heater and AC, based on target temperature vs. actual temperature.
    /// It also monitors the powerSaveMode room property, tolerating a larger temperature difference.
    /// </summary>
    public class HvacControlDevice : Device
    {
        private const double Tolerance = 1.0;

        public HvacControlDevice(Guid deviceId, JObject parameters)
            : base(deviceId)
        {
        }

        private string Hvac { get; set; }

        private double? DesiredTemperature { get; set; }

        private double? Temperature { get; set; }

        public override async Task OnConnected(IDurableActorContext context, JObject roomProperties)
        {
            await this.SyncDeviceState(context);

            await this.OnPropertiesUpdated(context, roomProperties, this.RoomActor);
        }

        public override async Task OnPropertiesUpdated(IDurableActorContext context, JObject updatedRoomProperties, ActorId source)
        {
            if (updatedRoomProperties.TryGetTemperatureProperty(out var value))
            {
                this.Temperature = value;
            }

            if (updatedRoomProperties.TryGetDesiredTemperatureProperty(out var value2))
            {
                this.DesiredTemperature = value2;
            }

            if (this.DesiredTemperature.HasValue && this.Temperature.HasValue)
            {
                await this.ControlByTemperature(context, this.DesiredTemperature.Value, this.Temperature.Value);
            }
            else if (this.Hvac != RoomProperties.HvacOff)
            {
                await this.SyncDeviceState(context, RoomProperties.HvacOff);
            }
        }

        private async Task ControlByTemperature(IDurableActorContext context, double desired, double current)
        {
            if ((this.Hvac == RoomProperties.HvacCool && current <= desired)
                || (this.Hvac == RoomProperties.HvacHeat && current >= desired))
            {
                await this.SyncDeviceState(context, RoomProperties.HvacOff);
            }
            else if (this.Hvac != RoomProperties.HvacCool && current > desired + Tolerance)
            {
                await this.SyncDeviceState(context, RoomProperties.HvacCool);
            }
            else if (this.Hvac != RoomProperties.HvacHeat && current < desired - Tolerance)
            {
                await this.SyncDeviceState(context, RoomProperties.HvacHeat);
            }
        }

        private async Task SyncDeviceState(IDurableActorContext context, string setToValue = null)
        {
            // to perform I/O, we must wrap it in an activity
            var reportedState = await context.CallActivityAsync<string>(nameof(SendRestRequestToHvacControl), setToValue);

            if (reportedState != this.Hvac)
            {
                this.Hvac = reportedState;
                context.SignalActor(
                    this.RoomActor,
                    "update-properties",
                    (new JObject().SetHvacProperty(reportedState), context.Self));
            }
        }

        public static Task<string> SendRestRequestToHvacControl([ActivityTrigger] IDurableActivityContext context)
        {
            var setToValue = context.GetInput<string>();
            string reportedState = null;

            try
            {
                // send http REST request here
                // to set the light status (if setToValue != null) 
                // and to inquire about the current status
                reportedState = RoomProperties.HvacOff;
            }
            catch (Exception)
            {
            }

            return Task.FromResult(reportedState);
        }

        public override Task OnDeviceSpecificOperation(IDurableActorContext context)
        {
            switch (context.OperationName)
            {
                case "device-data":

                    // here we would handle device data if needed
                    break;

                default:
                    throw new InvalidOperationException("no such operation");
            }

            return Task.CompletedTask;
        }
    }
}
