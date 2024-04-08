namespace HandheldCompanion.Devices;

public class AYANEO2S : AYANEO2
{
    public AYANEO2S()
    {
        // https://www.amd.com/en/products/apu/amd-ryzen-7-7840U
        this.nTDP = new double[] { 15, 15, 20 };
        this.cTDP = new double[] { 3, 30 };
        this.GfxClock = new double[] { 100, 2700 };
        this.CpuClock = 5100;
    }
}
