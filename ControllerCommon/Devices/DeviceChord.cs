using System.Collections.Generic;
using System.Linq;
using WindowsInput.Events;

namespace ControllerCommon.Devices
{
    public class DeviceChord
    {
        public string name;
        public Dictionary<bool, List<KeyCode>> chords = new Dictionary<bool, List<KeyCode>>()
        {
            { true, new List<KeyCode>() },
            { false, new List<KeyCode>() }
        };

        public DeviceChord(string name, List<KeyCode> chordDown, List<KeyCode> chordUP)
        {
            this.name = name;

            this.chords[true].AddRange(chordDown);
            this.chords[false].AddRange(chordUP);
        }
    }
}
