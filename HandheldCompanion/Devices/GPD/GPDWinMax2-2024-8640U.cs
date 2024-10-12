using System.Collections.Generic;
using System.Numerics;

namespace HandheldCompanion.Devices;

public class GPDWinMax2_2024_8640U : GPDWinMax2
{
    public GPDWinMax2_2024_8640U()
    {
        // https://www.amd.com/en/products/processors/laptop/ryzen/8000-series/amd-ryzen-5-8640u.html
        nTDP = new double[] { 15, 3, 28 }; //default, low, high
        cTDP = new double[] { 3, 28 };
        GfxClock = new double[] { 100, 2600 };
        CpuClock = 4900;

        GyrometerAxis = new Vector3(1.0f, -1.0f, -1.0f);
        GyrometerAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'Y' },
            { 'Y', 'Z' },
            { 'Z', 'X' }
        };

        AccelerometerAxis = new Vector3(-1.0f, -1.0f, 1.0f);
        AccelerometerAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'X' },
            { 'Y', 'Z' },
            { 'Z', 'Y' }
        };
    }
}