using System;
using System.Collections.Generic;
using FontFamily = System.Windows.Media.FontFamily;

namespace HandheldCompanion.Managers
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
            UI = 5,
        }

        public static SortedDictionary<ushort, InputsHotkey> InputsHotkeys = new()
        {
            { 01, new InputsHotkey(InputsHotkeyType.Overlay,    "\uEDE3",   "overlayGamepad",                   "Segoe Fluent Icons",   20, false,  true) },
            { 02, new InputsHotkey(InputsHotkeyType.Overlay,    "\uEDA4",   "overlayTrackpads",                 "Segoe Fluent Icons",   20, false,  true) },

            { 10, new InputsHotkey(InputsHotkeyType.Quicktools, "\uEC7A",   "quickTools",                       "Segoe Fluent Icons",   20, false,  true) },
            { 11, new InputsHotkey(InputsHotkeyType.Quicktools, "\u2795",   "increaseTDP",                      "Segoe UI Symbol",      20, false,  true) },
            { 12, new InputsHotkey(InputsHotkeyType.Quicktools, "\u2796",   "decreaseTDP",                      "Segoe UI Symbol",      20, false,  true) },
            { 13, new InputsHotkey(InputsHotkeyType.Quicktools, "\uE769",   "suspendResumeTask",                "Segoe Fluent Icons",   20, false,  true) },

            { 20, new InputsHotkey(InputsHotkeyType.Windows,    "\uE765",   "shortcutKeyboard",                 "Segoe Fluent Icons",   20, false,  true) },
            { 21, new InputsHotkey(InputsHotkeyType.Windows,    "\uE138",   "shortcutDesktop",                  "Segoe UI Symbol",      20, false,  true) },
            { 22, new InputsHotkey(InputsHotkeyType.Windows,    "ESC",      "shortcutESC",                      "Segoe UI",             12, false,  true) },
            { 23, new InputsHotkey(InputsHotkeyType.Windows,    "\uEE49",   "shortcutExpand",                   "Segoe Fluent Icons",   20, false,  true) },
            { 24, new InputsHotkey(InputsHotkeyType.Windows,    "\uE7C4",   "shortcutTaskview",                 "Segoe MDL2 Assets",    20, false,  true) },
            { 25, new InputsHotkey(InputsHotkeyType.Windows,    "\uE71D",   "shortcutTaskManager",              "Segoe Fluent Icons",   20, false,  true) },
            { 26, new InputsHotkey(InputsHotkeyType.Windows,    "\uE8BB",   "shortcutKillApp",                  "Segoe Fluent Icons",   20, false,  true) },

            { 30, new InputsHotkey(InputsHotkeyType.Handheld,   "\uE7C4",   "shortcutMainwindow",               "Segoe Fluent Icons",   20, false,  true) },
            { 31, new InputsHotkey(InputsHotkeyType.Handheld,   "\uE2E8",   "shortcutGuide",                    "Segoe UI Symbol",      20, false,  true) },

            { 40, new InputsHotkey(InputsHotkeyType.Custom,     "\u2780",   "shortcutCustom0",                  "Segoe UI Symbol",      20, false,  true) },
            { 41, new InputsHotkey(InputsHotkeyType.Custom,     "\u2781",   "shortcutCustom1",                  "Segoe UI Symbol",      20, false,  true) },
            { 42, new InputsHotkey(InputsHotkeyType.Custom,     "\u2782",   "shortcutCustom2",                  "Segoe UI Symbol",      20, false,  true) },
            { 43, new InputsHotkey(InputsHotkeyType.Custom,     "\u2783",   "shortcutCustom3",                  "Segoe UI Symbol",      20, false,  true) },
            { 44, new InputsHotkey(InputsHotkeyType.Custom,     "\u2784",   "shortcutCustom4",                  "Segoe UI Symbol",      20, false,  true) },
            { 45, new InputsHotkey(InputsHotkeyType.Custom,     "\u2785",   "shortcutCustom5",                  "Segoe UI Symbol",      20, false,  true) },
            { 46, new InputsHotkey(InputsHotkeyType.Custom,     "\u2786",   "shortcutCustom6",                  "Segoe UI Symbol",      20, false,  true) },
            { 47, new InputsHotkey(InputsHotkeyType.Custom,     "\u2787",   "shortcutCustom7",                  "Segoe UI Symbol",      20, false,  true) },
            { 48, new InputsHotkey(InputsHotkeyType.Custom,     "\u2788",   "shortcutCustom8",                  "Segoe UI Symbol",      20, false,  true) },
            { 49, new InputsHotkey(InputsHotkeyType.Custom,     "\u2789",   "shortcutCustom9",                  "Segoe UI Symbol",      20, false,  true) },

            // UI hotkeys shouldn't be listed on the hotkeys page
            { 50, new InputsHotkey(InputsHotkeyType.UI,         "\uEDE3",   "shortcutProfilesPage",             "Segoe Fluent Icons",   20, false,  true) },
            { 51, new InputsHotkey(InputsHotkeyType.UI,         "\uEDE3",   "shortcutProfilesSettingsMode0",    "Segoe Fluent Icons",   20, false,  true) },
        };

        public string Glyph { get; set; }
        public string Listener { get; set; }
        public string Description { get; set; }
        public FontFamily fontFamily { get; set; }
        public double fontSize { get; set; }
        public InputsHotkeyType hotkeyType { get; set; }
        public bool OnKeyDown { get; set; }
        public bool OnKeyUp { get; set; }

        public InputsHotkey(InputsHotkeyType hotkeyType, string glyph, string listener, string fontFamily, double fontSize, bool onKeyDown, bool onKeyUp)
        {
            this.hotkeyType = hotkeyType;
            this.Glyph = glyph;
            this.Listener = listener;
            this.fontFamily = new FontFamily(fontFamily);
            this.fontSize = fontSize;
            this.OnKeyDown = onKeyDown;
            this.OnKeyUp = onKeyUp;
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
