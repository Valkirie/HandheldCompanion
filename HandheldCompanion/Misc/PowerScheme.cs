using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace HandheldCompanion.Misc;

public enum PowerIndexType
{
    AC,
    DC
}

// For a reference on additional subgroup and setting GUIDs, run the command powercfg.exe /qh
// This will list all hidden settings with both the names and GUIDs, descriptions, current values, and allowed values.

public static class PowerSubGroup
{
    public static readonly Guid SUB_PROCESSOR = new("54533251-82be-4824-96c1-47b60b740d00");
    public static readonly Guid SUB_SLEEP = new("238C9FA8-0AAD-41ED-83F4-97BE242C8F20");
}

public static class PowerProfileGroup
{
    public static readonly Guid GUID_LOWPOWER = new("4569E601-272E-4869-BCAB-1C6C03D7966F");
    public static readonly Guid GUID_LOWLATENCY = new("0DA965DC-8FCF-4c0b-8EFE-8DD5E7BC959A");
    public static readonly Guid GUID_SUSTAINEDPERF = new("0AABB002-A307-447e-9B81-1D819DF6C6D0");
    public static readonly Guid GUID_GAMEMODE = new("D4140C81-EBBA-4e60-8561-6918290359CD");
    public static readonly Guid GUID_CONSTRAINED = new("EE1E4F72-E368-46b1-B3C6-5048B11C2DBD");
    public static readonly Guid GUID_STANDBY = new("8BC6262C-C026-411d-AE3B-7E2F70811A13");
    public static readonly Guid GUID_SCREENOFF = new("2e92e666-c3f6-42c3-89bd-94d40fabcde5");
    public static readonly Guid GUID_LOWQOS = new("C04A802D-2205-4910-AE98-3B51E3BB72F2");
    public static readonly Guid GUID_MEDIUMQOS = new("A4A61B5F-F42C-4d23-B3AB-5C27DF9F0F18");
    public static readonly Guid GUID_MULTIMEDIAQOS = new("0C3D5326-944B-4aab-8AD8-FE422A0E50E0");
    public static readonly Guid GUID_UTILITYQOS = new("33CC3A0D-45EE-43CA-86C4-695BFC9A313B");
    public static readonly Guid GUID_ECOQOS = new("336C7511-F109-4172-BB3A-3EA51F815ADA");

    // List of all profiles for easy iteration
    public static readonly List<Guid> AllProfiles = new List<Guid>
    {
        GUID_LOWPOWER,
        GUID_LOWLATENCY,
        GUID_SUSTAINEDPERF,
        GUID_GAMEMODE,
        GUID_CONSTRAINED,
        GUID_STANDBY,
        GUID_SCREENOFF,
        GUID_LOWQOS,
        GUID_MEDIUMQOS,
        GUID_MULTIMEDIAQOS,
        GUID_UTILITYQOS,
        GUID_ECOQOS
    };
}

public static class SleepSetting
{
    public static readonly Guid HIBERNATE_TIMEOUT = new Guid("9D7815A6-7EE4-497E-8888-515A05F02364");
}

public static class PowerSetting
{
    public static Guid PERFBOOSTMODE = new("be337238-0d82-4146-a960-4f3749d470c7"); // Processor performance boost mode
    public static Guid PROCFREQMAX = new("75b0ae3f-bce0-45a7-8c89-c9611c25e100"); // Maximum processor frequency in MHz, 0 for no limit (default)
    public static Guid PROCFREQMAX1 = new("75b0ae3f-bce0-45a7-8c89-c9611c25e101"); // Maximum processor frequency for processor power efficiency class 1 in MHz, 0 for no limit (default)
    public static Guid CPMINCORES = new("0cc5b647-c1df-4637-891a-dec35c318583"); // Processor performance core parking min cores, expressed as a percent from 0 - 100
    public static Guid CPMAXCORES = new("ea062031-0e34-4ff1-9b6d-eb1059334028"); // Processor performance core parking max cores, expressed as a percent from 0 - 100
    public static Guid PERFEPP = new("36687f9e-e3a5-4dbf-b1dc-15eb381c6863"); // Processor energy performance preference policy, expressed as a percent from 0 - 100
    public static Guid PERFEPP1 = new("36687f9e-e3a5-4dbf-b1dc-15eb381c6864"); // Processor energy performance preference policy for Processor Power Efficiency Class 1, expressed as a percent from 0 - 100

    public static Guid HETEROGENEOUS_POLICY = new Guid("7f2f5cfa-f10c-4823-b5e1-e93ae85f46b5");
    public static Guid HETEROGENEOUS_THREAD_SCHEDULING_POLICY = new Guid("93b8b6dc-0698-4d1c-9ee4-0644e900c85d");
    public static Guid HETEROGENEOUS_SHORT_THREAD_SCHEDULING_POLICY = new Guid("bae08b81-2d5e-4688-ad6a-13243356654b");
}

public enum PerfBoostMode
{
    Disabled = 0,
    Enabled = 1,
    Aggressive = 2,
    EfficientEnabled = 3,
    EfficientAggressive = 4,
    AggressiveAtGuaranteed = 5,
    EfficientAggressiveAtGuaranteed = 6
}

