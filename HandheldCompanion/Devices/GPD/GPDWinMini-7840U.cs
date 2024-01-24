using HandheldCompanion.Inputs;
using System.Collections.Generic;
using System.Numerics;
using WindowsInput.Events;

namespace HandheldCompanion.Devices;

public class GPDWinMini_7840U : IDevice
{
    public GPDWinMini_7840U()
    {
        // https://www.amd.com/en/products/apu/amd-ryzen-7-7840u
        nTDP = new double[] { 15, 15, 18 };
        cTDP = new double[] { 5, 18 };
        GfxClock = new double[] { 200, 2700 };
        CpuClock = 5100;

        // device specific settings
        ProductIllustration = "device_gpd_winmini";

        // device specific capacities
        //Capabilities = DeviceCapabilities.FanControl;

        ECDetails = new ECDetails
        {
            AddressFanControl = 0x275,
            AddressFanDuty = 0x1809,
            AddressStatusCommandPort = 0x4E,
            AddressDataPort = 0x4F,
            FanValueMin = 0,
            FanValueMax = 184
        };

        GyrometerAxis = new Vector3(1.0f, 1.0f, -1.0f);
        GyrometerAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'Y' },
            { 'Y', 'Z' },
            { 'Z', 'X' }
        };

        AccelerometerAxis = new Vector3(1.0f, -1.0f, 1.0f);
        AccelerometerAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'X' },
            { 'Y', 'Z' },
            { 'Z', 'Y' }
        };

        // Disabled this one as Win Max 2 also sends an Xbox guide input when Menu key is pressed.
        OEMChords.Add(new DeviceChord("Menu",
            new List<KeyCode> { KeyCode.LButton | KeyCode.XButton2 },
            new List<KeyCode> { KeyCode.LButton | KeyCode.XButton2 },
            true, ButtonFlags.OEM1
        ));

        // note, need to manually configured in GPD app
        OEMChords.Add(new DeviceChord("L4",
            new List<KeyCode> { KeyCode.F11, KeyCode.L },
            new List<KeyCode> { KeyCode.F11, KeyCode.L },
            false, ButtonFlags.OEM2
        ));

        OEMChords.Add(new DeviceChord("R4",
            new List<KeyCode> { KeyCode.F12, KeyCode.R },
            new List<KeyCode> { KeyCode.F12, KeyCode.R },
            false, ButtonFlags.OEM3
        ));
    }

    public override string GetGlyph(ButtonFlags button)
    {
        switch (button)
        {
            case ButtonFlags.OEM2:
                return "\u2276";
            case ButtonFlags.OEM3:
                return "\u2277";
        }

        return defaultGlyph;
    }
}