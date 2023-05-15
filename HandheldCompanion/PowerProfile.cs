using System;
using System.Runtime.InteropServices;

namespace PowerProfileUtils
{
    public enum PowerIndexType
    {
        AC,
        DC
    }

    // For a reference on additional subgroup and setting GUIDs, run the command powercfg.exe /qh
    // This will list all hidden settings with both the names and GUIDs, descriptions, current values, and allowed values.

    public static class PowerSubGroup
    {
        public static Guid SUB_PROCESSOR = new("54533251-82be-4824-96c1-47b60b740d00");
    }

    public static class PowerSetting
    {
        public static Guid PERFBOOSTMODE = new("be337238-0d82-4146-a960-4f3749d470c7");
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

    public static class PowerProfile
    {
        #region imports
        /// <summary>
        /// Retrieves the active power scheme and returns a GUID that identifies the scheme.
        /// </summary>
        /// <param name="UserRootPowerKey">This parameter is reserved for future use and must be set to zero.</param>
        /// <param name="ActivePolicyGuid">A pointer that receives a pointer to a GUID structure. Use the LocalFree function to free this memory.</param>
        /// <returns>Returns zero if the call was successful, and a nonzero value if the call failed.</returns>
        [DllImportAttribute("powrprof.dll", EntryPoint = "PowerGetActiveScheme")]
        private static extern uint PowerGetActiveScheme(nint UserRootPowerKey, out nint ActivePolicyGuid);

        /// <summary>
        /// Sets the active power scheme for the current user.
        /// </summary>
        /// <param name="UserRootPowerKey">This parameter is reserved for future use and must be set to zero.</param>
        /// <param name="SchemeGuid">The identifier of the power scheme.</param>
        /// <returns>Returns zero if the call was successful, and a nonzero value if the call failed.</returns>
        [DllImportAttribute("powrprof.dll", EntryPoint = "PowerSetActiveScheme")]
        private static extern uint PowerSetActiveScheme(nint UserRootPowerKey, in Guid SchemeGuid);

        /// <summary>
        /// Retrieves the AC value index of the specified power setting.
        /// </summary>
        /// <param name="RootPowerKey">This parameter is reserved for future use and must be set to zero.</param>
        /// <param name="SchemeGuid">The identifier of the power scheme.</param>
        /// <param name="SubGroupOfPowerSettingsGuid">The subgroup of power settings.</param>
        /// <param name="PowerSettingGuid">The identifier of the power setting.</param>
        /// <param name="AcValueIndex">A pointer to a variable that receives the AC value index.</param>
        /// <returns>Returns zero if the call was successful, and a nonzero value if the call failed.</returns>
        [DllImportAttribute("powrprof.dll", EntryPoint = "PowerReadACValueIndex")]
        private static extern uint PowerReadACValueIndex(nint RootPowerKey, in Guid SchemeGuid, in Guid SubGroupOfPowerSettingsGuid, in Guid PowerSettingGuid, out uint AcValueIndex);

        /// <summary>
        /// Retrieves the DC value index of the specified power setting.
        /// </summary>
        /// <param name="RootPowerKey">This parameter is reserved for future use and must be set to zero.</param>
        /// <param name="SchemeGuid">The identifier of the power scheme.</param>
        /// <param name="SubGroupOfPowerSettingsGuid">The subgroup of power settings.</param>
        /// <param name="PowerSettingGuid">The identifier of the power setting.</param>
        /// <param name="DcValueIndex">A pointer to a variable that receives the DC value index.</param>
        /// <returns>Returns zero if the call was successful, and a nonzero value if the call failed.</returns>
        [DllImportAttribute("powrprof.dll", EntryPoint = "PowerReadDCValueIndex")]
        private static extern uint PowerReadDCValueIndex(nint RootPowerKey, in Guid SchemeGuid, in Guid SubGroupOfPowerSettingsGuid, in Guid PowerSettingGuid, out uint DcValueIndex);

