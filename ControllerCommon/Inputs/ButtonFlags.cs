using System;

namespace ControllerCommon.Inputs
{
    [Serializable]
    public enum ButtonFlags : byte
    {
        None = 0,

        DPadUp = 1,
        DPadDown = 2,
        DPadLeft = 3,
        DPadRight = 4,

        Start = 5,
        Back = 6,

        LeftThumb = 7,
        RightThumb = 8,

        L1 = 9,
        R1 = 10,
        L2 = 11,
        R2 = 12,

        B1 = 13,
        B2 = 14,
        B3 = 15,
        B4 = 16,
        B5 = 17,
        B6 = 18,
        B7 = 19,
        B8 = 20,

        LStickUp = 21,
        LStickDown = 22,
        LStickLeft = 23,
        LStickRight = 24,

        RStickUp = 25,
        RStickDown = 26,
        RStickLeft = 27,
        RStickRight = 28,

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

        // Steam Deck
        LPadTouch = 40,
        LPadClick = 41,
        RPadTouch = 42,
        RPadClick = 43,

        L3 = 44,
        R3 = 45,
        L4 = 46,
        R4 = 47,
        L5 = 48,
        R5 = 49
    }

    /*
    [Serializable]
    public class ButtonFlags
    {
        private ButtonFlags _ButtonFlags;

        public ButtonFlags this[ButtonFlags button]
        {
            get
            {
                return _ButtonFlags;
            }

            set
            {
                _ButtonFlags = value;
            }
        }
    }
    */
}
