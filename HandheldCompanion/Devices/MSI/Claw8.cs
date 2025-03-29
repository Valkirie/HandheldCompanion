using HandheldCompanion.Extensions;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using WindowsInput.Events;

namespace HandheldCompanion.Devices;

public class Claw8 : ClawA1M
{
    #region imports
    [DllImport("intelGEDll.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern int getEGmode();

    [DllImport("intelGEDll.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern int setEGmode(int setMode);

    public enum EnduranceGamingControl
    {
        Off = 0,    // Endurance Gaming disable
        On = 1,     // Endurance Gaming enable
        Auto = 2,   // Endurance Gaming auto
    }

    public enum EnduranceGamingMode
    {
        Performance = 0,        // Endurance Gaming better performance mode
        Balanced = 1,           // Endurance Gaming balanced mode
        MaximumBattery = 2,     // Endurance Gaming maximum battery mode
    }

    [DllImport("intelGEDll.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern int setEGControlMode(EnduranceGamingControl control, EnduranceGamingMode mode);
    #endregion

    protected string Scope { get; set; } = "root\\WMI";
    protected string Path { get; set; } = "MSI_ACPI.InstanceName='ACPI\\PNP0C14\\0_0'";

    public Claw8()
    {
        // device specific settings
        ProductIllustration = "device_msi_claw";

        // https://www.intel.com/content/www/us/en/products/sku/240957/intel-core-ultra-7-processor-258v-12m-cache-up-to-4-80-ghz/specifications.html
        nTDP = new double[] { 28, 28, 37 };
        cTDP = new double[] { 20, 37 };
        GfxClock = new double[] { 100, 1950 };
        CpuClock = 4800;

        GyrometerAxis = new Vector3(1.0f, 1.0f, -1.0f);

        // device specific capacities
        Capabilities |= DeviceCapabilities.FanControl;
        Capabilities |= DeviceCapabilities.FanOverride;
        Capabilities |= DeviceCapabilities.WMIMethod;

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

        this.OEMChords.Add(new KeyboardChord("LButton",
            [KeyCode.LButton | KeyCode.OemClear],
            [KeyCode.LButton | KeyCode.OemClear],
            true, ButtonFlags.OEM5
        ));
    }

    public override bool Open()
    {
        var success = base.Open();
        if (!success)
            return false;

        // manage events
        ManagerFactory.powerProfileManager.Applied += PowerProfileManager_Applied;

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

        return true;
    }

    private void QueryPowerProfile()
    {
        PowerProfileManager_Applied(ManagerFactory.powerProfileManager.GetCurrent(), UpdateSource.Background);
    }

    private void PowerProfileManager_Initialized()
    {
        QueryPowerProfile();
    }

    private void PowerProfileManager_Applied(PowerProfile profile, UpdateSource source)
    {
        if (profile.FanProfile.fanMode != FanMode.Hardware)
        {
            byte[] fanTable = new byte[7];
            fanTable[0] = (byte)profile.FanProfile.fanSpeeds[4];
            fanTable[1] = (byte)profile.FanProfile.fanSpeeds[1];
            fanTable[2] = (byte)profile.FanProfile.fanSpeeds[2];
            fanTable[3] = (byte)profile.FanProfile.fanSpeeds[4];
            fanTable[4] = (byte)profile.FanProfile.fanSpeeds[6];
            fanTable[5] = (byte)profile.FanProfile.fanSpeeds[8];
            fanTable[6] = (byte)profile.FanProfile.fanSpeeds[10];

            // update fan table
            SetFanTable(fanTable);
        }

        // MSI Center, API_UserScenario
        if (profile.Guid == BetterBatteryGuid)
            setEGControlMode(EnduranceGamingControl.Auto, EnduranceGamingMode.MaximumBattery);
        else if (profile.Guid == BetterPerformanceGuid)
            setEGControlMode(EnduranceGamingControl.Off, EnduranceGamingMode.MaximumBattery);
        else if (profile.Guid == BestPerformanceGuid)
            setEGControlMode(EnduranceGamingControl.Off, EnduranceGamingMode.MaximumBattery);

        SetFanControl(profile.FanProfile.fanMode != FanMode.Hardware);
    }

    public override void Close()
    {
        SetFanFullSpeed(false);

        ManagerFactory.powerProfileManager.Applied -= PowerProfileManager_Applied;
        ManagerFactory.powerProfileManager.Initialized -= PowerProfileManager_Initialized;

        base.Close();
    }

    private void SetFanTable(byte[] fanTable)
    {
        /*
         * iDataBlockIndex = 1; // CPU
         * iDataBlockIndex = 2; // GPU
         */
        byte iDataBlockIndex = 1;

        // default: 49, 0, 40, 49, 58, 67, 75, 75
        byte[] dataFan = WMI.Get(Scope, Path, "Get_Fan", iDataBlockIndex, 32, out bool readFan);

        // Build the complete 32-byte package:
        byte[] fullPackage = new byte[32];
        fullPackage[0] = iDataBlockIndex;
        Array.Copy(fanTable, 0, fullPackage, 1, fanTable.Length);

        WMI.Set(Scope, Path, "Set_Fan", fullPackage);
    }

    public override void set_long_limit(int limit)
    {
        SetCPUPowerLimit(81, [(byte)limit]);
    }

    public override void set_short_limit(int limit)
    {
        SetCPUPowerLimit(80, [(byte)limit]);
    }

    private void SetCPUPowerLimit(int PL, byte[] limit)
    {
        // Build the complete 32-byte package:
        byte[] fullPackage = new byte[32];
        fullPackage[0] = (byte)PL;
        Array.Copy(limit, 0, fullPackage, 1, limit.Length);

        WMI.Set(Scope, Path, "Set_Data", fullPackage);
    }

    public override void SetFanControl(bool enable, int mode = 0)
    {
        // Build the complete 32-byte package:
        byte[] fullPackage = new byte[32];
        fullPackage[0] = 212;

        /*
         * Get_AP
         * switch (iDataBlockIndex)
         * case 0: length = 6;
         * case 1: length = 3;
         * case 2: length = 7;
         */

        byte[] data = WMI.Get(Scope, Path, "Get_AP", 1, 3, out bool readSuccess);
        if (readSuccess)
            data[0] = data[0].SetBit(7, enable);
        fullPackage[1] = data[0];

        WMI.Set(Scope, Path, "Set_Data", fullPackage);
    }

    public void SetFanFullSpeed(bool enabled)
    {
        // Build the complete 32-byte package:
        byte[] fullPackage = new byte[32];
        fullPackage[0] = 152;

        byte[] data = WMI.Get(Scope, Path, "Get_Data", 152, 1, out bool readSuccess);
        if (readSuccess)
            data[0] = data[0].SetBit(7, enabled);
        fullPackage[1] = data[0];

        WMI.Set(Scope, Path, "Set_Data", fullPackage);
    }
}