        /// <summary>
        /// Sets the AC value index of the specified power setting.
        /// </summary>
        /// <param name="RootPowerKey">This parameter is reserved for future use and must be set to zero.</param>
        /// <param name="SchemeGuid">The identifier of the power scheme.</param>
        /// <param name="SubGroupOfPowerSettingsGuid">The subgroup of power settings.</param>
        /// <param name="PowerSettingGuid">The identifier of the power setting.</param>
        /// <param name="AcValueIndex">The AC value index.</param>
        /// <returns>Returns zero if the call was successful, and a nonzero value if the call failed.</returns>
        [DllImportAttribute("powrprof.dll", EntryPoint = "PowerWriteACValueIndex")]
        private static extern uint PowerWriteACValueIndex(nint RootPowerKey, in Guid SchemeGuid, in Guid SubGroupOfPowerSettingsGuid, in Guid PowerSettingGuid, uint AcValueIndex);

        /// <summary>
        /// Sets the DC value index of the specified power setting.
        /// </summary>
        /// <param name="RootPowerKey">This parameter is reserved for future use and must be set to zero.</param>
        /// <param name="SchemeGuid">The identifier of the power scheme.</param>
        /// <param name="SubGroupOfPowerSettingsGuid">The subgroup of power settings.</param>
        /// <param name="PowerSettingGuid">The identifier of the power setting.</param>
        /// <param name="DcValueIndex">The DC value index.</param>
        /// <returns>Returns zero if the call was successful, and a nonzero value if the call failed.</returns>
        [DllImportAttribute("powrprof.dll", EntryPoint = "PowerWriteDCValueIndex")]
        private static extern uint PowerWriteDCValueIndex(nint RootPowerKey, in Guid SchemeGuid, in Guid SubGroupOfPowerSettingsGuid, in Guid PowerSettingGuid, uint DcValueIndex);

        /// <summary>
        /// Frees the specified local memory object and invalidates its handle.
        /// </summary>
        /// <param name="hMem">A handle to the local memory object.</param>
        /// <returns>If the function succeeds, the return value is zero, and if the function fails, the return value is equal to a handle to the local memory object.</returns>
        [DllImportAttribute("kernel32.dll", EntryPoint = "LocalFree")]
        private static extern nint LocalFree(nint hMem);
        #endregion

        // Wrapper for the actual PowerGetActiveScheme. Converts GUID to the built-in type on output and handles the LocalFree call.
        /// <summary>
        /// Retrieves the active power scheme and returns a GUID that identifies the scheme.
        /// </summary>
        /// <param name="UserRootPowerKey">This parameter is reserved for future use and must be set to zero.</param>
        /// <param name="ActivePolicyGuid">A pointer that receives a GUID structure.</param>
        /// <returns>Returns zero if the call was successful, and a nonzero value if the call failed.</returns>
        private static uint PowerGetActiveScheme(nint UserRootPowerKey, out Guid ActivePolicyGuid)
        {
            nint activePolicyGuidPtr = nint.Zero;
            ActivePolicyGuid = Guid.Empty;

            uint result = PowerGetActiveScheme(UserRootPowerKey, out activePolicyGuidPtr);

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

        public static bool GetValue(PowerIndexType powerType, Guid SchemeGuid, Guid SubGroupOfPowerSettingsGuid, Guid PowerSettingGuid, out uint value)
        {
            switch (powerType)
            {
                case PowerIndexType.AC:
                    return PowerReadACValueIndex(nint.Zero, SchemeGuid, SubGroupOfPowerSettingsGuid, PowerSettingGuid, out value) == 0;
                case PowerIndexType.DC:
                    return PowerReadDCValueIndex(nint.Zero, SchemeGuid, SubGroupOfPowerSettingsGuid, PowerSettingGuid, out value) == 0;
            }

            value = 0;
            return false;
        }

        public static bool SetValue(PowerIndexType powerType, Guid SchemeGuid, Guid SubGroupOfPowerSettingsGuid, Guid PowerSettingGuid, uint value)
        {
            switch (powerType)
            {
                case PowerIndexType.AC:
                    return PowerWriteACValueIndex(nint.Zero, SchemeGuid, SubGroupOfPowerSettingsGuid, PowerSettingGuid, value) == 0;
                case PowerIndexType.DC:
                    return PowerWriteDCValueIndex(nint.Zero, SchemeGuid, SubGroupOfPowerSettingsGuid, PowerSettingGuid, value) == 0;
            }

            return false;
        }
    }
}
