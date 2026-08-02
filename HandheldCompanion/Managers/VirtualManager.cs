using HandheldCompanion.Controllers;
using HandheldCompanion.Helpers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Shared;
using HandheldCompanion.Targets;
using HandheldCompanion.Utils;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using static HandheldCompanion.Managers.ControllerManager;

namespace HandheldCompanion.Managers
{
    public static class VirtualManager
    {
        public static VIIPERTarget? vTarget;

        // settings vars
        public static HIDmode HIDmode = HIDmode.NoController;
        private static HIDmode defaultHIDmode = HIDmode.NoController;
        public static HIDstatus HIDstatus = HIDstatus.Disconnected;

        private static readonly SemaphoreSlim controllerLock = new SemaphoreSlim(1, 1);

        public static ushort VendorId = 0x45E;
        public static ushort ProductId = 0x28E;

        private static readonly object temporaryControllerLock = new object();
        private static readonly List<VIIPERTarget> temporaryControllers = new List<VIIPERTarget>();
        private static ushort temporaryProductIdSeed = ProductId;

        // Sleep state tracking: when the system is in sleep mode, only report meaningful input changes
        // to prevent the virtual controller from waking the device with constant gyro reports.
        private static bool isSystemSleeping = false;

        // Xbox stick noise filter threshold: ignore axis value changes smaller than this
        private const short AxisNoiseThreshold = 150;

        public static bool IsInitialized;

        public static event ControllerSelectedEventHandler? ControllerSelected;
        public delegate void ControllerSelectedEventHandler(HIDmode mode);

        public static event InitializedEventHandler? Initialized;
        public delegate void InitializedEventHandler();

        public static event VibrateEventHandler? Vibrated;
        public delegate void VibrateEventHandler(byte LargeMotor, byte SmallMotor);

        public static event ConnectStatusChangedEventHandler? StatusChanged;
        public delegate void ConnectStatusChangedEventHandler(VirtualManagerStatus status, int attempt, int maxAttempts);

        public static event MasterIntervalOverrideChangedEventHandler? MasterIntervalOverrideChanged;
        public delegate void MasterIntervalOverrideChangedEventHandler(int? overrideHz);

        static VirtualManager()
        {
        }

        public static int? GetMasterIntervalOverrideHz()
        {
            return vTarget?.MasterIntervalOverrideHz;
        }

        private static void NotifyMasterIntervalOverrideChanged()
        {
            MasterIntervalOverrideChanged?.Invoke(GetMasterIntervalOverrideHz());
        }

