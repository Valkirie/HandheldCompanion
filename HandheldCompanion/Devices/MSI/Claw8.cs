using HandheldCompanion.Extensions;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using System;
using System.Linq;

namespace HandheldCompanion.Devices;

public class Claw8 : ClawA1M
{
    public Claw8()
    {
        // device specific settings
        ProductIllustration = "device_msi_claw";

        // https://www.intel.com/content/www/us/en/products/sku/240957/intel-core-ultra-7-processor-258v-12m-cache-up-to-4-80-ghz/specifications.html
        nTDP = new double[] { 28, 28, 37 };
        cTDP = new double[] { 20, 37 };
        GfxClock = new double[] { 100, 1950 };
        CpuClock = 4800;

        // device specific capacities
        Capabilities |= DeviceCapabilities.FanControl;
        Capabilities |= DeviceCapabilities.FanOverride;

        // overwrite ClawA1M default power profiles
        PowerProfile powerProfile = DevicePowerProfiles.FirstOrDefault(profile => profile.Guid == BetterBatteryGuid);
        if (powerProfile != null)
            powerProfile.TDPOverrideValues = new[] { 20.0d, 20.0d, 20.0d };

        powerProfile = DevicePowerProfiles.FirstOrDefault(profile => profile.Guid == BetterPerformanceGuid);
        if (powerProfile != null)
            powerProfile.TDPOverrideValues = new[] { 30.0d, 30.0d, 30.0d };

        powerProfile = DevicePowerProfiles.FirstOrDefault(profile => profile.Guid == BestPerformanceGuid);
        if (powerProfile != null)
            powerProfile.TDPOverrideValues = new[] { 35.0d, 35.0d, 35.0d };
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
            fanTable[1] = (byte)profile.FanProfile.fanSpeeds[1];
            fanTable[2] = (byte)profile.FanProfile.fanSpeeds[2];
            fanTable[3] = (byte)profile.FanProfile.fanSpeeds[4];
            fanTable[4] = (byte)profile.FanProfile.fanSpeeds[6];
            fanTable[5] = (byte)profile.FanProfile.fanSpeeds[8];
            fanTable[6] = (byte)profile.FanProfile.fanSpeeds[10];

            // update fan table
            SetFanTable(fanTable);
        }

        SetFanControl(profile.FanProfile.fanMode != FanMode.Hardware);
    }

    private void SetFanTable(byte[] fanTable)
    {
        /*
         * iDataBlockIndex = 1; // CPU
         * iDataBlockIndex = 2; // GPU
         */

        // Build the complete 32-byte package:
        byte iDataBlockIndex = 1;
        byte[] fullPackage = new byte[32];
        fullPackage[0] = iDataBlockIndex;
        Array.Copy(fanTable, 0, fullPackage, 1, fanTable.Length);

        WMI.Set(Scope, Path, "Set_Fan", fullPackage);
    }

    public override void Close()
    {
        SetFanFullSpeed(false);

        ManagerFactory.powerProfileManager.Applied -= PowerProfileManager_Applied;
        ManagerFactory.powerProfileManager.Initialized -= PowerProfileManager_Initialized;

        base.Close();
    }

    public string Scope { get; set; } = "root\\WMI";
    public string Path { get; set; } = "MSI_ACPI.InstanceName='ACPI\\PNP0C14\\0_0'";

    public void SetCPUPowerLimit(int PL, byte[] limit)
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

        byte[] data = WMI.Get(Scope, Path, "Get_AP", 1);
        data[0] = data[0].SetBit(7, enable);
        fullPackage[1] = data[0];

        WMI.Set(Scope, Path, "Set_Data", fullPackage);
    }

    public void SetFanFullSpeed(bool enabled)
    {
        // Build the complete 32-byte package:
        byte[] fullPackage = new byte[32];
        fullPackage[0] = 152;

        byte[] data = WMI.Get(Scope, Path, "Get_Data", 152);
        data[0] = data[0].SetBit(7, enabled);
        fullPackage[1] = data[0];

        WMI.Set(Scope, Path, "Set_Data", fullPackage);
    }
}