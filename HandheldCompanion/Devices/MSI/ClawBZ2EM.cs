using HandheldCompanion.Misc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace HandheldCompanion.Devices;

public class ClawBZ2EM : ClawA1M
{
    public ClawBZ2EM()
    {
        // device specific settings
        ProductIllustration = "device_msi_claw8";

        // https://www.amd.com/en/products/processors/handhelds/ryzen-z-series/z2-series/z2-extreme.html
        nTDP = new double[] { 20, 20, 33 };
        cTDP = new double[] { 15, 35 };
        GfxClock = new double[] { 100, 2900 };
        CpuClock = 5000;

        // unknown ?
        GyrometerAxis = new Vector3(1.0f, 1.0f, -1.0f);

        // overwrite ClawA1M default power profiles
        Dictionary<Guid, double[]> tdpOverrides = new Dictionary<Guid, double[]>
        {
            { BetterBatteryGuid,      new double[] { 15, 15, 15 } },
            { BetterPerformanceGuid,  new double[] { 20, 20, 20 } },
            { BestPerformanceGuid,    new double[] { 28, 28, 28 } }
        };

        foreach (KeyValuePair<Guid, double[]> kvp in tdpOverrides)
        {
            PowerProfile? profile = DevicePowerProfiles.FirstOrDefault(p => p.Guid == kvp.Key);
            if (profile != null) profile.TDPOverrideValues = kvp.Value;
        }
    }

    public override void set_short_limit(int limit)
    {
        base.set_short_limit(limit); // sPPT
        SetCPUPowerLimit(82, limit); // fPPT
    }
}