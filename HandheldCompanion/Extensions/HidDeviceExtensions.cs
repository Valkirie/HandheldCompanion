using HidLibrary;
using System;

namespace HandheldCompanion.Extensions
{
    public static class HidDeviceExtensions
    {
        public static bool Write(this HidDevice device, byte[] data, int timeout = 0, int reportLength = 64)
        {
            // allocate the full-size buffer
            var buffer = new byte[reportLength];

            // copy whatever data we have (if data.Length > reportLength, we only copy the first reportLength bytes)
            Array.Copy(data, buffer, Math.Min(data.Length, reportLength));

            // send it
            return device.Write(buffer, timeout);
        }

        public static bool WriteFeatureData(this HidDevice device, byte[] data, int reportLength = 64)
        {
            // allocate the full-size buffer
            var buffer = new byte[reportLength];

            // copy whatever data we have (if data.Length > reportLength, we only copy the first reportLength bytes)
            Array.Copy(data, buffer, Math.Min(data.Length, reportLength));

            return device.WriteFeatureData(data);
        }
    }
}
