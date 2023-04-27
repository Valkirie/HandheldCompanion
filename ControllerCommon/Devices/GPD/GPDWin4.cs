using ControllerCommon.Inputs;
using ControllerCommon.Managers;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Windows.Documents;
using WindowsInput.Events;

namespace ControllerCommon.Devices
{
    public class GPDWin4 : IDevice
    {
        public GPDWin4() : base()
        {
            // device specific settings
            this.ProductIllustration = "device_gpd4";

            // https://www.amd.com/fr/products/apu/amd-ryzen-7-6800u
            this.nTDP = new double[] { 15, 15, 28 };
            this.cTDP = new double[] { 5, 28 };
            this.GfxClock = new double[] { 100, 2200 };

            // device specific capacities
            this.Capacities = DeviceCapacities.FanControl;

            this.FanDetails = new FanDetails()
            {
                AddressControl = 0xC311,
                AddressDuty = 0xC880,
                AddressRegistry = 0x2E,
                AddressData = 0x2F,
                ValueMin = 1,
                ValueMax = 127,
            };

            this.AngularVelocityAxis = new Vector3(-1.0f, -1.0f, 1.0f);
            this.AngularVelocityAxisSwap = new()
            {
                { 'X', 'X' },
                { 'Y', 'Z' },
                { 'Z', 'Y' },
            };

            this.AccelerationAxisSwap = new()
            {
                { 'X', 'X' },
                { 'Y', 'Z' },
                { 'Z', 'Y' },
            };

            // Note, OEM1 not configured as this device has it's own Menu button for guide button

            // Note, chords need to be manually configured in GPD app first by end user

            // GPD Back buttons do not have a "hold", configured buttons are key down and up immediately
            // Holding back buttons will result in same key down and up input every 2-3 seconds
            // Configured chords in GPD app need unique characters otherwise this leads to a
            // "mixed" result when pressing both buttons at the same time
            OEMChords.Add(new DeviceChord("Bottom button left",
                new List<KeyCode>() { KeyCode.F11, KeyCode.L },
                new List<KeyCode>() { KeyCode.F11, KeyCode.L },
                false, ButtonFlags.OEM2
                ));

            OEMChords.Add(new DeviceChord("Bottom button right",
                new List<KeyCode>() { KeyCode.F12, KeyCode.R },
                new List<KeyCode>() { KeyCode.F12, KeyCode.R },
                false, ButtonFlags.OEM3
                ));
        }

        public override void SetFanControl(bool enable)
        {
            switch(enable)
            {
                case false:
                    base.SetFanDuty(0);
                    return;
            }
        }

        public override void SetFanDuty(double percent)
        {
            if (FanDetails.AddressControl == 0)
                return;

            double duty = percent * (FanDetails.ValueMax - FanDetails.ValueMin) / 100 + FanDetails.ValueMin;
            byte data = Convert.ToByte(duty);

            ECRamDirectWrite(FanDetails.AddressControl, FanDetails, data);
        }

        public override bool Open()
        {
            bool success = base.Open();
            if (!success)
                return false;

            // allow fan manipulation
            byte EC_Chip_ID1 = ECRamReadByte(0x2000);
            if (EC_Chip_ID1 == 0x55)
            {
                byte EC_Chip_Ver = ECRamReadByte(0x1060);
                EC_Chip_Ver = (byte)(EC_Chip_Ver | 0x80);

                LogManager.LogInformation("Unlocked GPD WIN 4 ({0}) fan control", EC_Chip_Ver);
                return ECRamDirectWrite(0x1060, EC_Chip_Ver);
            }

            return false;
        }

        public override void Close()
        {
            base.Close();
        }
    }
}
