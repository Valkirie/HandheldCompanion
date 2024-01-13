using HandheldCompanion.Managers;
using HandheldCompanion.Utils;
using System.Windows.Forms;
using System.Windows.Media;
using static HandheldCompanion.Utils.DeviceUtils;

namespace HandheldCompanion.Devices.AYANEO
{
    public class AYANEODevice : IDevice
    {
        private enum JoystickSelection
        {
            Left = 1,
            Right = 2,
            Both = 3,
        }

        private byte[] zones = { 4, 1, 2, 3, 4 }; // Four zones per LED Ring, repeat first zone.
        private byte maxIntensity = 100; // Use the max brightness for color brightness combination value

        private int prevBatteryLevelPercentage;
        private PowerLineStatus prevPowerStatus;

        public AYANEODevice()
        {
            prevPowerStatus = SystemInformation.PowerStatus.PowerLineStatus;
            prevBatteryLevelPercentage = (int)(SystemInformation.PowerStatus.BatteryLifePercent * 100);
            SystemManager.PowerStatusChanged += PowerManager_PowerStatusChanged;
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
            if (powerStatus.PowerLineStatus == PowerLineStatus.Online && prevPowerStatus == PowerLineStatus.Offline)
            {
                LogManager.LogDebug("Ayaneo LED, device went from battery to charging, apply color");
                base.PowerStatusChange(this);
            }

            // Check if the device went from charging to battery
            if (powerStatus.PowerLineStatus == PowerLineStatus.Offline && prevPowerStatus == PowerLineStatus.Online)
            {
                LogManager.LogDebug("Ayaneo LED, device went from charging to battery, apply color");
                base.PowerStatusChange(this);
            }

            // Check for the battery level change scenarios

            // Check if the battery went from 99 or lower to 100
            if (prevBatteryLevelPercentage <= 99 && currentBatteryLevelPercentage >= 100)
            {
                LogManager.LogDebug("Ayaneo LED, device went from < 99% battery to 100%, apply color");
                base.PowerStatusChange(this);
            }

            // Check if the battery went from 6 or higher to 5 or lower
            if (prevBatteryLevelPercentage >= 6 && currentBatteryLevelPercentage <= 5)
            {
                LogManager.LogDebug("Ayaneo LED, device went from > 5% battery <= 5%, apply color");
                base.PowerStatusChange(this);
            }

            // Track battery level % and power status for next round
            prevBatteryLevelPercentage = currentBatteryLevelPercentage;
            prevPowerStatus = powerStatus.PowerLineStatus;
        }

        private void SetJoystick(JoystickSelection joyStick)
        {
            ECRAMWrite(0x6d, (byte)joyStick);
        }

        public override bool SetLedColor(Color MainColor, Color SecondaryColor, LEDLevel level, int speed)
        {
            if (!DynamicLightingCapabilities.HasFlag(level))
                return false;

            switch (level)
            {
                case LEDLevel.SolidColor:
                    SetJoystick(JoystickSelection.Both);
                    SetLEDColor(MainColor);
                    break;
            }

            return true;
        }

        public override bool SetLedBrightness(int brightness)
        {
            // we might want to store colors on SetLedColor() and brightness on SetLedBrightness()
            // so that we can let people mess with brightness slider
            return base.SetLedBrightness(brightness);
        }
        
        private void SetLEDColor(Color color)
        {
            using (new ScopedLock(updateLock))
            {
                byte[] colorValues = { color.R, color.G, color.B };

                for (byte zone = 0; zone < zones.Length; zone++)
                {
                    // For R, G and B seperate. R = 0, G = 1, B = 2
                    for (byte colorComponentIndex = 0; colorComponentIndex < colorValues.Length; colorComponentIndex++)
                    {
                        byte zoneColorComponent = (byte)(zones[zone] * 3 + colorComponentIndex); // Indicates which Zone and which color component
                        byte colorComponentValueBrightness = (byte)(colorValues[colorComponentIndex] * maxIntensity / byte.MaxValue); // Convert 0-255 to 0-100

                        SetLED(zoneColorComponent, colorComponentValueBrightness);
                    }
                }
            }
        }

        private void SetLED(byte zoneColorComponent, byte colorComponentValueBrightness)
        {
            ECRAMWrite(0xbf, 0x10);
            ECRAMWrite(0xb1, zoneColorComponent);
            ECRAMWrite(0xb2, colorComponentValueBrightness);
            ECRAMWrite(0xbf, 0xff);
        }
    }
}
