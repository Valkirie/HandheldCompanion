namespace HandheldCompanion.Devices;

public class OneXPlayer2Pro : OneXPlayer2
{
    public OneXPlayer2Pro()
    {
        // device specific settings
        ProductIllustration = "device_onexplayer_2";
        ProductModel = "ONEXPLAYER 2 7840U";

        // https://www.amd.com/en/products/apu/amd-ryzen-7-7840u
        nTDP = new double[] { 15, 15, 20 };
        cTDP = new double[] { 4, 30 };
        GfxClock = new double[] { 100, 2700 };
        CpuClock = 5100;
    }
}