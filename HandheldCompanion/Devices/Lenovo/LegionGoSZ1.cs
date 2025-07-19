using HandheldCompanion.Actions;
using HandheldCompanion.Inputs;
using HidLibrary;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Windows.Media;
using static HandheldCompanion.Utils.DeviceUtils;

namespace HandheldCompanion.Devices
{
    public class LegionGoSZ1 : LegionGo
    {
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

        private byte[] ConvertHex(string hex) => Convert.FromHexString(hex);

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
            }

            base.Device_Inserted(reScan);
        }

        private IEnumerable<byte[]> ControllerFactoryReset()
        {
            yield return ConvertHex("040801"); // enable touchpad
            yield return ConvertHex("080300"); // disable touchpad vibration
            yield return ConvertHex("040400"); // disable hibernation
            yield return ConvertHex("040701"); // enable gyro
            yield return ConvertHex("040501"); // enable HID IMU
            yield return ConvertHex("041002"); // 500Hz polling
        }

        #region RGB
        public override bool SetLedColor(Color MainColor, Color SecondaryColor, LEDLevel level, int speed = 100)
        {
            if (hidDevices.TryGetValue(INPUT_HID_ID, out HidDevice device))
            {
                byte mode = level switch
                {
                    LEDLevel.Breathing => 1,
                    LEDLevel.Rainbow => 2,
                    LEDLevel.Wheel => 3,
                    _ => 0,
                };

                byte brightness = (byte)Math.Clamp((int)(64 * 1.0), 0, 63);
                byte r_speed = (byte)Math.Clamp((int)(64 * (speed / 100.0)), 0, 63);

                byte[] cmd = new byte[]
                {
                    0x10, 0x05, mode,
                    MainColor.R, MainColor.G, MainColor.B,
                    brightness, r_speed
                };

                device.Write(Convert.FromHexString("0406 01")); // Enable LED
                device.Write(Convert.FromHexString("1002 03")); // Load profile 3
                device.Write(cmd);
            }

            return true;
        }
        #endregion
    }
}
