// Reference : thanks to https://github.com/RomanYazvinsky/ for initial discovery of XGM payloads

using HidLibrary;
using System;
using System.Collections.Generic;
using System.Text;

namespace HandheldCompanion.Devices.ASUS
{
    public static class XGM
    {
        private static int VendorId = 0x0b05;
        private static int[] ProductIds = { 0x1970, 0x1a9a };

        public static void Write(byte[] data)
        {
            IEnumerable<HidDevice> devices = IDevice.GetHidDevices(VendorId, ProductIds, 300);
            foreach (HidDevice device in devices)
            {
                if (!device.IsConnected)
                    continue;

                // prepare payload
                byte[] payload = new byte[300];
                Array.Copy(data, payload, data.Length);

                if (!device.IsOpen)
                    device.OpenDevice();
                device.WriteFeatureData(payload);
                try { device.CloseDevice(); } catch { }
            }
        }

        public static void Init()
        {
            if (!AsusACPI.IsXGConnected()) return;
            Write(Encoding.ASCII.GetBytes("^ASUS Tech.Inc."));
        }

        public static void Light(bool status)
        {
            if (!AsusACPI.IsXGConnected()) return;
            Write(new byte[] { 0x5e, 0xc5, status ? (byte)0x50 : (byte)0 });
        }

        public static void Reset()
        {
            if (!AsusACPI.IsXGConnected()) return;
            Write(new byte[] { 0x5e, 0xd1, 0x02 });
        }

        public static void SetFan(byte[] curve)
        {
            if (AsusACPI.IsInvalidCurve(curve)) return;
            if (!AsusACPI.IsXGConnected()) return;

            byte[] msg = new byte[19];
            Array.Copy(new byte[] { 0x5e, 0xd1, 0x01 }, msg, 3);
            Array.Copy(curve, 0, msg, 3, curve.Length);

            Write(msg);
        }
    }
}
