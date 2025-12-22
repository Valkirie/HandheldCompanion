namespace HandheldCompanion.Devices;

public class GPDWin4_2023_7640U : GPDWin4_2023
{
    public GPDWin4_2023_7640U()
    {
        // https://www.amd.com/en/products/apu/amd-ryzen-5-7640u
        GfxClock = new double[] { 200, 2600 };
        CpuClock = 4900;
    }
}
