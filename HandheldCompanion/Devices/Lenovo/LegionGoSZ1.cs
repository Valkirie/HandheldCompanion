using HandheldCompanion.Actions;
using HandheldCompanion.Inputs;
using HidLibrary;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Windows.Media;
using static HandheldCompanion.Devices.Lenovo.SapientiaUsb;
using static HandheldCompanion.Utils.DeviceUtils;

namespace HandheldCompanion.Devices
{
    public class LegionGoSZ1 : LegionGo
    {
        private const bool USE_SAPIENTIAUSB = false;

        private LightionProfile lightProfile = new();

        private byte[] ConvertHex(string hex) => Convert.FromHexString(hex);

        public LegionGoSZ1()
        {
            // device specific settings
            // todo: create image
            ProductIllustration = "device_legion_go";

            // used to monitor OEM specific inputs
            vendorId = 0x1A86;
            productIds = [
                0xE310, // xinput
                0xE311, // dinput
            ];
            hidFilters = new()
            {
                { 0xE310, new HidFilter(unchecked((short)0xFFA0), unchecked((short)0x0001)) }, // xinput
                { 0xE311, new HidFilter(unchecked((short)0xFFA0), unchecked((short)0x0001)) }, // dinput
            };

            GyrometerAxis = new Vector3(-1.0f, 1.0f, 1.0f);
            AccelerometerAxis = new Vector3(-1.0f, 1.0f, 1.0f);

            nTDP = new double[] { 15, 15, 20 };
            cTDP = new double[] { 5, 30 };
            GfxClock = new double[] { 100, 2700 };
            CpuClock = 5000;

            Capabilities |= DeviceCapabilities.DynamicLighting;
            Capabilities |= DeviceCapabilities.DynamicLightingBrightness;

            DynamicLightingCapabilities |= LEDLevel.SolidColor;
            DynamicLightingCapabilities |= LEDLevel.Breathing;
            DynamicLightingCapabilities |= LEDLevel.Rainbow;
            DynamicLightingCapabilities |= LEDLevel.Wheel;

            DefaultLayout.AxisLayout[AxisLayoutFlags.RightPad] = [new MouseActions { MouseType = MouseActionsType.Move, Filtering = true, Sensivity = 15 }];
            DefaultLayout.ButtonLayout[ButtonFlags.RightPadClick] = [new MouseActions { MouseType = MouseActionsType.LeftButton }];
        }

        public override bool IsReady()
        {
            IEnumerable<HidDevice> devices = GetHidDevices(vendorId, productIds, 0);
            foreach (HidDevice device in devices)
            {
                if (!device.IsConnected)
                    continue;

                if (!hidFilters.TryGetValue(device.Attributes.ProductId, out HidFilter hidFilter))
                    continue;

                if (device.Capabilities.InputReportByteLength != 65 || device.Capabilities.OutputReportByteLength != 65)
                    continue;

                hidDevices[INPUT_HID_ID] = device;

                return true;
            }

            return false;
        }

        protected override async void Device_Inserted(bool reScan = false)
        {
            if (reScan)
                await WaitUntilReady();

            if (hidDevices.TryGetValue(INPUT_HID_ID, out HidDevice device))
            {
                device.MonitorDeviceEvents = true;
                device.Removed += Device_Removed;
                device.OpenDevice();

                // Send controller init packet sequence
                foreach (var cmd in ControllerFactoryReset())
                    device.Write(cmd);

                // disable built-in swap
                device.Write(ControllerLegionSwap(false));

                // load RGB profile
                device.Write(RgbLoadProfile(0x03));
            }

            base.Device_Inserted(reScan);

#if USE_SAPIENTIAUSB
        // disable QuickLightingEffect(s)
        SetQuickLightingEffect(3, 1);
        SetQuickLightingEffectEnable(3, false);

        // get current light profile(s)
        lightProfile = GetCurrentLightProfile(3);
#endif
        }

        private IEnumerable<byte[]> ControllerFactoryReset()
        {
            // reset XInput mapping
            yield return ConvertHex("12010108038203000000000482040000000005820500000000068206000000000782070000000008820800000000098209000000000a820a0000000000000000");
            yield return ConvertHex("120102080b820b000000000c820c000000000d820d000000000e820e000000000f820f0000000010821000000000128212000000001382130000000000000000");
            yield return ConvertHex("120103081482140000000015821500000000168216000000001782170000000018821800000000198219000000001c821c000000001d821d0000000000000000");
            yield return ConvertHex("12010402238223000000002482240000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000");

            yield return ConvertHex("040801"); // enable touchpad
            yield return ConvertHex("080300"); // disable touchpad vibration
            yield return ConvertHex("040400"); // disable hibernation
            yield return ConvertHex("040701"); // enable gyro
            yield return ConvertHex("040501"); // enable HID IMU
            yield return ConvertHex("041002"); // 500Hz polling
        }