public enum CoreParkingMode
{
    AllCoresAuto,
    AllCoresPrefPCore,
    AllCoresPrefECore,
    OnlyPCore,
    OnlyECore,
}

public static class PowerScheme
{
    // Wrapper for the actual PowerGetActiveScheme. Converts GUID to the built-in type on output and handles the LocalFree call.
    /// <summary>
    ///     Retrieves the active power scheme and returns a GUID that identifies the scheme.
    /// </summary>
    /// <param name="UserRootPowerKey">This parameter is reserved for future use and must be set to zero.</param>
    /// <param name="ActivePolicyGuid">A pointer that receives a GUID structure.</param>
    /// <returns>Returns zero if the call was successful, and a nonzero value if the call failed.</returns>
    private static uint PowerGetActiveScheme(nint UserRootPowerKey, out Guid ActivePolicyGuid)
    {
        var activePolicyGuidPtr = nint.Zero;
        ActivePolicyGuid = Guid.Empty;

        var result = PowerGetActiveScheme(UserRootPowerKey, out activePolicyGuidPtr);

        if (result == 0 && activePolicyGuidPtr != nint.Zero)
        {
            ActivePolicyGuid = (Guid)Marshal.PtrToStructure(activePolicyGuidPtr, typeof(Guid));
            LocalFree(activePolicyGuidPtr);
        }

        return result;
    }

    public static bool GetActiveScheme(out Guid ActivePolicyGuid)
    {
        return PowerGetActiveScheme(nint.Zero, out ActivePolicyGuid) == 0;
    }

    public static bool SetActiveScheme(Guid SchemeGuid)
    {
        return PowerSetActiveScheme(nint.Zero, SchemeGuid) == 0;
    }

    public static bool GetValue(PowerIndexType powerType, Guid SchemeGuid, Guid SubGroupOfPowerSettingsGuid,
        Guid PowerSettingGuid, out uint value)
    {
        switch (powerType)
        {
            case PowerIndexType.AC:
                return PowerReadACValueIndex(nint.Zero, SchemeGuid, SubGroupOfPowerSettingsGuid, PowerSettingGuid,
                    out value) == 0;
            case PowerIndexType.DC:
                return PowerReadDCValueIndex(nint.Zero, SchemeGuid, SubGroupOfPowerSettingsGuid, PowerSettingGuid,
                    out value) == 0;
        }

        value = 0;
        return false;
    }

    public static bool SetValue(PowerIndexType powerType, Guid SchemeGuid, Guid SubGroupOfPowerSettingsGuid,
        Guid PowerSettingGuid, uint value)
    {
        switch (powerType)
        {
            case PowerIndexType.AC:
                return PowerWriteACValueIndex(nint.Zero, SchemeGuid, SubGroupOfPowerSettingsGuid, PowerSettingGuid,
                    value) == 0;
            case PowerIndexType.DC:
                return PowerWriteDCValueIndex(nint.Zero, SchemeGuid, SubGroupOfPowerSettingsGuid, PowerSettingGuid,
                    value) == 0;
        }

        return false;
    }

    public static bool SetAttribute(Guid SubGroupOfPowerSettingsGuid, Guid PowerSettingGuid, uint value)
    {
        return PowerWriteSettingAttributes(SubGroupOfPowerSettingsGuid, PowerSettingGuid, value) == 0;
    }

    public static uint[] ReadPowerCfg(Guid SubGroup, Guid Settings)
    {
        var results = new uint[2];

        if (GetActiveScheme(out var currentScheme))
        {
            // read AC/DC values
            GetValue(PowerIndexType.AC, currentScheme, SubGroup, Settings,
                out results[(int)PowerIndexType.AC]);
            GetValue(PowerIndexType.DC, currentScheme, SubGroup, Settings,
                out results[(int)PowerIndexType.DC]);
        }

        return results;
    }

    public static void WritePowerCfg(Guid SubGroup, Guid Settings, uint ACValue, uint DCValue)
    {
        if (GetActiveScheme(out var currentScheme))
        {
            // unhide attribute
            SetAttribute(SubGroup, Settings, 2);

            // set value(s)
            SetValue(PowerIndexType.AC, currentScheme, SubGroup, Settings, ACValue);
            SetValue(PowerIndexType.DC, currentScheme, SubGroup, Settings, DCValue);

            // activate scheme
            SetActiveScheme(currentScheme);
        }
    }

    #region imports

    /// <summary>
    ///     Retrieves the active power scheme and returns a GUID that identifies the scheme.
    /// </summary>
    /// <param name="UserRootPowerKey">This parameter is reserved for future use and must be set to zero.</param>
    /// <param name="ActivePolicyGuid">
    ///     A pointer that receives a pointer to a GUID structure. Use the LocalFree function to
    ///     free this memory.
    /// </param>
    /// <returns>Returns zero if the call was successful, and a nonzero value if the call failed.</returns>
    [DllImport("powrprof.dll", EntryPoint = "PowerGetActiveScheme")]
    private static extern uint PowerGetActiveScheme(nint UserRootPowerKey, out nint ActivePolicyGuid);

