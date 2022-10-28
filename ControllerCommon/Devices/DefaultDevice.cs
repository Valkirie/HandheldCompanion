using System.Collections.Generic;
using WindowsInput.Events;

namespace ControllerCommon.Devices
{
    public class DefaultDevice : Device
    {
        public DefaultDevice() : base()
        {
            listeners.Add(new DeviceChord("Function 1", new List<KeyCode>() { KeyCode.F1 }));
            listeners.Add(new DeviceChord("Function 2", new List<KeyCode>() { KeyCode.F2 }));
            listeners.Add(new DeviceChord("Function 3", new List<KeyCode>() { KeyCode.F3 }));
            listeners.Add(new DeviceChord("Function 4", new List<KeyCode>() { KeyCode.F4 }));
            listeners.Add(new DeviceChord("Function 5", new List<KeyCode>() { KeyCode.F5 }));
            listeners.Add(new DeviceChord("Function 6", new List<KeyCode>() { KeyCode.F6 }));
            listeners.Add(new DeviceChord("Function 7", new List<KeyCode>() { KeyCode.F7 }));
            listeners.Add(new DeviceChord("Function 8", new List<KeyCode>() { KeyCode.F8 }));
            listeners.Add(new DeviceChord("Function 9", new List<KeyCode>() { KeyCode.F9 }));
            listeners.Add(new DeviceChord("Function 10", new List<KeyCode>() { KeyCode.F10 }));
            listeners.Add(new DeviceChord("Function 11", new List<KeyCode>() { KeyCode.F11 }));
            listeners.Add(new DeviceChord("Function 12", new List<KeyCode>() { KeyCode.F12 }));
        }
    }
}
