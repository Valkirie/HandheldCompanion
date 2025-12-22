using System.Collections.Generic;
using System.Numerics;

namespace HandheldCompanion.Devices;

public class GPDWinMini_HX370 : GPDWinMini_8840U
{
    public GPDWinMini_HX370()
    {
        // https://www.amd.com/en/products/processors/laptop/ryzen/300-series/amd-ryzen-ai-9-365.html
        nTDP = new double[] { 15, 15, 28 };
        cTDP = new double[] { 3, 28 };
        GfxClock = new double[] { 100, 2900 };
        CpuClock = 5100;

        this.GyrometerAxis = new Vector3(-1.0f, -1.0f, 1.0f);
        this.GyrometerAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'Y' }, // out X from in Y (sign -)
            { 'Y', 'Z' }, // out Y from in Z (sign -)
            { 'Z', 'X' }  // out Z from in X (sign +)
        };

        this.AccelerometerAxis = new Vector3(-1.0f, 1.0f, 1.0f);
        this.AccelerometerAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'X' }, // out X from in X (sign -)
            { 'Y', 'Z' }, // out Y from in Z (sign +)
            { 'Z', 'Y' }  // out Z from in Y (sign +)
        };
    }
}