    /// <summary>
    ///     Sets the active power scheme for the current user.
    /// </summary>
    /// <param name="UserRootPowerKey">This parameter is reserved for future use and must be set to zero.</param>
    /// <param name="SchemeGuid">The identifier of the power scheme.</param>
    /// <returns>Returns zero if the call was successful, and a nonzero value if the call failed.</returns>
    [DllImport("powrprof.dll", EntryPoint = "PowerSetActiveScheme")]
    private static extern uint PowerSetActiveScheme(nint UserRootPowerKey, in Guid SchemeGuid);

    /// <summary>
    ///     Retrieves the AC value index of the specified power setting.
    /// </summary>
    /// <param name="RootPowerKey">This parameter is reserved for future use and must be set to zero.</param>
    /// <param name="SchemeGuid">The identifier of the power scheme.</param>
    /// <param name="SubGroupOfPowerSettingsGuid">The subgroup of power settings.</param>
    /// <param name="PowerSettingGuid">The identifier of the power setting.</param>
    /// <param name="AcValueIndex">A pointer to a variable that receives the AC value index.</param>
    /// <returns>Returns zero if the call was successful, and a nonzero value if the call failed.</returns>
    [DllImport("powrprof.dll", EntryPoint = "PowerReadACValueIndex")]
    private static extern uint PowerReadACValueIndex(nint RootPowerKey, in Guid SchemeGuid,
        in Guid SubGroupOfPowerSettingsGuid, in Guid PowerSettingGuid, out uint AcValueIndex);

    /// <summary>
    ///     Retrieves the DC value index of the specified power setting.
    /// </summary>
    /// <param name="RootPowerKey">This parameter is reserved for future use and must be set to zero.</param>
    /// <param name="SchemeGuid">The identifier of the power scheme.</param>
    /// <param name="SubGroupOfPowerSettingsGuid">The subgroup of power settings.</param>
    /// <param name="PowerSettingGuid">The identifier of the power setting.</param>
    /// <param name="DcValueIndex">A pointer to a variable that receives the DC value index.</param>
    /// <returns>Returns zero if the call was successful, and a nonzero value if the call failed.</returns>
    [DllImport("powrprof.dll", EntryPoint = "PowerReadDCValueIndex")]
    private static extern uint PowerReadDCValueIndex(nint RootPowerKey, in Guid SchemeGuid,
        in Guid SubGroupOfPowerSettingsGuid, in Guid PowerSettingGuid, out uint DcValueIndex);

    /// <summary>
    ///     Sets the AC value index of the specified power setting.
    /// </summary>
    /// <param name="RootPowerKey">This parameter is reserved for future use and must be set to zero.</param>
    /// <param name="SchemeGuid">The identifier of the power scheme.</param>
    /// <param name="SubGroupOfPowerSettingsGuid">The subgroup of power settings.</param>
    /// <param name="PowerSettingGuid">The identifier of the power setting.</param>
    /// <param name="AcValueIndex">The AC value index.</param>
    /// <returns>Returns zero if the call was successful, and a nonzero value if the call failed.</returns>
    [DllImport("powrprof.dll", EntryPoint = "PowerWriteACValueIndex")]
    private static extern uint PowerWriteACValueIndex(nint RootPowerKey, in Guid SchemeGuid,
        in Guid SubGroupOfPowerSettingsGuid, in Guid PowerSettingGuid, uint AcValueIndex);

    /// <summary>
    ///     Sets the DC value index of the specified power setting.
    /// </summary>
    /// <param name="RootPowerKey">This parameter is reserved for future use and must be set to zero.</param>
    /// <param name="SchemeGuid">The identifier of the power scheme.</param>
    /// <param name="SubGroupOfPowerSettingsGuid">The subgroup of power settings.</param>
    /// <param name="PowerSettingGuid">The identifier of the power setting.</param>
    /// <param name="DcValueIndex">The DC value index.</param>
    /// <returns>Returns zero if the call was successful, and a nonzero value if the call failed.</returns>
    [DllImport("powrprof.dll", EntryPoint = "PowerWriteDCValueIndex")]
    private static extern uint PowerWriteDCValueIndex(nint RootPowerKey, in Guid SchemeGuid,
        in Guid SubGroupOfPowerSettingsGuid, in Guid PowerSettingGuid, uint DcValueIndex);

    /// <summary>
    ///     Frees the specified local memory object and invalidates its handle.
    /// </summary>
    /// <param name="hMem">A handle to the local memory object.</param>
    /// <returns>
    ///     If the function succeeds, the return value is zero, and if the function fails, the return value is equal to a
    ///     handle to the local memory object.
    /// </returns>
    [DllImport("kernel32.dll", EntryPoint = "LocalFree")]
    private static extern nint LocalFree(nint hMem);

    [DllImport("powrprof.dll", EntryPoint = "PowerWriteSettingAttributes")]
    private static extern uint PowerWriteSettingAttributes(in Guid SubGroupOfPowerSettingsGuid,
        in Guid PowerSettingGuid, uint Attributes);

    #endregion
}