using HandheldCompanion.Misc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace HandheldCompanion.Devices;

public class ROGAllyX : ROGAlly
{
    public ROGAllyX()
    {
        // device specific settings
        ProductIllustration = "device_rog_ally_x";

        // used to monitor OEM specific inputs
        productIds = [0x1B4C];

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