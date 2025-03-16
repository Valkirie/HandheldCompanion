using HandheldCompanion.Controllers;
using HandheldCompanion.Helpers;
using HandheldCompanion.Shared;
using HandheldCompanion.Targets;
using HandheldCompanion.Utils;
using Nefarius.ViGEm.Client;
using System;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using static HandheldCompanion.Managers.ControllerManager;

namespace HandheldCompanion.Managers
{
    public static class VirtualManager
    {
        #region imports
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
        #endregion

        // controllers vars
        public static ViGEmClient vClient;
        public static ViGEmTarget vTarget;

        // dll vars
        private const string dllName = "vigemclient.dll";
        private static IntPtr Module = IntPtr.Zero;

        // drivers vars
        private const string driverName = "ViGEmBus";

        // settings vars
        public static HIDmode HIDmode = HIDmode.NoController;
        private static HIDmode defaultHIDmode = HIDmode.NoController;
        public static HIDstatus HIDstatus = HIDstatus.Disconnected;

        private static readonly SemaphoreSlim controllerLock = new SemaphoreSlim(1, 1);

        private static readonly Random ProductGenerator = new Random();
        public static ushort ProductId = 0x28E; // Xbox 360
        public static ushort VendorId = 0x45E;  // Microsoft

        public static bool IsInitialized;

        public static event ControllerSelectedEventHandler ControllerSelected;
        public delegate void ControllerSelectedEventHandler(HIDmode mode);

        public static event InitializedEventHandler Initialized;
        public delegate void InitializedEventHandler();

        public static event VibrateEventHandler Vibrated;
        public delegate void VibrateEventHandler(byte LargeMotor, byte SmallMotor);

        static VirtualManager()
        {
            // verifying ViGEm is installed
            try
            {
                vClient = new ViGEmClient();
                Module = GetModuleHandle(dllName);
            }
            catch (Exception)
            {
                LogManager.LogCritical("ViGEm is missing. Please get it from: {0}", "https://github.com/ViGEm/ViGEmBus/releases");
                MessageBox.Show("Unable to start Handheld Companion, the ViGEm application is missing.\n\nPlease get it from: https://github.com/ViGEm/ViGEmBus/releases", "Error");
                throw new InvalidOperationException();
            }
        }

        public static async void Start()
        {
            if (IsInitialized)
                return;

            // wait until drivers are fully loaded
            using (ServiceController sc = new ServiceController(driverName))
                while (sc.Status != ServiceControllerStatus.Running)
                    await Task.Delay(250).ConfigureAwait(false); // Avoid blocking the synchronization context

            // manage events
            ManagerFactory.settingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
            ManagerFactory.profileManager.Applied += ProfileManager_Applied;
            ManagerFactory.profileManager.Discarded += ProfileManager_Discarded;

            // raise events
            switch (ManagerFactory.settingsManager.Status)
            {
                default:
                case ManagerStatus.Initializing:
                    ManagerFactory.settingsManager.Initialized += SettingsManager_Initialized;
                    break;
                case ManagerStatus.Initialized:
                    QuerySettings();
                    break;
            }

            /*
            if (ManagerFactory.profileManager.IsInitialized)
            {
                ProfileManager_Applied(ManagerFactory.profileManager.GetCurrent(), UpdateSource.Background);
            }
            */

            IsInitialized = true;
            Initialized?.Invoke();

            LogManager.LogInformation("{0} has started", "VirtualManager");
        }

