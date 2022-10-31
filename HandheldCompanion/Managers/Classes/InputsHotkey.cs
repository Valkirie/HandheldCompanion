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
            Custom = 4,
        }

        public static SortedDictionary<ushort, InputsHotkey> InputsHotkeys = new()
        {
            { 01, new InputsHotkey(InputsHotkeyType.Overlay,    "\uEDE3",   "overlayGamepad",       "Segoe Fluent Icons",   20, false) },
            { 02, new InputsHotkey(InputsHotkeyType.Overlay,    "\uEDA4",   "overlayTrackpads",     "Segoe Fluent Icons",   20, false) },

            { 10, new InputsHotkey(InputsHotkeyType.Quicktools, "\uEC7A",   "quickTools",           "Segoe Fluent Icons",   20, false) },
            { 11, new InputsHotkey(InputsHotkeyType.Quicktools, "\u2795",   "increaseTDP",          "Segoe UI Symbol",      20, false) },
            { 12, new InputsHotkey(InputsHotkeyType.Quicktools, "\u2796",   "decreaseTDP",          "Segoe UI Symbol",      20, false) },
            { 13, new InputsHotkey(InputsHotkeyType.Quicktools, "\uE769",   "suspendResumeTask",    "Segoe Fluent Icons",   20, false) },

            { 20, new InputsHotkey(InputsHotkeyType.Windows,    "\uE765",   "shortcutKeyboard",     "Segoe Fluent Icons",   20, false) },
            { 21, new InputsHotkey(InputsHotkeyType.Windows,    "\uE138",   "shortcutDesktop",      "Segoe UI Symbol",      20, false) },
            { 22, new InputsHotkey(InputsHotkeyType.Windows,    "ESC",      "shortcutESC",          "Segoe UI",             12, false) },
            { 23, new InputsHotkey(InputsHotkeyType.Windows,    "\uEE49",   "shortcutExpand",       "Segoe Fluent Icons",   20, false) },
            { 24, new InputsHotkey(InputsHotkeyType.Windows,    "\uE7C4",   "shortcutTaskview",     "Segoe MDL2 Assets",    20, false) },
            { 25, new InputsHotkey(InputsHotkeyType.Windows,    "\uE71D",   "shortcutTaskManager",  "Segoe Fluent Icons",   20, false) },

            { 30, new InputsHotkey(InputsHotkeyType.Handheld,   "\uE7C4",   "shortcutMainwindow",   "Segoe Fluent Icons",   20, false) },
            { 31, new InputsHotkey(InputsHotkeyType.Handheld,   "\uE2E8",   "shortcutGuide",        "Segoe UI Symbol",      20, true) },

            { 40, new InputsHotkey(InputsHotkeyType.Custom,     "\u2780",   "shortcutCustom0",      "Segoe UI Symbol",      20, false) },
            { 41, new InputsHotkey(InputsHotkeyType.Custom,     "\u2781",   "shortcutCustom1",      "Segoe UI Symbol",      20, false) },
            { 42, new InputsHotkey(InputsHotkeyType.Custom,     "\u2782",   "shortcutCustom2",      "Segoe UI Symbol",      20, false) },
            { 43, new InputsHotkey(InputsHotkeyType.Custom,     "\u2783",   "shortcutCustom3",      "Segoe UI Symbol",      20, false) },
            { 44, new InputsHotkey(InputsHotkeyType.Custom,     "\u2784",   "shortcutCustom4",      "Segoe UI Symbol",      20, false) },
            { 45, new InputsHotkey(InputsHotkeyType.Custom,     "\u2785",   "shortcutCustom5",      "Segoe UI Symbol",      20, false) },
            { 46, new InputsHotkey(InputsHotkeyType.Custom,     "\u2786",   "shortcutCustom6",      "Segoe UI Symbol",      20, false) },
            { 47, new InputsHotkey(InputsHotkeyType.Custom,     "\u2787",   "shortcutCustom7",      "Segoe UI Symbol",      20, false) },
            { 48, new InputsHotkey(InputsHotkeyType.Custom,     "\u2788",   "shortcutCustom8",      "Segoe UI Symbol",      20, false) },
            { 49, new InputsHotkey(InputsHotkeyType.Custom,     "\u2789",   "shortcutCustom9",      "Segoe UI Symbol",      20, false) },
        };

        public string Glyph { get; set; }
        public string Listener { get; set; }
        public string Description { get; set; }
        public FontFamily fontFamily { get; set; }
        public double fontSize { get; set; }
        public InputsHotkeyType hotkeyType { get; set; }
        public bool OnKeyDown { get; set; }

        public InputsHotkey(InputsHotkeyType hotkeyType, string glyph, string listener, string fontFamily, double fontSize, bool onKeyDown)
        {
            this.hotkeyType = hotkeyType;
            this.Glyph = glyph;
            this.Listener = listener;
            this.fontFamily = new FontFamily(fontFamily);
            this.fontSize = fontSize;
            this.OnKeyDown = onKeyDown;
        }

        public InputsHotkey()
        {
        }

        public string GetName()
        {
            // return localized string if available
            string listener = Listener;

            switch (hotkeyType)
            {
                case InputsHotkeyType.Custom:
                    listener = "shortcutCustom";
                    break;
            }

            string root = Properties.Resources.ResourceManager.GetString($"InputsHotkey_{listener}");

            if (!string.IsNullOrEmpty(root))
                return root;

            return Listener;
        }

        public string GetDescription()
        {
            // return localized string if available
            string listener = Listener;

            switch (hotkeyType)
            {
                case InputsHotkeyType.Custom:
                    listener = "shortcutCustom";
                    break;
            }

            string root = Properties.Resources.ResourceManager.GetString($"InputsHotkey_{listener}Desc");

            if (!string.IsNullOrEmpty(root))
                return root;

            return string.Empty;
        }
    }
}
