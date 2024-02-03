namespace HandheldCompanion.Devices;

public class GPDWinMini_7640U : GPDWinMini_7840U
{
    public GPDWinMini_7640U()
    {
        // https://www.amd.com/en/products/apu/amd-ryzen-5-7640u
        GfxClock = new double[] { 200, 2600 };
        CpuClock = 4900;
    }
}
