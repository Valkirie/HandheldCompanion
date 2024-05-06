namespace HandheldCompanion.Devices;

public class GPDWin4_2024_8640U : GPDWin4_2024_8840U
{
    public GPDWin4_2024_8640U()
    {
        // https://www.amd.com/en/support/apu/amd-ryzen-processors/amd-ryzen-5-processors-radeon-graphics/amd-ryzen-5-8640u
        GfxClock = new double[] { 200, 2600 };
        CpuClock = 4900;
    }
}
