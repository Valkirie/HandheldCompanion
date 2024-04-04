namespace HandheldCompanion.Devices;

public class AYANEO2021Pro : AYANEO2021
{
    public AYANEO2021Pro()
    {
        // https://www.amd.com/en/support/apu/amd-ryzen-processors/amd-ryzen-7-mobile-processors-radeon-graphics/amd-ryzen-7-4800u
        this.nTDP = new double[] { 15, 15, 20 };
        this.cTDP = new double[] { 3, 25 };
        this.GfxClock = new double[] { 100, 1750 };
        this.CpuClock = 4200;
    }
}