using System;
using System.Collections.Generic;
using System.Text;

namespace HandheldCompanion.Managers.Classes
{
    public class InputsHotkey
    {
        public static InputsHotkey virtualController = new InputsHotkey(0, "\uEDE3", "overlayGamepad", "Display virtual controller");
        public static InputsHotkey virtualTrackpads = new InputsHotkey(1, "\uEDA4", "overlayTrackpads", "Display virtual trackpads");
        public static InputsHotkey quicktoolsWindow = new InputsHotkey(2, "\uEC7A", "quickTools", "Summon quicktools window");

        public static List<InputsHotkey> Hotkeys = new()
        {
            virtualController,
            virtualTrackpads,
            quicktoolsWindow
        };

        public int Id { get; set; }
        public string Glyph { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }

        public InputsHotkey(int id, string glyph, string name, string description)
        {
            Id = id;
            Glyph = glyph;
            Name = name;
            Description = description;
        }

        public InputsHotkey()
        {
        }
    }
}
