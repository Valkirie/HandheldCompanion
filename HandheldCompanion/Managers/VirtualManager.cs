using HandheldCompanion.Controllers;
using HandheldCompanion.Helpers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Shared;
using HandheldCompanion.Targets;
using HandheldCompanion.Utils;
using Nefarius.ViGEm.Client;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using static HandheldCompanion.Managers.ControllerManager;

namespace HandheldCompanion.Managers
{
    public enum HIDBackend
    {
        ViGEM,
        VIIPER
    }

    public static class VirtualManager
    {
        // controllers vars
        public static ViGEmClient? vClient;
        public static VTarget? vTarget;

        // drivers vars
        private const string driverName = "ViGEmBus";

        // settings vars
        public static HIDmode HIDmode = HIDmode.NoController;
        private static HIDmode defaultHIDmode = HIDmode.NoController;
        public static HIDstatus HIDstatus = HIDstatus.Disconnected;
        public static HIDBackend HIDBackend = HIDBackend.ViGEM;

        private static readonly SemaphoreSlim controllerLock = new SemaphoreSlim(1, 1);

        public static ushort VendorId = 0x45E;
        public static ushort ProductId = 0x28E;

        private static readonly object temporaryControllerLock = new object();
        private static readonly List<VTarget> temporaryControllers = new List<VTarget>();
        private static ushort temporaryProductIdSeed = ProductId;

        // Sleep state tracking: when the system is in sleep mode, only report meaningful input changes
        // to prevent the virtual controller from waking the device with constant gyro reports.
        private static bool isSystemSleeping = false;

        // Xbox stick noise filter threshold: ignore axis value changes smaller than this
        private const short AxisNoiseThreshold = 140;

        // Trigger (L2/R2) noise filter threshold: much smaller since range is 0-255 vs sticks at ±32k
        private const short TriggerNoiseThreshold = 6;

        // ponytail: State caching for UpdateInputs deduplication. Only skips updates when inputs are unchanged
        // beyond noise thresholds. Gyro/motion always change, so we only cache button & axis state.
        private static ControllerState? prevControllerState = null;

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
        { }

