using HandheldCompanion.Inputs;
using System.Threading;
using System.Windows.Media;
using static HandheldCompanion.Utils.DeviceUtils;

namespace HandheldCompanion.Devices
{
    /// <summary>
    /// ONEXPLAYER 3.
    ///
    /// The ONEXPLAYER 3 follows the X2/X2 Mini EC architecture:
    /// - Intel platform
    /// - WMI/ACPI EC access
    /// - X2-style fan registers
    /// - CommonHid vendor controller interface
    ///
    /// This class intentionally derives from OneXPlayerX2 so that the
    /// X2 WMI EC implementation and CommonHid/controller handling are
    /// reused. Only OXP3-specific device identification/configuration
    /// is defined here.
    /// </summary>
    public class OneXPlayer3 : OneXPlayerX2
    {
        public OneXPlayer3()
        {
            ProductIllustration = "device_onexplayer_3";
            ProductModel = "ONEXPLAYER3";

            // OXP3 exposes its lighting through the Gen2 HID interface.
            EnableSerialPort = false;
            DynamicLightingCapabilities |= LEDLevel.Breathing;

            // The OXP3 uses the X2 CommonHid controller interface:
            // VID 0x1A86, PID 0xFE00, vendor collection FF00:0001.
            // The usage collection identifies the relevant HID interface (MI_02).
            vendorId = 0x1A86;
            productIds = [PID_VENDOR];
        }

        public override bool SetLedBrightness(int brightness)
        {
            // HHD's x2 rgb_sides = (0x01, 0x02, 0x07) and secondary_sides = (0x05, 0x06).
            hidDevices.TryGetValue(VendorHidId, out HidLibrary.HidDevice? device);
            if (device is null)
                return false;

            bool result = true;
            foreach (byte side in new byte[] { 0x01, 0x02, 0x07, 0x05, 0x06 })
            {
                result &= SendV1Brightness(device, brightness, side);
                Thread.Sleep(100);
            }
            return result;
        }

        public override bool SetLedColor(Color mainColor, Color secondaryColor, LEDLevel level, int speed = 100)
        {
            if (level is not (LEDLevel.SolidColor or LEDLevel.Breathing))
                return false;

            hidDevices.TryGetValue(VendorHidId, out HidLibrary.HidDevice? device);
            if (device is null)
                return false;

            bool breathing = level == LEDLevel.Breathing;
            bool result = true;
            // HHD sends the primary color to the two joystick zones and center zone.
            foreach (byte side in new byte[] { 0x01, 0x02, 0x07 })
            {
                result &= SendV1SolidColor(device, mainColor, side, breathing);
                Thread.Sleep(100);
            }
            // There is intentionally no side 0 aggregate zone on the X2 Mini Pro.
            foreach (byte side in new byte[] { 0x05, 0x06 })
            {
                result &= SendV1SolidColor(device, secondaryColor, side, breathing);
                Thread.Sleep(100);
            }
            return result;
        }

        protected override ButtonFlags MapVendorButton(byte buttonId)
        {
            return buttonId switch
            {
                0x22 => ButtonFlags.L4,   // M1 (left back paddle)
                0x23 => ButtonFlags.R4,   // M2 (right back paddle)
                0x24 => ButtonFlags.OEM3, // HOME
                _ => base.MapVendorButton(buttonId),
            };
        }

        public override bool IsBatteryProtectionSupported(int majorVersion, int minorVersion)
        {
            // No OXP3-specific battery-protection register implementation
            // has been established by the supplied OneXConsole/HC sources.
            //
            // Do not inherit the X1 battery-protection implementation merely
            // because OneXPlayerX1 exposes it.
            return false;
        }
    }
}
