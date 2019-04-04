using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;

namespace RoomSample
{
    /// <summary>
    /// Room properties can be read and written externally as JSON,
    /// so we implement them as extension methods on JObject.
    /// </summary>
    public static class RoomProperties
    {
        public const string LightsProperty = "lights"; // string enumeration
        public const string LightsOn = "on";
        public const string LightsOff = "off";
        public const string LightsUnknown = "unknown";

        public const string HvacProperty = "hvac"; // string enumeration
        public const string HvacHeat = "heat";
        public const string HvacCool = "cool";
        public const string HvacOff = "off";

        public const string OccupancyProperty = "occupancy"; // string enumeration
        public const string OccupancyFree = "free";
        public const string OccupancyOccupied = "occupied";

        public const string PowerSaveModeProperty = "powerSaveMode"; // string enumeration
        public const string PowerSaveModeOn = "on";
        public const string PowerSaveModeOff = "off";

        public const string TemperatureProperty = "temperature"; // double

        public const string PreferredTemperature = "preferredTemperature"; // double or "none"
        public const string PreferredTemperatureNone = "none";

        public static bool TryGetLightsProperty(this JObject jobject, out string value)
        {
            value = jobject[LightsProperty]?.Value<string>();
            return value != null;
        }

        public static JObject SetLightsProperty(this JObject jobject, string value)
        {
            jobject[LightsProperty] = value;
            return jobject;
        }

        public static bool TryGetHvacProperty(this JObject jobject, out string value)
        {
            value = jobject[HvacProperty]?.Value<string>();
            return value != null;
        }

        public static JObject SetHvacProperty(this JObject jobject, string value)
        {
            jobject[HvacProperty] = value;
            return jobject;
        }

        public static bool TryGetOccupancyProperty(this JObject jobject, out string value)
        {
            value = jobject[OccupancyProperty]?.Value<string>();
            return value != null;
        }

        public static JObject SetOccupancyProperty(this JObject jobject, string value)
        {
            jobject[OccupancyProperty] = value;
            return jobject;
        }

        public static bool TryGetPowerSaveModeProperty(this JObject jobject, out string value)
        {
            value = jobject[PowerSaveModeProperty]?.Value<string>();
            return value != null;
        }

        public static JObject SetPowerSaveModeProperty(this JObject jobject, string value)
        {
            jobject[PowerSaveModeProperty] = value;
            return jobject;
        }

        public static bool TryGetTemperatureProperty(this JObject jobject, out double value)
        {
            if (jobject.TryGetValue(TemperatureProperty, out var jtoken))
            {
                value = (double)jtoken;
                return true;
            }

            value = 0.0;
            return false;
        }

        public static JObject SetTemperatureProperty(this JObject jobject, double value)
        {
            jobject[TemperatureProperty] = value;
            return jobject;
        }

        public static bool TryGetDesiredTemperatureProperty(this JObject jobject, out double? value)
        {
            if (jobject.TryGetValue(PreferredTemperature, out var jtoken))
            {
                value = jtoken.Value<double>(); // null if not a double
                return true;
            }

            value = null;
            return false;
        }

        public static JObject SetDesiredTemperatureProperty(this JObject jobject, double? value)
        {
            if (value.HasValue)
            {
                jobject[PreferredTemperature] = value;
            }
            else
            {
                jobject[PreferredTemperature] = PreferredTemperatureNone;
            }

            return jobject;
        }
    }
}
