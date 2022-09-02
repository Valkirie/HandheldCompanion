using ControllerCommon.Managers;
using HandheldCompanion.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static ControllerCommon.Utils.ProcessUtils;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TrayNotify;

namespace HandheldCompanion.Managers
{
    public static class EnergyManager
    {
        #region structs
        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESS_POWER_THROTTLING_STATE
        {
            public const uint PROCESS_POWER_THROTTLING_CURRENT_VERSION = 1;

            public uint Version;
            public ProcessorPowerThrottlingFlags ControlMask;
            public ProcessorPowerThrottlingFlags StateMask;
        }
        #endregion

        // EnergyManager
        private static IntPtr pThrottleOn = IntPtr.Zero;
        private static IntPtr pThrottleOff = IntPtr.Zero;
        private static int szControlBlock = 0;
        private static bool IsEnabled;

        static EnergyManager()
        {
            szControlBlock = Marshal.SizeOf<PROCESS_POWER_THROTTLING_STATE>();
            pThrottleOn = Marshal.AllocHGlobal(szControlBlock);
            pThrottleOff = Marshal.AllocHGlobal(szControlBlock);

            var throttleState = new PROCESS_POWER_THROTTLING_STATE
            {
                Version = PROCESS_POWER_THROTTLING_STATE.PROCESS_POWER_THROTTLING_CURRENT_VERSION,
                ControlMask = ProcessorPowerThrottlingFlags.PROCESS_POWER_THROTTLING_EXECUTION_SPEED,
                StateMask = ProcessorPowerThrottlingFlags.PROCESS_POWER_THROTTLING_EXECUTION_SPEED,
            };

            var unthrottleState = new PROCESS_POWER_THROTTLING_STATE
            {
                Version = PROCESS_POWER_THROTTLING_STATE.PROCESS_POWER_THROTTLING_CURRENT_VERSION,
                ControlMask = ProcessorPowerThrottlingFlags.PROCESS_POWER_THROTTLING_EXECUTION_SPEED,
                StateMask = ProcessorPowerThrottlingFlags.None,
            };

            Marshal.StructureToPtr(throttleState, pThrottleOn, false);
            Marshal.StructureToPtr(unthrottleState, pThrottleOff, false);
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

            SetProcessInformation(processEx.Handle, PROCESS_INFORMATION_CLASS.ProcessPowerThrottling,
                enable ? pThrottleOn : pThrottleOff, (uint)szControlBlock);
            SetPriorityClass(processEx.Handle, enable ? PriorityClass.IDLE_PRIORITY_CLASS : PriorityClass.NORMAL_PRIORITY_CLASS);

            LogManager.LogDebug("Process {0} has efficiency mode set to: {1}", processEx.Name, enable);
        }
    }
}
