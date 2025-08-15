using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
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

        // https://www.amd.com/fr/products/processors/laptop/ryzen/7000-series/amd-ryzen-7-7840u.html
        // https://www.amd.com/fr/products/processors/laptop/ryzen/8000-series/amd-ryzen-7-8840u.html
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

    public override void OpenEvents()
    {
        base.OpenEvents();

        // raise events
        switch (ManagerFactory.powerProfileManager.Status)
        {
            default:
            case ManagerStatus.Initializing:
                ManagerFactory.powerProfileManager.Initialized += PowerProfileManager_Initialized;
                break;
            case ManagerStatus.Initialized:
                QueryPowerProfile();
                break;
        }
    }

    public override void Close()
    {
        ManagerFactory.powerProfileManager.Applied -= PowerProfileManager_Applied;
        ManagerFactory.powerProfileManager.Initialized -= PowerProfileManager_Initialized;

        base.Close();
    }

    private void QueryPowerProfile()
    {
        // manage events
        ManagerFactory.powerProfileManager.Applied += PowerProfileManager_Applied;

        PowerProfileManager_Applied(ManagerFactory.powerProfileManager.GetCurrent(), UpdateSource.Background);
    }

    private void PowerProfileManager_Initialized()
    {
        QueryPowerProfile();
    }

    private void PowerProfileManager_Applied(PowerProfile profile, UpdateSource source)
    {
        if (profile.IsDeviceDefault())
            setThermalMode(profile.OEMPowerMode);
        else
            setThermalMode((int)MinisForumMode.Performance);
    }
}