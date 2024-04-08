using System.Collections.Generic;
using System.Numerics;

namespace HandheldCompanion.Devices;

public class GPDWinMax2AMD : GPDWinMax2
{
    public GPDWinMax2AMD()
    {
        // https://www.amd.com/fr/products/apu/amd-ryzen-7-6800u
        nTDP = new double[] { 15, 15, 28 };
        cTDP = new double[] { 15, 28 };
        GfxClock = new double[] { 100, 2200 };
        CpuClock = 4700;

        GyrometerAxis = new Vector3(1.0f, -1.0f, -1.0f);
        GyrometerAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'Y' },
            { 'Y', 'Z' },
            { 'Z', 'X' }
        };

        AccelerometerAxis = new Vector3(-1.0f, 1.0f, 1.0f);
        AccelerometerAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'X' },
            { 'Y', 'Z' },
            { 'Z', 'Y' }
        };
    }
}