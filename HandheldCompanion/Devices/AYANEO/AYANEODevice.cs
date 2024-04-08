using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using System.Windows.Forms;

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

        private int prevBatteryLevelPercentage;
        private PowerLineStatus prevPowerStatus;

        public AYANEODevice()
        {
            this.prevPowerStatus = SystemInformation.PowerStatus.PowerLineStatus;
            this.prevBatteryLevelPercentage = (int)(SystemInformation.PowerStatus.BatteryLifePercent * 100);
            SystemManager.PowerStatusChanged += PowerManager_PowerStatusChanged;
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

            return defaultGlyph;
        }

        private void PowerManager_PowerStatusChanged(PowerStatus powerStatus)
        {
            // Ayaneo devices automatically set LED color and or effect in the following scenarios
            // - Plugged in, charging
            // - Fully charged, battery 100%
            // - Battery almost empty, battery 5% or less
            // This function overrides this change based on settings

            // Get power information and bettery as a % of 100
            int currentBatteryLevelPercentage = (int)(powerStatus.BatteryLifePercent * 100);

            // Check if the device went from battery to charging
            if (powerStatus.PowerLineStatus == PowerLineStatus.Online && this.prevPowerStatus == PowerLineStatus.Offline)
            {
                LogManager.LogDebug("Ayaneo LED, device went from battery to charging, apply color");
                base.PowerStatusChange(this);
            }

            // Check if the device went from charging to battery
            if (powerStatus.PowerLineStatus == PowerLineStatus.Offline && this.prevPowerStatus == PowerLineStatus.Online)
            {
                LogManager.LogDebug("Ayaneo LED, device went from charging to battery, apply color");
                base.PowerStatusChange(this);
            }

            // Check for the battery level change scenarios

            // Check if the battery went from 99 or lower to 100
            if (this.prevBatteryLevelPercentage <= 99 && currentBatteryLevelPercentage >= 100)
            {
                LogManager.LogDebug("Ayaneo LED, device went from < 99% battery to 100%, apply color");
                base.PowerStatusChange(this);
            }

            // Check if the battery went from 6 or higher to 5 or lower
            if (this.prevBatteryLevelPercentage >= 6 && currentBatteryLevelPercentage <= 5)
            {
                LogManager.LogDebug("Ayaneo LED, device went from > 5% battery <= 5%, apply color");
                base.PowerStatusChange(this);
            }

            // Track battery level % and power status for next round
            this.prevBatteryLevelPercentage = currentBatteryLevelPercentage;
            this.prevPowerStatus = powerStatus.PowerLineStatus;
        }
    }
}
