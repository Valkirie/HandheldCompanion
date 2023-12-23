using HandheldCompanion.Inputs;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Windows.Media;
using WindowsInput.Events;
using static HandheldCompanion.Utils.DeviceUtils;
namespace HandheldCompanion.Devices;

public class AYANEOKUN : AYANEO.AYANEODevice
{
    public AYANEOKUN()
    {
        // device specific settings
        ProductIllustration = "device_aya_kun";
        ProductModel = "AYANEO KUN";

        // https://www.amd.com/en/products/apu/amd-ryzen-7-7840u
        nTDP = new double[] { 15, 15, 20 };
        cTDP = new double[] { 3, 54 };
        GfxClock = new double[] { 100, 2700 };
        CpuClock = 5100;

        GyrometerAxis = new Vector3(1.0f, 1.0f, 1.0f);
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

        // device specific capacities
        Capabilities = DeviceCapabilities.FanControl;
        //Capabilities |= DeviceCapabilities.DynamicLighting;
        //DynamicLightingCapabilities |= LEDLevel.SolidColor;

        ECDetails = new ECDetails
        {
            AddressFanControl = 0x44A,
            AddressFanDuty = 0x44B,
            AddressStatusCommandPort = 0x4E,
            AddressDataPort = 0x4F,
            FanValueMin = 0,
            FanValueMax = 100
        };

        OEMChords.Add(new DeviceChord("Custom Key Big",
            new List<KeyCode> { KeyCode.RControlKey, KeyCode.LWin, KeyCode.F17 },
            new List<KeyCode> { KeyCode.F17, KeyCode.LWin, KeyCode.RControlKey },
            false, ButtonFlags.OEM1
        ));

        OEMChords.Add(new DeviceChord("Custom Key Small",
            new List<KeyCode> { KeyCode.LWin, KeyCode.D },
            new List<KeyCode> { KeyCode.LWin, KeyCode.D },
            false, ButtonFlags.OEM2
        ));

        OEMChords.Add(new DeviceChord("Custom Key Top Left",
            new List<KeyCode> { KeyCode.RControlKey, KeyCode.LWin, KeyCode.F16 },
            new List<KeyCode> { KeyCode.F16, KeyCode.LWin, KeyCode.RControlKey },
            false, ButtonFlags.OEM3
        ));

        OEMChords.Add(new DeviceChord("Custom Key Top Right",
            new List<KeyCode> { KeyCode.RControlKey, KeyCode.LWin, KeyCode.F15 },
            new List<KeyCode> { KeyCode.F15, KeyCode.LWin, KeyCode.RControlKey },
            false, ButtonFlags.OEM4
        ));

        OEMChords.Add(new DeviceChord("T",
            new List<KeyCode> { KeyCode.RControlKey, KeyCode.LWin, KeyCode.F18 },
            new List<KeyCode> { KeyCode.F18, KeyCode.LWin, KeyCode.RControlKey },
            false, ButtonFlags.OEM5
        ));

        OEMChords.Add(new DeviceChord("Guide",
            new List<KeyCode> { KeyCode.LButton, KeyCode.XButton2 },
            new List<KeyCode> { KeyCode.LButton, KeyCode.XButton2 },
            false, ButtonFlags.OEM6
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
                return "\u2209";
            case ButtonFlags.OEM4:
                return "\u220A";
            case ButtonFlags.OEM5:
                return "\u0054";
            case ButtonFlags.OEM6:
                return "\uE001";
        }

        return defaultGlyph;
    }
}