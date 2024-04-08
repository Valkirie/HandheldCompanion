namespace HandheldCompanion.Devices;

public class AYANEOAIRPlusAMD : AYANEOAIRPlus
{
    public AYANEOAIRPlusAMD()
    {
        // https://www.amd.com/en/products/apu/amd-ryzen-7-6800u
        this.nTDP = new double[] { 15, 15, 20 };
        this.cTDP = new double[] { 3, 33 };
        this.GfxClock = new double[] { 100, 2200 };
        this.CpuClock = 4700;
    }
}