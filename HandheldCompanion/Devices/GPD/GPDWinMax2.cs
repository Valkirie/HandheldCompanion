using HandheldCompanion.Inputs;
using System.Collections.Generic;
using WindowsInput.Events;

namespace HandheldCompanion.Devices;

public class GPDWinMax2 : IDevice
{
    public GPDWinMax2()
    {
        // device specific settings
        ProductIllustration = "device_gpd_winmax2";

        // device specific capacities
        Capabilities = DeviceCapabilities.FanControl;

        ECDetails = new ECDetails
        {
            AddressControl = 0x275,
            AddressDuty = 0x1809,
            AddressRegistry = 0x4E,
            AddressData = 0x4F,
            ValueMin = 0,
            ValueMax = 184
        };

        // Disabled this one as Win Max 2 also sends an Xbox guide input when Menu key is pressed.
        OEMChords.Add(new DeviceChord("Menu",
            new List<KeyCode> { KeyCode.LButton | KeyCode.XButton2 },
            new List<KeyCode> { KeyCode.LButton | KeyCode.XButton2 },
            true, ButtonFlags.OEM1
        ));

        // note, need to manually configured in GPD app
        OEMChords.Add(new DeviceChord("Bottom button left",
            new List<KeyCode> { KeyCode.F11, KeyCode.L },
            new List<KeyCode> { KeyCode.F11, KeyCode.L },
            false, ButtonFlags.OEM2
        ));

        OEMChords.Add(new DeviceChord("Bottom button right",
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
                return "\u220E";
            case ButtonFlags.OEM3:
                return "\u220F";
        }

        return defaultGlyph;
    }
}