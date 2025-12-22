using System.Collections.Generic;
using System.Numerics;

namespace HandheldCompanion.Devices;

public class GPDWinMini_8840U : GPDWinMini
{
    public GPDWinMini_8840U()
    {
        // https://www.amd.com/en/products/processors/laptop/ryzen/8000-series/amd-ryzen-7-8840u.html

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
