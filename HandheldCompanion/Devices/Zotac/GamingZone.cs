using HandheldCompanion.Commands.Functions.HC;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Shared;
using HandheldCompanion.Utils;
using HidLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Windows.Media;
using WindowsInput.Events;
using static HandheldCompanion.Utils.DeviceUtils;

namespace HandheldCompanion.Devices.Zotac
{
    public class GamingZone : IDevice
    {
        private const byte INPUT_HID_ID = 0x00;

        private static Dictionary<uint, uint> defaultVRamSize = new Dictionary<uint, uint>
        {
            { 16U, 4U }, // 16GB RAM => 4GB base VRAM
            { 32U, 6U },
            { 64U, 12U }
        };

        private static Dictionary<KeyCode, byte> BtnKeyboardMapPos = new()
        {
            {KeyCode.A,4},{KeyCode.B,5},{KeyCode.C,6},{KeyCode.D,7},{KeyCode.E,8},{KeyCode.F,9},{KeyCode.G,10},{KeyCode.H,11},
            {KeyCode.I,12},{KeyCode.J,13},{KeyCode.K,14},{KeyCode.L,15},{KeyCode.M,16},{KeyCode.N,17},{KeyCode.O,18},{KeyCode.P,19},
            {KeyCode.Q,20},{KeyCode.R,21},{KeyCode.S,22},{KeyCode.T,23},{KeyCode.U,24},{KeyCode.V,25},{KeyCode.W,26},{KeyCode.X,27},
            {KeyCode.Y,28},{KeyCode.Z,29},
            {KeyCode.D1,30},{KeyCode.D2,31},{KeyCode.D3,32},{KeyCode.D4,33},{KeyCode.D5,34},{KeyCode.D6,35},{KeyCode.D7,36},
            {KeyCode.D8,37},{KeyCode.D9,38},{KeyCode.D0,39},
            {KeyCode.Return,40},{KeyCode.Escape,41},{KeyCode.Backspace,42},{KeyCode.Tab,43},{KeyCode.Space,44},
            {KeyCode.OemMinus,45},{KeyCode.Oemplus,46},{KeyCode.OemOpenBrackets,47},{KeyCode.OemCloseBrackets,48},
            {KeyCode.OemPipe,49},{KeyCode.OemSemicolon,51},{KeyCode.OemQuotes,52},{KeyCode.Oemtilde,53},
            {KeyCode.Oemcomma,54},{KeyCode.OemPeriod,55},{KeyCode.OemQuestion,56},
            {KeyCode.CapsLock,57},
            {KeyCode.F1,58},{KeyCode.F2,59},{KeyCode.F3,60},{KeyCode.F4,61},{KeyCode.F5,62},{KeyCode.F6,63},{KeyCode.F7,64},{KeyCode.F8,65},
            {KeyCode.F9,66},{KeyCode.F10,67},{KeyCode.F11,68},{KeyCode.F12,69},
            {KeyCode.PrintScreen,70},{KeyCode.Scroll,71},{KeyCode.Pause,72},{KeyCode.Insert,73},{KeyCode.Home,74},{KeyCode.PageUp,75},
            {KeyCode.Delete,76},{KeyCode.End,77},{KeyCode.PageDown,78},
            {KeyCode.Right,79},{KeyCode.Left,80},{KeyCode.Down,81},{KeyCode.Up,82},
            {KeyCode.NumLock,83},{KeyCode.Divide,84},{KeyCode.Multiply,85},{KeyCode.Subtract,86},{KeyCode.Add,87},
            {KeyCode.NumPad1,89},{KeyCode.NumPad2,90},{KeyCode.NumPad3,91},{KeyCode.NumPad4,92},{KeyCode.NumPad5,93},
            {KeyCode.NumPad6,94},{KeyCode.NumPad7,95},{KeyCode.NumPad8,96},{KeyCode.NumPad9,97},{KeyCode.NumPad0,98},{KeyCode.Decimal,99},
            {KeyCode.Apps,101}
        };

