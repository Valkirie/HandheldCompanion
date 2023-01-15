using ControllerCommon.Controllers;
using ControllerCommon.Inputs;
using System.Collections.Generic;
using System.Linq;
using WindowsInput.Events;

namespace ControllerCommon.Devices
{
    public class DeviceChord
    {
        public string name;
        public bool silenced;
        public ButtonState state = new();

        public Dictionary<bool, List<KeyCode>> chords = new Dictionary<bool, List<KeyCode>>()
        {
            { true, new List<KeyCode>() },
            { false, new List<KeyCode>() }
        };

        public DeviceChord(string name, List<KeyCode> chordDown, List<KeyCode> chordUP, bool silenced = false, ButtonFlags button = ButtonFlags.None)
        {
            this.name = name;
            this.silenced = silenced;
            this.state[button] = true;

            this.chords[true].AddRange(chordDown);
            this.chords[false].AddRange(chordUP);
        }

        public string GetChord(bool IsKeyDown)
        {
            return string.Join(" | ", chords[IsKeyDown].OrderBy(key => key).ToList());
        }
    }
}
