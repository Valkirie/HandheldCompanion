using ControllerCommon;
using ControllerCommon.Managers;
using HandheldCompanion.Views;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static ControllerCommon.Utils.ProcessUtils;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TrayNotify;

namespace HandheldCompanion.Managers
{
    public static class EnergyManager
    {
        private static bool IsEnabled;

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
            // set efficiency mode if event pId is different from foreground pId
            if (backgroundEx != null && !backgroundEx.Bypassed)
                ToggleEfficiencyMode(backgroundEx, false);

            // set efficency mode
            if (processEx != null && !processEx.Bypassed)
                ToggleEfficiencyMode(processEx, true);
        }

        private static void ProcessManager_ProcessStarted(ProcessEx processEx, bool startup)
        {
            if (startup)
                ToggleEfficiencyMode(processEx, false);
        }

        private static void SettingsManager_SettingValueChanged(string name, object value)
        {
            switch (name)
            {
                case "UseEnergyStar":
                    IsEnabled = Convert.ToBoolean(value);
                    break; 
            }
        }

        private static void ToggleEfficiencyMode(ProcessEx processEx, bool enable)
        {
            if (!IsEnabled)
                return;

            bool result;
            switch(enable)
            {
                case true:
                    result = SwitchToHighQoS(processEx.Process.Handle);
                    break;
                case false:
                    result = SwitchToEcoQoS(processEx.Process.Handle);
                    break;
            }

            if (result)
                LogManager.LogDebug("Process {0} has efficiency mode set to: {1}", processEx.Name, enable);
        }

        public static bool GetProcessInfo(IntPtr handle, WinAPI.PROCESS_INFORMATION_CLASS piClass, out object processInfo)
        {
            Type infoType = null;
            switch (piClass)
            {
                case WinAPI.PROCESS_INFORMATION_CLASS.ProcessMemoryPriority:
                    infoType = typeof(WinAPI.MEMORY_PRIORITY_INFORMATION);
                    break;
                case WinAPI.PROCESS_INFORMATION_CLASS.ProcessPowerThrottling:
                    infoType = typeof(WinAPI.PROCESS_POWER_THROTTLING_STATE);
                    break;
                default:
                    break;
            }

            if (infoType != null)
            {
                int sizeOfProcessInfo = Marshal.SizeOf(infoType);
                var pProcessInfo = Marshal.AllocHGlobal(sizeOfProcessInfo);
                var result = WinAPI.GetProcessInformation(handle, piClass, pProcessInfo, sizeOfProcessInfo);
                processInfo = Marshal.PtrToStructure(pProcessInfo, infoType);
                Marshal.FreeHGlobal(pProcessInfo);
                return result != 0;
            }

            processInfo = null;
            return false;
        }

        public static bool SetProcessInfo(IntPtr handle, WinAPI.PROCESS_INFORMATION_CLASS piClass, object processInfo)
        {
            Type infoType = null;
            switch (piClass)
            {
                case WinAPI.PROCESS_INFORMATION_CLASS.ProcessMemoryPriority:
                    infoType = typeof(WinAPI.MEMORY_PRIORITY_INFORMATION);
                    break;
                case WinAPI.PROCESS_INFORMATION_CLASS.ProcessPowerThrottling:
                    infoType = typeof(WinAPI.PROCESS_POWER_THROTTLING_STATE);
                    break;
                default:
                    break;
            }

            if (infoType != null)
            {
                int sizeOfProcessInfo = Marshal.SizeOf(infoType);

                var pProcessInfo = Marshal.AllocHGlobal(sizeOfProcessInfo);
                Marshal.StructureToPtr(processInfo, pProcessInfo, false);
                var result = WinAPI.SetProcessInformation(handle, piClass, pProcessInfo, sizeOfProcessInfo);
                Marshal.FreeHGlobal(pProcessInfo);
                return result != 0;
            }

            return false;
        }

        public static bool SwitchToEcoQoS(IntPtr Handle)
        {
            WinAPI.PROCESS_POWER_THROTTLING_STATE pi = new WinAPI.PROCESS_POWER_THROTTLING_STATE
            {
                Version = WinAPI.PROCESS_POWER_THROTTLING_CURRENT_VERSION,
                ControlMask = WinAPI.PROCESS_POWER_THROTTLING_EXECUTION_SPEED,
                StateMask = WinAPI.PROCESS_POWER_THROTTLING_EXECUTION_SPEED
            };

            return SetProcessInfo(Handle, WinAPI.PROCESS_INFORMATION_CLASS.ProcessPowerThrottling, pi);
        }

        public static bool SwitchToHighQoS(IntPtr Handle)
        {
            WinAPI.PROCESS_POWER_THROTTLING_STATE pi = new WinAPI.PROCESS_POWER_THROTTLING_STATE
            {
                Version = WinAPI.PROCESS_POWER_THROTTLING_CURRENT_VERSION,
                ControlMask = WinAPI.PROCESS_POWER_THROTTLING_EXECUTION_SPEED,
                StateMask = 0
            };

            return SetProcessInfo(Handle, WinAPI.PROCESS_INFORMATION_CLASS.ProcessPowerThrottling, pi);
        }
    }
}
