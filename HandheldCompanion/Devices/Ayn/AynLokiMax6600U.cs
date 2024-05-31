namespace HandheldCompanion.Devices;

public class LokiMax6600U : AynLoki
{
    public LokiMax6600U()
    {
        // https://www.amd.com/en/products/apu/amd-ryzen-5-6600u
        nTDP = new double[] { 15, 15, 20 };
        cTDP = new double[] { 5, 28 };
        GfxClock = new double[] { 100, 1900 };
        CpuClock = 4500;
    }
}