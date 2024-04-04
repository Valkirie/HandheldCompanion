using System;
using System.Threading;
using static HandheldCompanion.Utils.DeviceUtils;

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

        protected void CEiiEcHelper_RgbStart()
        {
            this.ECRamDirectWrite(0x87, 0xa5);
        }

        public bool ECRamDirectWrite(byte address, byte data, byte offset = 0xd1)
        {
            ushort address2 = BitConverter.ToUInt16(new byte[] { (byte)address, offset }, 0);
            return base.ECRamDirectWrite(address2, this.ECDetails, data);
        }
    }
}
