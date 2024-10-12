using System.Collections.Generic;
using System.Numerics;

namespace HandheldCompanion.Devices;

public class GPDWinMax2_2022_6800U : GPDWinMax2
{
    public GPDWinMax2_2022_6800U()
    {
        // https://www.amd.com/fr/products/apu/amd-ryzen-7-6800u
        nTDP = new double[] { 15, 15, 28 };
        cTDP = new double[] { 3, 28 };
        GfxClock = new double[] { 100, 2200 };
        CpuClock = 4700;

        GyrometerAxis = new Vector3(1.0f, -1.0f, - 1.0f);
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