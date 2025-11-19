using HandheldCompanion.Inputs;
using HandheldCompanion.Shared;
using HidLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace HandheldCompanion.Devices
{
    public class OneXAOKZOE : IDevice
    {
        protected enum FanControlMode
        {
            Manual = 0x01,
            Automatic = 0x00,
            Reset = 0x4C
        }

        protected byte ACPI_FanMode_Address = 0x4A;           // Define the ACPI memory address for fan control mode
        protected byte ACPI_FanPWMDutyCycle_Address = 0x4B;   // Fan control PWM value

        public OneXAOKZOE()
        {
            UseOpenLib = true;

            // device specific capacities
            Capabilities = DeviceCapabilities.FanControl;
        }

        public override bool Open()
        {
            bool success = base.Open();
            if (!success)
                return false;

            // allow OneX turbo button to pass key inputs
            EcWriteByte(0xF1, 0x40);
            EcWriteByte(0xF2, 0x02);

            if (EcReadByte(0xF1) == 0x40 && EcReadByte(0xF2) == 0x02)
                LogManager.LogInformation("Unlocked {0} OEM button", ButtonFlags.OEM1);

            return success;
        }

        public override void Close()
        {
            EcWriteByte(0xF1, 0x00);
            EcWriteByte(0xF2, 0x00);

            if (EcReadByte(0xF1) == 0x00 && EcReadByte(0xF2) == 0x00)
                LogManager.LogInformation("Locked {0} OEM button", ButtonFlags.OEM1);

            base.Close();
        }

        public override void SetFanDuty(double percent)
        {
            if (ECDetails.AddressFanDuty == 0)
                return;

            if (!UseOpenLib || !IsOpen)
                return;

            var duty = percent * (ECDetails.FanValueMax - ECDetails.FanValueMin) / 100 + ECDetails.FanValueMin;
            var data = Convert.ToByte(duty);

            ECRamDirectWriteByte(ECDetails.AddressFanDuty, ECDetails, data);
        }

        public override void SetFanControl(bool enable, int mode = 0)
        {
            if (ECDetails.AddressFanControl == 0)
                return;

            if (!UseOpenLib || !IsOpen)
                return;

            var data = Convert.ToByte(enable);
            ECRamDirectWriteByte(ECDetails.AddressFanControl, ECDetails, data);
        }

        /// <summary>
        /// Build a HID v1 vendor command, matching HHD gen_cmd() layout:
        /// [cid, 0x3F, idx, *cmd, 0x00..., 0x3F, cid] (64 bytes total, without report ID)
        /// </summary>
        public static byte[] BuildV1Command(byte cid, byte idx, IList<byte> cmd)
        {
            const int size = 64;
            byte[] buffer = new byte[size];

            int offset = 0;
            buffer[offset++] = cid;    // command id
            buffer[offset++] = 0x3F;   // constant
            buffer[offset++] = idx;    // index (0x01 in HHD)

            // leave last 2 bytes for [0x3F, cid]
            int maxCmdBytes = size - 5; // 3 header + 2 footer
            int cmdLen = Math.Min(cmd.Count, maxCmdBytes);

            for (int i = 0; i < cmdLen; i++)
                buffer[offset++] = cmd[i];

            buffer[size - 2] = 0x3F;
            buffer[size - 1] = cid;

            return buffer;
        }

        #region HID v1 helpers (X1 mini / G1 / AOKZOE A1X, etc.)

        /// <summary>
        /// Common helper for HID v1 brightness command (gen_brightness).
        /// Maps 0–100 % to enabled + low/medium/high.
        /// </summary>
        protected bool SendV1Brightness(HidDevice? device, int brightness, byte side = 0x00)
        {
            if (device is null || !device.IsConnected)
                return false;

            bool enabled = brightness > 0;

            byte level;
            if (!enabled || brightness <= 33)
                level = 0x01; // low
            else if (brightness <= 66)
                level = 0x03; // medium
            else
                level = 0x04; // high

            List<byte> cmd = new()
            {
                0xFD,               // brightness command
                side,               // side (0 = both)
                0x02,               // constant
                (byte)(enabled ? 1 : 0),
                0x05,               // constant
                level               // brightness level
            };
            byte[] payload = BuildV1Command(0xB8, 0x01, cmd);

            return device.Write(WithReportID(payload));
        }

        /// <summary>
        /// HID v1 solid colour helper (gen_rgb_solid).
        /// </summary>
        protected bool SendV1SolidColor(HidDevice? device, Color color, byte side = 0x00)
        {
            if (device is null || !device.IsConnected)
                return false;

            List<byte> cmd = new()
            {
                0xFE,           // solid-color mode
                side,           // side (0 = both sticks / light zones)
                0x02            // constant
            };
            // 18 repetitions of RGB (54 bytes)
            for (int i = 0; i < 18; i++)
            {
                cmd.Add(color.R);
                cmd.Add(color.G);
                cmd.Add(color.B);
            }

            // trailing [r, g] as in HHD
            cmd.Add(color.R);
            cmd.Add(color.G);

            byte[] payload = BuildV1Command(0xB8, 0x01, cmd);
            return device.Write(WithReportID(payload));
        }

        /// <summary>
        /// HID v1 "flowing" / rainbow helper (gen_rgb_mode("flowing")).
        /// </summary>
        protected bool SendV1Rainbow(HidDevice? device, byte side = 0x00)
        {
            if (device is null || !device.IsConnected)
                return false;

            List<byte> cmd = new()
            {
                0x03,       // flowing mode
                side,
                0x02
            };
            byte[] payload = BuildV1Command(0xB8, 0x01, cmd);
            return device.Write(WithReportID(payload));
        }

        #endregion

        #region HID v2 helpers (OneXFly / F1 Pro / future V2-based devices)

        /// <summary>
        /// HID v2 brightness helper. Maps 0–100 % to device range 0–4.
        /// </summary>
        protected bool SendV2Brightness(HidDevice? device, int brightness)
        {
            if (device is null || !device.IsConnected)
                return false;

            int level = (int)Math.Round(brightness / 20.0); // 0–4
            if (level < 0) level = 0;
            if (level > 4) level = 4;

            // [0x00, 0x07, 0xFF, 0xFD, 0x01, 0x05, level] – HidLibrary pads the rest with zeros
            byte[] msg = { 0x00, 0x07, 0xFF, 0xFD, 0x01, 0x05, (byte)level };
            return device.Write(msg);
        }

        /// <summary>
        /// HID v2 solid-colour helper.
        /// </summary>
        protected bool SendV2SolidColor(HidDevice? device, Color color)
        {
            if (device is null || !device.IsConnected)
                return false;

            // Data message consists of a prefix, LED option, RGB data, and closing byte (0x00)
            byte[] prefix = { 0x00, 0x07, 0xFF };
            byte[] ledOption = { 0xFE }; // solid
            byte[] rgbData = Enumerable
                .Repeat(new[] { color.R, color.G, color.B }, 20)
                .SelectMany(bytes => bytes)
                .ToArray();

            byte[] msg = prefix
                .Concat(ledOption)
                .Concat(rgbData)
                .Concat(new byte[] { 0x00 })
                .ToArray();

            return device.Write(msg);
        }

        /// <summary>
        /// HID v2 rainbow / flowing helper.
        /// </summary>
        protected bool SendV2Rainbow(HidDevice? device)
        {
            if (device is null || !device.IsConnected)
                return false;

            byte[] prefix = { 0x00, 0x07, 0xFF };
            byte[] ledOption = { 0x03 }; // flowing mode
            byte[] rgbData = Enumerable.Repeat((byte)0x00, 60).ToArray();

            byte[] msg = prefix
                .Concat(ledOption)
                .Concat(rgbData)
                .Concat(new byte[] { 0x00 })
                .ToArray();

            return device.Write(msg);
        }

        #endregion
    }
}