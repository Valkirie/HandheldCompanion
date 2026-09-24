// #define USE_SAPIENTIAUSB
using HandheldCompanion.Actions;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HidLibrary;
using System;
using System.Collections.Generic;
using System.Windows.Media;
using WindowsInput.Events;
using static HandheldCompanion.Devices.Lenovo.SapientiaUsb;
using static HandheldCompanion.Utils.DeviceUtils;

namespace HandheldCompanion.Devices.Lenovo
{
    public class LegionGoTablet : LegionGo
    {
        public enum GamepadMode : byte
        {
            XInput = 1,
            DInput = 2,
        }

        public const int LeftJoyconIndex = 3;
        public const int RightJoyconIndex = 4;

        private LightionProfile lightProfileL = new();
        private LightionProfile lightProfileR = new();

        public GamepadMode ControllerMode => (GamepadMode)(ManagerFactory.settingsManager.GetInt("LegionControllerMode") + 1);

        public LegionGoTablet()
        {
            // device specific settings
            ProductIllustration = "device_legion_go";

            // used to monitor OEM specific inputs
            vendorId = 0x17EF;
            productIds = [
                0x6182, // xinput
                0x6183, // dinput
                0x6184, // dual_dinput
                0x6185, // fps
                0x61EB, // xinput (2025 FW)
                0x61EC, // dinput (2025 FW)
                0x61ED, // dual_dinput (2025 FW)
                0x61EE, // fps (2025 FW)
            ];
            hidFilters = new()
            {
                { 0x6182, new HidFilter(unchecked((short)0xFFA0), unchecked(0x0001)) }, // xinput (old FW)
                { 0x6183, new HidFilter(unchecked((short)0xFFA0), unchecked(0x0001)) }, // dinput (old FW)
                { 0x6184, new HidFilter(unchecked((short)0xFFA0), unchecked(0x0001)) }, // dual_dinput (old FW)
                { 0x6185, new HidFilter(unchecked((short)0xFFA0), unchecked(0x0001)) }, // fps (old FW)

                { 0x61EB, new HidFilter(unchecked((short)0xFFA0), unchecked(0x0001)) }, // xinput (2025 FW)
                { 0x61EC, new HidFilter(unchecked((short)0xFFA0), unchecked(0x0001)) }, // dinput (2025 FW)
                { 0x61ED, new HidFilter(unchecked((short)0xFFA0), unchecked(0x0001)) }, // dual_dinput (2025 FW)
                { 0x61EE, new HidFilter(unchecked((short)0xFFA0), unchecked(0x0001)) }, // fps (2025 FW)
            };

            // fix for threshold overflow
            GamepadMotion.SetCalibrationThreshold(124.0f, 2.0f);

            // https://www.amd.com/en/products/processors/handhelds/ryzen-z-series/z1-series/z1-extreme.html
            // https://www.amd.com/fr/products/processors/laptop/ryzen/7000-series/amd-ryzen-7-7840u.html
            nTDP = new double[] { 15, 15, 20 };
            cTDP = new double[] { 5, 30 };
            GfxClock = new double[] { 100, 2700 };
            CpuClock = 5100;

            // IMU matrices loaded from LegionGoTablet.json

            OEMChords.Add(new KeyboardChord("Desktop",
                [KeyCode.LWin, KeyCode.D],
                [KeyCode.LWin, KeyCode.D],
                true,
                flushInterval: 10
            ));

            OEMChords.Add(new KeyboardChord("Page",
                [KeyCode.LControl, KeyCode.LMenu, KeyCode.Tab],
                [KeyCode.LControl, KeyCode.LMenu, KeyCode.Tab],
                true,
                flushInterval: 10
            ));

            // device specific layout
            DefaultLayout.AxisLayout[AxisLayoutFlags.RightPad] = [new MouseActions { MouseType = MouseActionsType.Move, Filtering = true, Sensivity = 15 }];

            DefaultLayout.ButtonLayout[ButtonFlags.RightPadClick] = [new MouseActions { MouseType = MouseActionsType.LeftButton, HapticMode = HapticMode.Down, HapticStrength = HapticStrength.Low }];
            DefaultLayout.ButtonLayout[ButtonFlags.RightPadClickDown] = [new MouseActions { MouseType = MouseActionsType.RightButton, HapticMode = HapticMode.Down, HapticStrength = HapticStrength.High }];
            DefaultLayout.ButtonLayout[ButtonFlags.B5] = [new ButtonActions { Button = ButtonFlags.R1 }];
            DefaultLayout.ButtonLayout[ButtonFlags.B6] = [new MouseActions { MouseType = MouseActionsType.MiddleButton }];
            DefaultLayout.ButtonLayout[ButtonFlags.B7] = [new MouseActions { MouseType = MouseActionsType.ScrollUp }];
            DefaultLayout.ButtonLayout[ButtonFlags.B8] = [new MouseActions { MouseType = MouseActionsType.ScrollDown }];
        }

