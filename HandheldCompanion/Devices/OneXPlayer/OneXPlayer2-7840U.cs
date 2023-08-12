namespace HandheldCompanion.Devices;

public class OneXPlayer2_7840U : OneXPlayer2
{
    public OneXPlayer2_7840U()
    {
        // device specific settings
        ProductIllustration = "device_onexplayer_2";
        ProductModel = "ONEXPLAYER 2 7840U";

        // https://www.amd.com/en/products/apu/amd-ryzen-7-7840u
        nTDP = new double[] { 15, 15, 20 };
        cTDP = new double[] { 4, 30 };
        GfxClock = new double[] { 100, 2700 };
    }
}