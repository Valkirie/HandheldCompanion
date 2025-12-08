using HandheldCompanion.Misc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace HandheldCompanion.Devices;

public class XboxROGAlly : ROGAlly
{
    public XboxROGAlly()
    {
        // device specific settings
        ProductIllustration = "device_xbox_rog_ally";

        // https://www.amd.com/en/products/processors/handhelds/ryzen-z-series/z2-series/z2-a.html
        nTDP = new double[] { 20, 20, 33 };
        cTDP = new double[] { 15, 35 };
        GfxClock = new double[] { 100, 1800 };
        CpuClock = 3800;

        // overwrite ROGAlly default gyrometer axis settings
        GyrometerAxis = new Vector3(1.0f, 1.0f, -1.0f);

        // overwrite ROGAlly default power profiles
        Dictionary<Guid, double[]> tdpOverrides = new Dictionary<Guid, double[]>
        {
            { BetterBatteryGuid,      new double[] { 13.0d, 13.0d, 13.0d } },
            { BetterPerformanceGuid,  new double[] { 17.0d, 17.0d, 17.0d } },
            { BestPerformanceGuid,    new double[] { 25.0d, 25.0d, 25.0d } }
        };

        foreach (KeyValuePair<Guid, double[]> kvp in tdpOverrides)
        {
            PowerProfile? profile = DevicePowerProfiles.FirstOrDefault(p => p.Guid == kvp.Key);
            if (profile != null) profile.TDPOverrideValues = kvp.Value;
        }
    }
}