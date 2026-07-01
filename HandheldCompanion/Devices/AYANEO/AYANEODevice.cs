using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using System.Windows.Forms;
using System.Windows.Media;
using static HandheldCompanion.Utils.DeviceUtils;

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

        private Color ledColor = Colors.Black;
        private Color ledSecondaryColor = Colors.Black;
        private LEDLevel ledLevel = LEDLevel.SolidColor;
        private bool ledStatus = false;
        private int ledBrightness = 100;

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
                TDPOverrideValues = new[] { 8.0d, 8.0d, 8.0d }
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
                TDPOverrideValues = new[] { 28.0d, 28.0d, 28.0d }
            });
        }

        public override void OpenEvents()
        {
            base.OpenEvents();

            // manage events
            SystemManager.Initialized += SystemManager_Initialized;

            // raise events
            if (SystemManager.IsInitialized)
                SystemManager_Initialized();
        }

        private void SystemManager_Initialized()
        {
            // manage events
            SystemManager.PowerLineStatusChanged += SystemManager_PowerLineStatusChanged_Handler;
            SystemManager.SessionLockChanged += SystemManager_SessionLockChanged;

            // raise events
            UpdateLED();
        }

        public override void Close()
        {
            // manage events
            SystemManager.Initialized -= SystemManager_Initialized;
            SystemManager.PowerLineStatusChanged -= SystemManager_PowerLineStatusChanged_Handler;
            SystemManager.SessionLockChanged -= SystemManager_SessionLockChanged;

            base.Close();
        }

        public override bool SetLedColor(Color mainColor, Color secondaryColor, LEDLevel level, int speed = 100)
        {
            // cache colors and level for power event reapplication
            ledColor = mainColor;
            ledSecondaryColor = secondaryColor;
            ledLevel = level;

            return base.SetLedColor(mainColor, secondaryColor, level, speed);
        }

        public override bool SetLedStatus(bool status)
        {
            // cache status
            ledStatus = status;

            return base.SetLedStatus(status);
        }

        public override bool SetLedBrightness(int brightness)
        {
            // cache status
            ledBrightness = brightness;

            return base.SetLedBrightness(brightness);
        }

        private void SystemManager_PowerLineStatusChanged_Handler(PowerLineStatus prevPowerLineStatus, PowerLineStatus powerLineStatus)
        {
            if (powerLineStatus == PowerLineStatus.Online)
                return;

            UpdateLED();
        }

        private void SystemManager_SessionLockChanged(bool isLocked)
        {
            if (isLocked)
                return;

            UpdateLED();
        }

        private void UpdateLED()
        {
            // AYANEO-based devices turn the LED RED during charge and when recovering from sleep
            // reapply LED settings to restore the configured colors
            SetLedStatus(ledStatus);
            SetLedBrightness(ledBrightness);
            SetLedColor(ledColor, ledSecondaryColor, ledLevel);
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
