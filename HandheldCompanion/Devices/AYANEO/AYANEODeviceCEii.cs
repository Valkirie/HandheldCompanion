using HandheldCompanion.Inputs;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using WindowsInput.Events;

namespace HandheldCompanion.Devices.AYANEO
{
    // Base class implementing FAN/RGB control for all AYANEO device using the CEii protocol
    // AIR Plus AMD, AIR Plus Mendocino, AIR Plus Intel, Slide
    public class AYANEODeviceCEii : AYANEODeviceCEc
    {
        public AYANEODeviceCEii()
        {
            this.ECDetails = new ECDetails
            {
                AddressStatusCommandPort = 0x4e,
                AddressDataPort = 0x4f,
                AddressFanControl = 0xc8,
                AddressFanDuty = 0x04,
                FanValueMin = 0,
                FanValueMax = 255
            };

            this.GyrometerAxis = new Vector3(1.0f, 1.0f, -1.0f);
            this.GyrometerAxisSwap = new SortedDictionary<char, char>
            {
                { 'X', 'X' },
                { 'Y', 'Z' },
                { 'Z', 'Y' }
            };

            this.AccelerometerAxis = new Vector3(1.0f, 1.0f, -1.0f);
            this.AccelerometerAxisSwap = new SortedDictionary<char, char>
            {
                { 'X', 'X' },
                { 'Y', 'Z' },
                { 'Z', 'Y' }
            };

            this.OEMChords.Add(new DeviceChord("Custom Key Big",
                new List<KeyCode> { KeyCode.LControl, KeyCode.LWin, KeyCode.F17 },
                new List<KeyCode> { KeyCode.F17, KeyCode.LWin, KeyCode.LControl },
                false, ButtonFlags.OEM1
            ));
            this.OEMChords.Add(new DeviceChord("Custom Key Small",
                new List<KeyCode> { KeyCode.LWin, KeyCode.D },
                new List<KeyCode> { KeyCode.LWin, KeyCode.D },
                false, ButtonFlags.OEM2
            ));
            this.OEMChords.Add(new DeviceChord("Custom Key Top Left",
                new List<KeyCode> { KeyCode.LControl, KeyCode.LWin, KeyCode.F15 },
                new List<KeyCode> { KeyCode.F15, KeyCode.LWin, KeyCode.LControl },
                false, ButtonFlags.OEM3
            ));
            this.OEMChords.Add(new DeviceChord("Custom Key Top Right",
                new List<KeyCode> { KeyCode.LControl, KeyCode.LWin, KeyCode.F16 },
                new List<KeyCode> { KeyCode.F16, KeyCode.LWin, KeyCode.LControl },
                false, ButtonFlags.OEM4
            ));
        }

        public override void SetFanDuty(double percent)
        {
            if (ECDetails.AddressFanDuty == 0)
                return;

            if (!IsOpen)
                return;

            var duty = percent * (ECDetails.FanValueMax - ECDetails.FanValueMin) / 100 + ECDetails.FanValueMin;
            var data = Convert.ToByte(duty);
            this.ECRamDirectWrite((byte)this.ECDetails.AddressFanDuty, data, 0x18);
        }


        public override void SetFanControl(bool enable, int mode = 0)
        {
            if (this.ECDetails.AddressFanControl == 0)
                return;

            if (!this.IsOpen)
                return;

            byte data = enable ? (byte)0xa5 : (byte)0x00;
            this.ECRamDirectWrite((byte)this.ECDetails.AddressFanControl, data);
        }

        // Based on CEiiEcHelper_RgbI2cWrite (AYASpace) but renamed to override existing function
        protected override void CEcControl_RgbI2cWrite(LEDGroup group, byte command, byte argument)
        {
            byte applyAddress = group == LEDGroup.StickLeft ? (byte)0xc6 : (byte)0x86;
            short groupAddress = group == LEDGroup.StickLeft ? (short)0xb0 : (short)0x70;

            this.CEiiEcHelper_RgbStart();
            this.ECRamDirectWrite((byte)(groupAddress + command), argument);
            this.ECRamDirectWrite(applyAddress, 0x01);
            Thread.Sleep(10); // AYASpace does this so copied it here
        }

        public bool ECRamDirectWrite(byte address, byte data, byte offset = 0xd1)
        {
            ushort address2 = BitConverter.ToUInt16(new byte[] { (byte)address, offset }, 0);
            return base.ECRamDirectWrite(address2, this.ECDetails, data);
        }

        protected override void CEcControl_RgbHoldControl()
        {
            this.CEiiEcHelper_RgbStart();
        }

        protected override void CEcControl_RgbReleaseControl()
        {
            this.CEiiEcHelper_RgbStop();
        }

        protected void CEiiEcHelper_RgbStart()
        {
            this.ECRamDirectWrite(0x87, 0xa5);
        }

        protected void CEiiEcHelper_RgbStop()
        {
            this.ECRamDirectWrite(0x87, 0x00);
        }
    }
}
