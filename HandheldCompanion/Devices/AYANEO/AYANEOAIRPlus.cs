using HandheldCompanion.Devices.AYANEO;
using System.Collections.Generic;
using System.Numerics;

namespace HandheldCompanion.Devices;

public class AYANEOAIRPlus : AYANEODeviceCEii
{
    public AYANEOAIRPlus()
    {
        // device specific settings
        this.ProductIllustration = "device_aya_air";
        this.ProductModel = "AYANEOAir";

        this.GyrometerAxis = new Vector3(1.0f, 1.0f, -1.0f);
        this.GyrometerAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'X' },
            { 'Y', 'Z' },
            { 'Z', 'Y' }
        };

        this.AccelerometerAxis = new Vector3(1.0f, 1.0f, -1.0f);
        this.AccelerometerAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'X' },
            { 'Y', 'Z' },
            { 'Z', 'Y' }
        };
    }
}