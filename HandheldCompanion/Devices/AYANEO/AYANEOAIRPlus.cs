using HandheldCompanion.Inputs;
using System.Collections.Generic;
using System.Numerics;

using WindowsInput.Events;

namespace HandheldCompanion.Devices;

public class AYANEOAIRPlus : IDevice
{
    public AYANEOAIRPlus()
    {
        // device specific settings
        ProductIllustration = "device_aya_air";
        ProductModel = "AYANEOAir";

        AngularVelocityAxis = new Vector3(1.0f, -1.0f, -1.0f);
        AngularVelocityAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'X' },
            { 'Y', 'Z' },
            { 'Z', 'Y' }
        };

        AccelerationAxis = new Vector3(-1.0f, -1.0f, -1.0f);
        AccelerationAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'X' },
            { 'Y', 'Z' },
            { 'Z', 'Y' }
        };

        OEMChords.Add(new DeviceChord("Custom Key Top Right",
            new List<KeyCode> { KeyCode.LControl, KeyCode.LWin, KeyCode.F16 },
            new List<KeyCode> { KeyCode.F16, KeyCode.LControl, KeyCode.LWin },
            false, ButtonFlags.OEM3
        ));

        OEMChords.Add(new DeviceChord("Custom Key Top Left",
            new List<KeyCode> { KeyCode.LControl, KeyCode.LWin, KeyCode.F15 },
            new List<KeyCode> { KeyCode.F15, KeyCode.LControl, KeyCode.LWin },
            false, ButtonFlags.OEM4
        ));

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