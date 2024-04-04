namespace HandheldCompanion.Devices;

public class AYANEOAIRPlusAMDMendocino : AYANEOAIRPlus
{
    public AYANEOAIRPlusAMDMendocino()
    {
        // https://www.amd.com/en/products/apu/amd-ryzen-3-7320u
        this.nTDP = new double[] { 12, 12, 12 };
        this.cTDP = new double[] { 5, 15 };
        this.GfxClock = new double[] { 100, 1900 };
        this.CpuClock = 4100;

        this.ECDetails.AddressFanDuty = 0x02;
    }
}