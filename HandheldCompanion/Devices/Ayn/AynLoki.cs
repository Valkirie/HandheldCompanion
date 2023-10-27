using HandheldCompanion.Inputs;
using System.Collections.Generic;
using System.Numerics;
using WindowsInput.Events;

namespace HandheldCompanion.Devices;

public class AynLoki : IDevice
{
    public AynLoki()
    {
        // Ayn Loki device generic settings
        ProductIllustration = "device_ayn_loki";
        ProductModel = "AynLoki";

        AngularVelocityAxis = new Vector3(1.0f, 1.0f, -1.0f);
        AngularVelocityAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'Y' },
            { 'Y', 'Z' },
            { 'Z', 'X' }
        };

        AccelerationAxis = new Vector3(1.0f, -1.0f, -1.0f);
        AccelerationAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'X' },
            { 'Y', 'Z' },
            { 'Z', 'Y' }
        };

        OEMChords.Add(new DeviceChord("Guide",
            new List<KeyCode> { KeyCode.LButton, KeyCode.XButton2 },
            new List<KeyCode> { KeyCode.LButton, KeyCode.XButton2 },
            false, ButtonFlags.OEM1
        ));

        OEMChords.Add(new DeviceChord("LCC",
            new List<KeyCode> { KeyCode.LControl, KeyCode.LShift, KeyCode.LMenu, KeyCode.T },
            new List<KeyCode> { KeyCode.T, KeyCode.LMenu, KeyCode.LShift, KeyCode.LControl },
            false, ButtonFlags.OEM2
        ));
    }

    public override string GetGlyph(ButtonFlags button)
    {
        switch (button)
        {
            case ButtonFlags.OEM1:
                return "\u220C";
            case ButtonFlags.OEM2:
                return "\u220D";
        }

        return defaultGlyph;
    }
}