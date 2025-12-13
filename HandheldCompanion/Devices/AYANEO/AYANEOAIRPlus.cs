using HandheldCompanion.Devices.AYANEO;

namespace HandheldCompanion.Devices;

public class AYANEOAIRPlus : AYANEODeviceCEii
{
    public AYANEOAIRPlus()
    {
        // device specific settings
        this.ProductIllustration = "device_aya_air";
        this.ProductModel = "AYANEOAir";
    }
}