        public GamingZone()
        {
            // device specific settings
            this.ProductIllustration = "device_zotac_zone";
            this.UseOpenLib = true;

            // used to monitor OEM specific inputs
            vendorId = 0x1EE9;
            productIds = [0x1590];

            ECDetails = new ECDetails
            {
                AddressFanControl = 0x44A,           // EC RAM: mode (manual/auto)
                AddressFanDuty = 0x44B,              // EC RAM: fan speed (PWM 0-255)
                AddressStatusCommandPort = 0x4E,     // I/O port: status/command (decimal ECDetails.AddressStatusCommandPort)
                AddressDataPort = 0x4F,              // I/O port: data (decimal ECDetails.AddressDataPort)
                FanValueMin = 0,
                FanValueMax = 255
            };

            // https://www.amd.com/fr/products/processors/laptop/ryzen/7000-series/amd-ryzen-7-7840u.html
            // https://www.amd.com/fr/products/processors/laptop/ryzen/8000-series/amd-ryzen-7-8840u.html
            this.nTDP = new double[] { 15, 15, 30 };
            this.cTDP = new double[] { 8, 30 };
            this.GfxClock = new double[] { 100, 2700 };
            this.CpuClock = 5100;

            GyrometerAxis = new Vector3(1.0f, 1.0f, -1.0f);
            GyrometerAxisSwap = new SortedDictionary<char, char>
            {
                { 'X', 'X' },
                { 'Y', 'Z' },
                { 'Z', 'Y' }
            };

            AccelerometerAxis = new Vector3(1.0f, 1.0f, 1.0f);
            AccelerometerAxisSwap = new SortedDictionary<char, char>
            {
                { 'X', 'X' },
                { 'Y', 'Z' },
                { 'Z', 'Y' }
            };

            this.OEMChords.Add(new KeyboardChord("ZOTAC key",
                [KeyCode.LControl, KeyCode.LWin, KeyCode.F17],
                [KeyCode.LControl, KeyCode.LWin, KeyCode.F17],
                false, ButtonFlags.OEM1
            ));

            this.OEMChords.Add(new KeyboardChord("Dots key",
                [KeyCode.LControl, KeyCode.LWin, KeyCode.F18],
                [KeyCode.LControl, KeyCode.LWin, KeyCode.F18],
                false, ButtonFlags.OEM2
            ));

            this.OEMChords.Add(new KeyboardChord("Home key",
                [KeyCode.LWin, KeyCode.D],
                [KeyCode.LWin, KeyCode.D],
                false, ButtonFlags.OEM3
            ));

            this.OEMChords.Add(new KeyboardChord("M1",
                [KeyCode.LControl, KeyCode.LWin, KeyCode.F11],
                [KeyCode.LControl, KeyCode.LWin, KeyCode.F11],
                false, ButtonFlags.OEM4
            ));

            this.OEMChords.Add(new KeyboardChord("M2",
                [KeyCode.LControl, KeyCode.LWin, KeyCode.F12],
                [KeyCode.LControl, KeyCode.LWin, KeyCode.F12],
                false, ButtonFlags.OEM5
            ));

            // prepare hotkeys
            DeviceHotkeys[typeof(MainWindowCommands)].inputsChord.ButtonState[ButtonFlags.OEM1] = true;
            DeviceHotkeys[typeof(QuickToolsCommands)].inputsChord.ButtonState[ButtonFlags.OEM2] = true;

            // Quiet
            DevicePowerProfiles.Add(new(Properties.Resources.PowerProfileGamingZoneBetterBattery, Properties.Resources.PowerProfileGamingZoneBetterBatteryDesc)
            {
                Default = true,
                DeviceDefault = true,
                OSPowerMode = OSPowerMode.BetterBattery,
                CPUBoostLevel = CPUBoostLevel.Disabled,
                Guid = BetterBatteryGuid,
                TDPOverrideEnabled = true,
                TDPOverrideValues = new[] { 8.0d, 8.0d, 8.0d }
            });

            // Balance
            DevicePowerProfiles.Add(new(Properties.Resources.PowerProfileGamingZoneBetterPerformance, Properties.Resources.PowerProfileGamingZoneBetterPerformanceDesc)
            {
                Default = true,
                DeviceDefault = true,
                OSPowerMode = OSPowerMode.BetterPerformance,
                Guid = BetterPerformanceGuid,
                TDPOverrideEnabled = true,
                TDPOverrideValues = new[] { 15.0d, 15.0d, 15.0d }
            });

            // High
            DevicePowerProfiles.Add(new(Properties.Resources.PowerProfileGamingZoneBestPerformance, Properties.Resources.PowerProfileGamingZoneBestPerformanceDesc)
            {
                Default = true,
                DeviceDefault = true,
                OSPowerMode = OSPowerMode.BestPerformance,
                Guid = BestPerformanceGuid,
                TDPOverrideEnabled = true,
                TDPOverrideValues = new[] { 28.0d, 28.0d, 28.0d }
            });

            // device specific capacities
            Capabilities |= DeviceCapabilities.FanControl;
            Capabilities |= DeviceCapabilities.DynamicLighting;
            Capabilities |= DeviceCapabilities.DynamicLightingBrightness;
            Capabilities |= DeviceCapabilities.DynamicLightingSecondLEDColor;

            // dynamic lighting capacities
            DynamicLightingCapabilities |= LEDLevel.SolidColor;
            DynamicLightingCapabilities |= LEDLevel.Breathing;
            DynamicLightingCapabilities |= LEDLevel.Rainbow;
            DynamicLightingCapabilities |= LEDLevel.Gradient;

            // get physical installed RAM
            ulong TotalMemoryInKilobytes = 0;
            if (GetPhysicallyInstalledSystemMemory(out TotalMemoryInKilobytes))
            {
                physicalInstalledRamGB = (uint)(TotalMemoryInKilobytes / 1048576UL);
                if (!defaultVRamSize.ContainsKey(physicalInstalledRamGB))
                    physicalInstalledRamGB = defaultVRamSize.FirstOrDefault().Key;
            }
        }

