using HandheldCompanion.Managers;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;

namespace HandheldCompanion.Devices;

public class MinisforumV3 : IDevice
{
    [DllImport("minisforum.dll", CallingConvention = CallingConvention.Winapi)]
    public static extern int getThermalMode();

    [DllImport("minisforum.dll", CallingConvention = CallingConvention.Winapi)]
    public static extern bool setThermalMode(int mode);

    [DllImport("minisforum.dll", CallingConvention = CallingConvention.Winapi)]
    public static extern int getFanSpeed();

    public enum MinisForumMode
    {
        Quiet = 0x01,
        Balanced = 0x02,
        Performance = 0x03,
    }

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
            OEMPowerMode = (int)MinisForumMode.Quiet,
            CPUBoostLevel = CPUBoostLevel.Disabled,
            Guid = BetterBatteryGuid,
            TDPOverrideEnabled = true,
            TDPOverrideValues = new[] { 15.0d, 15.0d, 15.0d }
        });

        DevicePowerProfiles.Add(new(Properties.Resources.PowerProfileMinisforumV3BetterPerformance, Properties.Resources.PowerProfileMinisforumV3BetterPerformanceDesc)
        {
            Default = true,
            DeviceDefault = true,
            OSPowerMode = OSPowerMode.BetterPerformance,
            OEMPowerMode = (int)MinisForumMode.Balanced,
            Guid = BetterPerformanceGuid,
            TDPOverrideEnabled = true,
            TDPOverrideValues = new[] { 22.0d, 22.0d, 22.0d }
        });

        DevicePowerProfiles.Add(new(Properties.Resources.PowerProfileMinisforumV3BestPerformance, Properties.Resources.PowerProfileMinisforumV3BestPerformanceDesc)
        {
            Default = true,
            DeviceDefault = true,
            OSPowerMode = OSPowerMode.BestPerformance,
            OEMPowerMode = (int)MinisForumMode.Performance,
            Guid = BestPerformanceGuid,
            TDPOverrideEnabled = true,
            TDPOverrideValues = new[] { 28.0d, 28.0d, 28.0d }
        });
    }
}