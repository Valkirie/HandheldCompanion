using System.Collections.Generic;
using System.Linq;
using WindowsInput.Events;

namespace ControllerCommon.Devices
{
    public class DeviceChord
    {
        public string name;
        public bool silenced;

        public Dictionary<bool, List<KeyCode>> chords = new Dictionary<bool, List<KeyCode>>()
        {
            { true, new List<KeyCode>() },
            { false, new List<KeyCode>() }
        };

        public DeviceChord(string name, List<KeyCode> chordDown, List<KeyCode> chordUP, bool silenced = false)
        {
            this.name = name;
            this.silenced = silenced;

            this.chords[true].AddRange(chordDown);
            this.chords[false].AddRange(chordUP);
        }
    }
}
