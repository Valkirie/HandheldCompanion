using System.Collections.Generic;
using System.Numerics;

namespace HandheldCompanion.Devices;

public class OneXPlayerMiniIntel : OneXPlayerMini
{
    public OneXPlayerMiniIntel()
    {
        // https://ark.intel.com/content/www/us/en/ark/products/226254/intel-core-i71260p-processor-18m-cache-up-to-4-70-ghz.html
        nTDP = new double[] { 28, 28, 64 };
        cTDP = new double[] { 20, 64 };
        GfxClock = new double[] { 100, 1400 };

        AngularVelocityAxis = new Vector3(1.0f, 1.0f, -1.0f);
        AngularVelocityAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'Y' },
            { 'Y', 'Z' },
            { 'Z', 'X' }
        };

        AccelerationAxis = new Vector3(-1.0f, 1.0f, -1.0f);
    }
}