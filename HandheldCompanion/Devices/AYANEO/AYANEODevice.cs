using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;

namespace HandheldCompanion.Devices.AYANEO
{
    public class AYANEODevice : IDevice
    {
        protected enum LEDGroup
        {
            StickLeft = 1,
            StickRight = 2,
            StickBoth = 3,
            AYA = 4,
        }

        public AYANEODevice()
        {
            // device specific settings
            UseOpenLib = true;

            DevicePowerProfiles.Add(new(Properties.Resources.PowerProfileAYANEOBetterBattery, Properties.Resources.PowerProfileAYANEOBetterBatteryDesc)
            {
                Default = true,
                DeviceDefault = true,
                OSPowerMode = OSPowerMode.BetterBattery,
                CPUBoostLevel = CPUBoostLevel.Disabled,
                Guid = BetterBatteryGuid,
                TDPOverrideEnabled = true,
                TDPOverrideValues = new[] { 10.0d, 10.0d, 10.0d }
            });

            DevicePowerProfiles.Add(new(Properties.Resources.PowerProfileAYANEOBetterPerformance, Properties.Resources.PowerProfileAYANEOBetterPerformanceDesc)
            {
                Default = true,
                DeviceDefault = true,
                OSPowerMode = OSPowerMode.BetterPerformance,
                Guid = BetterPerformanceGuid,
                TDPOverrideEnabled = true,
                TDPOverrideValues = new[] { 15.0d, 15.0d, 15.0d }
            });

            DevicePowerProfiles.Add(new(Properties.Resources.PowerProfileAYANEOBestPerformance, Properties.Resources.PowerProfileAYANEOBestPerformanceDesc)
            {
                Default = true,
                DeviceDefault = true,
                OSPowerMode = OSPowerMode.BestPerformance,
                Guid = BestPerformanceGuid,
                TDPOverrideEnabled = true,
                TDPOverrideValues = new[] { 25.0d, 25.0d, 25.0d }
            });
        }

        public override string GetGlyph(ButtonFlags button)
        {
            switch (button)
            {
                case ButtonFlags.OEM1:
                    return "\uE003";
                case ButtonFlags.OEM2:
                    return "\u220B";
                case ButtonFlags.OEM3:
                    return "\u2209";
                case ButtonFlags.OEM4:
                    return "\u220A";
                case ButtonFlags.OEM5:
                    return "\u0054";
                case ButtonFlags.OEM6:
                    return "\uE001";
            }

            return base.GetGlyph(button);
        }
    }
}
