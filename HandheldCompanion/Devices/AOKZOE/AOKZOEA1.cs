using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using System.Collections.Generic;
using System.Numerics;
using WindowsInput.Events;

namespace HandheldCompanion.Devices;

public class AOKZOEA1 : IDevice
{
    public AOKZOEA1()
    {
        // device specific settings
        ProductIllustration = "device_aokzoe_a1";
        ProductModel = "AOKZOEA1";

        // https://www.amd.com/en/products/apu/amd-ryzen-7-6800u 
        nTDP = new double[] { 15, 15, 20 };
        cTDP = new double[] { 4, 28 };
        GfxClock = new double[] { 100, 2200 };

        AngularVelocityAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'X' },
            { 'Y', 'Z' },
            { 'Z', 'Y' }
        };

        AccelerationAxis = new Vector3(-1.0f, 1.0f, -1.0f);
        AccelerationAxisSwap = new SortedDictionary<char, char>
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
            AddressStatusCommandPort = 0x4E, // 78
            AddressDataPort = 0x4F,     // 79
            FanValueMin = 0,
            FanValueMax = 184
        };

        // Home
        OEMChords.Add(new DeviceChord("Home",
            new List<KeyCode> { KeyCode.LWin, KeyCode.D },
            new List<KeyCode> { KeyCode.LWin, KeyCode.D },
            false, ButtonFlags.OEM1
        ));

        // Home (long press 1.5s)
        OEMChords.Add(new DeviceChord("Home, Long-press",
            new List<KeyCode> { KeyCode.LWin, KeyCode.G },
            new List<KeyCode> { KeyCode.LWin, KeyCode.G },
            false, ButtonFlags.OEM6
        ));

        // Keyboard
        OEMChords.Add(new DeviceChord("Keyboard",
            new List<KeyCode> { KeyCode.RControlKey, KeyCode.LWin, KeyCode.O },
            new List<KeyCode> { KeyCode.O, KeyCode.LWin, KeyCode.RControlKey },
            false, ButtonFlags.OEM2
        ));

        // Turbo
        OEMChords.Add(new DeviceChord("Turbo",
            new List<KeyCode> { KeyCode.LControl, KeyCode.LWin, KeyCode.LMenu },
            new List<KeyCode> { KeyCode.LControl, KeyCode.LWin, KeyCode.LMenu },
            false, ButtonFlags.OEM3
        ));

        // Home + Keyboard
        OEMChords.Add(new DeviceChord("Home + Keyboard",
            new List<KeyCode> { KeyCode.RAlt, KeyCode.RControlKey, KeyCode.Delete },
            new List<KeyCode> { KeyCode.Delete, KeyCode.RControlKey, KeyCode.RAlt },
            false, ButtonFlags.OEM4
        ));

        // Home + Turbo
        OEMChords.Add(new DeviceChord("Home + Turbo",
            new List<KeyCode> { KeyCode.LWin, KeyCode.Snapshot },
            new List<KeyCode> { KeyCode.Snapshot, KeyCode.LWin },
            false, ButtonFlags.OEM5
        ));
    }

    public override bool Open()
    {
        var success = base.Open();
        if (!success)
            return false;

        // allow OneX button to pass key inputs
        LogManager.LogInformation("Unlocked {0} OEM button", ButtonFlags.OEM3);

        ECRamDirectWrite(0x4F1, ECDetails, 0x40);
        ECRamDirectWrite(0x4F2, ECDetails, 0x02);

        return (ECRamReadByte(0x4F1, ECDetails) == 0x40 && ECRamReadByte(0x4F2, ECDetails) == 0x02);
    }

    public override void Close()
    {
        LogManager.LogInformation("Locked {0} OEM button", ButtonFlags.OEM3);
        ECRamDirectWrite(0x4F1, ECDetails, 0x00);
        ECRamDirectWrite(0x4F2, ECDetails, 0x00);
        base.Close();
    }

    public override string GetGlyph(ButtonFlags button)
    {
        switch (button)
        {
            case ButtonFlags.OEM1:
                return "\u220C";
            case ButtonFlags.OEM2:
                return "\u2210";
            case ButtonFlags.OEM3:
                return "\u2211";
        }

        return defaultGlyph;
    }
}