        public static async void Start()
        {
            if (IsInitialized)
                return;

            // manage events
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

        private static void SettingsManager_Initialized()
        {
            QuerySettings();
        }

        private static void QuerySettings()
        {
            // manage events
            ManagerFactory.settingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

            // raise events
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

            SettingsManager_SettingValueChanged("VIIPERPort", ManagerFactory.settingsManager.GetInt("VIIPERPort"), false, true);
            SettingsManager_SettingValueChanged("VIIPEREnabled", ManagerFactory.settingsManager.GetString("VIIPEREnabled"), false, true);
            SettingsManager_SettingValueChanged("DSUport", ManagerFactory.settingsManager.GetInt("DSUport"), false, true);
            SettingsManager_SettingValueChanged("DSUEnabled", ManagerFactory.settingsManager.GetString("DSUEnabled"), false, true);
            SettingsManager_SettingValueChanged("HIDmode", selectedHIDMode, false, true);
            SettingsManager_SettingValueChanged("HIDstatus", HIDstatus, false, true);

            SetVIIPERStatus(ManagerFactory.settingsManager.GetBoolean("VIIPEREnabled")).ConfigureAwait(false);
            SetControllerModeCore(defaultHIDmode);
        }

        public static async Task Stop()
        {
            if (!IsInitialized)
                return;

            await Suspend(true).ConfigureAwait(false);

            // manage events
            ManagerFactory.settingsManager.SettingValueChanged -= SettingsManager_SettingValueChanged;
            ManagerFactory.settingsManager.Initialized -= SettingsManager_Initialized;
            ManagerFactory.profileManager.Applied -= ProfileManager_Applied;
            ManagerFactory.profileManager.Discarded -= ProfileManager_Discarded;

            IsInitialized = false;

            LogManager.LogInformation("{0} has stopped", "VirtualManager");
        }

        public static async Task Resume(bool OS)
        {
            if (OS)
            {
                // Update DSU status
                SetDSUStatus(ManagerFactory.settingsManager.GetBoolean("DSUEnabled"));
                await SetVIIPERStatus(ManagerFactory.settingsManager.GetBoolean("VIIPEREnabled"), false).ConfigureAwait(false);
            }

            await SetControllerMode(HIDmode).ConfigureAwait(false);
        }

        public static async Task Suspend(bool OS)
        {
            // Disconnect the controller first
            await SetControllerMode(HIDmode.NoController).ConfigureAwait(false);

            if (OS)
            {
                // Halt DSU
                SetDSUStatus(false);
                await SetVIIPERStatus(false).ConfigureAwait(false);
            }
        }

        private static void SettingsManager_SettingValueChanged(string name, object? value, bool temporary, bool initializing)
        {
            switch (name)
            {
                case "HIDmode":
                    {
                        // update variable
                        defaultHIDmode = (HIDmode)Convert.ToInt32(value);

                        if (initializing)
                            return;

                        _ = Task.Run(() =>
                        {
                            SetControllerMode(defaultHIDmode).ConfigureAwait(false);
                        });
                    }
                    break;
                case "HIDstatus":
                    {
                        HIDstatus selectedHIDstatus = (HIDstatus)Convert.ToInt32(value);

                        if (initializing)
                        {
                            HIDstatus = selectedHIDstatus;
                            return;
                        }

                        _ = Task.Run(() =>
                        {
                            SetControllerStatus((HIDstatus)Convert.ToInt32(value)).ConfigureAwait(false);
                        });
                    }
                    break;
                case "DSUEnabled":
                    SetDSUStatus(Convert.ToBoolean(value));
                    break;
                case "DSUport":
                    if (DSUServer.IsInitialized)
                        DSUServer.Restart(Convert.ToInt32(value));
                    else
                        DSUServer.serverPort = Convert.ToInt32(value);
                    break;
                case "VIIPEREnabled":
                    {
                        if (initializing)
                            return;

                        _ = Task.Run(() =>
                        {
                            bool enabled = Convert.ToBoolean(value);
                            SetVIIPERStatus(enabled).ConfigureAwait(false);
                        });
                    }
                    break;
                case "VIIPERPort":
                    ViiperServerManager.SetPort(Convert.ToInt32(value));
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
                case HIDmode.NoController:
                case HIDmode.Xbox360Controller:
                case HIDmode.DualShock4Controller:
                case HIDmode.DualSenseController:
                case HIDmode.SteamDeckController:
                case HIDmode.SwitchProController:
                    await SetControllerMode(profile.HID).ConfigureAwait(false);
                    break;

                case HIDmode.NotSelected:
                    await SetControllerMode(defaultHIDmode).ConfigureAwait(false);
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
                await SetControllerMode(defaultHIDmode).ConfigureAwait(false);
        }

        public static int CreateTemporaryControllers(int maxCount = 4)
        {
            if (!ViiperServerManager.IsRunning)
                return 0;

            DisposeTemporaryControllers();

            int created = 0;
            for (int i = 0; i < XInputController.MaxControllers && created < maxCount; i++)
            {
                Controller controller = new Controller((UserIndex)i);
                if (controller.IsConnected)
                    continue;

                VIIPERTarget target = CreateTemporaryControllerTarget();
                if (!target.Connect())
                {
                    target.Dispose();
                    continue;
                }

                lock (temporaryControllerLock)
                    temporaryControllers.Add(target);

                created++;
            }

            return created;
        }

        public static void DisposeTemporaryControllers()
        {
            lock (temporaryControllerLock)
            {
                foreach (VIIPERTarget target in temporaryControllers)
                {
                    try
                    {
                        target.Disconnect();
                    }
                    catch { }

                    try
                    {
                        target.Dispose();
                    }
                    catch { }
                }

                temporaryControllers.Clear();
            }
        }

        private static VIIPERTarget CreateTemporaryControllerTarget()
        {
            lock (temporaryControllerLock)
            {
                temporaryProductIdSeed++;
                if (temporaryProductIdSeed == 0)
                    temporaryProductIdSeed = 1;

                return new Xbox360Target(VendorId, temporaryProductIdSeed);
            }
        }

        private static void SetDSUStatus(bool started)
        {
            if (started)
                DSUServer.Start();
            else
                DSUServer.Stop();
        }

        private static async Task SetVIIPERStatus(bool started, bool restoreController = true)
        {
            if (started)
            {
                ViiperServerManager.Start();
                if (restoreController && ViiperServerManager.IsRunning && IsViiperBackedMode(HIDmode) && HIDstatus == HIDstatus.Connected)
                    await SetControllerMode(HIDmode).ConfigureAwait(false);
            }
            else
            {
                ViiperServerManager.Stop();
            }
        }

        private static bool IsViiperBackedMode(HIDmode mode)
        {
            return mode == HIDmode.Xbox360Controller
                || mode == HIDmode.DualShock4Controller
                || mode == HIDmode.DualSenseController
                || mode == HIDmode.SteamDeckController
                || mode == HIDmode.SteamController
                || mode == HIDmode.SwitchProController
                || mode == HIDmode.Free;
        }

        private static bool CanUseControllerMode(HIDmode mode)
        {
            if (!IsViiperBackedMode(mode))
                return true;

            if (!ManagerFactory.settingsManager.GetBoolean("VIIPEREnabled"))
            {
                LogManager.LogInformation("Skipping {0}: VIIPER server is disabled", mode);
                return false;
            }

            if (!ViiperServerManager.IsRunning)
            {
                StatusChanged?.Invoke(VirtualManagerStatus.Failed, 1, 1);
                LogManager.LogWarning("Skipping {0}: VIIPER server is not running", mode);
                return false;
            }

            return true;
        }

        public static async Task SetControllerMode(HIDmode mode)
        {
            await controllerLock.WaitAsync().ConfigureAwait(false);

            try
            {
                SetControllerModeCore(mode);
            }
            catch { }
            finally
            {
                controllerLock.Release();
            }
        }

        public static async Task SetControllerStatus(HIDstatus status)
        {
            await controllerLock.WaitAsync().ConfigureAwait(false);

            try
            {
                SetControllerStatusCore(status);
            }
            catch { }
            finally
            {
                controllerLock.Release();
            }
        }

        private static void SetControllerModeCore(HIDmode mode)
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
                vTarget.Connected -= OnTargetConnected;
                vTarget.Disconnected -= OnTargetDisconnected;
                vTarget.Vibrated -= OnTargetVibrated;
                vTarget.StatusChanged -= OnTargetConnectStatusChanged;
                bool success = vTarget.Disconnect();
                vTarget.Dispose();
                vTarget = null;
                NotifyMasterIntervalOverrideChanged();
            }

            // Create a new target based on the requested mode
            switch (mode)
            {
                case HIDmode.NoController:
                    {
                        HIDmode = mode;
                        ControllerSelected?.Invoke(mode);
                        NotifyMasterIntervalOverrideChanged();
                        SetControllerStatusCore(HIDstatus);
                    }
                    return;

                case HIDmode.DualShock4Controller:
                    vTarget = new DualShock4Target(0x054C, 0x05C4); // DualShock 4 [CUH-ZCT1x]
                    break;

                case HIDmode.DualSenseController:
                    vTarget = new DualSenseTarget(0x054C, 0x0CE6); // DualSense wireless controller (PS5)
                    break;

                case HIDmode.SteamDeckController:
                    vTarget = new SteamDeckTarget(0x28DE, 0x1205); // StemDeck Controller
                    break;

                case HIDmode.SteamController:
                    vTarget = new SteamControllerTarget(0x28DE, 0x1102); // Valve Steam Controller (wired)
                    break;

                case HIDmode.SwitchProController:
                    vTarget = new SwitchProTarget(0x057E, 0x2069); // Nintendo Switch Pro 2 Controller
                    break;

                case HIDmode.Xbox360Controller:
                    vTarget = new Xbox360Target(VendorId, ProductId);
                    break;
            }

            // If target creation failed, log an error (unless it's the NoController case)
            if (vTarget is null)
            {
                if (mode != HIDmode.NoController)
                    LogManager.LogError("Failed to initialise virtual controller with HIDmode: {0}", mode);
                NotifyMasterIntervalOverrideChanged();
                return;
            }

            if (!CanUseControllerMode(mode))
            {
                HIDmode = mode;
                ControllerSelected?.Invoke(mode);
                NotifyMasterIntervalOverrideChanged();
                return;
            }

            // Subscribe to target events
            vTarget.Connected += OnTargetConnected;
            vTarget.Disconnected += OnTargetDisconnected;
            vTarget.Vibrated += OnTargetVibrated;
            vTarget.StatusChanged += OnTargetConnectStatusChanged;

            // Update the current mode
            HIDmode = mode;

            // Notify subscribers about the controller change
            ControllerSelected?.Invoke(mode);
            NotifyMasterIntervalOverrideChanged();

            SetControllerStatusCore(HIDstatus);
        }

