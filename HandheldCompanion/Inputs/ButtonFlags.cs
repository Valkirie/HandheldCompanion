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

    [Description("Left Thumb Click")] LeftStickClick = 7,
    [Description("Right Thumb Click")] RightStickClick = 8,

    L1 = 9,
    R1 = 10,

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

    [Description("Up")] LeftStickUp = 21,
    [Description("Down")] LeftStickDown = 22,
    [Description("Left")] LeftStickLeft = 23,
    [Description("Right")] LeftStickRight = 24,

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

    Max = 63
}