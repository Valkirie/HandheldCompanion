using System;
using System.Collections.Generic;
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
            Handheld = 3,
        }

        public static Dictionary<ushort, InputsHotkey> Hotkeys = new()
        {
            { 1, new InputsHotkey(InputsHotkeyType.Overlay, "\uEDE3", "overlayGamepad") },
            { 2, new InputsHotkey(InputsHotkeyType.Overlay, "\uEDA4", "overlayTrackpads") },
            { 3, new InputsHotkey(InputsHotkeyType.Quicktools, "\uEC7A", "quickTools") },
            { 4, new InputsHotkey(InputsHotkeyType.Windows, "\uE765", "shortcutKeyboard") },
            { 5, new InputsHotkey(InputsHotkeyType.Windows, "\uE138", "shortcutDesktop", "Segoe UI Symbol") },
            { 6, new InputsHotkey(InputsHotkeyType.Windows, "ESC", "shortcutESC", "Segoe UI", 12) },
            { 7, new InputsHotkey(InputsHotkeyType.Windows, "\uEE49", "shortcutExpand") },
            { 8, new InputsHotkey(InputsHotkeyType.Windows, "\uE7C4", "shortcutTaskview") },
            { 9, new InputsHotkey(InputsHotkeyType.Handheld, "\uE7C4", "shortcutMainwindow") },
            { 10, new InputsHotkey(InputsHotkeyType.Handheld, "\uE2E8", "shortcutGuide", "Segoe UI Symbol") },
        };

        public string Glyph { get; set; }
        public string Listener { get; set; }
        public string Description { get; set; }
        public FontFamily fontFamily { get; set; }
        public double fontSize { get; set; } = 16.0d;
        public InputsHotkeyType hotkeyType { get; set; }

        public InputsHotkey(InputsHotkeyType hotkeyType, string glyph, string listener, string fontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets,Segoe UI Symbol", double fontSize = 16.0d)
        {
            this.hotkeyType = hotkeyType;
            this.Glyph = glyph;
            this.Listener = listener;
            this.fontFamily = new FontFamily(fontFamily);
            this.fontSize = fontSize;
        }

        public InputsHotkey()
        {
        }

        public string GetListener()
        {
            return Listener;
        }

        public string GetName()
        {
            // return localized string if available
            string root = Properties.Resources.ResourceManager.GetString($"InputsHotkey_{Listener}");

            if (!String.IsNullOrEmpty(root))
                return root;
            
            return Listener;
        }

        public string GetDescription()
        {
            // return localized string if available
            string root = Properties.Resources.ResourceManager.GetString($"InputsHotkey_{Listener}Desc");

            if (!String.IsNullOrEmpty(root))
                return root;

            return String.Empty;
        }
    }
}
