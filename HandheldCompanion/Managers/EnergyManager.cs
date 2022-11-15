using ControllerCommon.Managers;
using HandheldCompanion.Managers.Classes;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using static ControllerCommon.WinAPI;
using static PInvoke.Kernel32;

namespace HandheldCompanion.Managers
{
    public static class EnergyManager
    {
        private static bool IsEnabled;
        private static bool IsLoaded;
        private static bool IsInitialized;

        public enum QualityOfServiceLevel
        {
            /// <summary>
            /// Default system managed behavior. Let system manage all power throttling. <br/>
            /// </summary>
            Default,

            /// <summary>
            /// Description: Windowed applications that are in the foreground and in focus, or audible, 
            /// and explicitly tag processes with SetProcessInformation or threads with SetThreadInformation. <br/>
            /// Performance and power: Standard high performance. <br/>
            /// Release: 1709 <br/>
            /// </summary>
            High,

            /// <summary>
            /// Description: Windowed applications that may be visible to the end user but are not in focus. <br/>
            /// Performance and power: Varies by platform, between High and Low. <br/>
            /// Release: 1709 <br/>
            /// </summary>
            Medium,

            /// <summary>
            /// Description: Windowed applications that are not visible or audible to the end user. <br/>
            /// Performance and power: On battery, selects most efficient CPU frequency and schedules to efficient core. <br/>
            /// Release: 1709 <br/>
            /// </summary>
            Low,

            /// <summary>
            /// Description: Applications that explicitly tag processes with SetProcessInformation or threads with SetThreadInformation. <br/>
            /// Performance and power: Always selects most efficient CPU frequency and schedules to efficient cores. <br/>
            /// Release: Windows 11 <br/>
            /// </summary>
            Eco,

            /// <summary>
            /// Description: Threads explicitly tagged by the Multimedia Class Scheduler Service to denote multimedia batch buffering. <br/>
            /// Performance and power: CPU frequency reduced for efficient batch processing. <br/>
            /// Release: 2004 <br/>
            /// </summary>
            Media,

            /// <summary>
            /// Description: Threads explicitly tagged by Multimedia Class Scheduler Service to denote that audio threads require performance to meet deadlines. <br/>
            /// Performance and power: High performance to meet media deadlines. <br/>
            /// Release: 2004 <br/>
            /// </summary>
            Deadline,
        }

        static EnergyManager()
        {
        }

        public static void Start()
        {
            ProcessManager.ProcessStarted += ProcessManager_ProcessStarted;
            ProcessManager.ForegroundChanged += ProcessManager_ForegroundChanged;

            SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

            IsInitialized = true;
        }

        public static void Stop()
        {
            if (!IsInitialized)
                return;

            RestoreDefaultEfficiency();
        }

        private static void ProcessManager_ForegroundChanged(ProcessEx processEx, ProcessEx backgroundEx)
        {
            if (!IsEnabled)
                return;

            // set efficiency mode to Eco on background(ed) process
            if (backgroundEx != null && !backgroundEx.IsIgnored && !backgroundEx.IsSuspended())
                ToggleEfficiencyMode(backgroundEx.Id, QualityOfServiceLevel.Eco);

            // set efficency mode to High on foreground(ed) process
            if (processEx != null && !processEx.IsIgnored && !processEx.IsSuspended())
                ToggleEfficiencyMode(processEx.Id, QualityOfServiceLevel.High);
        }

        private static void ProcessManager_ProcessStarted(ProcessEx processEx, bool startup)
        {
            if (!startup)
                return;

            if (!IsEnabled)
                return;

            // do not set process QoS to Eco if is already in foreground
            ProcessEx foregroundProcess = ProcessManager.GetForegroundProcess();
            if (processEx == foregroundProcess)
                return;

            ToggleEfficiencyMode(processEx.Id, QualityOfServiceLevel.Eco);
        }

        private static void SettingsManager_SettingValueChanged(string name, object value)
        {
            switch (name)
            {
                case "UseEnergyStar":
                    {
                        IsEnabled = Convert.ToBoolean(value);

                        if (!SettingsManager.IsInitialized)
                            return;

                        if (!IsEnabled)
                        {
                            // On EcoQoS disable: restore default behavior
                            RestoreDefaultEfficiency();
                        }
                        else
                        {
                            // On EcoQoS enable: apply QoS
                            ToggleAllEfficiencyMode();
                        }
                    }
                    break;
            }
        }

        public static void ToggleAllEfficiencyMode()
        {
            ProcessEx foregroundProcess = ProcessManager.GetForegroundProcess();
            ToggleEfficiencyMode(foregroundProcess.Id, QualityOfServiceLevel.High);

            foreach (ProcessEx processEx in ProcessManager.GetProcesses())
            {
                if (processEx == foregroundProcess)
                    continue;

                if (processEx.IsIgnored || processEx.IsSuspended())
                    continue;

                ToggleEfficiencyMode(processEx.Id, QualityOfServiceLevel.Eco);
            }
        }

        public static void RestoreDefaultEfficiency()
        {
            foreach (ProcessEx processEx in ProcessManager.GetProcesses().Where(item => !item.IsIgnored && !item.IsSuspended()))
                ToggleEfficiencyMode(processEx.Id, QualityOfServiceLevel.High);
        }

