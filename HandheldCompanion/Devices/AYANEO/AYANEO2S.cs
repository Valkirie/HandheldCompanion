namespace HandheldCompanion.Devices;

public class AYANEO2S : AYANEO2
{
    public AYANEO2S()
    {
        // https://www.amd.com/en/products/apu/amd-ryzen-7-7840U
        nTDP = new double[] { 15, 15, 20 };
        cTDP = new double[] { 3, 30 };
        GfxClock = new double[] { 100, 2700 };
        CpuClock = 5100;
    }
}
