namespace HandheldCompanion.Devices;

public class AOKZOEA1Pro : AOKZOEA1
{
    public AOKZOEA1Pro()
    {
        // device specific settings
        ProductIllustration = "device_aokzoe_a1";
        ProductModel = "AOKZOEA1Pro";

        // https://www.amd.com/en/products/apu/amd-ryzen-7-7840u
        nTDP = new double[] { 15, 15, 20 };
        cTDP = new double[] { 4, 28 };
        GfxClock = new double[] { 100, 2700 };
        CpuClock = 5100;
    }
}