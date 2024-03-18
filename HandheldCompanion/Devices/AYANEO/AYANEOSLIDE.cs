using HandheldCompanion.Inputs;
using HandheldCompanion.Utils;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Windows.Media;
using WindowsInput.Events;
using static HandheldCompanion.Utils.DeviceUtils;
namespace HandheldCompanion.Devices;

public class AYANEOSlide : AYANEO.AYANEODevice
{
    public AYANEOSlide()
    {
        // device specific settings
        ProductIllustration = "device_aya_slide";
        ProductModel = "AYANEOSlide";

        // https://www.amd.com/en/products/apu/amd-ryzen-7-7840u
        nTDP = new double[] { 15, 15, 20 };
        cTDP = new double[] { 3, 54 };
        GfxClock = new double[] { 100, 2700 };
        CpuClock = 5100;

        GyrometerAxis = new Vector3(1.0f, 1.0f, -1.0f);
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
        // Capabilities = DeviceCapabilities.FanControl;
        // Capabilities |= DeviceCapabilities.DynamicLighting;
        // DynamicLightingCapabilities |= LEDLevel.SolidColor;


        ECDetails = new ECDetails
        {
            AddressStatusCommandPort = 0x4E, // unknown
            AddressDataPort = 0x4F, // unknown
            AddressFanControl = 0, // unknown
            AddressFanDuty = 0, // unknown
            FanValueMin = 0, // unknown
            FanValueMax = 100 // unknown
        };
        OEMChords.Clear();

        OEMChords.Add(new DeviceChord("Custom Key Big",
            new List<KeyCode> { KeyCode.LControl, KeyCode.LWin, KeyCode.F17 },
            new List<KeyCode> { KeyCode.F17, KeyCode.LControl, KeyCode.LWin },
            false, ButtonFlags.OEM1
        ));

        OEMChords.Add(new DeviceChord("Custom Key Small",
            new List<KeyCode> { KeyCode.LWin, KeyCode.D },
            new List<KeyCode> { KeyCode.D, KeyCode.LWin },
            false, ButtonFlags.OEM2
        ));


        OEMChords.Add(new DeviceChord("Custom Key Top Left",
            new List<KeyCode> { KeyCode.LControl, KeyCode.LWin, KeyCode.F15 },
            new List<KeyCode> { KeyCode.F15, KeyCode.LControl, KeyCode.LWin },
            false, ButtonFlags.OEM3
        ));

        OEMChords.Add(new DeviceChord("Custom Key Top Right",
            new List<KeyCode> { KeyCode.LControl, KeyCode.LWin, KeyCode.F16 },
            new List<KeyCode> { KeyCode.F16, KeyCode.LControl, KeyCode.LWin },
            false, ButtonFlags.OEM4
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
        }

        return defaultGlyph;
    }
}