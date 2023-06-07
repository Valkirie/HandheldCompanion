using System.Collections.Generic;
using ControllerCommon.Inputs;
using WindowsInput.Events;

namespace ControllerCommon.Devices;

public class DefaultDevice : IDevice
{
    public DefaultDevice()
    {
        OEMChords.Add(new DeviceChord("F1",
            new List<KeyCode> { KeyCode.F1 },
            new List<KeyCode> { KeyCode.F1 },
            false, ButtonFlags.OEM1
        ));

        OEMChords.Add(new DeviceChord("F2",
            new List<KeyCode> { KeyCode.F2 },
            new List<KeyCode> { KeyCode.F2 },
            false, ButtonFlags.OEM2
        ));

        OEMChords.Add(new DeviceChord("F3",
            new List<KeyCode> { KeyCode.F3 },
            new List<KeyCode> { KeyCode.F3 },
            false, ButtonFlags.OEM3
        ));

        OEMChords.Add(new DeviceChord("F4",
            new List<KeyCode> { KeyCode.F4 },
            new List<KeyCode> { KeyCode.F4 },
            false, ButtonFlags.OEM4
        ));
    }
}