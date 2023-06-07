using System.Collections.Generic;
using System.Numerics;

namespace ControllerCommon.Devices;

public class GPDWinMax2AMD : GPDWinMax2
{
    public GPDWinMax2AMD()
    {
        // https://www.amd.com/fr/products/apu/amd-ryzen-7-6800u
        nTDP = new double[] { 15, 15, 28 };
        cTDP = new double[] { 15, 28 };
        GfxClock = new double[] { 100, 2200 };

        AngularVelocityAxis = new Vector3(1.0f, 1.0f, -1.0f);
        AngularVelocityAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'Y' },
            { 'Y', 'Z' },
            { 'Z', 'X' }
        };

        AccelerationAxis = new Vector3(1.0f, -1.0f, 1.0f);
        AccelerationAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'X' },
            { 'Y', 'Z' },
            { 'Z', 'Y' }
        };
    }
}