        protected override void QuerySettings()
        {
            // raise events
            SettingsManager_SettingValueChanged("LegionControllerMode", ManagerFactory.settingsManager.GetInt("LegionControllerMode"), false, true);
            // SettingsManager_SettingValueChanged("LegionControllerPhysicalXInput", ManagerFactory.settingsManager.GetBoolean("LegionControllerPhysicalXInput"), false, true);

            base.QuerySettings();
        }

        protected override void SettingsManager_SettingValueChanged(string name, object? value, bool temporary, bool initializing)
        {
            switch (name)
            {
                case "LegionControllerMode":
                    {
                        GamepadMode controllerMode = (GamepadMode)(Convert.ToInt32(value) + 1);
                        ApplyGamepadMode(controllerMode);
                    }
                    break;
                    /*
                case "LegionControllerPhysicalXInput":
                    {
                        bool enabled = Convert.ToBoolean(value);
                        SetPhysicalXInputEnabled(enabled);
                    }
                    break;
                    */
            }

            base.SettingsManager_SettingValueChanged(name, value, temporary, initializing);
        }

        protected override async void Device_Inserted(bool reScan = false)
        {
            // if you still want to automatically re-attach:
            if (reScan)
                await WaitUntilReady();

            // listen for events
            if (hidDevices.TryGetValue(INPUT_HID_ID, out HidDevice? device))
            {
                device.OpenDevice();

                // reset controller to factory default
                lock (HidWriteLock)
                {
                    foreach (byte[] cmd in ControllerFactoryReset())
                        WriteReport(device, cmd);

                    // enable left gyro
                    foreach (byte[] cmd in EnableControllerGyro(LeftJoyconIndex))
                        WriteReport(device, cmd);
                    // enable right gyro
                    foreach (byte[] cmd in EnableControllerGyro(RightJoyconIndex))
                        WriteReport(device, cmd);

                    // load RGB profiles
                    WriteReport(device, RgbLoadProfile(LeftJoyconIndex, 0x03));
                    WriteReport(device, RgbLoadProfile(RightJoyconIndex, 0x03));

                    // disable windows-specific keyboard commands
                    WriteReport(device, SetSteamOSMode(true));
                }
            }

            base.Device_Inserted(reScan);

#if USE_SAPIENTIAUSB
            // disable QuickLightingEffect(s)
            SetQuickLightingEffect(0, 1);
            SetQuickLightingEffect(3, 1);
            SetQuickLightingEffect(4, 1);
            SetQuickLightingEffectEnable(0, false);
            SetQuickLightingEffectEnable(3, false);
            SetQuickLightingEffectEnable(4, false);

            // get current light profile(s)
            lightProfileL = GetCurrentLightProfile(3);
            lightProfileR = GetCurrentLightProfile(4);
#endif
        }

        #region Controller
        private IEnumerable<byte[]> EnableControllerGyro(int idx)
        {
            yield return new byte[] { 0x05, 0x06, 0x6A, 0x02, (byte)idx, 0x01, 0x01 }; // enable
            yield return new byte[] { 0x05, 0x06, 0x6A, 0x07, (byte)idx, 0x02, 0x01 }; // high-quality
        }

