namespace HandheldCompanion.Devices;

public class AYANEOAIRPro : AYANEOAIR
{
    public AYANEOAIRPro()
    {
        // https://www.amd.com/en/products/apu/amd-ryzen-7-5825u
        nTDP = new double[] { 12, 12, 15 };
        cTDP = new double[] { 3, 18 };
        GfxClock = new double[] { 100, 2000 };
        CpuClock = 4500;
    }
}