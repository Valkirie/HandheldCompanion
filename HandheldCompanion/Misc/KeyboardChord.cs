using HandheldCompanion.Inputs;
using System.Collections.Generic;
using System.Linq;

using WindowsInput.Events;

namespace HandheldCompanion;

public class KeyboardChord
{
    public SortedDictionary<bool, List<KeyCode>> chords = new()
    {
        { true, new List<KeyCode>() },
        { false, new List<KeyCode>() }
    };

    public string name;
    public bool silenced;
    public ButtonState state = new();

    public KeyboardChord(string name, List<KeyCode> chordDown, List<KeyCode> chordUP, bool silenced = false,
        ButtonFlags button = ButtonFlags.None)
    {
        this.name = name;
        this.silenced = silenced;
        state[button] = true;

        chords[true].AddRange(chordDown);
        chords[false].AddRange(chordUP);
    }

    public List<KeyCode> GetChord(bool IsKeyDown)
    {
        return chords[IsKeyDown].OrderBy(key => key).OrderBy(key => key).ToList();
    }
}