        #region RGB
        private byte[] RgbLoadProfile(int profile) => new byte[] { 0x10, 0x02, (byte)profile };
        private byte[] RgbEnable(bool enable) => new byte[] { 0x04, 0x06, (byte)(enable ? 1 : 0) };
        private byte[] RgbSetProfile(int profile, byte mode, byte red, byte green, byte blue, double brightness, double speed)
        {
            byte r_brightness = Math.Clamp(ClampByte((int)(64 * brightness)), (byte)0, (byte)63);
            byte r_speed = Math.Clamp(ClampByte((int)(64 * speed)), (byte)0, (byte)63);

            return new byte[]
            {
                0x10,
                (byte)(profile + 2), // profile selector: 0x03 → profile 1, 0x04 → profile 2...
                mode,
                red,
                green,
                blue,
                r_brightness,
                r_speed
            };
        }

        public override bool SetLedBrightness(int brightness)
        {
            lightProfile.brightness = brightness;

#if USE_SAPIENTIAUSB
        SetLightingEffectProfileID(3, lightProfile);
#endif

            if (hidDevices.TryGetValue(INPUT_HID_ID, out HidDevice device))
            {
                byte[] cmd = RgbSetProfile(1, (byte)lightProfile.effect, (byte)lightProfile.r, (byte)lightProfile.g, (byte)lightProfile.b, lightProfile.brightness / 100.0, lightProfile.speed / 100.0);
                device.Write(cmd);
            }

            return true;
        }

        public override bool SetLedStatus(bool status)
        {
#if USE_SAPIENTIAUSB
        SetLightingEnable(3, status);
#endif

            if (hidDevices.TryGetValue(INPUT_HID_ID, out HidDevice device))
                device.Write(RgbEnable(status));

            return true;
        }

        public override bool SetLedColor(Color MainColor, Color SecondaryColor, LEDLevel level, int speed = 100)
        {
            // Speed is inverted for Legion Go
            lightProfile.speed = 100 - speed;

            // 1 - solid color
            // 2 - breathing
            // 3 - rainbow
            // 4 - spiral rainbow
            switch (level)
            {
                case LEDLevel.Breathing:
                    lightProfile.effect = 2;
                    break;
                case LEDLevel.Rainbow:
                    lightProfile.effect = 3;
                    break;
                case LEDLevel.Wheel:
                    lightProfile.effect = 4;
                    break;
                default:
                    lightProfile.effect = 1;
                    break;
            }

            SetLightProfileColors(MainColor, MainColor);

#if USE_SAPIENTIAUSB
        SetLightingEffectProfileID(3, lightProfile);
#endif

            if (hidDevices.TryGetValue(INPUT_HID_ID, out HidDevice device))
            {
                lightProfile.brightness = (byte)Math.Clamp((int)(64 * 1.0), 0, 63);
                lightProfile.speed = (byte)Math.Clamp((int)(64 * (speed / 100.0)), 0, 63);

                byte[] cmd = RgbSetProfile(1, (byte)lightProfile.effect, (byte)lightProfile.r, (byte)lightProfile.g, (byte)lightProfile.b, lightProfile.brightness / 100.0, lightProfile.speed / 100.0);
                device.Write(cmd);
            }

            return true;
        }

        private void SetLightProfileColors(Color MainColor, Color SecondaryColor)
        {
            lightProfile.r = MainColor.R;
            lightProfile.g = MainColor.G;
            lightProfile.b = MainColor.B;

#if USE_SAPIENTIAUSB
#endif

            if (hidDevices.TryGetValue(INPUT_HID_ID, out HidDevice device))
            {
                byte[] cmd = RgbSetProfile(1, (byte)lightProfile.effect, (byte)lightProfile.r, (byte)lightProfile.g, (byte)lightProfile.b, lightProfile.brightness / 100.0, lightProfile.speed / 100.0);
                device.Write(cmd);
            }
        }
        #endregion
    }
}