        private IEnumerable<byte[]> DisableControllerGyro(int idx)
        {
            yield return new byte[] { 0x05, 0x06, 0x6A, 0x07, (byte)idx, 0x01, 0x01 }; // disable high-quality
        }

        private byte[] SetSteamOSMode(bool enabled)
        {
            return new byte[] { 0x05, 0x06, 0x69, 0x09, 0x01, (byte)(enabled ? 0x02 : 0x01), 0x01 };
        }

        public bool ApplyGamepadMode(GamepadMode mode)
        {
            if (mode != GamepadMode.XInput && mode != GamepadMode.DInput)
                return false;

            if (!hidDevices.TryGetValue(INPUT_HID_ID, out HidDevice? device))
                return false;

            return WriteReport(device, [0x05, 0x00, 0x04, 0x0E, 0x03, (byte)mode]);
        }

        public bool SetPhysicalXInputEnabled(bool enabled)
        {
            if (!hidDevices.TryGetValue(INPUT_HID_ID, out HidDevice? device))
                return false;

            byte value = enabled ? (byte)0x01 : (byte)0x02;
            bool result = true;
            lock (HidWriteLock)
            {
                result &= WriteReport(device, [0x05, 0x00, 0x04, 0x0F, 0x00, value]);
                result &= WriteReport(device, [0x05, 0x00, 0x04, 0x0F, LeftJoyconIndex, value]);
                result &= WriteReport(device, [0x05, 0x00, 0x04, 0x0F, RightJoyconIndex, value]);
            }
            return result;
        }

        public override void SetPassthrough(bool enabled)
        {
#if USE_SAPIENTIAUSB
            SetTouchPadStatus(enabled ? 0 : 1);
#else
            if (hidDevices.TryGetValue(INPUT_HID_ID, out HidDevice? device))
            {
                lock (HidWriteLock)
                    WriteReport(device, [0x05, 0x06, 0x6B, 0x02, 0x04, (enabled ? (byte)0x01 : (byte)0x00), 0x01]);
            }
#endif
            base.SetPassthrough(enabled);
        }

        private IEnumerable<byte[]> ControllerFactoryReset()
        {
            // hex strings from Python, parsed into byte[]
            yield return new byte[] { 0x04, 0x05, 0x05, 0x01, 0x01, 0x01, 0x01 };
            yield return new byte[] { 0x04, 0x05, 0x05, 0x01, 0x01, 0x02, 0x01 };
            yield return new byte[] { 0x04, 0x05, 0x05, 0x01, 0x01, 0x03, 0x01 };
            yield return new byte[] { 0x04, 0x05, 0x05, 0x01, 0x01, 0x04, 0x01 };
        }
        #endregion

        #region RGB
        public override bool SetLedBrightness(int brightness)
        {
            lightProfileL.brightness = brightness;
            lightProfileR.brightness = brightness;

#if USE_SAPIENTIAUSB
            SetLightingEffectProfileID(LeftJoyconIndex, lightProfileL);
            SetLightingEffectProfileID(RightJoyconIndex, lightProfileR);
#else
            if (hidDevices.TryGetValue(INPUT_HID_ID, out HidDevice? device))
            {
                // write RGB
                lock (HidWriteLock)
                    foreach (byte[] cmd in RgbMultiLoadSettings((RgbMode)lightProfileL.effect, 0x03, (byte)lightProfileL.r, (byte)lightProfileL.g, (byte)lightProfileL.b, lightProfileL.brightness, lightProfileL.speed, false))
                        WriteReport(device, cmd);
            }
#endif
            return true;
        }

        public override bool SetLedStatus(bool status)
        {
#if USE_SAPIENTIAUSB
            SetLightingEnable(0, status);
#else
            if (hidDevices.TryGetValue(INPUT_HID_ID, out HidDevice? device))
            {
                // write RGB
                lock (HidWriteLock)
                    foreach (byte[] cmd in RgbMultiEnable(status))
                        WriteReport(device, cmd);
            }
#endif
            return true;
        }

