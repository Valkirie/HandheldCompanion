using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using System.Collections.Generic;
using System.Numerics;
using WindowsInput.Events;

namespace HandheldCompanion.Devices
{
    public class OneXPlayer2 : IDevice
    {
        public OneXPlayer2() : base()
        {
            // device specific settings
            this.ProductIllustration = "device_onexplayer_2";
            this.ProductModel = "ONEXPLAYER 2 6800U";

            // https://www.amd.com/en/products/apu/amd-ryzen-7-6800u
            this.nTDP = new double[] { 15, 15, 20 };
            this.cTDP = new double[] { 4, 28 };
            this.GfxClock = new double[] { 100, 2200 };
            this.CpuClock = 4700;

            GyrometerAxis = new Vector3(-1.0f, 1.0f, -1.0f);
            this.GyrometerAxisSwap = new()
            {
                { 'X', 'X' },
                { 'Y', 'Z' },
                { 'Z', 'Y' },
            };

            AccelerometerAxis = new Vector3(-1.0f, 1.0f, -1.0f);
            this.AccelerometerAxisSwap = new()
            {
                { 'X', 'X' },
                { 'Y', 'Z' },
                { 'Z', 'Y' },
            };

            // device specific capacities
            this.Capabilities = DeviceCapabilities.FanControl;

            ECDetails = new ECDetails
            {
                AddressFanControl = 0x44A,
                AddressFanDuty = 0x44B,
                AddressStatusCommandPort = 0x4E,
                AddressDataPort = 0x4F,
                FanValueMin = 0,
                FanValueMax = 184
            };

            // Choose OEM2 due to presense of physical Xbox Guide button
            // Dirty implementation, below chords get spammed 2-3x by device
            OEMChords.Add(new DeviceChord("Turbo",
                new List<KeyCode>() { KeyCode.LWin, KeyCode.LMenu, KeyCode.LControl },
                new List<KeyCode>() { KeyCode.LWin, KeyCode.LMenu, KeyCode.LControl },
                false, ButtonFlags.OEM2
                ));
        }
        public override string GetGlyph(ButtonFlags button)
        {
            switch (button)
            {
                case ButtonFlags.OEM2:
                    return "\u2211";
            }

            return defaultGlyph;
        }

        public override bool Open()
        {
            var success = base.Open();
            if (!success)
                return false;

            // allow OneX button to pass key inputs
            LogManager.LogInformation("Unlocked {0} OEM button", ButtonFlags.OEM2);

            ECRamDirectWrite(0x4EB, ECDetails, 0xEB);

            return ECRamReadByte(0x4EB, ECDetails) == 0xEB;
        }

        public override void Close()
        {
            LogManager.LogInformation("Locked {0} OEM button", ButtonFlags.OEM2);
            ECRamDirectWrite(0x4EB, ECDetails, 0x00);
            base.Close();
        }
    }
}
