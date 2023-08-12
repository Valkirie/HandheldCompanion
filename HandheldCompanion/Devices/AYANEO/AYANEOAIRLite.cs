namespace HandheldCompanion.Devices;

public class AYANEOAIRLite : AYANEOAIR
{
    public AYANEOAIRLite()
    {
        // https://www.amd.com/en/products/apu/amd-ryzen-5-5560u
        nTDP = new double[] { 8, 8, 12 };
        cTDP = new double[] { 3, 12 };
        GfxClock = new double[] { 100, 1600 };
    }
}