        public override bool SetLedColor(Color MainColor, Color SecondaryColor, LEDLevel level, int speed = 100)
        {
            // Speed is inverted for Legion Go
            lightProfileL.speed = 100 - speed;
            lightProfileR.speed = 100 - speed;

            // 1 - solid color
            // 2 - breathing
            // 3 - rainbow
            // 4 - spiral rainbow
            switch (level)
            {
                case LEDLevel.Breathing:
                    {
                        lightProfileL.effect = 2;
                        lightProfileR.effect = 2;
                    }
                    break;
                case LEDLevel.Rainbow:
                    {
                        lightProfileL.effect = 3;
                        lightProfileR.effect = 3;
                    }
                    break;
                case LEDLevel.Wheel:
                    {
                        lightProfileL.effect = 4;
                        lightProfileR.effect = 4;
                    }
                    break;
                default:
                    {
                        lightProfileL.effect = 1;
                        lightProfileR.effect = 1;
                    }
                    break;
            }

            SetLightProfileColors(MainColor, SecondaryColor);

#if USE_SAPIENTIAUSB
            SetLightingEffectProfileID(LeftJoyconIndex, lightProfileL);
            SetLightingEffectProfileID(RightJoyconIndex, lightProfileR);
#else
            if (hidDevices.TryGetValue(INPUT_HID_ID, out HidDevice? device))
            {
                // write RGB
                lock (HidWriteLock)
                    foreach (byte[] cmd in RgbMultiLoadSettings((RgbMode)lightProfileL.effect, 0x03, (byte)lightProfileL.r, (byte)lightProfileL.g, (byte)lightProfileL.b, lightProfileL.brightness, lightProfileL.speed, false))
                        WriteReport(device, cmd);
            }
#endif
            return true;
        }

        private void SetLightProfileColors(Color MainColor, Color SecondaryColor)
        {
            lightProfileL.r = MainColor.R;
            lightProfileL.g = MainColor.G;
            lightProfileL.b = MainColor.B;

            lightProfileR.r = SecondaryColor.R;
            lightProfileR.g = SecondaryColor.G;
            lightProfileR.b = SecondaryColor.B;
        }

        private byte[] RgbSetProfile(int idx, byte profile, RgbMode mode, byte red, byte green, byte blue, double brightness = 1, double speed = 1)
        {
            byte r_brightness = Math.Clamp(ClampByte((int)(64 * brightness / 100)), (byte)0, (byte)63);
            byte r_period = Math.Clamp(ClampByte((int)(64 * speed / 100)), (byte)0, (byte)63);

            return
            [
                0x05, 0x0C, 0x72, 0x01,
                (byte)idx,
                (byte)mode,
                red, green, blue,
                r_brightness,
                r_period,
                profile,
                0x01
            ];
        }

        private byte[] RgbLoadProfile(int idx, int profile)
        {
            return [0x05, 0x06, 0x73, 0x02, (byte)idx, (byte)profile, 0x01];
        }

        private byte[] RgbEnable(int idx, bool enable)
        {
            return [0x05, 0x06, 0x70, 0x02, (byte)idx, (byte)(enable ? 1 : 0), 0x01];
        }

        private IEnumerable<byte[]> RgbMultiLoadSettings(RgbMode mode, byte profile, byte red, byte green, byte blue, double brightness = 1, double speed = 1, bool init = true)
        {
            List<byte[]> cmds = new List<byte[]>
            {
                // left + right
                RgbSetProfile(LeftJoyconIndex, profile, mode, red, green, blue, brightness, speed),
                RgbSetProfile(RightJoyconIndex, profile, mode, red, green, blue, brightness, speed)
            };

            if (init)
            {
                cmds.Add(RgbLoadProfile(LeftJoyconIndex, profile));
                cmds.Add(RgbLoadProfile(RightJoyconIndex, profile));
                cmds.Add(RgbEnable(LeftJoyconIndex, true));
                cmds.Add(RgbEnable(RightJoyconIndex, true));
            }

            return cmds;
        }

        private IEnumerable<byte[]> RgbMultiEnable(bool enable)
        {
            yield return RgbEnable(LeftJoyconIndex, enable);
            yield return RgbEnable(RightJoyconIndex, enable);
        }
        #endregion
    }
}
