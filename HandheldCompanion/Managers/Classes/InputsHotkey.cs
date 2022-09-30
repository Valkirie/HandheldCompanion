using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using static Microsoft.WindowsAPICodePack.Shell.PropertySystem.SystemProperties.System;
using FontFamily = System.Windows.Media.FontFamily;

namespace HandheldCompanion.Managers.Classes
{
    public class InputsHotkey
    {
        [Flags]
        public enum InputsHotkeyType : ushort
        {
            Overlay = 0,
            Quicktools = 1,
            Windows = 2,
        }

        public static Dictionary<ushort, InputsHotkey> Hotkeys = new()
        {
            { 1, new InputsHotkey(InputsHotkeyType.Overlay, "\uEDE3", "overlayGamepad", "Display virtual controller") },
            { 2, new InputsHotkey(InputsHotkeyType.Overlay, "\uEDA4", "overlayTrackpads", "Display virtual trackpads") },
            { 3, new InputsHotkey(InputsHotkeyType.Quicktools, "\uEC7A", "quickTools", "Summon quicktools window") },
            { 4, new InputsHotkey(InputsHotkeyType.Windows, "\uE765", "shortcutKeyboard", "Summon touch keyboard") },
            { 5, new InputsHotkey(InputsHotkeyType.Windows, "\uE782", "shortcutDesktop", "Summon desktop", "HoloLens MDL2 Assets") },
            { 6, new InputsHotkey(InputsHotkeyType.Windows, "\uEF2C", "shortcutESC", "Send ESCAPE key") },
            { 7, new InputsHotkey(InputsHotkeyType.Windows, "\uEE49", "shortcutExpand", "Send ALT + ENTER keystroke") },
            { 8, new InputsHotkey(InputsHotkeyType.Windows, "\uE7C4", "shortcutTaskview", "Send WINDOWS + TAB keystroke") },
        };

        public string Glyph { get; set; }
        public string Listener { get; set; }
        public string Description { get; set; }
        public FontFamily fontFamily { get; set; }
        public InputsHotkeyType hotkeyType { get; set; }

        public InputsHotkey(InputsHotkeyType _hotkeyType, string glyph, string listener, string description, string _fontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets,Segoe UI Symbol, HoloLens MDL2 Assets")
        {
            hotkeyType = _hotkeyType;
            Glyph = glyph;
            Listener = listener;
            Description = description;
            fontFamily = new FontFamily(_fontFamily);
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
            string root = Properties.Resources.ResourceManager.GetString($"InputsHotkey_{Listener}");

            if (String.IsNullOrEmpty(root))
                return Listener;
            else
                return root;
        }

        public string GetDescription()
        {
            // return localized string if available
            string root = Properties.Resources.ResourceManager.GetString($"InputsHotkey_{Listener}Desc");

            if (String.IsNullOrEmpty(root))
                return Description;
            else
                return root;
        }
    }
}
