namespace HandheldCompanion.Devices;

public class AYANEONEXTLite4500U : AYANEONEXTLite
{
    public AYANEONEXTLite()
    {
        // https://www.amd.com/en/support/apu/amd-ryzen-processors/amd-ryzen-5-mobile-processors-radeon-graphics/amd-ryzen-5-4500u
        this.GfxClock = new double[] { 100, 1500 };
        this.CpuClock = 4000 ;
    }
}