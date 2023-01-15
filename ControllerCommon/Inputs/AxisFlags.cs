using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCommon.Inputs
{
    public enum AxisFlags : byte
    {
        None = 0,

        LeftThumbX = 1,
        RightThumbX = 2,
        LeftThumbY = 3,
        RightThumbY = 4,

        L2 = 5,
        R2 = 6,

        // Steam Deck
        LeftPadX = 7,
        LeftPadY = 8,
        RightPadX = 9,
        RightPadY = 10,
    }
}
