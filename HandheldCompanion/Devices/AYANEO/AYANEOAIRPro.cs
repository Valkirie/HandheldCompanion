namespace HandheldCompanion.Devices;

public class AYANEOAIRPro : AYANEOAIR
{
    public AYANEOAIRPro()
    {
        // https://www.amd.com/en/products/apu/amd-ryzen-7-5825u
        this.nTDP = new double[] { 12, 12, 15 };
        this.cTDP = new double[] { 3, 18 };
        this.GfxClock = new double[] { 100, 2000 };
        this.CpuClock = 4500;
    }
}