using HandheldCompanion.Misc;
using System;
using System.Numerics;

namespace HandheldCompanion.Devices.Lenovo
{
    public class LegionGoTablet2 : LegionGoTablet
    {
        // EC registers for Legion Go 2 fan control (from https://github.com/Rodpad/LeGo2-Fan-Control)
        private const ushort REG_RPM_READ = 0xC6C0; // current fan RPM (2 bytes LE)
        private const ushort REG_FACTORY_TARGET = 0xC6C2; // factory fan target RPM (2 bytes LE)
        private const ushort REG_OVERRIDE_WRITE = 0xC6C8; // fan RPM override (2 bytes LE, write 0 to release)
        private const ushort REG_POWER_MODE = 0xC683; // current power mode byte

        // Fan RPM range observed on Legion Go 2
        private const int FAN_RPM_MIN = 1500;
        private const int FAN_RPM_MAX = 5200;

        // EC I/O ports (same Super I/O protocol as GamingZone)
        private static readonly ECDetails LGo2ECDetails = new ECDetails
        {
            AddressStatusCommandPort = 0x4E,
            AddressDataPort = 0x4F,
            AddressFanControl = REG_OVERRIDE_WRITE,
            AddressFanDuty = REG_OVERRIDE_WRITE,
            FanValueMin = FAN_RPM_MIN,
            FanValueMax = FAN_RPM_MAX,
        };

        public LegionGoTablet2()
        {
            // device specific settings
            ProductIllustration = "device_legion_go2";

            // https://www.amd.com/en/products/processors/handhelds/ryzen-z-series/z2-series/z2-extreme.html
            nTDP = new double[] { 15, 15, 20 };
            cTDP = new double[] { 15, 35 };
            GfxClock = new double[] { 100, 2900 };
            CpuClock = 5000;

            GyroMatrix = new() { Axis = new Vector3(1.0f, 1.0f, -1.0f) };
            AcceleroMatrix = new() { Axis = new Vector3(-1.0f, -1.0f, 1.0f) };

            // Enable direct EC I/O for fan control
            UseOpenLib = true;
            ECDetails = LGo2ECDetails;
        }

        /// <summary>
        /// Reads a 16-bit little-endian value from two consecutive EC registers.
        /// </summary>
        private ushort ECReadUInt16(ushort reg)
        {
            byte lo = ECRamDirectReadByte(reg, ECDetails);
            byte hi = ECRamDirectReadByte((ushort)(reg + 1), ECDetails);
            return (ushort)((hi << 8) | lo);
        }

        /// <summary>
        /// Writes a 16-bit little-endian value to two consecutive EC registers.
        /// </summary>
        private void ECWriteUInt16(ushort reg, ushort value)
        {
            ECRamDirectWriteByte(reg, ECDetails, (byte)(value & 0xFF));
            ECRamDirectWriteByte((ushort)(reg + 1), ECDetails, (byte)((value >> 8) & 0xFF));
        }

        /// <summary>
        /// Enable/disable EC fan override. Writing 0 to REG_OVERRIDE_WRITE releases control back to firmware.
        /// </summary>
        public override void SetFanControl(bool enable, int mode = 0)
        {
            if (!UseOpenLib || !IsOpen)
                return;

            if (!enable)
            {
                // Release fan control back to EC/BIOS
                ECWriteUInt16(REG_OVERRIDE_WRITE, 0);
            }
        }

        /// <summary>
        /// Sets target fan speed as a percentage (0-100), mapped to RPM range [FAN_RPM_MIN, FAN_RPM_MAX].
        /// Writing a non-zero RPM value to REG_OVERRIDE_WRITE overrides the EC's fan control.
        /// </summary>
        public override void SetFanDuty(double percent)
        {
            if (!UseOpenLib || !IsOpen)
                return;

            int rpm = (int)Math.Round(FAN_RPM_MIN + percent / 100.0 * (FAN_RPM_MAX - FAN_RPM_MIN));
            rpm = Math.Clamp(rpm, FAN_RPM_MIN, FAN_RPM_MAX);

            // The EC treats 0 as "release override"; use 1 as the minimum override sentinel
            ushort writeValue = (ushort)(rpm == 0 ? 1 : rpm);
            ECWriteUInt16(REG_OVERRIDE_WRITE, writeValue);
        }

        /// <summary>
        /// Returns current fan speed as a percentage based on measured RPM.
        /// </summary>
        public override float ReadFanDuty()
        {
            if (!UseOpenLib || !IsOpen)
                return 0;

            ushort rpm = ECReadUInt16(REG_RPM_READ);
            if (rpm == 0) return 0;

            float percent = (float)(rpm - FAN_RPM_MIN) / (FAN_RPM_MAX - FAN_RPM_MIN) * 100.0f;
            return Math.Clamp(percent, 0f, 100f);
        }

        public override void PowerProfileManager_Applied(PowerProfile profile, UpdateSource source)
        {
            // Apply OEM power mode via WMI (inherited from LegionGo)
            int currentFanMode = GetSmartFanMode();
            if (Enum.IsDefined(typeof(LegionMode), profile.OEMPowerMode) && currentFanMode != profile.OEMPowerMode)
                SetSmartFanMode(profile.OEMPowerMode);

            if (profile.FanProfile.fanMode != FanMode.Hardware)
            {
                // Software fan control: map fan speed curve to RPM override
                SetFanControl(true);
                double fanPercent = profile.FanProfile.GetFanSpeed();
                SetFanDuty(fanPercent);
            }
            else
            {
                // Hardware fan control: release EC override
                SetFanControl(false);
            }
        }

        public override void Close()
        {
            // Release fan override before closing
            if (UseOpenLib && IsOpen)
                ECWriteUInt16(REG_OVERRIDE_WRITE, 0);

            base.Close();
        }
    }
}
