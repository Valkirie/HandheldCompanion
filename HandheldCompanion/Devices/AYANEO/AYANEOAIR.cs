using HandheldCompanion.Inputs;
using System.Collections.Generic;
using System.Numerics;
using WindowsInput.Events;
namespace HandheldCompanion.Devices;

public class AYANEOAIR : AYANEO.AYANEODevice
{
    public AYANEOAIR()
    {
        // device specific settings
        ProductIllustration = "device_aya_air";
        ProductModel = "AYANEOAir";

        // https://www.amd.com/en/products/apu/amd-ryzen-5-5560u
        nTDP = new double[] { 12, 12, 15 };
        cTDP = new double[] { 3, 15 };
        GfxClock = new double[] { 100, 1600 };
        CpuClock = 4000;

        GyrometerAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'X' },
            { 'Y', 'Z' },
            { 'Z', 'Y' }
        };

        AccelerometerAxis = new Vector3(-1.0f, 1.0f, -1.0f);
        AccelerometerAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'X' },
            { 'Y', 'Z' },
            { 'Z', 'Y' }
        };

        // device specific capacities
        Capabilities = DeviceCapabilities.FanControl;
        Capabilities |= DeviceCapabilities.DynamicLighting;

        ECDetails = new ECDetails
        {
            AddressFanControl = 0x44A,
            AddressFanDuty = 0x44B,
            AddressStatusCommandPort = 0x4E,
            AddressDataPort = 0x4F,
            FanValueMin = 0,
            FanValueMax = 100
        };

        OEMChords.Add(new DeviceChord("Custom Key Top Right",
            new List<KeyCode> { KeyCode.RControlKey, KeyCode.LWin, KeyCode.F10 },
            new List<KeyCode> { KeyCode.F10, KeyCode.LWin, KeyCode.RControlKey },
            false, ButtonFlags.OEM3
        ));

        OEMChords.Add(new DeviceChord("Custom Key Top Left",
            new List<KeyCode> { KeyCode.RControlKey, KeyCode.LWin, KeyCode.F11 },
            new List<KeyCode> { KeyCode.F11, KeyCode.LWin, KeyCode.RControlKey },
            false, ButtonFlags.OEM4
        ));

        OEMChords.Add(new DeviceChord("Custom Key Big",
            new List<KeyCode> { KeyCode.RControlKey, KeyCode.LWin, KeyCode.F12 },
            new List<KeyCode> { KeyCode.F12, KeyCode.LWin, KeyCode.RControlKey },
            false, ButtonFlags.OEM1
        ));

        OEMChords.Add(new DeviceChord("Custom Key Small",
            new List<KeyCode> { KeyCode.LWin, KeyCode.D },
            new List<KeyCode> { KeyCode.LWin, KeyCode.D },
            false, ButtonFlags.OEM2
        ));
    }

    public override string GetGlyph(ButtonFlags button)
    {
        switch (button)
        {
            case ButtonFlags.OEM1:
                return "\uE003";
            case ButtonFlags.OEM2:
                return "\u220B";
            case ButtonFlags.OEM3:
                return "\u220A";
            case ButtonFlags.OEM4:
                return "\u2209";
        }

        return defaultGlyph;
    }
}