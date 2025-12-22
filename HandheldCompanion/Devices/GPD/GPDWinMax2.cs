using HandheldCompanion.Commands.Functions.HC;
using HandheldCompanion.Inputs;
using System.Collections.Generic;
using System.Numerics;
using WindowsInput.Events;

namespace HandheldCompanion.Devices;

public class GPDWinMax2 : IDevice
{
    public GPDWinMax2()
    {
        // device specific settings
        ProductIllustration = "device_gpd_winmax2";
        UseOpenLib = true;

        // device specific capacities
        Capabilities = DeviceCapabilities.FanControl;

        ECDetails = new ECDetails
        {
            AddressFanControl = 0x275,
            AddressFanDuty = 0x1809,
            AddressStatusCommandPort = 0x4E,
            AddressDataPort = 0x4F,
            FanValueMin = 0,
            FanValueMax = 184
        };

        GyrometerAxis = new Vector3(1.0f, -1.0f, -1.0f);
        GyrometerAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'Y' },
            { 'Y', 'Z' },
            { 'Z', 'X' }
        };
        AccelerometerAxis = new Vector3(-1.0f, -1.0f, 1.0f);
        AccelerometerAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'X' },
            { 'Y', 'Z' },
            { 'Z', 'Y' }
        };

        // Disabled this one as Win Max 2 also sends an Xbox guide input when Menu key is pressed.
        OEMChords.Add(new KeyboardChord("Menu",
            [KeyCode.LButton | KeyCode.XButton2],
            [KeyCode.LButton | KeyCode.XButton2],
            true, ButtonFlags.OEM1
        ));

        // note, need to manually configured in GPD app
        OEMChords.Add(new KeyboardChord("Bottom button left",
            [KeyCode.F11, KeyCode.L],
            [KeyCode.F11, KeyCode.L],
            false, ButtonFlags.OEM2
        ));

        OEMChords.Add(new KeyboardChord("Bottom button right",
            [KeyCode.F12, KeyCode.R],
            [KeyCode.F12, KeyCode.R],
            false, ButtonFlags.OEM3
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
                return "\u220E";
            case ButtonFlags.OEM3:
                return "\u220F";
        }

        return defaultGlyph;
    }
}