using System.Collections.Generic;
using WindowsInput.Events;

namespace ControllerCommon.Devices
{
    public class DefaultDevice : Device
    {
        public DefaultDevice() : base()
        {
            for (int i = (int)KeyCode.F1; i < (int)KeyCode.F12; i++)
            {
                listeners.Add(new DeviceChord($"Function {i}",
                    new List<KeyCode>() { (KeyCode)i },
                    new List<KeyCode>() { (KeyCode)i }
                    ));
            }
        }
    }
}
