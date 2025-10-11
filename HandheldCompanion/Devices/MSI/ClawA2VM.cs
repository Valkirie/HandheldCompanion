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
        nTDP = new double[] { 17, 17, 37 };
        cTDP = new double[] { 8, 37 };
        GfxClock = new double[] { 100, 1950 };
        CpuClock = 4800;

        GyrometerAxis = new Vector3(1.0f, 1.0f, -1.0f);

        // overwrite ClawA1M default power profiles
        Dictionary<Guid, double[]> tdpOverrides = new Dictionary<Guid, double[]>
        {
            { BetterBatteryGuid,      new double[] { 8, 8, 9 } },
            { BetterPerformanceGuid,  new double[] { 17, 17, 18 } },
            { BestPerformanceGuid,    new double[] { 30, 30, 31 } }
        };

        foreach (KeyValuePair<Guid, double[]> kvp in tdpOverrides)
        {
            PowerProfile? profile = DevicePowerProfiles.FirstOrDefault(p => p.Guid == kvp.Key);
            if (profile != null) profile.TDPOverrideValues = kvp.Value;
        }
    }

    public override bool Open()
    {
        base.Open();

        // unlock TDP
        set_long_limit(30);
        set_short_limit(37);

        return true;
    }
}