        public override void OpenEvents()
        {
            base.OpenEvents();

            // manage events
            ControllerManager.ControllerPlugged += ControllerManager_ControllerPlugged;
            ControllerManager.ControllerUnplugged += ControllerManager_ControllerUnplugged;

            Device_Inserted();
        }

        public override void Close()
        {
            // close devices
            lock (this.updateLock)
            {
                foreach (HidDevice hidDevice in hidDevices.Values)
                    hidDevice.Dispose();
                hidDevices.Clear();
            }

            // manage events
            ControllerManager.ControllerPlugged -= ControllerManager_ControllerPlugged;
            ControllerManager.ControllerUnplugged -= ControllerManager_ControllerUnplugged;

            base.Close();
        }

        protected override void QuerySettings()
        {
            // raise events
            SettingsManager_SettingValueChanged("ZotacGamingZoneVRAM", ManagerFactory.settingsManager.GetInt("ZotacGamingZoneVRAM"), false);

            base.QuerySettings();
        }

        protected override void SettingsManager_SettingValueChanged(string name, object value, bool temporary)
        {
            switch (name)
            {
                case "ZotacGamingZoneVRAM":
                    uint size = Convert.ToUInt32(value);
                    SetVRamSize(size);
                    break;
            }

            base.SettingsManager_SettingValueChanged(name, value, temporary);
        }

        private void ControllerManager_ControllerPlugged(Controllers.IController Controller, bool IsPowerCycling)
        {
            if (Controller.GetVendorID() == vendorId && productIds.Contains(Controller.GetProductID()))
                Device_Inserted(true);
        }

        private void ControllerManager_ControllerUnplugged(Controllers.IController Controller, bool IsPowerCycling, bool WasTarget)
        {
            // hack, force rescan
            if (Controller.GetVendorID() == vendorId && productIds.Contains(Controller.GetProductID()))
                Device_Removed();
        }

        private void Device_Removed()
        {
            if (hidDevices.TryGetValue(INPUT_HID_ID, out HidDevice device))
            {
                device.MonitorDeviceEvents = false;
                device.Removed -= Device_Removed;
                try { device.Dispose(); } catch { }
            }
        }

        private async void Device_Inserted(bool reScan = false)
        {
            // if you still want to automatically re-attach:
            if (reScan)
                await WaitUntilReady();

            if (hidDevices.TryGetValue(INPUT_HID_ID, out HidDevice device))
            {
                device.MonitorDeviceEvents = true;
                device.Removed += Device_Removed;
                device.OpenDevice();

                // device.Write(RestoreProfileSet());
                device.Write(RemapM1_CtrlWinF11());
                device.Write(RemapM2_CtrlWinF12());
            }
        }