        /// <summary>
        /// Initializes the ViGEm backend by ensuring the service is running and creating the client.
        /// </summary>
        /// <returns>True if ViGEm was successfully initialized, false otherwise.</returns>
        private static bool InitializeViGEm()
        {
            try
            {
                // Ensure the ViGEmBus service is running
                if (!EnsureViGEmServiceRunning())
                {
                    LogManager.LogWarning("Failed to start ViGEmBus service");
                    return false;
                }

                // Create the ViGEm client if not already created
                if (vClient is null)
                    vClient = new ViGEmClient();

                LogManager.LogInformation("ViGEm backend initialized successfully");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.LogWarning("Failed to initialize ViGEm backend: {0}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Uninitializes the ViGEm backend by disposing the client.
        /// </summary>
        private static void UninitializeViGEm()
        {
            try
            {
                // Dispose the ViGEm client
                if (vClient is not null)
                {
                    vClient.Dispose();
                    vClient = null;
                    LogManager.LogInformation("ViGEm client disposed");
                }
            }
            catch (Exception ex)
            {
                LogManager.LogWarning("Error during ViGEm uninitialization: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Ensures the ViGEmBus service is running, starting it if necessary.
        /// </summary>
        /// <returns>True if the service is running after this call, false otherwise.</returns>
        private static bool EnsureViGEmServiceRunning()
        {
            try
            {
                using (ServiceController sc = new ServiceController(driverName))
                {
                    // Check if service exists
                    if (sc.ServiceName != driverName)
                    {
                        LogManager.LogWarning("ViGEmBus service not found");
                        return false;
                    }

                    // If service is not running, try to start it
                    if (sc.Status != ServiceControllerStatus.Running)
                    {
                        LogManager.LogInformation("Starting ViGEmBus service...");
                        sc.Start();
                        sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(5));
                    }

                    return sc.Status == ServiceControllerStatus.Running;
                }
            }
            catch (Exception ex)
            {
                LogManager.LogWarning("Failed to ensure ViGEmBus service is running: {0}", ex.Message);
                return false;
            }
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

            // Initialize ViGEm backend if selected
            if (!InitializeViGEm())
            {
                LogManager.LogWarning("Failed to initialize ViGEm backend");
                HIDBackend = HIDBackend.VIIPER;
            }

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
            SettingsManager_SettingValueChanged("VIIPERPort", ManagerFactory.settingsManager.GetInt("VIIPERPort"), false, true);
            SettingsManager_SettingValueChanged("VIIPEREnabled", ManagerFactory.settingsManager.GetString("VIIPEREnabled"), false, true);
            SettingsManager_SettingValueChanged("DSUport", ManagerFactory.settingsManager.GetInt("DSUport"), false, true);
            SettingsManager_SettingValueChanged("DSUEnabled", ManagerFactory.settingsManager.GetString("DSUEnabled"), false, true);
            SettingsManager_SettingValueChanged("HIDmode", selectedHIDMode, false, true);
            SettingsManager_SettingValueChanged("HIDstatus", ManagerFactory.settingsManager.GetString("HIDstatus"), false, true);

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
            if (!controllerLock.Wait(3000))
                return;

            try
            {
                // Re-initialize ViGEm if we're using that backend
                if (!InitializeViGEm())
                    LogManager.LogWarning("Failed to re-initialize ViGEm backend");
            }
            catch (Exception ex)
            {
                LogManager.LogWarning("Error during ViGEm resume: {0}", ex.Message);
            }
            finally
            {
                controllerLock.Release();
            }

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

            if (!controllerLock.Wait(3000))
                return;

            try
            {
                // Uninitialize ViGEm
                UninitializeViGEm();
            }
            catch (Exception ex)
            {
                LogManager.LogWarning("Error during ViGEm suspend: {0}", ex.Message);
            }
            finally
            {
                controllerLock.Release();
            }

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

                        // skip if initializing
                        if (initializing)
                            return;

                        _ = SetControllerMode(defaultHIDmode);
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

                        _ = SetControllerStatus(selectedHIDstatus);
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
                    // don't restore controller if initializing
                    _ = SetVIIPERStatus(Convert.ToBoolean(value), restoreController: !initializing);
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

                VTarget target = CreateTemporaryControllerTarget();
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

        private static VTarget CreateTemporaryControllerTarget()
        {
            lock (temporaryControllerLock)
            {
                temporaryProductIdSeed++;
                if (temporaryProductIdSeed == 0)
                    temporaryProductIdSeed = 1;

                return HIDBackend == HIDBackend.ViGEM
                    ? new ViXbox360Target(VendorId, temporaryProductIdSeed)
                    : new Xbox360Target(VendorId, temporaryProductIdSeed);
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
                if (restoreController && ViiperServerManager.IsRunning && HIDstatus == HIDstatus.Connected)
                    await SetControllerMode(HIDmode).ConfigureAwait(false);
            }
            else
            {
                ViiperServerManager.Stop();
            }
        }

        private static bool CanUseControllerMode(HIDmode mode)
        {
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
            if (!await controllerLock.WaitAsync(3000).ConfigureAwait(false))
                return;

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
            if (!await controllerLock.WaitAsync(3000).ConfigureAwait(false))
                return;

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
                // Events will be cleared when target is disposed
                vTarget.Disconnect();
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
                    vTarget = HIDBackend == HIDBackend.ViGEM
                        ? new ViDualShock4Target(0x054C, 0x05C4)
                        : new DualShock4Target(0x054C, 0x05C4);
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
                    vTarget = HIDBackend == HIDBackend.ViGEM
                        ? new ViXbox360Target(VendorId, ProductId)
                        : new Xbox360Target(VendorId, ProductId);
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

            if (vTarget is VIIPERTarget && !CanUseControllerMode(mode))
            {
                HIDmode = mode;
                ControllerSelected?.Invoke(mode);
                NotifyMasterIntervalOverrideChanged();
                return;
            }

            vTarget.Connected += (t) => OnTargetConnected(t);
            vTarget.Disconnected += (t) => OnTargetDisconnected(t);
            vTarget.Vibrated += OnTargetVibrated;
            vTarget.StatusChanged += (t, status, attempt, maxAttempts) => OnTargetConnectStatusChanged(t, status, attempt, maxAttempts);

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

            if (vTarget is VIIPERTarget && !CanUseControllerMode(HIDmode))
                return;

            bool success = false;
            switch (status)
            {
                case HIDstatus.Connected:
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

        private static void OnTargetConnectStatusChanged(VTarget target, VirtualManagerStatus status, int attempt, int maxAttempts)
        {
            StatusChanged?.Invoke(status, attempt, maxAttempts);
        }

        private static void OnTargetConnected(VTarget target)
        {
            ToastManager.SendToast($"{target}", "is now connected"); //, $"controller_{(uint)target.HID}_1", true);
        }

        private static void OnTargetDisconnected(VTarget target)
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
        /// Compares two axis states with noise filter thresholds.
        /// Sticks use AxisNoiseThreshold (150); triggers (L2/R2) use TriggerNoiseThreshold (10) 
        /// since they operate in 0-255 range vs sticks in ±32k range.
        /// Returns true if any axis values differ by more than their respective threshold.
        /// </summary>
        private static bool AxisStateHasSignificantChange(AxisState? previous, AxisState current)
        {
            if (previous is null)
                return !current.IsEmpty();

            foreach (AxisFlags axis in AxisState.TrueAxis)
            {
                short prevValue = previous[axis];
                short currValue = current[axis];

                // Use different thresholds for triggers vs sticks
                short threshold = (axis == AxisFlags.L2 || axis == AxisFlags.R2)
                    ? TriggerNoiseThreshold
                    : AxisNoiseThreshold;

                if (Math.Abs(currValue - prevValue) > threshold)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Compares button states for exact equality.
        /// Buttons are digital, so no thresholding needed.
        /// </summary>
        private static bool ButtonStateHasChanged(ButtonState? previous, ButtonState current)
        {
            if (previous is null)
                return !current.IsEmpty();

            return !previous.Contains(current) || !current.Contains(previous);
        }

        /// <summary>
        /// Compares gyro states with loose thresholds to ignore sensor noise.
        /// Gyro/accel data changes constantly; we only detect large meaningful movements.
        /// ponytail: Ceiling—this compares all sensor states which is O(n). For strict dedup, 
        /// compare only the "active" sensor. Upgrade path: accept SensorState hint or cache last active.
        /// </summary>
        private static bool GyroStateHasSignificantChange(GyroState? previous, GyroState current)
        {
            if (previous is null)
                return true; // First update, always send

            // Loose thresholds: 1.0 deg/s for gyro, 0.2g for accel (human-perceptible movements)
            const float gyroThreshold = 1.0f;
            const float accelThreshold = 0.2f;

            // Check the default sensor state (most common case)
            var prevGyro = previous.GetGyroscope(GyroState.SensorState.Default);
            var currGyro = current.GetGyroscope(GyroState.SensorState.Default);
            var prevAccel = previous.GetAccelerometer(GyroState.SensorState.Default);
            var currAccel = current.GetAccelerometer(GyroState.SensorState.Default);

            // Simple distance check: if either gyro or accel vector moved significantly, report change
            var gyroDelta = currGyro - prevGyro;
            var accelDelta = currAccel - prevAccel;

            return gyroDelta.LengthSquared() > (gyroThreshold * gyroThreshold) ||
                   accelDelta.LengthSquared() > (accelThreshold * accelThreshold);
        }

        public static void UpdateInputs(ControllerState controllerState, GamepadMotion gamepadMotion)
        {
            // Skip sending inputs to virtual controller when listening for hotkey inputs
            if (InputsManager.IsListening)
                return;

            if (isSystemSleeping)
                return;

            // Deduplicate: skip if controller state hasn't changed meaningfully
            if (prevControllerState is not null)
            {
                bool buttonChanged = ButtonStateHasChanged(prevControllerState.ButtonState, controllerState.ButtonState);
                bool axisChanged = AxisStateHasSignificantChange(prevControllerState.AxisState, controllerState.AxisState);
                bool gyroChanged = GyroStateHasSignificantChange(prevControllerState.GyroState, controllerState.GyroState);

                if (!buttonChanged && !axisChanged && !gyroChanged)
                    return; // No significant change, skip update
            }

            vTarget?.UpdateInputs(controllerState, gamepadMotion);

            // Cache the current state for next tick
            prevControllerState = controllerState.Clone() as ControllerState;
        }
    }
}