        private static void QuerySettings()
        {
            // Retrieve the default HID mode from settings
            HIDmode selectedHIDMode = (HIDmode)ManagerFactory.settingsManager.GetInt("HIDmode");

            // Check if ProfileManager is initialized and a valid profile is available
            if (ManagerFactory.profileManager.IsReady)
            {
                Profile currentProfile = ManagerFactory.profileManager.GetCurrent();
                if (currentProfile != null && currentProfile.HID != HIDmode.NotSelected)
                    selectedHIDMode = currentProfile.HID;
            }

            // load a few variables
            HIDstatus = (HIDstatus)ManagerFactory.settingsManager.GetInt("HIDstatus");

            // apply settings
            SettingsManager_SettingValueChanged("HIDmode", selectedHIDMode, false);
            SettingsManager_SettingValueChanged("HIDstatus", HIDstatus, false);
            SettingsManager_SettingValueChanged("DSUEnabled", ManagerFactory.settingsManager.GetString("DSUEnabled"), false);
        }

        private static void SettingsManager_Initialized()
        {
            QuerySettings();
        }

        public static void Stop()
        {
            if (!IsInitialized)
                return;

            Suspend(true);

            // manage events
            ManagerFactory.settingsManager.SettingValueChanged -= SettingsManager_SettingValueChanged;
            ManagerFactory.settingsManager.Initialized -= SettingsManager_Initialized;
            ManagerFactory.profileManager.Applied -= ProfileManager_Applied;
            ManagerFactory.profileManager.Discarded -= ProfileManager_Discarded;

            IsInitialized = false;

            LogManager.LogInformation("{0} has stopped", "VirtualManager");
        }

        public static void Resume(bool OS)
        {
            controllerLock.Wait();
            try
            {
                if (Module == IntPtr.Zero)
                    Module = LoadLibrary(dllName);

                // Create a new ViGEm client if needed
                if (vClient is null)
                    vClient = new ViGEmClient();

                if (OS)
                {
                    // Update DSU status
                    SetDSUStatus(ManagerFactory.settingsManager.GetBoolean("DSUEnabled"));
                }
            }
            finally
            {
                controllerLock.Release();
            }

            SetControllerMode(HIDmode, OS);
        }

        public static void Suspend(bool OS)
        {
            // Disconnect the controller first
            SetControllerMode(HIDmode.NoController);

            controllerLock.Wait();
            try
            {
                // Dispose of the ViGEm client and unload the module
                if (vClient is not null)
                {
                    vClient.Dispose();
                    vClient = null;

                    if (Module != IntPtr.Zero)
                    {
                        FreeLibrary(Module);
                        Module = IntPtr.Zero;
                    }
                }

                if (OS)
                {
                    // Halt DSU
                    SetDSUStatus(false);
                }
            }
            finally
            {
                controllerLock.Release();
            }
        }

        private static void SettingsManager_SettingValueChanged(string name, object value, bool temporary)
        {
            switch (name)
            {
                case "HIDmode":
                    {
                        // update variable
                        defaultHIDmode = (HIDmode)Convert.ToInt32(value);
                        SetControllerMode(defaultHIDmode);
                    }
                    break;
                case "HIDstatus":
                    {
                        // skip on cold boot, retrieved by Start() function and called by SetControllerMode()
                        if (ManagerFactory.settingsManager.IsReady)
                            SetControllerStatus((HIDstatus)Convert.ToInt32(value));
                    }
                    break;
                case "DSUEnabled":
                    SetDSUStatus(Convert.ToBoolean(value));
                    break;
            }
        }

        private static async void ProfileManager_Applied(Profile profile, UpdateSource source)
        {
            // SetControllerMode takes care of ignoring identical mode switching
            if (HIDmode == profile.HID || (profile.HID == HIDmode.NotSelected && HIDmode == defaultHIDmode))
                return;

            while (ControllerManager.managerStatus == ControllerManagerStatus.Busy)
                await Task.Delay(1000).ConfigureAwait(false); // Avoid blocking the synchronization context

            switch (profile.HID)
            {
                case HIDmode.Xbox360Controller:
                case HIDmode.DualShock4Controller:
                    SetControllerMode(profile.HID);
                    break;

                case HIDmode.NotSelected:
                    SetControllerMode(defaultHIDmode);
                    break;
            }
        }