        public static void ToggleEfficiencyMode(int pId, QualityOfServiceLevel level, ProcessEx parent = null)
        {
            bool result = false;
            IntPtr hProcess = OpenProcess((uint)(ProcessAccessFlags.QueryLimitedInformation | ProcessAccessFlags.SetInformation), false, (uint)pId);

            switch (level)
            {
                case QualityOfServiceLevel.High:
                    result = SwitchToHighQoS(hProcess);
                    break;
                case QualityOfServiceLevel.Eco:
                    result = SwitchToEcoQoS(hProcess);
                    break;
                case QualityOfServiceLevel.Default:
                    result = SwitchToDefaultQoS(hProcess);
                    break;
            }

            try
            {
                CloseHandle(hProcess);
            }
            catch (Exception) { }

            // process failed or process is child
            if (!result || parent != null)
                return;

            ProcessEx processEx = ProcessManager.GetProcesses(pId);
            if (processEx is null)
                return;

            processEx.EcoQoS = level;

            // apply Efficiency Mode to Children(s)
            foreach (int childId in processEx.Children)
                ToggleEfficiencyMode(childId, level, parent);

            LogManager.LogDebug("Process {0} and {1} subprocess(es) have efficiency mode set to: {2}", processEx.Name, processEx.Children.Count, level);
        }

        public static bool GetProcessInfo(IntPtr handle, PROCESS_INFORMATION_CLASS piClass, out object processInfo)
        {
            Type infoType = null;
            switch (piClass)
            {
                case PROCESS_INFORMATION_CLASS.ProcessMemoryPriority:
                    infoType = typeof(MEMORY_PRIORITY_INFORMATION);
                    break;
                case PROCESS_INFORMATION_CLASS.ProcessPowerThrottling:
                    infoType = typeof(PROCESS_POWER_THROTTLING_STATE);
                    break;
                default:
                    break;
            }

            if (infoType != null)
            {
                int sizeOfProcessInfo = Marshal.SizeOf(infoType);
                var pProcessInfo = Marshal.AllocHGlobal(sizeOfProcessInfo);
                var result = GetProcessInformation(handle, piClass, pProcessInfo, sizeOfProcessInfo);
                processInfo = Marshal.PtrToStructure(pProcessInfo, infoType);
                Marshal.FreeHGlobal(pProcessInfo);
                return result != 0;
            }

            processInfo = null;
            return false;
        }

        public static bool SetProcessInfo(IntPtr handle, PROCESS_INFORMATION_CLASS piClass, object processInfo)
        {
            Type infoType = null;
            switch (piClass)
            {
                case PROCESS_INFORMATION_CLASS.ProcessMemoryPriority:
                    infoType = typeof(MEMORY_PRIORITY_INFORMATION);
                    break;
                case PROCESS_INFORMATION_CLASS.ProcessPowerThrottling:
                    infoType = typeof(PROCESS_POWER_THROTTLING_STATE);
                    break;
                default:
                    break;
            }

            if (infoType != null)
            {
                int sizeOfProcessInfo = Marshal.SizeOf(infoType);

                var pProcessInfo = Marshal.AllocHGlobal(sizeOfProcessInfo);
                Marshal.StructureToPtr(processInfo, pProcessInfo, false);
                var result = SetProcessInformation(handle, piClass, pProcessInfo, sizeOfProcessInfo);
                Marshal.FreeHGlobal(pProcessInfo);
                return result != 0;
            }

            return false;
        }

        // Let system manage all power throttling. ControlMask is set to 0 as we don’t want 
        // to control any mechanisms.
        private static bool SwitchToDefaultQoS(IntPtr Handle)
        {
            PROCESS_POWER_THROTTLING_STATE pi = new PROCESS_POWER_THROTTLING_STATE
            {
                Version = 0,
                ControlMask = 0,
                StateMask = 0
            };

            SetPriorityClass(Handle, (int)PriorityClass.NORMAL_PRIORITY_CLASS);
            return SetProcessInfo(Handle, PROCESS_INFORMATION_CLASS.ProcessPowerThrottling, pi);
        }

        // Turn EXECUTION_SPEED throttling on. 
        // ControlMask selects the mechanism and StateMask declares which mechanism should be on or off.
        private static bool SwitchToEcoQoS(IntPtr Handle)
        {
            PROCESS_POWER_THROTTLING_STATE pi = new PROCESS_POWER_THROTTLING_STATE
            {
                Version = 1,
                ControlMask = ProcessorPowerThrottlingFlags.PROCESS_POWER_THROTTLING_EXECUTION_SPEED,
                StateMask = ProcessorPowerThrottlingFlags.PROCESS_POWER_THROTTLING_EXECUTION_SPEED
            };

            SetPriorityClass(Handle, (int)PriorityClass.IDLE_PRIORITY_CLASS);
            return SetProcessInfo(Handle, PROCESS_INFORMATION_CLASS.ProcessPowerThrottling, pi);
        }

        // Turn EXECUTION_SPEED throttling off. 
        // ControlMask selects the mechanism and StateMask is set to zero as mechanisms should be turned off.
        private static bool SwitchToHighQoS(IntPtr Handle)
        {
            PROCESS_POWER_THROTTLING_STATE pi = new PROCESS_POWER_THROTTLING_STATE
            {
                Version = 1,
                ControlMask = ProcessorPowerThrottlingFlags.PROCESS_POWER_THROTTLING_EXECUTION_SPEED,
                StateMask = 0
            };

            SetPriorityClass(Handle, (int)PriorityClass.HIGH_PRIORITY_CLASS);
            return SetProcessInfo(Handle, PROCESS_INFORMATION_CLASS.ProcessPowerThrottling, pi);
        }
    }
}
