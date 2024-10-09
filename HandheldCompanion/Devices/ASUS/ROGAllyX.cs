using HandheldCompanion.Misc;
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
        _pid = 0x1B4C;

        // overwrite ROGAlly default gyrometer axis settings
        GyrometerAxis = new Vector3(1.0f, 1.0f, -1.0f);

        // overwrite ROGAlly default power profiles
        PowerProfile powerProfile = DevicePowerProfiles.FirstOrDefault(profile => profile.Guid == BetterBatteryGuid);
        if (powerProfile != null)
            powerProfile.TDPOverrideValues = new[] { 13.0d, 13.0d, 13.0d };

        powerProfile = DevicePowerProfiles.FirstOrDefault(profile => profile.Guid == BetterPerformanceGuid);
        if (powerProfile != null)
            powerProfile.TDPOverrideValues = new[] { 17.0d, 17.0d, 17.0d };

        powerProfile = DevicePowerProfiles.FirstOrDefault(profile => profile.Guid == BestPerformanceGuid);
        if (powerProfile != null)
            powerProfile.TDPOverrideValues = new[] { 25.0d, 25.0d, 25.0d };
    }
}