using HandheldCompanion.Misc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace HandheldCompanion.Devices;

public class ClawA2VM : ClawA1M
{
    public ClawA2VM()
    {
        // device specific settings
        ProductIllustration = "device_msi_claw8";

        // https://www.intel.com/content/www/us/en/products/sku/240957/intel-core-ultra-7-processor-258v-12m-cache-up-to-4-80-ghz/specifications.html
        nTDP = new double[] { 28, 28, 37 };
        cTDP = new double[] { 20, 37 };
        GfxClock = new double[] { 100, 1950 };
        CpuClock = 4800;

        GyrometerAxis = new Vector3(1.0f, 1.0f, -1.0f);

        // overwrite ClawA1M default power profiles
        Dictionary<Guid, double[]> tdpOverrides = new Dictionary<Guid, double[]>
        {
            { BetterBatteryGuid,      new double[] { 20.0d, 20.0d, 20.0d } },
            { BetterPerformanceGuid,  new double[] { 30.0d, 30.0d, 30.0d } },
            { BestPerformanceGuid,    new double[] { 35.0d, 35.0d, 35.0d } }
        };

        foreach (KeyValuePair<Guid, double[]> kvp in tdpOverrides)
        {
            PowerProfile? profile = DevicePowerProfiles.FirstOrDefault(p => p.Guid == kvp.Key);
            if (profile != null) profile.TDPOverrideValues = kvp.Value;
        }
    }
}