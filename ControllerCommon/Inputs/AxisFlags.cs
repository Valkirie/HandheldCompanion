using System;

namespace ControllerCommon.Inputs;

[Serializable]
public enum AxisFlags : byte
{
    None = 0,
    LeftThumbX = 1,
    LeftThumbY = 2,
    RightThumbX = 3,
    RightThumbY = 4,
    L2 = 5,
    R2 = 6,

    // Steam Deck
    LeftPadX = 7,
    RightPadX = 8,
    LeftPadY = 9,
    RightPadY = 10,

    Max = 11
}