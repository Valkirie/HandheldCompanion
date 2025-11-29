using System.Collections.Generic;
using System.Numerics;

namespace HandheldCompanion.Devices;

public class AYANEOAIRPlus : AYANEO.AYANEODeviceCEii
{
    public AYANEOAIRPlus()
    {
        // device specific settings
        this.ProductIllustration = "device_aya_air";
        this.ProductModel = "AYANEOAir";
    }
}