        public bool CycleController()
        {
            if (hidDevices.TryGetValue(INPUT_HID_ID, out HidDevice device))
                return device.Write(RestoreProfileSet());

            return false;
        }

        private byte[] RestoreProfileSet()
        {
            byte[] data = new byte[65]; // 65 = Report ID + 64 bytes payload
            data[0] = 0x00;    // Report ID

            data[1] = 225;     // HEADER_TAG
            data[2] = 0;       // reserved
            data[3] = 0;       // sequence
            data[4] = 60;      // PAYLOAD_SIZE

            data[5] = (byte)HIDCommand.RestoreProfileSet;

            ushort crc = CalcZotacCRC(data, 5, 62);
            data[63] = (byte)(crc >> 8);
            data[64] = (byte)(crc & 0xFF);

            return data;
        }

        private byte[] RemapM1()
        {
            // Allocate 65 bytes: 1 for Report ID, 64 for payload
            byte[] data = new byte[65];
            data[0] = 0x00;      // Report ID (assumed 0, common for vendor devices)

            data[1] = 225;      // HEADER_TAG
            data[2] = 0;        // unused/reserved
            data[3] = 0;        // sequence
            data[4] = 60;       // PAYLOAD_SIZE

            data[5] = 161;      // COMMAND (SetBtnMap)
            data[6] = 1;        // SourceKey: M1

            // Target: BtnA (from your mapping: byteIndex=1, byteShift=4) = set bit 4 in byte 8
            // Remember: our 64-byte logic moves up by 1 in 65-byte (due to ReportID)
            data[8] = 0b00010000;

            // CRC: compute over data[5]..data[62] (corresponding to logical [4]..[61])
            ushort CRC = CalcZotacCRC(data, 5, 62);
            data[63] = (byte)(CRC >> 8);    // CRC High byte ([62]+1)
            data[64] = (byte)(CRC & 0xFF);  // CRC Low byte ([63]+1)

            return data;
        }

        private byte[] RemapM1_CtrlWinF11()
        {
            byte[] data = new byte[65];
            data[0] = 0x00;   // Report ID

            data[1] = 225;    // HEADER_TAG
            data[2] = 0;      // reserved
            data[3] = 0;      // sequence
            data[4] = 60;     // PAYLOAD_SIZE

            data[5] = (byte)HIDCommand.SetBtnMap;
            data[6] = (byte)ButtonID.M1;

            // Modifier
            data[11] = (byte)ModifierKeyID.LeftCtrl | (byte)ModifierKeyID.LeftWinCMD;

            // Keyboard: F11
            data[13] = BtnKeyboardMapPos[KeyCode.F11];

            ushort CRC = CalcZotacCRC(data, 5, 62);
            data[63] = (byte)(CRC >> 8);
            data[64] = (byte)(CRC & 0xFF);

            return data;
        }

        private byte[] RemapM2_CtrlWinF12()
        {
            byte[] data = new byte[65];
            data[0] = 0x00;   // Report ID

            data[1] = 225;
            data[2] = 0;
            data[3] = 0;
            data[4] = 60;

            data[5] = (byte)HIDCommand.SetBtnMap;
            data[6] = (byte)ButtonID.M2;

            // Modifier
            data[11] = (byte)ModifierKeyID.LeftCtrl | (byte)ModifierKeyID.LeftWinCMD;

            // Keyboard: F12
            data[13] = BtnKeyboardMapPos[KeyCode.F12];

            ushort CRC = CalcZotacCRC(data, 5, 62);
            data[63] = (byte)(CRC >> 8);
            data[64] = (byte)(CRC & 0xFF);

            return data;
        }

        private enum ButtonID
        {
            None = 0,
            M1 = 1,
            M2 = 2,
            LTouchUp = 3,
            LTouchDown = 4,
            LTouchLeft = 5,
            LTouchRight = 6,
            RTouchUp = 7,
            RTouchDown = 8,
            RTouchLeft = 9,
            RTouchRight = 10,
            LB = 11,
            RB = 12,
            LTrigger = 13,
            RTrigger = 14,
            BtnA = 15,
            BtnB = 16,
            BtnX = 17,
            BtnY = 18,
            DpadUp = 19,
            DpadDown = 20,
            DpadLeft = 21,
            DpadRight = 22,
            LStickBtn = 23,
            RStickBtn = 24,
            MaxSourceBtn = 24
        }

