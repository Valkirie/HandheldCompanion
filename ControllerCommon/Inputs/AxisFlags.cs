using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCommon.Inputs
{
    [Serializable]
    public enum AxisFlags : byte
    {
        None = 0,
        LeftThumbX = 1, LeftThumbY = 2,
        RightThumbX = 3, RightThumbY = 4,
        L2 = 5, R2 = 6,

        // Steam Deck
        LeftPadX = 7, RightPadX = 8,
        LeftPadY = 9, RightPadY = 10,
    }
}
