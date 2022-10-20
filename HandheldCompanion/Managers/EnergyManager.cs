using ControllerCommon;
using ControllerCommon.Managers;
using HandheldCompanion.Managers.Classes;
using HandheldCompanion.Views;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using static ControllerCommon.WinAPI;
using static PInvoke.Kernel32;

namespace HandheldCompanion.Managers
{
    public static class EnergyManager
    {
        private static bool IsEnabled;
        private static bool IsLoaded;

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
            MainWindow.processManager.ProcessStarted += ProcessManager_ProcessStarted;
            MainWindow.processManager.ForegroundChanged += ProcessManager_ForegroundChanged;

            SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
        }

        private static void ProcessManager_ForegroundChanged(ProcessEx processEx, ProcessEx backgroundEx)
        {
            if (!IsEnabled)
                return;

            // set efficiency mode if event pId is different from foreground pId
            if (backgroundEx != null && !backgroundEx.Bypassed)
                ToggleEfficiencyMode(backgroundEx, QualityOfServiceLevel.Eco);

            // set efficency mode
            if (processEx != null && !processEx.Bypassed)
                ToggleEfficiencyMode(processEx, QualityOfServiceLevel.High);
        }

        private static void ProcessManager_ProcessStarted(ProcessEx processEx, bool startup)
        {
            if (!startup)
                return;

            if (!IsEnabled)
                return;

            ToggleEfficiencyMode(processEx, QualityOfServiceLevel.Eco);
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
                            // restore default behavior when disabled
                            foreach (ProcessEx processEx in MainWindow.processManager.GetProcesses())
                                ToggleEfficiencyMode(processEx, QualityOfServiceLevel.High);
                            return;
                        }

                        // apply QoS when enabled
                        ProcessEx foregroundProcess = MainWindow.processManager.GetForegroundProcess();
                        foreach (ProcessEx processEx in MainWindow.processManager.GetProcesses())
                        {
                            if (processEx == foregroundProcess)
                                ToggleEfficiencyMode(processEx, QualityOfServiceLevel.High);

                            ToggleEfficiencyMode(processEx, QualityOfServiceLevel.Eco);
                        }
                    }
                    break;
            }
        }

        private static void ToggleEfficiencyMode(ProcessEx processEx, QualityOfServiceLevel level)
        {
            bool result = false;

            processEx.hProcesses.Clear();

            processEx.hProcesses.Add(OpenProcess((uint)(ProcessAccessFlags.QueryLimitedInformation | ProcessAccessFlags.SetInformation), false, (uint)processEx.Id));

            foreach (Process proc in ProcessEx.GetChildProcesses(processEx.Process))
                processEx.hProcesses.Add(OpenProcess((uint)ProcessAccessFlags.SetInformation, false, (uint)proc.Id));

            foreach (IntPtr hProcess in processEx.hProcesses)
            {
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

                CloseHandle(hProcess);
            }

            if (!result)
                return;

            processEx.QoL = level;
            LogManager.LogDebug("Process {0} has efficiency mode set to: {1}", processEx.Name, level);
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
        public static bool SwitchToDefaultQoS(IntPtr Handle)
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
        public static bool SwitchToEcoQoS(IntPtr Handle)
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
        public static bool SwitchToHighQoS(IntPtr Handle)
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
