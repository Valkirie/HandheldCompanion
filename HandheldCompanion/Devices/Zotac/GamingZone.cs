using HandheldCompanion.Extensions;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Shared;
using HandheldCompanion.Utils;
using HidLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Media;
using WindowsInput.Events;
using static HandheldCompanion.Utils.DeviceUtils;

namespace HandheldCompanion.Devices.Zotac
{
    public class GamingZone : IDevice
    {
        [DllImport("Kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        protected static extern bool GetPhysicallyInstalledSystemMemory(out ulong TotalMemoryInKilobytes);

        private const byte INPUT_HID_ID = 0x00;

        protected uint physicalInstalledRamGB = 16; // TODO: Detect dynamically if needed
        protected readonly Dictionary<uint, uint> defaultVRamSize = new Dictionary<uint, uint>
        {
            { 16U, 4U }, // 16GB RAM => 4GB base VRAM
            { 32U, 6U },
            { 64U, 12U }
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

            // https://www.amd.com/en/products/apu/amd-ryzen-7-ECDetails.AddressStatusCommandPort40u
            // https://www.amd.com/en/products/apu/amd-ryzen-7-8840u
            this.nTDP = new double[] { 15, 15, 20 };
            this.cTDP = new double[] { 3, 28 };
            this.GfxClock = new double[] { 100, 2700 };
            this.CpuClock = 5100;

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

            // device specific capacities
            Capabilities |= DeviceCapabilities.FanControl;
            Capabilities |= DeviceCapabilities.DynamicLighting;
            Capabilities |= DeviceCapabilities.DynamicLightingBrightness;
            Capabilities |= DeviceCapabilities.DynamicLightingSecondLEDColor;

            // dynamic lighting capacities
            DynamicLightingCapabilities |= LEDLevel.Breathing;
            DynamicLightingCapabilities |= LEDLevel.Rainbow;
            DynamicLightingCapabilities |= LEDLevel.Wave;
            DynamicLightingCapabilities |= LEDLevel.Wheel;
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

            data[5] = 241;     // COMMAND (Restore Profile Set)

            // The rest is zeroed by default

            // CRC over [5]..[62] (payload bytes [4]..[61] in logical packet)
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

            data[5] = 161;    // COMMAND (SetBtnMap)
            data[6] = 1;      // SourceKey: M1

            // Modifier: LeftCtrl (1) | LeftWinCMD (8) = 9
            data[11] = 1 | 8;

            // Keyboard: F11
            data[13] = 68;   // BtnKeyboardMapPos[122]

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

            data[5] = 161;
            data[6] = 2;      // SourceKey: M2 (if that's your enum)

            // Modifier: LeftCtrl (1) | LeftWinCMD (8) = 9
            data[11] = 1 | 8;

            // Keyboard: F12
            data[13] = 69;   // BtnKeyboardMapPos[123]

            ushort CRC = CalcZotacCRC(data, 5, 62);
            data[63] = (byte)(CRC >> 8);
            data[64] = (byte)(CRC & 0xFF);

            return data;
        }

        private enum BrightnessID
        {
            Brightness_0 = 0,
            Brightness_25 = 25, // 0x00000019
            Brightness_50 = 50, // 0x00000032
            Brightness_75 = 75, // 0x0000004B
            Brightness_100 = 100, // 0x00000064
        }

        private enum LightEffect
        {
            Rainbow = 0,
            Breathe = 1,
            Stars = 2,
            Fade = 3,
            Dance = 4,
            Flash = 5,
            Wink = 6,
            Random = 7,
            Off = 240, // 0x000000F0
        }

        private enum LightSpeed
        {
            Slow,
            Normal,
            Fast,
        }

        private enum LEDSettings
        {
            Speed = 1,
            Effect = 2,
            Brightness = 3,
            Color = 4,
        }

        private byte[] SendLedCmd(LEDSettings setting, byte value)
        {
            byte[] data = new byte[65];
            data[0] = 0x00;   // Report ID

            data[1] = 225;
            data[2] = 0;
            data[3] = 0;
            data[4] = 60;

            data[5] = 173;
            data[6] = (byte)setting;
            data[7] = value;

            ushort CRC = CalcZotacCRC(data, 5, 62);
            data[63] = (byte)(CRC >> 8);
            data[64] = (byte)(CRC & 0xFF);

            return data;
        }

        private byte[] SendLedRGB(byte LedNumSet, Color MainColor, Color SecondaryColor)
        {
            byte[] data = new byte[65];
            data[0] = 0x00;   // Report ID

            data[1] = 225;
            data[2] = 0;
            data[3] = 0;
            data[4] = 60;

            data[5] = 173;          // COMMAND
            data[6] = 0;            // SETTING
            data[7] = LedNumSet;    // LedNumSet

            // Set all 10 LEDs to red (0xFF0000)
            for (int i = 0; i < 10; i++)
            {
                int pos = 8 + (i * 3);
                data[pos]       = LedNumSet == 0 ? MainColor.R : SecondaryColor.R;  // R
                data[pos + 1]   = LedNumSet == 0 ? MainColor.G : SecondaryColor.G;  // G
                data[pos + 2]   = LedNumSet == 0 ? MainColor.B : SecondaryColor.B;  // B
            }

            // CRC over [5]..[62] (i.e., [4]..[61] in 64-byte logic)
            ushort crc = CalcZotacCRC(data, 5, 62);
            data[63] = (byte)(crc >> 8);
            data[64] = (byte)(crc & 0xFF);

            return data;
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
            uint num1 = (uint)(((int)seed ^ (int)c) & 0xFF);
            uint num2 = num1 & 0x0F;
            int num3 = ((int)num2 << 4) ^ (int)num1;
            uint num4 = (uint)((uint)num3 >> 4);
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
            if (brightness == 0.0)
                return BrightnessID.Brightness_0;
            if (brightness == 0.25)
                return BrightnessID.Brightness_25;
            if (brightness == 0.5)
                return BrightnessID.Brightness_50;
            if (brightness == 0.75)
                return BrightnessID.Brightness_75;
            if (brightness == 1.0)
                return BrightnessID.Brightness_100;
            throw new ArgumentException($"Unknown brightness {brightness}");
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
            // Apply the color for the left and right LED
            // todo: figure out who's who
            LightEffect lightEffect = LightEffect.Off;
            switch (level)
            {
                default:
                case LEDLevel.SolidColor:
                    lightEffect = LightEffect.Off; // ??
                    break;
                case LEDLevel.Breathing:
                    lightEffect = LightEffect.Breathe;
                    break;
                case LEDLevel.Rainbow:
                    lightEffect = LightEffect.Rainbow;
                    break;
                case LEDLevel.Wave:
                    lightEffect = LightEffect.Dance; // ??
                    break;
                case LEDLevel.Wheel:
                    lightEffect = LightEffect.Stars; // ??
                    break;
                case LEDLevel.Gradient:
                    lightEffect = LightEffect.Stars; // ??
                    break;
            }

            byte speedValue = (byte)ConvertToLightSpeed(speed);

            if (hidDevices.TryGetValue(INPUT_HID_ID, out HidDevice hidDevice))
            {
                if (!hidDevice.IsConnected)
                    return false;

                hidDevice.Write(SendLedCmd(LEDSettings.Effect, speedValue));
                hidDevice.Write(SendLedCmd(LEDSettings.Speed, speedValue));

                // send command twice ?
                hidDevice.Write(SendLedRGB(0, MainColor, SecondaryColor));
                hidDevice.Write(SendLedRGB(1, MainColor, SecondaryColor));

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

                data[5] = 251; // HIDSaveConfigDataReportCMD

                return hidDevice.Write(data);
            }

            return false;
        }

        #region EC
        // additionSize in GB
        public void SetVRamSize(uint additionSize)
        {
            uint vRAM = (physicalInstalledRamGB + additionSize) * 4U;

            openLibSys.WriteIoPortByte((ushort)112, (byte)122);
            openLibSys.WriteIoPortByte((ushort)113, (byte)vRAM);
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
