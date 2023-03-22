using ControllerCommon.Devices;
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
            { 01, new InputsHotkey(InputsHotkeyType.Overlay,    "\uEDE3",   "overlayGamepad",                   "Segoe Fluent Icons",   20, false,  true) },
            { 02, new InputsHotkey(InputsHotkeyType.Overlay,    "\uEDA4",   "overlayTrackpads",                 "Segoe Fluent Icons",   20, false,  true) },

            // Quicktools hotkeys
            { 10, new InputsHotkey(InputsHotkeyType.Quicktools, "\uEC7A",   "quickTools",                       "Segoe Fluent Icons",   20, false,  true) },
            { 11, new InputsHotkey(InputsHotkeyType.Quicktools, "\u2795",   "increaseTDP",                      "Segoe UI Symbol",      20, false,  true) },
            { 12, new InputsHotkey(InputsHotkeyType.Quicktools, "\u2796",   "decreaseTDP",                      "Segoe UI Symbol",      20, false,  true) },
            { 13, new InputsHotkey(InputsHotkeyType.Quicktools, "\uE769",   "suspendResumeTask",                "Segoe Fluent Icons",   20, false,  true) },
            { 14, new InputsHotkey(InputsHotkeyType.Quicktools, "\uE708",   "QuietModeEnabled",                  "Segoe Fluent Icons",   20, false,  true) },

            // Microsoft Windows hotkeys
            { 20, new InputsHotkey(InputsHotkeyType.Windows,    "\uE765",   "shortcutKeyboard",                 "Segoe Fluent Icons",   20, false,  true) },
            { 21, new InputsHotkey(InputsHotkeyType.Windows,    "\uE138",   "shortcutDesktop",                  "Segoe UI Symbol",      20, false,  true) },
            { 22, new InputsHotkey(InputsHotkeyType.Windows,    "ESC",      "shortcutESC",                      "Segoe UI",             12, false,  true) },
            { 23, new InputsHotkey(InputsHotkeyType.Windows,    "\uEE49",   "shortcutExpand",                   "Segoe Fluent Icons",   20, false,  true) },
            { 24, new InputsHotkey(InputsHotkeyType.Windows,    "\uE7C4",   "shortcutTaskview",                 "Segoe MDL2 Assets",    20, false,  true) },
            { 25, new InputsHotkey(InputsHotkeyType.Windows,    "\uE71D",   "shortcutTaskManager",              "Segoe Fluent Icons",   20, false,  true) },
            { 26, new InputsHotkey(InputsHotkeyType.Windows,    "\uE8BB",   "shortcutKillApp",                  "Segoe Fluent Icons",   20, false,  true) },
            { 27, new InputsHotkey(InputsHotkeyType.Windows,    "\uE7E7",   "shortcutControlCenter",            "Segoe Fluent Icons",   20, false,  true) },

            // Handheld Companion hotkeys
            { 30, new InputsHotkey(InputsHotkeyType.HC,         "\uE7C4",   "shortcutMainwindow",               "Segoe Fluent Icons",   20, false,  true) },
            { 31, new InputsHotkey(InputsHotkeyType.HC,         "\uE961",   "shortcutDesktopLayout",            "Segoe Fluent Icons",   20, false,  true) },

            // Device specific hotkeys
            // { 40, new InputsHotkey(InputsHotkeyType.Device,     "\uE2E8",   "shortcutGuide",                    "Segoe UI Symbol",      20, false,  true) },
            { 41, new InputsHotkey(InputsHotkeyType.Device,     "\uEFA5",   "SteamDeckLizardMouse",             "Segoe MDL2 Assets",    20, false,  true,   typeof(SteamDeck)) },
            { 42, new InputsHotkey(InputsHotkeyType.Device,     "\uF093",   "SteamDeckLizardButtons",           "Segoe MDL2 Assets",    20, false,  true,   typeof(SteamDeck)) },

            // User customizable hotkeys
            { 50, new InputsHotkey(InputsHotkeyType.Custom,     "\u2780",   "shortcutCustom0",                  "Segoe UI Symbol",      20, false,  true) },
            { 51, new InputsHotkey(InputsHotkeyType.Custom,     "\u2781",   "shortcutCustom1",                  "Segoe UI Symbol",      20, false,  true) },
            { 52, new InputsHotkey(InputsHotkeyType.Custom,     "\u2782",   "shortcutCustom2",                  "Segoe UI Symbol",      20, false,  true) },
            { 53, new InputsHotkey(InputsHotkeyType.Custom,     "\u2783",   "shortcutCustom3",                  "Segoe UI Symbol",      20, false,  true) },
            { 54, new InputsHotkey(InputsHotkeyType.Custom,     "\u2784",   "shortcutCustom4",                  "Segoe UI Symbol",      20, false,  true) },
            { 55, new InputsHotkey(InputsHotkeyType.Custom,     "\u2785",   "shortcutCustom5",                  "Segoe UI Symbol",      20, false,  true) },
            { 56, new InputsHotkey(InputsHotkeyType.Custom,     "\u2786",   "shortcutCustom6",                  "Segoe UI Symbol",      20, false,  true) },
            { 57, new InputsHotkey(InputsHotkeyType.Custom,     "\u2787",   "shortcutCustom7",                  "Segoe UI Symbol",      20, false,  true) },
            { 58, new InputsHotkey(InputsHotkeyType.Custom,     "\u2788",   "shortcutCustom8",                  "Segoe UI Symbol",      20, false,  true) },
            { 59, new InputsHotkey(InputsHotkeyType.Custom,     "\u2789",   "shortcutCustom9",                  "Segoe UI Symbol",      20, false,  true) },

            // Special, UI hotkeys
            { 60, new InputsHotkey(InputsHotkeyType.Embedded,         "\uEDE3",   "shortcutProfilesPage@",            "Segoe Fluent Icons",   20, true,   true) },
            { 61, new InputsHotkey(InputsHotkeyType.Embedded,         "\uEDE3",   "shortcutProfilesPage@@",           "Segoe Fluent Icons",   20, true,   true) },
            { 62, new InputsHotkey(InputsHotkeyType.Embedded,         "\uEDE3",   "shortcutProfilesSettingsMode0",    "Segoe Fluent Icons",   20, true,   true) },
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

        public InputsHotkey(InputsHotkeyType hotkeyType, string glyph, string listener, string fontFamily, double fontSize, bool onKeyDown, bool onKeyUp, Type deviceType) : this(hotkeyType, glyph, listener, fontFamily, fontSize, onKeyDown, onKeyUp)
        {
            this.DeviceType = deviceType;
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
