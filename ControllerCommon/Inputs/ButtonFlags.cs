using System;
using System.ComponentModel;

namespace ControllerCommon.Inputs
{
    [Serializable]
    public enum ButtonFlags : byte
    {
        None = 0,

        [Description("DPad Up")]
        DPadUp = 1,
        [Description("DPad Down")]
        DPadDown = 2,
        [Description("DPad Left")]
        DPadLeft = 3,
        [Description("DPad Right")]
        DPadRight = 4,

        [Description("Start")]
        Start = 5,
        [Description("Back")]
        Back = 6,

        [Description("Left Thumb Click")]
        LeftThumb = 7,
        [Description("Right Thumb Click")]
        RightThumb = 8,

        L1 = 9, R1 = 10,

        [Description("Soft pull")]
        L2 = 11,
        [Description("Soft pull")]
        R2 = 12,
        [Description("Full pull")]
        L3 = 44,
        [Description("Full pull")]
        R3 = 45,

        B1 = 13, B2 = 14, B3 = 15, B4 = 16, B5 = 17, B6 = 18, B7 = 19, B8 = 20,

        [Description("Up")]
        LeftThumbUp = 21,
        [Description("Down")]
        LeftThumbDown = 22,
        [Description("Left")]
        LeftThumbLeft = 23,
        [Description("Right")]
        LeftThumbRight = 24,

        [Description("Up")]
        RightThumbUp = 25,
        [Description("Down")]
        RightThumbDown = 26,
        [Description("Left")]
        RightThumbLeft = 27,
        [Description("Right")]
        RightThumbRight = 28,

        Special = 29,

        OEM1 = 30, OEM2 = 31, OEM3 = 32, OEM4 = 33, OEM5 = 34,
        OEM6 = 35, OEM7 = 36, OEM8 = 37, OEM9 = 38, OEM10 = 39,

        // Steam Deck
        [Description("Touch")]
        LeftPadTouch = 40,
        [Description("Touch")]
        RightPadTouch = 41,

        [Description("Click")]
        LeftPadClick = 42,
        [Description("Click")]
        RightPadClick = 43,

        L4 = 46, R4 = 47,
        L5 = 48, R5 = 49,

        [Description("Touch")]
        LeftThumbTouch = 50,
        [Description("Touch")]
        RightThumbTouch = 51,

        [Description("Up")]
        LeftPadClickUp = 52,
        [Description("Down")]
        LeftPadClickDown = 53,
        [Description("Left")]
        LeftPadClickLeft = 54,
        [Description("Right")]
        LeftPadClickRight = 55,

        [Description("Up")]
        RightPadClickUp = 56,
        [Description("Down")]
        RightPadClickDown = 57,
        [Description("Left")]
        RightPadClickLeft = 58,
        [Description("Right")]
        RightPadClickRight = 59,
    }
}
