namespace HandheldCompanion.Devices;

public class AYANEOFlipKB : AYANEO.AYANEODeviceCEc
{
    public AYANEOFlipKB()
    {
        // device specific settings
        this.ProductIllustration = "device_aya_flip_kb";
        this.ProductModel = "AYANEO Flip KB";

        // AYASpace CEcRgb::i2c_write skips the group-3 confirm write when IsAyaFlip.
        rgbConfirmation = false;

        // https://www.amd.com/fr/products/processors/laptop/ryzen/7000-series/amd-ryzen-7-7840u.html
        // https://www.amd.com/fr/products/processors/laptop/ryzen/8000-series/amd-ryzen-7-8840u.html
        this.nTDP = new double[] { 15, 15, 20 };
        this.cTDP = new double[] { 3, 28 };
        this.GfxClock = new double[] { 100, 2700 };
        this.CpuClock = 5100;

        // IMU matrices loaded from AYANEOFlipKB.json
    }
}