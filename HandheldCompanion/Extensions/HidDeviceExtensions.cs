using HidLibrary;
using System;

namespace HandheldCompanion.Extensions
{
    public static class HidDeviceExtensions
    {
        /// <summary>
        /// Pads or truncates the given data so its length exactly matches the device's OutputReportByteLength,
        /// then writes it.
        /// </summary>
        public static bool Write(this HidDevice device, byte[] data, int timeout = 0, int reportLength = 64)
        {
            // get the report length (usually 64)
            // reportLength = device.Capabilities.OutputReportByteLength;

            // allocate the full-size buffer
            var buffer = new byte[reportLength];

            // copy whatever data we have (if data.Length > reportLength, we only copy the first reportLength bytes)
            Array.Copy(data, buffer, Math.Min(data.Length, reportLength));

            // any remaining bytes in buffer are already zero

            // send it
            return device.Write(buffer, timeout);
        }
    }
}
