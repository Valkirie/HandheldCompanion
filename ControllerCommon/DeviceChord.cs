using ControllerCommon.Inputs;
using System.Collections.Generic;
using System.Linq;
using WindowsInput.Events;

namespace ControllerCommon
{
    public class DeviceChord
    {
        public string name;
        public bool silenced;
        public ButtonState state = new();

        public SortedDictionary<bool, List<KeyCode>> chords = new SortedDictionary<bool, List<KeyCode>>()
        {
            { true, new List<KeyCode>() },
            { false, new List<KeyCode>() }
        };

        public DeviceChord(string name, List<KeyCode> chordDown, List<KeyCode> chordUP, bool silenced = false, ButtonFlags button = ButtonFlags.None)
        {
            this.name = name;
            this.silenced = silenced;
            state[button] = true;

            chords[true].AddRange(chordDown);
            chords[false].AddRange(chordUP);
        }

        public string GetChord(bool IsKeyDown)
        {
            return string.Join(" | ", chords[IsKeyDown].OrderBy(key => key).ToList());
        }
    }
}
