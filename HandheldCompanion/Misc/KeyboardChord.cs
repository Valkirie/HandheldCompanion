using HandheldCompanion.Inputs;
using System.Collections.Generic;

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

    public KeyboardChord(string name, List<KeyCode> chordDown = null, List<KeyCode> chordUP = null, bool silenced = false, ButtonFlags button = ButtonFlags.None)
    {
        this.name = name;
        this.silenced = silenced;
        state[button] = true;

        if (chordDown is not null)
            chords[true].AddRange(chordDown);
        if (chordUP is not null)
            chords[false].AddRange(chordUP);
    }

    public List<KeyCode> GetChord(bool IsKeyDown)
    {
        return chords[IsKeyDown];
    }
}