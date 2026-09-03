namespace HandheldCompanion.Devices;

public class AYANEOAIRPlusNxx : AYANEOAIRPlus
{
    public AYANEOAIRPlusNxx()
    {
        ProductModel = "AYANEO AIR Plus Nxx";

        // Intel N-series. AYASpace IsAB05Eii + IntelTdpPL1Write + FanReadIntel
        nTDP = new double[] { 6, 6, 15 };
        cTDP = new double[] { 5, 15 };
        GfxClock = new double[] { 100, 750 };
        CpuClock = 3800;

        ECDetails.AddressFanDuty = 0x02;
    }
}
