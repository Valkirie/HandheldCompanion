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

        public LegionGoSZ1()
        {
            // device specific settings
            ProductIllustration = "device_legion_go_s";

            // used to monitor OEM specific inputs
            vendorId = 0x1A86;
            productIds = [
                0xE310, // xinput
                0xE311, // dinput
            ];
            hidFilters = new()
            {
                { 0xE310, new HidFilter(unchecked((short)0xFFA0), unchecked(0x0001)) }, // xinput
                { 0xE311, new HidFilter(unchecked((short)0xFFA0), unchecked(0x0001)) }, // dinput
            };

            GyrometerAxis = new Vector3(-1.0f, 1.0f, 1.0f);
            AccelerometerAxis = new Vector3(-1.0f, 1.0f, 1.0f);

            nTDP = new double[] { 15, 15, 20 };
            cTDP = new double[] { 5, 30 };
            GfxClock = new double[] { 100, 2700 };
            CpuClock = 5000;

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
                foreach (byte[] cmd in ControllerFactoryReset())
                    device.Write(WithReportID(cmd));

                // load RGB profile
                lightProfile.profile = 0x03;
                device.Write(WithReportID(RgbLoadProfile((byte)lightProfile.profile)));
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

            // todo: moveme to LegionGo main class and drop Sapientia
            yield return ConvertHex("040400"); // disable hibernation
            yield return ConvertHex("040701"); // enable gyro
            yield return ConvertHex("040501"); // enable HID IMU
            yield return ConvertHex("041002"); // 500Hz polling
        }

        public override void SetPassthrough(bool enabled)
        {
#if USE_SAPIENTIAUSB
            SetTouchPadStatus(enabled ? 0 : 1);
#else
            if (hidDevices.TryGetValue(INPUT_HID_ID, out HidDevice device))
            {
                device.Write(WithReportID([0x04, 0x08, (enabled ? (byte)0x00 : (byte)0x01)])); // touchpad
                device.Write(WithReportID([0x08, 0x03, (enabled ? (byte)0x00 : (byte)0x01)])); // touchpad vibration
            }
#endif
            base.SetPassthrough(enabled);
        }

        public override void SetControllerSwap(bool enabled)
        {
            if (hidDevices.TryGetValue(INPUT_HID_ID, out HidDevice device))
                device.Write(WithReportID([0x05, 0x06, 0x69, 0x04, 0x01, (byte)(enabled ? 0x02 : 0x01), 0x01]));

            base.SetControllerSwap(enabled);
        }

        #region RGB
        private byte[] RgbLoadProfile(byte profile) => [0x10, 0x02, profile];
        private byte[] RgbEnable(bool enable) => [0x04, 0x06, (byte)(enable ? 1 : 0)];

        private byte[] RgbSetProfile(byte profile, byte mode, byte red, byte green, byte blue, double brightness, double speed)
        {
            byte r_brightness = Math.Clamp(ClampByte((int)brightness), (byte)0, (byte)100);
            byte r_speed = Math.Clamp(ClampByte((int)speed), (byte)0, (byte)100);

            return
            [
                0x10,
                (byte)(profile + 0x02),
                mode,
                red, green, blue,
                r_brightness,
                r_speed
            ];
        }

        public override bool SetLedBrightness(int brightness)
        {
            lightProfile.brightness = brightness;

#if USE_SAPIENTIAUSB
            return SetLightingEffectProfileID(3, lightProfile);
#else
            if (hidDevices.TryGetValue(INPUT_HID_ID, out HidDevice device))
            {
                byte[] cmd = RgbSetProfile((byte)lightProfile.profile, (byte)lightProfile.effect, (byte)lightProfile.r, (byte)lightProfile.g, (byte)lightProfile.b, lightProfile.brightness, lightProfile.speed);
                return device.Write(WithReportID(cmd));
            }
#endif
            return false;
        }

        public override bool SetLedStatus(bool status)
        {
#if USE_SAPIENTIAUSB
            return SetLightingEnable(3, status);
#else
            if (hidDevices.TryGetValue(INPUT_HID_ID, out HidDevice device))
                return device.Write(WithReportID(RgbEnable(status)));
#endif
            return false;
        }

        public override bool SetLedColor(Color MainColor, Color SecondaryColor, LEDLevel level, int speed = 100)
        {
            // Speed is inverted for Legion Go
            lightProfile.speed = 100 - speed;

            /*
            Solid Color     0x00
            Breathe         0x01
            Chromatographic 0x02
            Rainbow Spiral  0x03
            */

            switch (level)
            {
                case LEDLevel.Breathing:
                    lightProfile.effect = 0x01;
                    break;
                case LEDLevel.Rainbow:
                    lightProfile.effect = 0x03;
                    break;
                case LEDLevel.Wheel:
                    lightProfile.effect = 0x02;
                    break;
                default:
                    lightProfile.effect = 0x00;
                    break;
            }

            SetLightProfileColors(MainColor, SecondaryColor);

#if USE_SAPIENTIAUSB
            return SetLightingEffectProfileID(3, lightProfile);
#else
            if (hidDevices.TryGetValue(INPUT_HID_ID, out HidDevice device))
            {
                byte[] cmd = RgbSetProfile((byte)lightProfile.profile, (byte)lightProfile.effect, (byte)lightProfile.r, (byte)lightProfile.g, (byte)lightProfile.b, lightProfile.brightness, lightProfile.speed);
                return device.Write(WithReportID(cmd));
            }
#endif
            return false;
        }

        private void SetLightProfileColors(Color MainColor, Color SecondaryColor)
        {
            lightProfile.r = MainColor.R;
            lightProfile.g = MainColor.G;
            lightProfile.b = MainColor.B;
        }
        #endregion
    }
}
