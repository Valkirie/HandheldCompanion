using System.Collections.Generic;
using System.Numerics;

namespace HandheldCompanion.Devices;

public class GPDWinMini_8840U : GPDWinMini
{
    public GPDWinMini_8840U()
    {
        // https://www.amd.com/en/products/processors/laptop/ryzen/8000-series/amd-ryzen-7-8840u.html

        GyroMatrix = new()
        {
            Axis = new Vector3(1.0f, -1.0f, 1.0f),
            AxisSwap = new SortedDictionary<char, char>
            {
                { 'X', 'X' },
                { 'Y', 'Z' },
                { 'Z', 'Y' }
            }
        };

        AcceleroMatrix = new()
        {
            Axis = new Vector3(-1.0f, 1.0f, 1.0f),
            AxisSwap = new SortedDictionary<char, char>
            {
                { 'X', 'X' },
                { 'Y', 'Z' },
                { 'Z', 'Y' }
            }
        };
    }
}