        private enum ModifierKeyID
        {
            None = 0,
            LeftCtrl = 1,
            LeftShift = 2,
            LeftAlt = 4,
            LeftWinCMD = 8,
            RightCtrl = 16,
            RightShift = 32,
            RightAlt = 64,
            RightWinCMD = 128,
        }

        private enum BrightnessID
        {
            Brightness_0 = 0,
            Brightness_25 = 25,     // 0x00000019
            Brightness_50 = 50,     // 0x00000032
            Brightness_75 = 75,     // 0x0000004B
            Brightness_100 = 100,   // 0x00000064
        }

        private enum LightEffect
        {
            Rainbow = 0,    // HasSpeed
            Breathe = 1,    // HasSpeed & HasColor
            Stars = 2,      // HasSpeed & HasColor
            Fade = 3,       // HasSpeed
            Dance = 4,      // HasColor
            Flash = 5,      // HasSpeed
            Wink = 6,       // HasSpeed & HasColor
            Random = 7,     // HasSpeed & HasColor
            Off = 240,
        }

        private enum LightSpeed
        {
            Slow,
            Normal,
            Fast,
        }

        private enum LEDSettings
        {
            Spectra = 0,
            Speed = 1,
            Effect = 2,
            Brightness = 3,
            Color = 4,
        }

        private enum HIDCommand : byte
        {
            EnterISBMode = 188,
            MotorTest = 189,
            RestoreProfileSet = 241,
            SaveConfigData = 251,
            SetBtnMap = 161,
            SetCursorSpeed = 163,
            SetInvertStick = 167,
            SetLed = 173, // includes SetLedBrightnessCMD, SetLightEffect, SetLightSpeed, SetSpectraColor, etc.
            SetMFInfo = 182,
            SetPresentProfile = 177,
            SetProfileNum = 179,
            SetStickDZ = 165,
            SetStickSensitivity = 186,
            SetTriggerDZ = 180,
            SetVBStrength = 169,
            SetButtonTurbo = 184,
            SetTriggerLockInfo = 183 // for SetTriggerLockNumAndPresentLoc
        }

        private byte[] SendLedCmd(LEDSettings setting, byte value)
        {
            byte[] data = new byte[65];
            data[0] = 0x00;   // Report ID

            data[1] = 225;
            data[2] = 0;
            data[3] = 0;
            data[4] = 60;

            data[5] = (byte)HIDCommand.SetLed;
            data[6] = (byte)setting;
            data[7] = value;

            ushort CRC = CalcZotacCRC(data, 5, 62);
            data[63] = (byte)(CRC >> 8);
            data[64] = (byte)(CRC & 0xFF);

            return data;
        }

        private byte[] SendLedRGB(byte LedNumSet, uint color)
        {
            byte[] data = new byte[65];
            data[0] = 0x00;   // Report ID

            data[1] = 225;
            data[2] = 0;
            data[3] = 0;
            data[4] = 60;

            data[5] = (byte)HIDCommand.SetLed;
            data[6] = (byte)LEDSettings.Spectra;
            data[7] = LedNumSet;

            // Set all 10 LEDs to red
            for (int i = 0; i < 10; i++)
            {
                int pos = 10 + (i * 3);

                data[pos] = (byte)((color & 0xFF0000) >> 16);     // R
                data[pos + 1] = (byte)((color & 0x00FF00) >> 8);  // G
                data[pos + 2] = (byte)(color & 0x0000FF);         // B
            }

            ushort crc = CalcZotacCRC(data, 5, 62);
            data[63] = (byte)(crc >> 8);
            data[64] = (byte)(crc & 0xFF);

            return data;
        }

        private static uint ToSpectraRGB(Color color)
        {
            return (uint)((color.R << 16) | (color.G << 8) | (color.B));
        }

