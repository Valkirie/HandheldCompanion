using System;
using System.Collections.Generic;
using System.Text;
using static Microsoft.WindowsAPICodePack.Shell.PropertySystem.SystemProperties.System;

namespace HandheldCompanion.Managers.Classes
{
    public class InputsHotkey
    {
        public static List<InputsHotkey> Hotkeys = new()
        {
            new InputsHotkey(0, "\uEDE3", "overlayGamepad", "Display virtual controller"),
            new InputsHotkey(1, "\uEDA4", "overlayTrackpads", "Display virtual trackpads"),
            new InputsHotkey(2, "\uEC7A", "quickTools", "Summon quicktools window")
        };

        public ushort Id { get; set; }
        public string Glyph { get; set; }
        public string Listener { get; set; }
        public string Description { get; set; }

        public InputsHotkey(ushort id, string glyph, string listener, string description)
        {
            Id = id;
            Glyph = glyph;
            Listener = listener;
            Description = description;
        }

        public InputsHotkey()
        {
        }

        public string GetGlyph()
        {
            return Glyph;
        }

        public string GetListener()
        {
            return Listener;
        }

        public string GetName()
        {
            // return localized string if available
            string root = Properties.Resources.ResourceManager.GetString($"InputsHotkey{Id}");

            if (root != null)
                return root;
            else
                return Listener;
        }

        public string GetDescription()
        {
            // return localized string if available
            string root = Properties.Resources.ResourceManager.GetString($"InputsHotkey{Id}Desc");

            if (root != null)
                return root;
            else
                return Description;
        }

        public ushort GetId()
        {
            return Id;
        }
    }
}
