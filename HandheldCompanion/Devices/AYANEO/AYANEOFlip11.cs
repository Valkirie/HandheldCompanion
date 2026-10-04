using HandheldCompanion.Devices.AYANEO;

namespace HandheldCompanion.Devices;

public class AYANEOFlip11 : AYANEOFlip1SKB
{
    public AYANEOFlip11()
    {
        ProductIllustration = "device_aya_flip_kb";
        ProductModel = "AYANEO FLIP 11";

        rgbConfirmation = false;

        cTDP = new double[] { 5, 54 };
    }
}
