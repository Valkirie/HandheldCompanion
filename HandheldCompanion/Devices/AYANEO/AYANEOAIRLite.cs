namespace HandheldCompanion.Devices;

public class AYANEOAIRLite : AYANEOAIR
{
    public AYANEOAIRLite()
    {
        // https://www.amd.com/en/products/apu/amd-ryzen-5-5560u
        this.nTDP = new double[] { 8, 8, 12 };
        this.cTDP = new double[] { 3, 12 };
        this.GfxClock = new double[] { 100, 1600 };
        this.CpuClock = 4000;
    }
}