        public static ushort CalcZotacCRC(byte[] data, int start, int end)
        {
            ushort seed = CalcFast(0, data[start]);
            for (ushort i = (ushort)(start + 1); i <= end; ++i)
                seed = CalcFast(seed, data[i]);
            return seed;
        }

        private static ushort CalcFast(ushort seed, byte c)
        {
            uint num1 = (uint)((seed ^ c) & 0xFF);
            uint num2 = num1 & 0x0F;
            int num3 = ((int)num2 << 4) ^ (int)num1;
            uint num4 = (uint)num3 >> 4;
            uint intermediate = (uint)(((num3 << 1) ^ (int)num4) << 4 ^ (int)num2);
            return (ushort)((((intermediate << 3) ^ num4) ^ ((uint)seed >> 8)) & 0xFFFF);
        }

        public override bool IsReady()
        {
            IEnumerable<HidDevice> devices = GetHidDevices(vendorId, productIds, 0);
            foreach (HidDevice device in devices)
            {
                if (!device.IsConnected)
                    continue;

                // mi_03
                if (device.Capabilities.InputReportByteLength != 65 || device.Capabilities.OutputReportByteLength != 65)
                    continue;

                hidDevices[INPUT_HID_ID] = device;
                return true;
            }

            return false;
        }

        private static LightSpeed ConvertToLightSpeed(double speed)
        {
            if (speed <= 33)
                return LightSpeed.Slow;
            else if (speed > 33 && speed <= 66)
                return LightSpeed.Normal;
            else
                return LightSpeed.Fast;
        }

        private static BrightnessID ConvertToBrightnessID(double brightness)
        {
            if (brightness == 0)
                return BrightnessID.Brightness_0;
            else if (brightness >= 25 && brightness < 50)
                return BrightnessID.Brightness_25;
            else if (brightness >= 50 && brightness < 75)
                return BrightnessID.Brightness_50;
            else if (brightness >= 75 && brightness < 100)
                return BrightnessID.Brightness_75;
            else if (brightness == 100)
                return BrightnessID.Brightness_100;

            return BrightnessID.Brightness_0;
        }

        public override bool SetLedBrightness(int brightness)
        {
            // Map to closest valid value
            byte value = (byte)ConvertToBrightnessID(brightness);

            if (hidDevices.TryGetValue(INPUT_HID_ID, out HidDevice hidDevice))
            {
                if (!hidDevice.IsConnected)
                    return false;

                return hidDevice.Write(SendLedCmd(LEDSettings.Brightness, value));
            }

            return false;
        }

        public override bool SetLedColor(Color MainColor, Color SecondaryColor, LEDLevel level, int speed)
        {
            LightEffect lightEffect = LightEffect.Off;
            switch (level)
            {
                default:
                case LEDLevel.SolidColor:
                case LEDLevel.Ambilight:
                    lightEffect = LightEffect.Dance; // confirmed by Vei
                    break;
                case LEDLevel.Breathing:
                    lightEffect = LightEffect.Breathe;
                    break;
                case LEDLevel.Rainbow:
                    lightEffect = LightEffect.Rainbow;
                    break;
                case LEDLevel.Gradient:
                    lightEffect = LightEffect.Stars;
                    break;
            }

            byte speedValue = (byte)ConvertToLightSpeed(speed);

            if (hidDevices.TryGetValue(INPUT_HID_ID, out HidDevice hidDevice))
            {
                if (!hidDevice.IsConnected)
                    return false;

                hidDevice.Write(SendLedCmd(LEDSettings.Effect, (byte)lightEffect));
                hidDevice.Write(SendLedCmd(LEDSettings.Speed, speedValue));

                // Use official Spectra color packing
                uint mainRGB = ToSpectraRGB(MainColor);
                uint secondaryRGB = ToSpectraRGB(SecondaryColor);

                hidDevice.Write(SendLedRGB(0, mainRGB));
                hidDevice.Write(SendLedRGB(1, secondaryRGB));

                int LEDBrightness = ManagerFactory.settingsManager.GetInt("LEDBrightness");
                SetLedBrightness(LEDBrightness);
                SaveConfigData();

                return true;
            }

            return false;
        }

