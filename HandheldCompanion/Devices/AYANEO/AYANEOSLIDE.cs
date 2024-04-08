using System.Numerics;

namespace HandheldCompanion.Devices;

public class AYANEOSlide : AYANEO.AYANEODeviceCEii
{
    public AYANEOSlide()
    {
        // device specific settings
        this.ProductIllustration = "device_aya_slide";
        this.ProductModel = "AYANEOSlide";

        // https://www.amd.com/en/products/apu/amd-ryzen-7-7840u
        this.nTDP = new double[] { 15, 15, 20 };
        this.cTDP = new double[] { 3, 54 };
        this.GfxClock = new double[] { 100, 2700 };
        this.CpuClock = 5100;

        this.GyrometerAxis = new Vector3(1.0f, 1.0f, -1.0f);
        this.AccelerometerAxis = new Vector3(-1.0f, 1.0f, -1.0f);
    }
}