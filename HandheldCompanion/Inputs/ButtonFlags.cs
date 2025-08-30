using System;
using System.ComponentModel;

namespace HandheldCompanion.Inputs;

[Serializable]
public enum ButtonFlags : byte
{
    None = 0,

    [Description("DPad Up")] DPadUp = 1,
    [Description("DPad Down")] DPadDown = 2,
    [Description("DPad Left")] DPadLeft = 3,
    [Description("DPad Right")] DPadRight = 4,

    [Description("Start")] Start = 5,
    [Description("Back")] Back = 6,

    [Description("Left Thumb")] LeftStickClick = 7,
    [Description("Right Thumb")] RightStickClick = 8,

    L1 = 9,
    R1 = 10,

    // UI only
    [Description("Soft pull")] L2Soft = 11,
    [Description("Soft pull")] R2Soft = 12,
    [Description("Full pull")] L2Full = 44,
    [Description("Full pull")] R2Full = 45,

    B1 = 13,
    B2 = 14,
    B3 = 15,
    B4 = 16,
    B5 = 17,
    B6 = 18,
    B7 = 19,
    B8 = 20,

    // UI only
    [Description("Up")] LeftStickUp = 21,
    [Description("Down")] LeftStickDown = 22,
    [Description("Left")] LeftStickLeft = 23,
    [Description("Right")] LeftStickRight = 24,

    // UI only
    [Description("Up")] RightStickUp = 25,
    [Description("Down")] RightStickDown = 26,
    [Description("Left")] RightStickLeft = 27,
    [Description("Right")] RightStickRight = 28,

    Special = 29,

    OEM1 = 30,
    OEM2 = 31,
    OEM3 = 32,
    OEM4 = 33,
    OEM5 = 34,
    OEM6 = 35,
    OEM7 = 36,
    OEM8 = 37,
    OEM9 = 38,
    OEM10 = 39,

    [Description("Left Pad Touch")] LeftPadTouch = 40,
    [Description("Right Pad Touch")] RightPadTouch = 41,

    [Description("Left Pad Click")] LeftPadClick = 42,
    [Description("Right Pad Click")] RightPadClick = 43,

    L4 = 46,
    R4 = 47,
    L5 = 48,
    R5 = 49,

    [Description("Left Thumb Touch")] LeftStickTouch = 50,
    [Description("Left Thumb Touch")] RightStickTouch = 51,

    [Description("Up")] LeftPadClickUp = 52,
    [Description("Down")] LeftPadClickDown = 53,
    [Description("Left")] LeftPadClickLeft = 54,
    [Description("Right")] LeftPadClickRight = 55,

    [Description("Up")] RightPadClickUp = 56,
    [Description("Down")] RightPadClickDown = 57,
    [Description("Left")] RightPadClickLeft = 58,
    [Description("Right")] RightPadClickRight = 59,

    [Description("Volume Up")] VolumeUp = 60,
    [Description("Volume Down")] VolumeDown = 61,

    Special2 = 62,
    B9 = 63,
    B10 = 64,
    B11 = 65,
    B12 = 66,
    B13 = 67,
    B14 = 68,
    B15 = 69,

    // Reserved for device hotkeys
    HOTKEY_RESERVED0 = 70,
    HOTKEY_RESERVED1 = 71,
    HOTKEY_RESERVED2 = 72,
    HOTKEY_RESERVED3 = 73,
    HOTKEY_RESERVED4 = 74,
    HOTKEY_RESERVED5 = 75,
    HOTKEY_RESERVED6 = 76,
    HOTKEY_RESERVED7 = 77,
    HOTKEY_RESERVED8 = 78,
    HOTKEY_RESERVED9 = 79,

    HOTKEY_USER0 = 80,
    HOTKEY_USER1 = 81,
    HOTKEY_USER2 = 82,
    HOTKEY_USER3 = 83,
    HOTKEY_USER4 = 84,
    HOTKEY_USER5 = 85,
    HOTKEY_USER6 = 86,
    HOTKEY_USER7 = 87,
    HOTKEY_USER8 = 88,
    HOTKEY_USER9 = 89,
    HOTKEY_USER10 = 90,
    HOTKEY_USER11 = 91,
    HOTKEY_USER12 = 92,
    HOTKEY_USER13 = 93,
    HOTKEY_USER14 = 94,
    HOTKEY_USER15 = 95,
    HOTKEY_USER16 = 96,
    HOTKEY_USER17 = 97,
    HOTKEY_USER18 = 98,
    HOTKEY_USER19 = 99,
    HOTKEY_USER20 = 100,
    HOTKEY_USER21 = 101,
    HOTKEY_USER22 = 102,
    HOTKEY_USER23 = 103,
    HOTKEY_USER24 = 104,
    HOTKEY_USER25 = 105,
    HOTKEY_USER26 = 106,
    HOTKEY_USER27 = 107,
    HOTKEY_USER28 = 108,
    HOTKEY_USER29 = 109,
    HOTKEY_USER30 = 110,
    HOTKEY_USER31 = 111,
    HOTKEY_USER32 = 112,
    HOTKEY_USER33 = 113,
    HOTKEY_USER34 = 114,
    HOTKEY_USER35 = 115,
    HOTKEY_USER36 = 116,
    HOTKEY_USER37 = 117,
    HOTKEY_USER38 = 118,
    HOTKEY_USER39 = 119,
    HOTKEY_USER40 = 120,
    HOTKEY_USER41 = 121,
    HOTKEY_USER42 = 122,
    HOTKEY_USER43 = 123,
    HOTKEY_USER44 = 124,
    HOTKEY_USER45 = 125,
    HOTKEY_USER46 = 126,
    HOTKEY_USER47 = 127,
    HOTKEY_USER48 = 128,
    HOTKEY_USER49 = 129,
    HOTKEY_USER50 = 130,
    HOTKEY_USER51 = 131,
    HOTKEY_USER52 = 132,
    HOTKEY_USER53 = 133,
    HOTKEY_USER54 = 134,
    HOTKEY_USER55 = 135,
    HOTKEY_USER56 = 136,
    HOTKEY_USER57 = 137,
    HOTKEY_USER58 = 138,
    HOTKEY_USER59 = 139,

    HOTKEY_GYRO_ACTIVATION = 140,
    HOTKEY_GYRO_ACTIVATION_QP = 141,
    HOTKEY_GYRO_AIMING = 142,

    HOTKEY_END = 150,

    Max = 151
}