        private static void SetControllerStatusCore(HIDstatus status)
        {
            if (vTarget is null)
            {
                if (status == HIDstatus.Disconnected)
                    HIDstatus = status;

                return;
            }

            bool success = false;
            switch (status)
            {
                case HIDstatus.Connected:
                    if (!CanUseControllerMode(HIDmode))
                        break;

                    success = vTarget.IsConnected || vTarget.Connect();
                    break;
                case HIDstatus.Disconnected:
                    success = !vTarget.IsConnected || vTarget.Disconnect();
                    break;
            }

            // Only update the internal status if the operation was successful
            if (success)
                HIDstatus = status;
        }

        private static void OnTargetConnectStatusChanged(VIIPERTarget target, VirtualManagerStatus status, int attempt, int maxAttempts)
        {
            StatusChanged?.Invoke(status, attempt, maxAttempts);
        }

        private static void OnTargetConnected(VIIPERTarget target)
        {
            ToastManager.SendToast($"{target}", "is now connected"); //, $"controller_{(uint)target.HID}_1", true);
        }

        private static void OnTargetDisconnected(VIIPERTarget target)
        {
            ToastManager.SendToast($"{target}", "is now disconnected"); //, $"controller_{(uint)target.HID}_0", true);
        }

        private static void OnTargetVibrated(byte LargeMotor, byte SmallMotor)
        {
            Vibrated?.Invoke(LargeMotor, SmallMotor);
        }

        /// <summary>
        /// Sets the system sleep state. When sleeping, UpdateInputs will only update the virtual
        /// controller if button state has changed, preventing gyro from waking the device.
        /// </summary>
        public static void SetSystemSleepState(bool sleeping)
        {
            isSystemSleeping = sleeping;
        }

        /// <summary>
        /// Compares two axis states with a noise filter threshold for Xbox mode.
        /// Returns true if axis values differ by more than the noise threshold.
        /// </summary>
        private static bool AxisStateHasSignificantChange(AxisState? previous, AxisState current)
        {
            if (previous is null)
                return !current.IsEmpty();

            foreach (AxisFlags axis in AxisState.TrueAxis)
            {
                short prevValue = previous[axis];
                short currValue = current[axis];

                if (Math.Abs(currValue - prevValue) > AxisNoiseThreshold)
                    return true;
            }

            return false;
        }

        public static void UpdateInputs(ControllerState controllerState, GamepadMotion gamepadMotion)
        {
            // Skip sending inputs to virtual controller when listening for hotkey inputs
            if (InputsManager.IsListening)
                return;

            if (isSystemSleeping)
                return;

            vTarget?.UpdateInputs(controllerState, gamepadMotion);
        }
    }
}