namespace HandheldCompanion.Devices;

public class AYANEOAIRPlusAMDMendocino : AYANEOAIRPlus
{
    public AYANEOAIRPlusAMDMendocino()
    {
        // https://www.amd.com/en/products/apu/amd-ryzen-3-7320u
        nTDP = new double[] { 12, 12, 12 };
        cTDP = new double[] { 5, 15 };
        GfxClock = new double[] { 100, 1900 };
        CpuClock = 4100;
    }
}