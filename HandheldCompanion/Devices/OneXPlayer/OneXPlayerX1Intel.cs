﻿namespace HandheldCompanion.Devices;

public class OneXPlayerX1Intel : OneXPlayerX1
{
    public OneXPlayerX1Intel()
    {
        // https://www.intel.com/content/www/us/en/products/sku/236847/intel-core-ultra-7-processor-155h-24m-cache-up-to-4-80-ghz/specifications.html
        // follow the values presented in OneXConsole
        nTDP = new double[] { 15, 15, 35 };
        cTDP = new double[] { 6, 35 };
        GfxClock = new double[] { 100, 2250 };
        CpuClock = 4800;
    }
}
