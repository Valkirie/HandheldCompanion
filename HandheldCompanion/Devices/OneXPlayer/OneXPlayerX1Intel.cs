using HandheldCompanion.Managers;

namespace HandheldCompanion.Devices;

public class OneXPlayerX1Intel : OneXPlayerX1
{
    public OneXPlayerX1Intel()
    {
        // https://www.intel.com/content/www/us/en/products/sku/236847/intel-core-ultra-7-processor-155h-24m-cache-up-to-4-80-ghz/specifications.html
        // follow the values presented in OneXConsole
        nTDP = new double[] { 15, 15, 35 };
        cTDP = new double[] { 6, 35 };
        GfxClock = new double[] { 100, 2250 };
        CpuClock = 4800;

        // Power Saving
        DevicePowerProfiles.Add(new(Properties.Resources.PowerProfileOneXPlayerX1IntelBetterBattery, Properties.Resources.PowerProfileOneXPlayerX1IntelBetterBatteryDesc)
        {
            Default = true,
            DeviceDefault = true,
            OSPowerMode = OSPowerMode.BetterBattery,
            CPUBoostLevel = CPUBoostLevel.Disabled,
            Guid = new("961cc777-2547-4f9d-8174-7d86181b8a7a"),
            TDPOverrideEnabled = true,
            TDPOverrideValues = new[] { 15.0d, 15.0d, 15.0d }
        });

        // Performance
        DevicePowerProfiles.Add(new(Properties.Resources.PowerProfileOneXPlayerX1IntelBetterPerformance, Properties.Resources.PowerProfileOneXPlayerX1IntelBetterPerformanceDesc)
        {
            Default = true,
            DeviceDefault = true,
            OSPowerMode = OSPowerMode.BetterPerformance,
            CPUBoostLevel = CPUBoostLevel.Enabled,
            Guid = new("3af9B8d9-7c97-431d-ad78-34a8bfea439f"),
            TDPOverrideEnabled = true,
            TDPOverrideValues = new[] { 30.0d, 30.0d, 30.0d }
        });

        // Max Performance
        DevicePowerProfiles.Add(new(Properties.Resources.PowerProfileOneXPlayerX1IntelBestPerformance, Properties.Resources.PowerProfileOneXPlayerX1IntelBestPerformanceDesc)
        {
            Default = true,
            DeviceDefault = true,
            OSPowerMode = OSPowerMode.BestPerformance,
            CPUBoostLevel = CPUBoostLevel.Enabled,
            Guid = new("ded574b5-45a0-4f42-8737-46345c09c238"),
            TDPOverrideEnabled = true,
            TDPOverrideValues = new[] { 35.0d, 35.0d, 64.0d },
            EPPOverrideEnabled = true,
            EPPOverrideValue = 32,
        });
    }
}