        private bool SaveConfigData()
        {
            if (hidDevices.TryGetValue(INPUT_HID_ID, out HidDevice hidDevice))
            {
                if (!hidDevice.IsConnected)
                    return false;

                byte[] data = new byte[65];
                data[0] = 0x00;   // Report ID

                data[1] = 225;
                data[2] = 0;
                data[3] = 0;
                data[4] = 60;

                data[5] = (byte)HIDCommand.SaveConfigData;

                return hidDevice.Write(data);
            }

            return false;
        }

        #region EC
        // additionSize in GB
        public void SetVRamSize(uint additionSize)
        {
            uint vRAM = (physicalInstalledRamGB + additionSize) * 4U;

            openLibSys.WriteIoPortByte(112, 122);
            openLibSys.WriteIoPortByte(113, (byte)vRAM);
        }

        public override void SetFanControl(bool enable, int mode)
        {
            if (!IsOpen)
                return;

            ECRamDirectWrite(ECDetails.AddressFanControl, ECDetails, Convert.ToByte(enable));
        }

        public override void SetFanDuty(double percent)
        {
            if (!IsOpen)
                return;

            byte fanValue = (byte)InputUtils.MapRange((float)percent, 0.0f, 100.0f, byte.MinValue, byte.MaxValue);
            ECRamDirectWrite(ECDetails.AddressFanDuty, ECDetails, fanValue);
        }

        public override float ReadFanDuty() => ECRamDirectReadByte(ECDetails.AddressFanDuty, ECDetails);
        #endregion

        #region WMI
        private void SetVRamSizeWMI(uint additionSize)
        {
            try
            {
                uint vRAM = (physicalInstalledRamGB + additionSize) * 4U;

                WMI.Call("root\\WMI",
                    $"SELECT * FROM UMAInterface",
                    "SetEcValue",
                    new Dictionary<string, object>
                    {
                        { "Index", (ushort)122 },   // VRAM EC register
                        { "Value", (byte)vRAM }
                    });
            }
            catch (Exception ex)
            {
                LogManager.LogError("Error in SetVRamSizeWMI: {0}, additionSize: {1}", ex.Message, additionSize);
            }
        }

        private void SetFanControlWMI(bool enabled)
        {
            try
            {
                WMI.Call("root\\WMI",
                    $"SELECT * FROM UMAInterface",
                    "SetEcValue",
                    new Dictionary<string, object>
                    {
                        { "Index", (ushort)74 },
                        { "Value", 1 }
                    });
            }
            catch (Exception ex)
            {
                LogManager.LogError("Error in SetFanControlWMI: {0}, Enabled: {1}", ex.Message, enabled);
            }
        }

        private void SetFanDutyWMI(double fanSpeed)
        {
            try
            {
                byte fanValue = (byte)((fanSpeed / 100.0d) * 255.0d);

                WMI.Call("root\\WMI",
                    $"SELECT * FROM UMAInterface",
                    "SetEcValue",
                    new Dictionary<string, object>
                    {
                        { "Index", (ushort)75 },
                        { "Value", fanValue }
                    });
            }
            catch (Exception ex)
            {
                LogManager.LogError("Error in SetFanDutyWMI: {0}, FanSpeed: {1}", ex.Message, fanSpeed);
            }
        }

        private int ReadFanDutyWMI()
        {
            try
            {
                return WMI.Call<int>("root\\WMI",
                $"SELECT * FROM UMAInterface",
                "GetEcValue",
                new Dictionary<string, object>
                {
                    { "Index", (ushort)75 }
                },
                props => Convert.ToInt32(props["Data"]));
            }
            catch (Exception ex)
            {
                LogManager.LogError("Error in ReadFanDutyWMI: {0}", ex.Message);
                return -1;
            }
        }
        #endregion

        public override string GetGlyph(ButtonFlags button)
        {
            switch (button)
            {
                case ButtonFlags.OEM1:  // ZONE
                    return "\u221D";
                case ButtonFlags.OEM2:  // MORE
                    return "\u221E";
                case ButtonFlags.OEM3:  // HOME
                    return "\u21F9";
                case ButtonFlags.OEM4:  // M1
                    return "\u2212";
                case ButtonFlags.OEM5:  // M2
                    return "\u2213";
            }

            return base.GetGlyph(button);
        }
    }
}
