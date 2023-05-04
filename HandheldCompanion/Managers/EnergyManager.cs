using ControllerCommon.Managers;
using HandheldCompanion.Controls;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using static ControllerCommon.WinAPI;
using static PInvoke.Kernel32;

namespace HandheldCompanion.Managers
{
    public static class EnergyManager
    {
        public static bool IsEnabled;
        private static bool IsInitialized;

        public static event InitializedEventHandler Initialized;
        public delegate void InitializedEventHandler();

        public enum EfficiencyMode
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
            IsEnabled = SettingsManager.GetBoolean("UseEnergyStar");
        }

        public static void Start()
        {
            ProcessManager.ProcessStarted += ProcessManager_ProcessStarted;
            ProcessManager.ForegroundChanged += ProcessManager_ForegroundChanged;

            SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

            IsInitialized = true;
            Initialized?.Invoke();

            LogManager.LogInformation("{0} has started", "EnergyManager");
        }

        public static void Stop()
        {
            if (!IsInitialized)
                return;

            IsInitialized = false;

            RestoreEfficiencyModes();

            LogManager.LogInformation("{0} has stopped", "EnergyManager");
        }

        private static void ProcessManager_ForegroundChanged(ProcessEx process, ProcessEx background)
        {
            if (!IsEnabled)
                return;

            // set efficiency mode to Eco on background(ed) process
            if (background is not null)
                ToggleEfficiencyMode(background, EfficiencyMode.Eco);

            // set efficency mode to High on foreground(ed) process
            if (process is not null)
                ToggleEfficiencyMode(process, EfficiencyMode.High);
        }

        private static void ProcessManager_ProcessStarted(ProcessEx process, bool OnStartup)
        {
            if (OnStartup || !IsEnabled)
                return;

            // do not set process QoS to Eco if is already in foreground
            if (process == ProcessManager.GetForegroundProcess())
            {
                ToggleEfficiencyMode(process, EfficiencyMode.High);
                return;
            }

            ToggleEfficiencyMode(process, EfficiencyMode.Eco);
        }

        private static void SettingsManager_SettingValueChanged(string name, object value)
        {
            switch (name)
            {
                case "UseEnergyStar":
                    {
                        IsEnabled = Convert.ToBoolean(value);

                        new Thread(() =>
                        {
                            switch (IsEnabled)
                            {
                                case true:
                                    ToggleEfficiencyModes();
                                    break;
                                case false:
                                    RestoreEfficiencyModes();
                                    break;
                            }
                        }).Start();
                    }
                    break;
            }
        }

        public static void ToggleEfficiencyModes()
        {
            ProcessEx foreground = ProcessManager.GetForegroundProcess();
            if (foreground is not null)
                ToggleEfficiencyMode(foreground, EfficiencyMode.High);

            Parallel.ForEach(ProcessManager.GetProcesses(), new ParallelOptions { MaxDegreeOfParallelism = PerformanceManager.MaxDegreeOfParallelism }, process =>
            {
                if (process == foreground)
                    return;

                if (process.Filter != ProcessEx.ProcessFilter.Allowed || process.IsSuspended())
                    return;

                ToggleEfficiencyMode(process, EfficiencyMode.Eco);
            });
        }

        public static void RestoreEfficiencyModes()
        {
            Parallel.ForEach(ProcessManager.GetProcesses(), new ParallelOptions { MaxDegreeOfParallelism = PerformanceManager.MaxDegreeOfParallelism }, process =>
            {
                ToggleEfficiencyMode(process, EfficiencyMode.High);
            });
        }

        public static void ToggleEfficiencyMode(ProcessEx process, EfficiencyMode mode, ProcessEx parent = null)
        {
            if (process.Filter != ProcessEx.ProcessFilter.Allowed || process.IsSuspended())
                return;

            int pId = process.GetProcessId();
            ToggleEfficiencyMode(pId, mode, parent);
        }

        public static void ToggleEfficiencyMode(int pId, EfficiencyMode mode, ProcessEx parent = null)
        {
            bool result = false;

            try
            {
                IntPtr hProcess = OpenProcess((uint)(ProcessAccessFlags.QueryLimitedInformation | ProcessAccessFlags.SetInformation), false, (uint)pId);

                switch (mode)
                {
                    case EfficiencyMode.High:
                        result = SwitchToHighQoS(hProcess);
                        break;
                    case EfficiencyMode.Eco:
                        result = SwitchToEcoQoS(hProcess);
                        break;
                    case EfficiencyMode.Default:
                        result = SwitchToDefaultQoS(hProcess);
                        break;
                }

                CloseHandle(hProcess);
            }
            catch { }

            // couldn't apply efficiency mode
            if (!result)
                return;

            // process is child
            if (parent is not null)
                return;

            ProcessEx processEx = ProcessManager.GetProcess(pId);
            if (processEx is null)
                return;

            // update efficiency mode (visual)
            processEx.SetEfficiencyMode(mode);
            LogManager.LogDebug("Process {0} and {1} subprocess(es) have efficiency mode set to: {2}", processEx.Title, processEx.Children.Count, mode);

            // apply efficiency mode to child processes
            processEx.RefreshChildProcesses();
            foreach (int childId in processEx.Children)
                ToggleEfficiencyMode(childId, mode, parent);
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

            if (infoType is not null)
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

            if (infoType is not null)
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
