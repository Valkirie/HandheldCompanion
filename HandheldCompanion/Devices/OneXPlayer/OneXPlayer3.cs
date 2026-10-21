using HandheldCompanion.Inputs;
using System.Linq;
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
            // The button opposite Turbo is delivered over vendor HID as 0x25 (see BuildRemapPage3)
            // instead of the X2 mouse chord, so expose it as a visible OEM4 button for layouts.
            OEMChords.RemoveAll(c => c.state.Buttons.Contains(ButtonFlags.OEM4));
            OEMChords.Add(new KeyboardChord("G-Key", null, null, false, ButtonFlags.OEM4));
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
                Thread.Sleep(200);
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
                Thread.Sleep(200);
            }
            // There is intentionally no side 0 aggregate zone on the X2 Mini Pro.
            foreach (byte side in new byte[] { 0x05, 0x06 })
            {
                result &= SendV1SolidColor(device, secondaryColor, side, breathing);
                Thread.Sleep(200);
            }
            return result;
        }

        // The X2 page 3 maps 0x25 to XBOX/HOME (01 21), which the firmware routes to the
        // gamepad Guide button and never reports over vendor HID. Map it as a keyboard-type
        // key (02 01 68) like the M1/M2 paddles so it produces proper press (01) / release (02)
        // reports with buttonId 0x25 on the vendor interface.
        protected override byte[] BuildRemapPage3(byte preset) =>
        [
            0x02, 0x38, 0x20, 0x03, preset,
            0x24, 0x02, 0x02, 0x05, 0x00, 0x00,
            0x25, 0x02, 0x01, 0x68, 0x00, 0x00,
        ];

        // OXP3 reports HOME (0x24) and G-Key (0x25) over vendor HID with real press (01) and
        // release (02) states, so forward them as-is to support holding, instead of the X2
        // fixed 100 ms tap.
        protected override void HandleEvent(byte buttonId, bool pressed)
        {
            ButtonFlags button = MapVendorButton(buttonId);
            if (button is ButtonFlags.OEM3 or ButtonFlags.OEM4)
            {
                if (pressed)
                    KeyPress(button);
                else
                    KeyRelease(button);
                return;
            }

            base.HandleEvent(buttonId, pressed);
        }

        protected override ButtonFlags MapVendorButton(byte buttonId)
        {
            return buttonId switch
            {
                0x22 => ButtonFlags.L4,   // M1 (left back paddle)
                0x23 => ButtonFlags.R4,   // M2 (right back paddle)
                0x24 => ButtonFlags.OEM3, // HOME
                0x25 => ButtonFlags.OEM4, // G-Key (button opposite Turbo)
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
