using HandheldCompanion.Inputs;
using System.Collections.Generic;
<<<<<<< HEAD
<<<<<<<< HEAD:HandheldCompanion/Devices/AYANEO/AYANEOAIR1S.cs
========
using System.Numerics;
>>>>>>>> f8fea3c25fb5fd254f5020d43305b7356ec9770d:HandheldCompanion/Devices/AYANEO/AYANEO2S.cs
=======
>>>>>>> f8fea3c25fb5fd254f5020d43305b7356ec9770d
using WindowsInput.Events;

namespace HandheldCompanion.Devices;

public class AYANEOAIR1S : AYANEOAIR
{
    public AYANEOAIR1S()
    {
        // https://www.amd.com/en/products/apu/amd-ryzen-7-7840u
        nTDP = new double[] { 15, 15, 20 };
        cTDP = new double[] { 4, 28 };
        GfxClock = new double[] { 100, 2700 };
<<<<<<< HEAD
        CpuClock = 5100;

<<<<<<<< HEAD:HandheldCompanion/Devices/AYANEO/AYANEOAIR1S.cs
        OEMChords.Clear();
========
        AngularVelocityAxis = new Vector3(1.0f, 1.0f, 1.0f);
        AngularVelocityAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'X' },
            { 'Y', 'Z' },
            { 'Z', 'Y' }
        };

        AccelerationAxis = new Vector3(1.0f, 1.0f, 1.0f);
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
            AddressControl = 0x44A,
            AddressDuty = 0x44B,
            AddressRegistry = 0x4E,
            AddressData = 0x4F,
            ValueMin = 0,
            ValueMax = 100
        };
>>>>>>>> f8fea3c25fb5fd254f5020d43305b7356ec9770d:HandheldCompanion/Devices/AYANEO/AYANEO2S.cs
=======

        OEMChords.Clear();
>>>>>>> f8fea3c25fb5fd254f5020d43305b7356ec9770d

        OEMChords.Add(new DeviceChord("Custom Key Top Right",
            new List<KeyCode> { KeyCode.RControlKey, KeyCode.LWin, KeyCode.F16 },
            new List<KeyCode> { KeyCode.F16, KeyCode.LWin, KeyCode.RControlKey },
            false, ButtonFlags.OEM3
        ));

        OEMChords.Add(new DeviceChord("Custom Key Top Left",
            new List<KeyCode> { KeyCode.RControlKey, KeyCode.LWin, KeyCode.F15 },
            new List<KeyCode> { KeyCode.F15, KeyCode.LWin, KeyCode.RControlKey },
            false, ButtonFlags.OEM4
        ));

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
    }
}