using HandheldCompanion.Extensions;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using WindowsInput.Events;

namespace HandheldCompanion.Devices;

public class ClawA2VM : ClawA1M
{
    #region imports
    [DllImport("intelGEDll.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern int getEGmode();

    [DllImport("intelGEDll.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern int setEGmode(int setMode);

    [DllImport("intelGEDll.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern int setEGControlMode(EnduranceGamingControl control, EnduranceGamingMode mode);

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
    #endregion

    public enum ShiftType
    {
        None = -1,
        SportMode = 0,
        ComfortMode = 1,
        GreenMode = 2,
        ECO = 3,
        User = 4,
    }

    public enum ShiftModeCalcType
    {
        Active,
        Deactive,
        ChangeToCurrentShiftType,
    }

    public ClawA2VM()
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
        Capabilities |= DeviceCapabilities.OEMPower;
        Capabilities |= DeviceCapabilities.BatteryChargeLimit;
        Capabilities |= DeviceCapabilities.BatteryChargeLimitPercent;

        // battery bypass settings
        BatteryBypassMin = 60;
        BatteryBypassMax = 100;
        BatteryBypassStep = 20;

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

        SetShiftMode(ShiftModeCalcType.Deactive);

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
        bool IsDcMode = SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Offline;
        if (profile.Guid == BetterBatteryGuid)
        {
            SetShiftMode(ShiftModeCalcType.ChangeToCurrentShiftType, IsDcMode ? ShiftType.None : ShiftType.ECO);
            setEGControlMode(EnduranceGamingControl.Auto, EnduranceGamingMode.MaximumBattery);
        }
        else if (profile.Guid == BetterPerformanceGuid)
        {
            SetShiftMode(ShiftModeCalcType.ChangeToCurrentShiftType, IsDcMode ? ShiftType.None : ShiftType.GreenMode);
            setEGControlMode(EnduranceGamingControl.Off, EnduranceGamingMode.MaximumBattery);
        }
        else if (profile.Guid == BestPerformanceGuid)
        {
            SetShiftMode(ShiftModeCalcType.ChangeToCurrentShiftType, IsDcMode ? ShiftType.None : ShiftType.SportMode);
            setEGControlMode(EnduranceGamingControl.Off, EnduranceGamingMode.MaximumBattery);
        }
        else
        {
            SetShiftMode(ShiftModeCalcType.ChangeToCurrentShiftType, IsDcMode ? ShiftType.None : ShiftType.SportMode);
            setEGControlMode(EnduranceGamingControl.Off, EnduranceGamingMode.Performance);
        }

        SetFanControl(profile.FanProfile.fanMode != FanMode.Hardware);
    }

    public override void Close()
    {
        SetFanFullSpeed(false);

        ManagerFactory.powerProfileManager.Applied -= PowerProfileManager_Applied;
        ManagerFactory.powerProfileManager.Initialized -= PowerProfileManager_Initialized;

        base.Close();
    }

    protected override void SettingsManager_SettingValueChanged(string name, object value, bool temporary)
    {
        switch (name)
        {
            case "BatteryChargeLimit":
                bool enabled = Convert.ToBoolean(value);
                SetBatteryMaster(enabled);
                break;

            case "BatteryChargeLimitPercent":
                int percent = Convert.ToInt32(value);
                SetBatteryChargeLimit(percent);
                break;
        }

        base.SettingsManager_SettingValueChanged(name, value, temporary);
    }

    private void SetBatteryMaster(bool enable)
    {
        // Data block index specific to battery mode settings
        byte dataBlockIndex = 215;

        // Get the current battery data (1 byte) from the device
        byte[] data = WMI.Get(Scope, Path, "Get_Data", dataBlockIndex, 1, out bool readSuccess);
        if (readSuccess)
            data[0] = data[0].SetBit(7, enable);

        // Build the complete 32-byte package
        byte[] fullPackage = new byte[32];
        fullPackage[0] = dataBlockIndex;
        fullPackage[1] = data[0];

        // Set the battery mode using the package.
        WMI.Set(Scope, Path, "Set_Data", fullPackage);
    }

    private bool GetBatteryChargeLimit(ref byte currentValue)
    {
        // Data block index specific to battery mode settings
        byte dataBlockIndex = 215;

        // Get the current battery data (1 byte) from the device
        byte[] data = WMI.Get(Scope, Path, "Get_Data", dataBlockIndex, 1, out bool readSuccess);
        if (readSuccess)
            currentValue = data[0];

        return readSuccess;
    }

    private void SetBatteryChargeLimit(int chargeLimit)
    {
        // Data block index specific to battery mode settings
        byte dataBlockIndex = 215;

        // Get the current battery data (1 byte) from the device
        byte currentValue = 0;
        GetBatteryChargeLimit(ref currentValue);

        // Update mask
        byte mask = (byte)((uint)currentValue & (uint)sbyte.MaxValue);

        // Build the complete 32-byte package
        byte[] fullPackage = new byte[32];
        fullPackage[0] = dataBlockIndex;
        fullPackage[1] = (byte)(currentValue - mask + chargeLimit);

        // Set the battery mode using the package.
        WMI.Set(Scope, Path, "Set_Data", fullPackage);
    }

    private void SetFanTable(byte[] fanTable)
    {
        /*
         * iDataBlockIndex = 1; // CPU
         * iDataBlockIndex = 2; // GPU
         */
        byte iDataBlockIndex = 1;

        // default: 49, 0, 40, 49, 58, 67, 75, 75
        byte[] data = WMI.Get(Scope, Path, "Get_Fan", iDataBlockIndex, 32, out bool readFan);

        // Build the complete 32-byte package:
        byte[] fullPackage = new byte[32];
        fullPackage[0] = iDataBlockIndex;
        Array.Copy(fanTable, 0, fullPackage, 1, fanTable.Length);

        WMI.Set(Scope, Path, "Set_Fan", fullPackage);
    }

    public override void set_long_limit(int limit)
    {
        SetCPUPowerLimit(81, limit);
    }

    public override void set_short_limit(int limit)
    {
        SetCPUPowerLimit(80, limit);
    }

    private void SetCPUPowerLimit(int iDataBlockIndex, int limit)
    {
        /*
         * iDataBlockIndex = 80; // Short
         * iDataBlockIndex = 81; // Long
         */

        // Build the complete 32-byte package:
        byte[] fullPackage = new byte[32];
        fullPackage[0] = (byte)iDataBlockIndex;
        fullPackage[1] = (byte)limit;

        WMI.Set(Scope, Path, "Set_Data", fullPackage);
    }

    public override void SetFanControl(bool enable, int mode = 0)
    {
        byte iDataBlockIndex = 1;

        byte[] data = WMI.Get(Scope, Path, "Get_AP", iDataBlockIndex, WMI.GetAPLength(iDataBlockIndex), out bool readSuccess);
        if (readSuccess)
            data[0] = data[0].SetBit(7, enable);

        // update data block index
        iDataBlockIndex = 212;

        // Build the complete 32-byte package:
        byte[] fullPackage = new byte[32];
        fullPackage[0] = iDataBlockIndex;
        fullPackage[1] = data[0];

        WMI.Set(Scope, Path, "Set_Data", fullPackage);
    }

    public void SetFanFullSpeed(bool enable)
    {
        byte iDataBlockIndex = 152;

        byte[] data = WMI.Get(Scope, Path, "Get_Data", iDataBlockIndex, 1, out bool readSuccess);
        if (readSuccess)
            data[0] = data[0].SetBit(7, enable);

        // Build the complete 32-byte package:
        byte[] fullPackage = new byte[32];
        fullPackage[0] = iDataBlockIndex;
        fullPackage[1] = data[0];

        WMI.Set(Scope, Path, "Set_Data", fullPackage);
    }

    public int GetShiftValue()
    {
        byte iDataBlockIndex = 0;

        // Optional: decode the value if needed.
        // bool isSupported = (shiftValue & 128) != 0;
        // bool isActive = (shiftValue & 64) != 0;
        // int modeValue = shiftValue & 0x3F; // lower 6 bits
        byte[] data = WMI.Get(Scope, Path, "Get_AP", iDataBlockIndex, WMI.GetAPLength(iDataBlockIndex), out bool readSuccess);
        if (readSuccess)
            return data[2];

        return -1;
    }

    public void SetShiftValue(int newShiftValue)
    {
        byte iDataBlockIndex = 210;

        byte[] fullPackage = new byte[32];
        fullPackage[0] = iDataBlockIndex;
        fullPackage[1] = (byte)newShiftValue;

        // Write the package back to the EC.
        WMI.Set(Scope, Path, "Set_Data", fullPackage);
    }

    public bool IsShiftSupported()
    {
        int currentValue = GetShiftValue();
        return (currentValue & 128) != 0;
    }

    public void SetShiftMode(ShiftModeCalcType calcType, ShiftType shiftType = ShiftType.None)
    {
        if (!IsShiftSupported())
            return;

        int ShiftModeValueInEC = GetShiftValue();
        ShiftModeValueInEC &= 195;

        switch (calcType)
        {
            case ShiftModeCalcType.Active:
                ShiftModeValueInEC |= 128;
                ShiftModeValueInEC |= 64;
                break;
            case ShiftModeCalcType.Deactive:
                ShiftModeValueInEC |= 128;
                ShiftModeValueInEC &= 191;
                break;
            case ShiftModeCalcType.ChangeToCurrentShiftType:
                ShiftModeValueInEC |= 192;
                ShiftModeValueInEC &= 252;
                switch (shiftType)
                {
                    case ShiftType.SportMode:
                        ShiftModeValueInEC += 4;
                        break;
                    case ShiftType.ComfortMode:
                        break;
                    case ShiftType.GreenMode:
                        ++ShiftModeValueInEC;
                        break;
                    case ShiftType.ECO:
                        ShiftModeValueInEC += 2;
                        break;
                    case ShiftType.User:
                        ShiftModeValueInEC += 3;
                        break;
                }
                break;
        }

        SetShiftValue(ShiftModeValueInEC);
    }
}