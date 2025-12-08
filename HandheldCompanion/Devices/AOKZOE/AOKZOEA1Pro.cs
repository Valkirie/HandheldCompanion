namespace HandheldCompanion.Devices;

public class AOKZOEA1Pro : AOKZOEA1
{
    public AOKZOEA1Pro()
    {
        // device specific settings
        ProductIllustration = "device_aokzoe_a1";
        ProductModel = "AOKZOEA1Pro";

        // https://www.amd.com/fr/products/processors/laptop/ryzen/7000-series/amd-ryzen-7-7840u.html
        nTDP = new double[] { 15, 15, 20 };
        cTDP = new double[] { 4, 28 };
        GfxClock = new double[] { 100, 2700 };
        CpuClock = 5100;

        ECDetails = new ECDetails
        {
            AddressFanControl = 0x44A,
            AddressFanDuty = 0x44B,
            AddressStatusCommandPort = 0x4E, // 78
            AddressDataPort = 0x4F,     // 79
            FanValueMin = 0,
            FanValueMax = 184
        };
    }
}