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
            HC = 3,
            Device = 4,
            Custom = 5,
            Embedded = 6,
        }

        public static SortedDictionary<ushort, InputsHotkey> InputsHotkeys = new()
        {
            // Overlay hotkeys
            { 01, new InputsHotkey(InputsHotkeyType.Overlay,    "\uEDE3",   "overlayGamepad",                   "Segoe Fluent Icons",   20, false,  true,   null,                   string.Empty,           false,  false) },
            { 02, new InputsHotkey(InputsHotkeyType.Overlay,    "\uEDA4",   "overlayTrackpads",                 "Segoe Fluent Icons",   20, false,  true,   null,                   string.Empty,           false,  false) },

            // Quicktools hotkeys
            { 10, new InputsHotkey(InputsHotkeyType.Quicktools, "\uEC7A",   "quickTools",                       "Segoe Fluent Icons",   20, false,  true,   null,                   string.Empty,           false,  false) },
            { 11, new InputsHotkey(InputsHotkeyType.Quicktools, "\u2795",   "increaseTDP",                      "Segoe UI Symbol",      20, true,   false,  null,                   string.Empty,           false,  false) },
            { 12, new InputsHotkey(InputsHotkeyType.Quicktools, "\u2796",   "decreaseTDP",                      "Segoe UI Symbol",      20, true,   false,  null,                   string.Empty,           false,  false) },
            { 13, new InputsHotkey(InputsHotkeyType.Quicktools, "\uE769",   "suspendResumeTask",                "Segoe Fluent Icons",   20, false,  true,   null,                   string.Empty,           false,  false) },

            // Microsoft Windows hotkeys
            { 20, new InputsHotkey(InputsHotkeyType.Windows,    "\uE765",   "shortcutKeyboard",                 "Segoe Fluent Icons",   20, false,  true,   null,                   string.Empty,           true,   false) },
            { 21, new InputsHotkey(InputsHotkeyType.Windows,    "\uE138",   "shortcutDesktop",                  "Segoe UI Symbol",      20, false,  true,   null,                   string.Empty,           true,   false) },
            { 22, new InputsHotkey(InputsHotkeyType.Windows,    "ESC",      "shortcutESC",                      "Segoe UI",             12, false,  true,   null,                   string.Empty,           false,  false) },
            { 23, new InputsHotkey(InputsHotkeyType.Windows,    "\uEE49",   "shortcutExpand",                   "Segoe Fluent Icons",   20, false,  true,   null,                   string.Empty,           false,  false) },
            { 24, new InputsHotkey(InputsHotkeyType.Windows,    "\uE7C4",   "shortcutTaskview",                 "Segoe MDL2 Assets",    20, false,  true,   null,                   string.Empty,           false,  false) },
            { 25, new InputsHotkey(InputsHotkeyType.Windows,    "\uE71D",   "shortcutTaskManager",              "Segoe Fluent Icons",   20, false,  true,   null,                   string.Empty,           false,  false) },
            { 26, new InputsHotkey(InputsHotkeyType.Windows,    "\uE8BB",   "shortcutKillApp",                  "Segoe Fluent Icons",   20, false,  true,   null,                   string.Empty,           false,  false) },
            { 27, new InputsHotkey(InputsHotkeyType.Windows,    "\uE7E7",   "shortcutControlCenter",            "Segoe Fluent Icons",   20, false,  true,   null,                   string.Empty,           true,   false) },

            // Handheld Companion hotkeys
            { 30, new InputsHotkey(InputsHotkeyType.HC,         "\uE7C4",   "shortcutMainwindow",               "Segoe Fluent Icons",   20, false,  true,   null,                   string.Empty,           true,   false) },
            { 31, new InputsHotkey(InputsHotkeyType.HC,         "\uE961",   "DesktopLayoutEnabled",             "Segoe Fluent Icons",   20, false,  true,   null,                   string.Empty,           true,   true) },

            // Device specific hotkeys
            { 40, new InputsHotkey(InputsHotkeyType.Device,     "\uE9CA",   "QuietModeToggled",                 "Segoe Fluent Icons",   20, false,  true,   null,                   "QuietModeDisclosure",  false,  true) },
            { 41, new InputsHotkey(InputsHotkeyType.Device,     "\uE706",   "increaseBrightness",               "Segoe Fluent Icons",   20, true,   false,  null,                   "HasBrightnessSupport", false,  false) },
            { 42, new InputsHotkey(InputsHotkeyType.Device,     "\uEC8A",   "decreaseBrightness",               "Segoe Fluent Icons",   20, true,   false,  null,                   "HasBrightnessSupport", false,  false) },
            { 43, new InputsHotkey(InputsHotkeyType.Device,     "\uE995",   "increaseVolume",                   "Segoe Fluent Icons",   20, true,   false,  null,                   "HasVolumeSupport",     false,  false) },
            { 44, new InputsHotkey(InputsHotkeyType.Device,     "\uE993",   "decreaseVolume",                   "Segoe Fluent Icons",   20, true,   false,  null,                   "HasVolumeSupport",     false,  false) },

            // User customizable hotkeys
            { 50, new InputsHotkey(InputsHotkeyType.Custom,     "\u2780",   "shortcutCustom0",                  "Segoe UI Symbol",      20, false,  true,   null,                   string.Empty,           false,  false) },
            { 51, new InputsHotkey(InputsHotkeyType.Custom,     "\u2781",   "shortcutCustom1",                  "Segoe UI Symbol",      20, false,  true,   null,                   string.Empty,           false,  false) },
            { 52, new InputsHotkey(InputsHotkeyType.Custom,     "\u2782",   "shortcutCustom2",                  "Segoe UI Symbol",      20, false,  true,   null,                   string.Empty,           false,  false) },
            { 53, new InputsHotkey(InputsHotkeyType.Custom,     "\u2783",   "shortcutCustom3",                  "Segoe UI Symbol",      20, false,  true,   null,                   string.Empty,           false,  false) },
            { 54, new InputsHotkey(InputsHotkeyType.Custom,     "\u2784",   "shortcutCustom4",                  "Segoe UI Symbol",      20, false,  true,   null,                   string.Empty,           false,  false) },
            { 55, new InputsHotkey(InputsHotkeyType.Custom,     "\u2785",   "shortcutCustom5",                  "Segoe UI Symbol",      20, false,  true,   null,                   string.Empty,           false,  false) },
            { 56, new InputsHotkey(InputsHotkeyType.Custom,     "\u2786",   "shortcutCustom6",                  "Segoe UI Symbol",      20, false,  true,   null,                   string.Empty,           false,  false) },
            { 57, new InputsHotkey(InputsHotkeyType.Custom,     "\u2787",   "shortcutCustom7",                  "Segoe UI Symbol",      20, false,  true,   null,                   string.Empty,           false,  false) },
            { 58, new InputsHotkey(InputsHotkeyType.Custom,     "\u2788",   "shortcutCustom8",                  "Segoe UI Symbol",      20, false,  true,   null,                   string.Empty,           false,  false) },
            { 59, new InputsHotkey(InputsHotkeyType.Custom,     "\u2789",   "shortcutCustom9",                  "Segoe UI Symbol",      20, false,  true,   null,                   string.Empty,           false,  false) },

            // Special, UI hotkeys
            { 60, new InputsHotkey(InputsHotkeyType.Embedded,   "\uEDE3",   "shortcutProfilesPage@",            "Segoe Fluent Icons",   20, true,   true,   null,                   string.Empty,           false,  false) },
            { 61, new InputsHotkey(InputsHotkeyType.Embedded,   "\uEDE3",   "shortcutProfilesPage@@",           "Segoe Fluent Icons",   20, true,   true,   null,                   string.Empty,           false,  false) },
            { 62, new InputsHotkey(InputsHotkeyType.Embedded,   "\uEDE3",   "shortcutProfilesSettingsMode0",    "Segoe Fluent Icons",   20, true,   true,   null,                   string.Empty,           false,  false) },
        };

        public string Glyph { get; set; }
        public string Listener { get; set; }
        public string Description { get; set; }
        public FontFamily fontFamily { get; set; }
        public double fontSize { get; set; }
        public InputsHotkeyType hotkeyType { get; set; }
        public bool OnKeyDown { get; set; }
        public bool OnKeyUp { get; set; }
        public Type DeviceType { get; set; }
        public string Settings { get; set; }
        public bool DefaultPinned { get; set; }
        public bool IsToggle { get; set; }

        public InputsHotkey(InputsHotkeyType hotkeyType, string glyph, string listener, string fontFamily, double fontSize, bool onKeyDown, bool onKeyUp, Type deviceType = null, string settings = "", bool defaultPinned = false, bool isToggle = false)
        {
            this.hotkeyType = hotkeyType;
            this.Glyph = glyph;
            this.Listener = listener;
            this.fontFamily = new FontFamily(fontFamily);
            this.fontSize = fontSize;
            this.OnKeyDown = onKeyDown;
            this.OnKeyUp = onKeyUp;

            this.DeviceType = deviceType;
            this.Settings = settings;
            this.DefaultPinned = defaultPinned;
            this.IsToggle = isToggle;
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
