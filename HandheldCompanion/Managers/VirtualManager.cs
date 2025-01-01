using HandheldCompanion.Controllers;
using HandheldCompanion.Helpers;
using HandheldCompanion.Shared;
using HandheldCompanion.Targets;
using HandheldCompanion.Utils;
using HandheldCompanion.Views;
using Nefarius.ViGEm.Client;
using System;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Windows;
using static HandheldCompanion.Managers.ControllerManager;

namespace HandheldCompanion.Managers
{
    public static class VirtualManager
    {
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

        public static ushort ProductId = 0x28E; // Xbox 360
        public static ushort VendorId = 0x45E;  // Microsoft
        private static object threadLock = new();

        public static bool IsInitialized;

        public static event HIDChangedEventHandler HIDchanged;
        public delegate void HIDChangedEventHandler(HIDmode HIDmode);

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

                MainWindow.SplashScreen.Close();
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
            ProfileManager.Applied += ProfileManager_Applied;
            ProfileManager.Discarded += ProfileManager_Discarded;

            // raise events
            switch (ManagerFactory.settingsManager.Status)
            {
                case ManagerStatus.Initializing:
                    ManagerFactory.settingsManager.Initialized += SettingsManager_Initialized;
                    break;
                case ManagerStatus.Initialized:
                    QuerySettings();
                    break;
            }

            /*
            if (ProfileManager.IsInitialized)
            {
                ProfileManager_Applied(ProfileManager.GetCurrent(), UpdateSource.Background);
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
            if (ProfileManager.IsInitialized)
            {
                Profile currentProfile = ProfileManager.GetCurrent();
                if (currentProfile != null && currentProfile.HID != HIDmode.NotSelected)
                    selectedHIDMode = currentProfile.HID;
            }

            // load a few variables
            HIDstatus = (HIDstatus)ManagerFactory.settingsManager.GetInt("HIDstatus");

            // apply settings
            SettingsManager_SettingValueChanged("HIDmode", selectedHIDMode, false);
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
            ProfileManager.Applied -= ProfileManager_Applied;
            ProfileManager.Discarded -= ProfileManager_Discarded;

            IsInitialized = false;

            LogManager.LogInformation("{0} has stopped", "VirtualManager");
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        public static void Resume(bool OS)
        {
            lock (threadLock)
            {
                if (Module == IntPtr.Zero)
                    Module = LoadLibrary(dllName);

                // create new ViGEm client
                if (vClient is null)
                    vClient = new ViGEmClient();

                if (OS)
                {
                    // update DSU status
                    SetDSUStatus(ManagerFactory.settingsManager.GetBoolean("DSUEnabled"));
                }
            }

            // set controller mode
            SetControllerMode(HIDmode);
        }

        public static void Suspend(bool OS)
        {
            // dispose virtual controller
            SetControllerMode(HIDmode.NoController);

            lock (threadLock)
            {
                // dispose ViGEm drivers
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
                    // halt DSU
                    SetDSUStatus(false);
                }
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
                        if (ManagerFactory.settingsManager.Status == ManagerStatus.Initialized)
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

        private static async void ProfileManager_Discarded(Profile profile, bool swapped)
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

        public static void SetControllerMode(HIDmode mode)
        {
            lock (threadLock)
            {
                // do not disconnect if similar to previous mode and connected
                if (HIDmode == mode)
                {
                    if (HIDstatus == HIDstatus.Disconnected)
                        return;
                    else if (vTarget is not null && vTarget.IsConnected)
                        return;
                }

                // disconnect current virtual controller
                if (vTarget is not null)
                {
                    vTarget.Disconnect();
                    vTarget.Dispose();
                    vTarget = null;
                }

                // this shouldn't happen !
                // todo: improve the overall locking logic here
                if (vClient is null)
                    return;

                switch (mode)
                {
                    default:
                    case HIDmode.NoController:
                        // controller was disposed already above
                        break;

                    case HIDmode.DualShock4Controller:
                        vTarget = new DualShock4Target();
                        break;

                    case HIDmode.Xbox360Controller:
                        // Generate a new random ProductId to help the controller pick empty slot rather than getting its previous one
                        // VendorId = (ushort)new Random().Next(ushort.MinValue, ushort.MaxValue);
                        ProductId = (ushort)new Random().Next(ushort.MinValue, ushort.MaxValue);
                        vTarget = new Xbox360Target(VendorId, ProductId);
                        break;
                }

                ControllerSelected?.Invoke(mode);

                // failed to initialize controller
                if (vTarget is null)
                {
                    if (mode != HIDmode.NoController)
                        LogManager.LogError("Failed to initialise virtual controller with HIDmode: {0}", mode);

                    return;
                }

                vTarget.Connected += OnTargetConnected;
                vTarget.Disconnected += OnTargetDisconnected;
                vTarget.Vibrated += OnTargetVibrated;

                // update current HIDmode
                HIDmode = mode;
            }

            // update status
            SetControllerStatus(HIDstatus);
        }

        public static void SetControllerStatus(HIDstatus status)
        {
            lock (threadLock)
            {
                if (vTarget is null)
                    return;

                bool success = false;
                switch (status)
                {
                    default:
                    case HIDstatus.Connected:
                        if (!vTarget.IsConnected)
                            success = vTarget.Connect();
                        break;
                    case HIDstatus.Disconnected:
                        if (vTarget.IsConnected)
                            success = vTarget.Disconnect();
                        break;
                }

                // update current HIDstatus
                if (success)
                    HIDstatus = status;
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