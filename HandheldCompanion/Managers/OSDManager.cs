using ControllerCommon.Managers;
using HandheldCompanion.Controls;
using PrecisionTiming;
using RTSSSharedMemoryNET;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using static ControllerCommon.WinAPI;
using static PInvoke.Kernel32;

namespace HandheldCompanion.Managers
{
    public static class OSDManager
    {
        public static bool IsEnabled;
        private static bool IsInitialized;

        private static int HookedProcessId;

        private static PrecisionTimer RefreshTimer;
        private static int RefreshInterval = 100;

        private static Dictionary<int, OSD> OnScreenDisplay = new();

        public static event InitializedEventHandler Initialized;
        public delegate void InitializedEventHandler();

        static OSDManager()
        {
            SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

            PlatformManager.RTSS.Hooked += RTSS_Hooked;
            PlatformManager.RTSS.Unhooked += RTSS_Unhooked;

            // timer used to monitor foreground application framerate
            RefreshInterval = SettingsManager.GetInt("OnScreenDisplayRefreshRate");
            RefreshTimer = new PrecisionTimer();
            RefreshTimer.SetAutoResetMode(true);
            RefreshTimer.SetResolution(0);
            RefreshTimer.SetPeriod(RefreshInterval);
            RefreshTimer.Tick += UpdateOSD;
        }

        private static void RTSS_Unhooked(int processId)
        {
            // clear previous display
            if (OnScreenDisplay is not null)
            {
                OnScreenDisplay[processId].Update(string.Empty);
                OnScreenDisplay[processId].Dispose();

                OnScreenDisplay.Remove(processId);
            }
        }

        private static void RTSS_Hooked(int processId)
        {
            if (!IsEnabled)
                return;

            ProcessEx processEx = ProcessManager.GetProcess(processId);
            if (processEx is null)
                return;

            OnScreenDisplay[processId] = new(processEx.Title);
            HookedProcessId = processId;
        }

        public static void Start()
        {
            IsInitialized = true;
            Initialized?.Invoke();

            LogManager.LogInformation("{0} has started", "OSDManager");
        }

        private static void UpdateOSD(object? sender, EventArgs e)
        {
            if (!IsEnabled)
                return;
            
            if (OnScreenDisplay.TryGetValue(HookedProcessId, out var OSD))
            {
                // temp (test)
                var FPS = PlatformManager.RTSS.GetInstantaneousFramerate();
                var ProfileName = ProfileManager.GetCurrent().Name;

                OSD.Update($"Profile: {ProfileName}\n{FPS} FPS");
            }
        }

        public static void Stop()
        {
            if (!IsInitialized)
                return;

            RefreshTimer.Stop();

            IsInitialized = false;

            LogManager.LogInformation("{0} has stopped", "OSDManager");
        }

        private static void SettingsManager_SettingValueChanged(string name, object value)
        {
            switch (name)
            {
                case "OnScreenDisplay":
                    IsEnabled = Convert.ToBoolean(value);

                    if (IsEnabled)
                    {
                        RefreshTimer.Start();
                    }
                    else
                    {
                        RefreshTimer.Stop();
                    }

                    break;
                case "OnScreenDisplayRefreshRate":
                    RefreshInterval = Convert.ToInt32(value);

                    RefreshTimer.Stop();
                    RefreshTimer.SetPeriod(RefreshInterval);

                    if (IsEnabled)
                        RefreshTimer.Start();
                    break;
            }
        }
    }
}
