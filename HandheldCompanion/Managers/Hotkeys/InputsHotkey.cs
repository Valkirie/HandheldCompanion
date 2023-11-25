using HandheldCompanion.Properties;
using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace HandheldCompanion.Managers;

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
        Embedded = 6
    }

    public static SortedDictionary<ushort, InputsHotkey> InputsHotkeys = new()
    {
        // Overlay hotkeys
        {
            01,
            new InputsHotkey(InputsHotkeyType.Overlay, "\uEDE3", "overlayGamepad", "Segoe Fluent Icons", 20, false,
                true, null, string.Empty)
        },
        {
            02,
            new InputsHotkey(InputsHotkeyType.Overlay, "\uEDA4", "overlayTrackpads", "Segoe Fluent Icons", 20, false,
                true, null, string.Empty)
        },

        // Quicktools hotkeys
        {
            10,
            new InputsHotkey(InputsHotkeyType.Quicktools, "\uEC7A", "quickTools", "Segoe Fluent Icons", 20, false, true,
                null, string.Empty)
        },
        {
            11,
            new InputsHotkey(InputsHotkeyType.Quicktools, "\u2795", "increaseTDP", "Segoe UI Symbol", 20, true, false,
                null, string.Empty)
        },
        {
            12,
            new InputsHotkey(InputsHotkeyType.Quicktools, "\u2796", "decreaseTDP", "Segoe UI Symbol", 20, true, false,
                null, string.Empty)
        },
        {
            13,
            new InputsHotkey(InputsHotkeyType.Quicktools, "\uE769", "suspendResumeTask", "Segoe Fluent Icons", 20,
                false, true, null, string.Empty)
        },

        // Microsoft Windows hotkeys
        {
            20,
            new InputsHotkey(InputsHotkeyType.Windows, "\uE765", "shortcutKeyboard", "Segoe Fluent Icons", 20, false,
                true, null, string.Empty, true)
        },
        {
            21,
            new InputsHotkey(InputsHotkeyType.Windows, "\uE138", "shortcutDesktop", "Segoe UI Symbol", 20, false, true,
                null, string.Empty, true)
        },
        {
            22,
            new InputsHotkey(InputsHotkeyType.Windows, "ESC", "shortcutESC", "Segoe UI", 12, false, true, null,
                string.Empty)
        },
        {
            23,
            new InputsHotkey(InputsHotkeyType.Windows, "\uEE49", "shortcutExpand", "Segoe Fluent Icons", 20, false,
                true, null, string.Empty)
        },
        {
            24,
            new InputsHotkey(InputsHotkeyType.Windows, "\uE7C4", "shortcutTaskview", "Segoe MDL2 Assets", 20, false,
                true, null, string.Empty)
        },
        {
            25,
            new InputsHotkey(InputsHotkeyType.Windows, "\uE71D", "shortcutTaskManager", "Segoe Fluent Icons", 20, false,
                true, null, string.Empty)
        },
        {
            26,
            new InputsHotkey(InputsHotkeyType.Windows, "\uE8BB", "shortcutKillApp", "Segoe Fluent Icons", 20, false,
                true, null, string.Empty)
        },
        {
            27,
            new InputsHotkey(InputsHotkeyType.Windows, "\uE7E7", "shortcutControlCenter", "Segoe Fluent Icons", 20,
                false, true, null, string.Empty, true)
        },
        {
            28,
            new InputsHotkey(InputsHotkeyType.Windows, "\uF7ED", "shortcutPrintScreen", "Segoe Fluent Icons", 20, false,
                true, null, string.Empty)
        },

        // Handheld Companion hotkeys
        {
            30,
            new InputsHotkey(InputsHotkeyType.HC, "\uE7C4", "shortcutMainwindow", "Segoe Fluent Icons", 20, false, true,
                null, string.Empty, true)
        },
        {
            31,
            new InputsHotkey(InputsHotkeyType.HC, "\uE961", "DesktopLayoutEnabled", "Segoe Fluent Icons", 20, false,
                true, null, string.Empty, true, true)
        },
        {
            32,
            new InputsHotkey(InputsHotkeyType.HC, "\u243C", "shortcutChangeHIDMode", "PromptFont", 20, false,
                true, null, string.Empty, true, false)
        },

        // Device specific hotkeys
        {
            40,
            new InputsHotkey(InputsHotkeyType.Device, "\uE9CA", "QuietModeToggled", "Segoe Fluent Icons", 20, false,
                true, null, "QuietModeDisclosure", false, true)
        },
        {
            41,
            new InputsHotkey(InputsHotkeyType.Device, "\uE706", "increaseBrightness", "Segoe Fluent Icons", 20, true,
                false, null, "HasBrightnessSupport")
        },
        {
            42,
            new InputsHotkey(InputsHotkeyType.Device, "\uEC8A", "decreaseBrightness", "Segoe Fluent Icons", 20, true,
                false, null, "HasBrightnessSupport")
        },
        {
            43,
            new InputsHotkey(InputsHotkeyType.Device, "\uE995", "increaseVolume", "Segoe Fluent Icons", 20, true, false,
                null, "HasVolumeSupport")
        },
        {
            44,
            new InputsHotkey(InputsHotkeyType.Device, "\uE993", "decreaseVolume", "Segoe Fluent Icons", 20, true, false,
                null, "HasVolumeSupport")
        },

        // User customizable hotkeys
        {
            50,
            new InputsHotkey(InputsHotkeyType.Custom, "\u2780", "shortcutCustom0", "Segoe UI Symbol", 20, false, true,
                null, string.Empty)
        },
        {
            51,
            new InputsHotkey(InputsHotkeyType.Custom, "\u2781", "shortcutCustom1", "Segoe UI Symbol", 20, false, true,
                null, string.Empty)
        },
        {
            52,
            new InputsHotkey(InputsHotkeyType.Custom, "\u2782", "shortcutCustom2", "Segoe UI Symbol", 20, false, true,
                null, string.Empty)
        },
        {
            53,
            new InputsHotkey(InputsHotkeyType.Custom, "\u2783", "shortcutCustom3", "Segoe UI Symbol", 20, false, true,
                null, string.Empty)
        },
        {
            54,
            new InputsHotkey(InputsHotkeyType.Custom, "\u2784", "shortcutCustom4", "Segoe UI Symbol", 20, false, true,
                null, string.Empty)
        },
        {
            55,
            new InputsHotkey(InputsHotkeyType.Custom, "\u2785", "shortcutCustom5", "Segoe UI Symbol", 20, false, true,
                null, string.Empty)
        },
        {
            56,
            new InputsHotkey(InputsHotkeyType.Custom, "\u2786", "shortcutCustom6", "Segoe UI Symbol", 20, false, true,
                null, string.Empty)
        },
        {
            57,
            new InputsHotkey(InputsHotkeyType.Custom, "\u2787", "shortcutCustom7", "Segoe UI Symbol", 20, false, true,
                null, string.Empty)
        },
        {
            58,
            new InputsHotkey(InputsHotkeyType.Custom, "\u2788", "shortcutCustom8", "Segoe UI Symbol", 20, false, true,
                null, string.Empty)
        },
        {
            59,
            new InputsHotkey(InputsHotkeyType.Custom, "\u2789", "shortcutCustom9", "Segoe UI Symbol", 20, false, true,
                null, string.Empty)
        },

        // Special, UI hotkeys
        {
            60,
            new InputsHotkey(InputsHotkeyType.Embedded, "\uEDE3", "shortcutProfilesPage@", "Segoe Fluent Icons", 20,
                true, true, null, string.Empty)
        },
        {
            61,
            new InputsHotkey(InputsHotkeyType.Embedded, "\uEDE3", "shortcutProfilesPage@@", "Segoe Fluent Icons", 20,
                true, true, null, string.Empty)
        },
        {
            62,
            new InputsHotkey(InputsHotkeyType.Embedded, "\uEDE3", "shortcutProfilesSettingsMode0", "Segoe Fluent Icons",
                20, true, true, null, string.Empty)
        }
    };

    public InputsHotkey(InputsHotkeyType hotkeyType, string glyph, string listener, string fontFamily, double fontSize,
        bool onKeyDown, bool onKeyUp, Type deviceType = null, string settings = "", bool defaultPinned = false,
        bool isToggle = false)
    {
        this.hotkeyType = hotkeyType;
        Glyph = glyph;
        Listener = listener;
        this.fontFamily = new FontFamily(fontFamily);
        this.fontSize = fontSize;
        OnKeyDown = onKeyDown;
        OnKeyUp = onKeyUp;

        DeviceType = deviceType;
        Settings = settings;
        DefaultPinned = defaultPinned;
        IsToggle = isToggle;
    }

    public InputsHotkey()
    {
    }

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

    public string GetName()
    {
        // return localized string if available
        var listener = Listener;

        switch (hotkeyType)
        {
            case InputsHotkeyType.Custom:
                listener = "shortcutCustom";
                break;
        }

        var root = Resources.ResourceManager.GetString($"InputsHotkey_{listener}");

        if (!string.IsNullOrEmpty(root))
            return root;

        return Listener;
    }

    public string GetDescription()
    {
        // return localized string if available
        var listener = Listener;

        switch (hotkeyType)
        {
            case InputsHotkeyType.Custom:
                listener = "shortcutCustom";
                break;
        }

        var root = Resources.ResourceManager.GetString($"InputsHotkey_{listener}Desc");

        if (!string.IsNullOrEmpty(root))
            return root;

        return string.Empty;
    }
}