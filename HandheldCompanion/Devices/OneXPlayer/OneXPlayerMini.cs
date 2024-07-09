using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using System.Collections.Generic;
using System.Numerics;
using WindowsInput.Events;

namespace HandheldCompanion.Devices;

public class OneXPlayerMini : IDevice
{
    public OneXPlayerMini()
    {
        // device specific settings
        ProductIllustration = "device_onexplayer_mini";
        ProductModel = "ONEXPLAYERMini";

        GyrometerAxis = new Vector3(1.0f, -1.0f, 1.0f);
        GyrometerAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'X' },
            { 'Y', 'Z' },
            { 'Z', 'Y' }
        };

        AccelerometerAxis = new Vector3(-1.0f, -1.0f, 1.0f);
        AccelerometerAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'X' },
            { 'Y', 'Z' },
            { 'Z', 'Y' }
        };

        // device specific capacities
        Capabilities = DeviceCapabilities.FanControl;

        ECDetails = new ECDetails
        {
            AddressFanControl = 0x44A,
            AddressFanDuty = 0x44B,
            AddressStatusCommandPort = 0x4E,
            AddressDataPort = 0x4F,
            FanValueMin = 0,
            FanValueMax = 255
        };

        OEMChords.Add(new KeyboardChord("Orange",
            new List<KeyCode> { KeyCode.LControl, KeyCode.LWin, KeyCode.LMenu },
            new List<KeyCode> { KeyCode.LControl, KeyCode.LWin, KeyCode.LMenu },
            false, ButtonFlags.OEM1
        ));

        OEMChords.Add(new KeyboardChord("Keyboard",
            new List<KeyCode> { KeyCode.LWin, KeyCode.RControlKey, KeyCode.O },
            new List<KeyCode> { KeyCode.O, KeyCode.RControlKey, KeyCode.LWin },
            false, ButtonFlags.OEM2
        ));

        OEMChords.Add(new KeyboardChord("Function",
            new List<KeyCode> { KeyCode.LWin, KeyCode.D },
            new List<KeyCode> { KeyCode.LWin, KeyCode.D },
            false, ButtonFlags.OEM3
        ));
    }
    public override string GetGlyph(ButtonFlags button)
    {
        switch (button)
        {
            case ButtonFlags.OEM1:
                return "\u2219";
            case ButtonFlags.OEM2:
                return "\u2210";
            case ButtonFlags.OEM3:
                return "\u2218";
        }

        return defaultGlyph;
    }

    public override bool Open()
    {
        var success = base.Open();
        if (!success)
            return false;

        // allow OneX button to pass key inputs
        LogManager.LogInformation("Unlocked {0} OEM button", ButtonFlags.OEM1);
        return ECRamDirectWrite(0x41E, ECDetails, 0x01);
    }

    public override void Close()
    {
        LogManager.LogInformation("Locked {0} OEM button", ButtonFlags.OEM1);
        ECRamDirectWrite(0x41E, ECDetails, 0x00);
        base.Close();
    }
}