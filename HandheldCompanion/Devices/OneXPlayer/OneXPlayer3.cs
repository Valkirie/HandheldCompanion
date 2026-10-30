using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Models;
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
            // G-Key and logo are a second LED set: their own color in solid and preset modes,
            // with a breathing toggle (the sticks can't do pure breathing, so no Breathing level)
            Capabilities |= DeviceCapabilities.DynamicLightingSecondLEDSet;

            // OneXConsole has no speed setting and the firmware breathing/effect speed is fixed
            Capabilities |= DeviceCapabilities.DynamicLightingFixedSpeed;

            // OneXConsole firmware effects for the stick zones (OXP theme ids)
            LEDPresets =
            [
                new ("LEDPreset_OneXPlayerX1_Preset01", "onexplayer/preset01.png", 0x0D), // Monster Woke
                new ("LEDPreset_OneXPlayerX1_Preset02", "onexplayer/preset02.png", 0x03), // Flowing Light
                new ("LEDPreset_OneXPlayerX1_Preset03", "onexplayer/preset03.png", 0x0B), // Sunset Afterglow
                new ("LEDPreset_OneXPlayerX1_Preset04", "onexplayer/preset04.png", 0x05), // Colorful Neon
                new ("LEDPreset_OneXPlayerX1_Preset05", "onexplayer/preset05.png", 0x07), // Dreamy
                new ("LEDPreset_OneXPlayerX1_Preset06", "onexplayer/preset06.png", 0x09), // Cyberpunk
                new ("LEDPreset_OneXPlayerX1_Preset07", "onexplayer/preset07.png", 0x0C), // Colourful
                new ("LEDPreset_OneXPlayerX1_Preset09", "onexplayer/preset09.png", OXPClassicPreset), // OXP classic
                new ("LEDPreset_OneXPlayerX1_Preset10", "onexplayer/preset10.png", 0x02), // Aurora
                new ("LEDPreset_OneXPlayerX1_Preset11", "onexplayer/preset11.png", 0x08), // Warm Sun
            ];

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

        // OneXConsole RGB partition table for OXP3 (shared with X2 Mini / X2 Mini Pro), command 0xB8:
        //   0x01 left stick, 0x02 right stick: solid color or firmware effect, no pure breathing (OXC also lists 0x07 "connector", not present on OXP3)
        //   0x05 G-Key, 0x06 logo: solid color or pure breathing (0xF0), no firmware effects
        private const byte LeftStickZone = 0x01, RightStickZone = 0x02, GKeyZone = 0x05, LogoZone = 0x06;

        // the MCU drops a zone command that arrives too soon after the previous one
        // (every other one at OneXConsole's 20 ms); 150 ms is the minimum, 300 ms is safe
        private const int ZoneDelay = 150;
        private const int SafeDelay = ZoneDelay * 2;
        private const int StickDelay = ZoneDelay * 4;

        // OneXConsole "OXP classic" is not a firmware effect, it is rendered as this solid color
        private const int OXPClassicPreset = 1000;
        private static readonly Color OXPClassicColor = Color.FromRgb(183, 48, 0);

        // requested LED state; HC sets brightness first, then the color or preset, and every
        // change is sent to the device by ApplyLeds() with one fixed sequence
        private readonly object LedLock = new();
        private int CurrentBrightness = 100;
        private bool CurrentIsPreset;
        private bool CurrentBreathing;
        private Color CurrentMainColor = Colors.Black;
        private Color CurrentSecondColor = Colors.Black;
        private int CurrentPreset;

        public override bool SetLedBrightness(int brightness)
        {
            // only stored: DynamicLightingManager always follows with SetLedColor/SetLEDPreset
            CurrentBrightness = brightness;
            return true;
        }

        public override bool SetLedColor(Color mainColor, Color secondaryColor, LEDLevel level, int speed = 100)
        {
            if (level is not (LEDLevel.SolidColor or LEDLevel.Breathing))
                return false;

            lock (LedLock)
            {
                CurrentIsPreset = false;
                // Breathing level only from settings saved before the second set breathing toggle
                CurrentBreathing = level == LEDLevel.Breathing || ManagerFactory.settingsManager.GetBoolean("LEDSecondSetBreathing");
                CurrentMainColor = mainColor;
                CurrentSecondColor = secondaryColor;
                return ApplyLeds();
            }
        }

        public override bool SetLEDPreset(LEDPreset? preset)
        {
            if (preset is null)
                return false;

            lock (LedLock)
            {
                CurrentIsPreset = true;
                CurrentBreathing = ManagerFactory.settingsManager.GetBoolean("LEDSecondSetBreathing");
                CurrentPreset = preset.Value;
                // G-Key and logo have no effects, they use the second color (solid or breathing)
                // (the UI always shows the second color picker on the presets page for this device)
                CurrentSecondColor = ManagerFactory.settingsManager.GetColor("LEDSecondColor");
                return ApplyLeds();
            }
        }

        // Sends the whole LED state, always in the same order and spacing:
        //  - logo: color, then on/level (it only starts breathing once it receives the on/level)
        //  - G-Key: on/level first (it switches on its color command alone), its color lands
        //    right after the logo's on/level so both breathe in sync
        //  - sticks last (the G-Key command got dropped right after the stick ones): solid main color
        //    (no pure breathing on sticks) or the firmware effect, then on/level
        // Brightness 0 only sends "off": a color or effect command would turn the zone back on.
        private bool ApplyLeds()
        {
            hidDevices.TryGetValue(VendorHidId, out HidLibrary.HidDevice? device);
            if (device is null || !device.IsConnected)
                return false;

            bool on = CurrentBrightness > 0;
            bool result = true;

            // leave a gap after whatever was sent before
            Thread.Sleep(SafeDelay);

            if (on)
            {
                result &= SendV1Brightness(device, CurrentBrightness, GKeyZone); Thread.Sleep(SafeDelay);
                result &= SendV1SolidColor(device, CurrentSecondColor, LogoZone, CurrentBreathing); Thread.Sleep(SafeDelay);
                result &= SendV1Brightness(device, CurrentBrightness, LogoZone); Thread.Sleep(ZoneDelay);
                result &= SendV1SolidColor(device, CurrentSecondColor, GKeyZone, CurrentBreathing); Thread.Sleep(SafeDelay);
            }
            else
            {
                result &= SendV1Brightness(device, 0, LogoZone); Thread.Sleep(SafeDelay);
                result &= SendV1Brightness(device, 0, GKeyZone); Thread.Sleep(SafeDelay);
            }

            // extra gap before the sticks: the left stick sometimes missed its command
            Thread.Sleep(SafeDelay);

            // sticks: colors/effects first, then on/level, 600 ms apart; with less, or with
            // on/level before the effect, the right stick randomly missed brightness changes
            if (on)
            {
                foreach (byte zone in new[] { LeftStickZone, RightStickZone })
                {
                    if (!CurrentIsPreset)
                        result &= SendV1SolidColor(device, CurrentMainColor, zone, false);
                    else if (CurrentPreset == OXPClassicPreset)
                        result &= SendV1SolidColor(device, OXPClassicColor, zone, false);
                    else
                        result &= WriteReport(device, WithReportID(BuildV1Command(0xB8, 0x01, new byte[] { (byte)CurrentPreset, zone, 0x02 })));
                    Thread.Sleep(StickDelay);
                }
            }

            foreach (byte zone in new[] { LeftStickZone, RightStickZone })
            {
                result &= SendV1Brightness(device, CurrentBrightness, zone);
                Thread.Sleep(StickDelay);
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

        // Battery protection (OneXConsole BatteryService defaults: charge limit 0x4A3, bypass 0x4A4).
        // Verified on OXP3: 0x4A3 reads back the charge limit set in OneXConsole.
        public override bool IsBatteryProtectionSupported(int majorVersion, int minorVersion) => true;

        protected override void SettingsManager_SettingValueChanged(string name, object? value, bool temporary, bool initializing)
        {
            if (name == "BatteryChargeLimit")
            {
                // re-apply: limit is only enforced while the toggle is on
                SetBatteryChargeLimit(ManagerFactory.settingsManager.GetInt("BatteryChargeLimitPercent"));
                return;
            }

            base.SettingsManager_SettingValueChanged(name, value, temporary, initializing);
        }

        public override void SetBatteryChargeLimit(int chargeLimit)
        {
            if (!ManagerFactory.settingsManager.GetBoolean("BatteryChargeLimit"))
                chargeLimit = 100;

            if (chargeLimit < 0 || chargeLimit > 100)
                return;

            TryWriteEcByte(ECBatteryLimitAddress, (byte)chargeLimit);
        }

        public override void SetBatteryBypassChargingMode(int modeIndex)
        {
            byte modeValue = modeIndex switch
            {
                1 => 0x01, // bypass on, charges during sleep/shutdown
                2 => 0x0B, // bypass always on (OneXConsole EC_VALUE_BYPASS_POWER_MODE2)
                _ => 0x00, // bypass off
            };

            TryWriteEcByte(ECBypassChargingAddress, modeValue);
        }
    }
}
