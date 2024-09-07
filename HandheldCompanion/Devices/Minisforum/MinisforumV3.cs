using HandheldCompanion.Managers;
using System.Collections.Generic;
using System.Numerics;

namespace HandheldCompanion.Devices;

public class MinisforumV3 : IDevice
{
    public MinisforumV3()
    {
        // device specific settings
        this.ProductIllustration = "device_minisforum_v3";
        this.ProductModel = "MINISFORUM V3";

        // https://www.amd.com/en/products/apu/amd-ryzen-7-7840u
        // https://www.amd.com/en/products/apu/amd-ryzen-7-8840u
        this.nTDP = new double[] { 15, 15, 20 };
        this.cTDP = new double[] { 3, 28 };
        this.GfxClock = new double[] { 100, 2700 };
        this.CpuClock = 5100;

        GyrometerAxis = new Vector3(1.0f, -1.0f, 1.0f);
        GyrometerAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'Y' },
            { 'Y', 'X' },
            { 'Z', 'Z' }
        };

        AccelerometerAxis = new Vector3(-1.0f, 1.0f, -1.0f);
        AccelerometerAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'Y' },
            { 'Y', 'X' },
            { 'Z', 'Z' }
        };

        // device specific capacities
        Capabilities |= DeviceCapabilities.None;

        DevicePowerProfiles.Add(new(Properties.Resources.PowerProfileMinisforumV3BetterBattery, Properties.Resources.PowerProfileMinisforumV3BetterBatteryDesc)
        {
            Default = true,
            DeviceDefault = true,
            OSPowerMode = OSPowerMode.BetterBattery,
            CPUBoostLevel = CPUBoostLevel.Disabled,
            Guid = new("961cc777-2547-4f9d-8174-7d86181b8a7a"),
            TDPOverrideEnabled = true,
            TDPOverrideValues = new[] { 15.0d, 15.0d, 15.0d }
        });

        DevicePowerProfiles.Add(new(Properties.Resources.PowerProfileMinisforumV3BetterPerformance, Properties.Resources.PowerProfileMinisforumV3BetterPerformanceDesc)
        {
            Default = true,
            DeviceDefault = true,
            OSPowerMode = OSPowerMode.BetterPerformance,
            Guid = new("3af9B8d9-7c97-431d-ad78-34a8bfea439f"),
            TDPOverrideEnabled = true,
            TDPOverrideValues = new[] { 22.0d, 22.0d, 22.0d }
        });

        DevicePowerProfiles.Add(new(Properties.Resources.PowerProfileMinisforumV3BestPerformance, Properties.Resources.PowerProfileMinisforumV3BestPerformanceDesc)
        {
            Default = true,
            DeviceDefault = true,
            OSPowerMode = OSPowerMode.BestPerformance,
            Guid = new("ded574b5-45a0-4f42-8737-46345c09c238"),
            TDPOverrideEnabled = true,
            TDPOverrideValues = new[] { 28.0d, 28.0d, 28.0d }
        });
    }
}