        private static async void ProfileManager_Discarded(Profile profile, bool swapped, Profile nextProfile)
        {
            // don't bother discarding settings, new one will be enforce shortly
            if (swapped)
                return;

            while (ControllerManager.managerStatus == ControllerManagerStatus.Busy)
                await Task.Delay(1000).ConfigureAwait(false); // Avoid blocking the synchronization context

            // restore default HID mode
            if (profile.HID != HIDmode.NotSelected)
                SetControllerMode(defaultHIDmode);
        }

        private static void SetDSUStatus(bool started)
        {
            if (started)
                DSUServer.Start();
            else
                DSUServer.Stop();
        }

        public static void SetControllerMode(HIDmode mode, bool OS = false)
        {
            controllerLock.Wait();
            try
            {
                // If the requested mode is already active, do nothing
                if (HIDmode == mode)
                {
                    if (HIDstatus == HIDstatus.Connected && (vTarget is not null && vTarget.IsConnected))
                        return;
                    else if (HIDstatus == HIDstatus.Disconnected && (vTarget is null || !vTarget.IsConnected))
                        return;
                }

                // Disconnect and dispose the current virtual controller if it exists
                if (vTarget is not null)
                {
                    vTarget.Disconnect();
                    vTarget.Dispose();
                    vTarget = null;
                }

                // Sanity-check: if the ViGEm client isn’t available, abort
                if (vClient is null)
                    return;

                // Create a new target based on the requested mode
                switch (mode)
                {
                    case HIDmode.NoController:
                        // Nothing to initialize
                        break;

                    case HIDmode.DualShock4Controller:
                        vTarget = new DualShock4Target();
                        break;

                    case HIDmode.Xbox360Controller:
                        // Optionally generate a new ProductId unless running in OS mode
                        if (!OS) ProductId = (ushort)ProductGenerator.Next(1, ushort.MaxValue);
                        vTarget = new Xbox360Target(VendorId, ProductId);
                        break;
                }

                // Notify subscribers about the controller change
                ControllerSelected?.Invoke(mode);

                // If target creation failed, log an error (unless it's the NoController case)
                if (vTarget is null)
                {
                    if (mode != HIDmode.NoController)
                        LogManager.LogError("Failed to initialise virtual controller with HIDmode: {0}", mode);
                    return;
                }

                // Subscribe to target events
                vTarget.Connected += OnTargetConnected;
                vTarget.Disconnected += OnTargetDisconnected;
                vTarget.Vibrated += OnTargetVibrated;

                // Update the current mode
                HIDmode = mode;
            }
            finally
            {
                controllerLock.Release();
            }

            // Update controller status synchronously
            SetControllerStatus(HIDstatus);
        }

        public static void SetControllerStatus(HIDstatus status)
        {
            controllerLock.Wait();
            try
            {
                if (vTarget is null)
                    return;

                bool success = false;
                switch (status)
                {
                    case HIDstatus.Connected:
                        if (!vTarget.IsConnected)
                            success = vTarget.Connect();
                        break;
                    case HIDstatus.Disconnected:
                        if (vTarget.IsConnected)
                            success = vTarget.Disconnect();
                        break;
                }

                // Only update the internal status if the operation was successful
                if (success)
                    HIDstatus = status;
            }
            finally
            {
                controllerLock.Release();
            }
        }

        private static void OnTargetConnected(ViGEmTarget target)
        {
            ToastManager.SendToast($"{target}", "is now connected", $"controller_{(uint)target.HID}_1", true);
        }

        private static void OnTargetDisconnected(ViGEmTarget target)
        {
            ToastManager.SendToast($"{target}", "is now disconnected", $"controller_{(uint)target.HID}_0", true);
        }

        private static void OnTargetVibrated(byte LargeMotor, byte SmallMotor)
        {
            Vibrated?.Invoke(LargeMotor, SmallMotor);
        }

        public static void UpdateInputs(ControllerState controllerState, GamepadMotion gamepadMotion)
        {
            vTarget?.UpdateInputs(controllerState, gamepadMotion);
        }
    }
}