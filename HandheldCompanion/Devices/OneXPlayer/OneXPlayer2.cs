using HandheldCompanion.Commands.Functions.HC;
using HandheldCompanion.Inputs;
using HandheldCompanion.Shared;
using System.Numerics;
using WindowsInput.Events;

namespace HandheldCompanion.Devices
{
    public class OneXPlayer2 : OneXAOKZOE
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

            GyrometerAxis = new Vector3(-1.0f, -1.0f, -1.0f);
            this.GyrometerAxisSwap = new()
            {
                { 'X', 'X' },
                { 'Y', 'Z' },
                { 'Z', 'Y' },
            };

            AccelerometerAxis = new Vector3(1.0f, -1.0f, -1.0f);
            this.AccelerometerAxisSwap = new()
            {
                { 'X', 'X' },
                { 'Y', 'Z' },
                { 'Z', 'Y' },
            };

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
            OEMChords.Add(new KeyboardChord("Turbo",
                [KeyCode.LWin, KeyCode.LMenu, KeyCode.LControl],
                [KeyCode.LWin, KeyCode.LMenu, KeyCode.LControl],
                false, ButtonFlags.OEM2
                ));

            // prepare hotkeys
            DeviceHotkeys[typeof(MainWindowCommands)].inputsChord.ButtonState[ButtonFlags.Special] = true;
            DeviceHotkeys[typeof(MainWindowCommands)].InputsChordType = InputsChordType.Long;
            DeviceHotkeys[typeof(QuickToolsCommands)].inputsChord.ButtonState[ButtonFlags.Special] = true;
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
            bool success = base.Open();
            if (!success)
                return false;

            // allow OneX button to pass key inputs
            EcWriteByte(0xEB, 0xEB);
            if (ECRamReadByte(0xEB) == 0xEB)
                LogManager.LogInformation("Unlocked {0} OEM button", ButtonFlags.OEM2);

            return success;
        }

        public override void Close()
        {
            EcWriteByte(0xEB, 0x00);
            if (EcReadByte(0xEB) == 0x00)
                LogManager.LogInformation("Locked {0} OEM button", ButtonFlags.OEM2);

            base.Close();
        }
    }

    public class OneXPlayer2Pro : OneXPlayer2
    {
        public OneXPlayer2Pro()
        {
            // device specific settings
            ProductIllustration = "device_onexplayer_2";
            ProductModel = "ONEXPLAYER 2 7840U";

            // https://www.amd.com/fr/products/processors/laptop/ryzen/7000-series/amd-ryzen-7-7840u.html
            nTDP = new double[] { 15, 15, 20 };
            cTDP = new double[] { 4, 30 };
            GfxClock = new double[] { 100, 2700 };
            CpuClock = 5100;
        }
    }
}
