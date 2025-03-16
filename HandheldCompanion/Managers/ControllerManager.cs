using HandheldCompanion.Controllers;
using HandheldCompanion.Devices;
using HandheldCompanion.Helpers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Misc;
using HandheldCompanion.Platforms;
using HandheldCompanion.Shared;
using HandheldCompanion.Utils;
using HandheldCompanion.Views;
using HandheldCompanion.Views.Pages;
using Nefarius.Utilities.DeviceManagement.Drivers;
using Nefarius.Utilities.DeviceManagement.Extensions;
using Nefarius.Utilities.DeviceManagement.PnP;
using SharpDX.DirectInput;
using SharpDX.XInput;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Shell;
using Windows.UI;
using Windows.UI.ViewManagement;
using static HandheldCompanion.Utils.DeviceUtils;
using static JSL;
using DeviceType = SharpDX.DirectInput.DeviceType;
using DriverStore = HandheldCompanion.Helpers.DriverStore;
using Timer = System.Timers.Timer;

namespace HandheldCompanion.Managers;

public static class ControllerManager
{
    private static readonly ConcurrentDictionary<string, IController> Controllers = new();
    public static readonly ConcurrentDictionary<string, bool> PowerCyclers = new();

    private static Thread watchdogThread;
    private static bool watchdogThreadRunning;
    private static bool ControllerManagement;

    private static int ControllerManagementAttempts = 0;
    private const int ControllerManagementMaxAttempts = 4;

    private static readonly XInputController? defaultXInput = new(new() { isVirtual = true }) { isPlaceholder = true };
    private static readonly DS4Controller? defaultDS4 = new(new(), new() { isVirtual = true }) { isPlaceholder = true };

    public static bool HasTargetController => GetTarget() != null;

    private static IController? targetController;
    private static FocusedWindow focusedWindows = FocusedWindow.None;
    private static ProcessEx? foregroundProcess;
    private static bool ControllerMuted;
    private static SensorFamily sensorSelection = SensorFamily.None;

    private static object targetLock = new object();
    public static ControllerManagerStatus managerStatus = ControllerManagerStatus.Pending;

    private static Timer scenarioTimer = new(100) { AutoReset = false };
    private static Timer pickTimer = new(500) { AutoReset = false };

    public static bool IsInitialized;

    public enum ControllerManagerStatus
    {
        Pending = 0,
        Busy = 1,
        Succeeded = 2,
        Failed = 3,
    }

    public static void Start()
    {
        if (IsInitialized)
            return;

        // manage events
        ManagerFactory.deviceManager.XUsbDeviceArrived += XUsbDeviceArrived;
        ManagerFactory.deviceManager.XUsbDeviceRemoved += XUsbDeviceRemoved;
        ManagerFactory.deviceManager.HidDeviceArrived += HidDeviceArrived;
        ManagerFactory.deviceManager.HidDeviceRemoved += HidDeviceRemoved;
        ManagerFactory.settingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
        ManagerFactory.processManager.ForegroundChanged += ProcessManager_ForegroundChanged;
        UIGamepad.GotFocus += GamepadFocusManager_GotFocus;
        UIGamepad.LostFocus += GamepadFocusManager_LostFocus;
        VirtualManager.Vibrated += VirtualManager_Vibrated;
        MainWindow.uiSettings.ColorValuesChanged += OnColorValuesChanged;

        // manage device events
        IDevice.GetCurrent().KeyPressed += CurrentDevice_KeyPressed;
        IDevice.GetCurrent().KeyReleased += CurrentDevice_KeyReleased;

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

        switch (ManagerFactory.deviceManager.Status)
        {
            default:
            case ManagerStatus.Initializing:
                ManagerFactory.deviceManager.Initialized += DeviceManager_Initialized;
                break;
            case ManagerStatus.Initialized:
                QueryDevices();
                break;
        }

        switch (ManagerFactory.processManager.Status)
        {
            default:
            case ManagerStatus.Initializing:
                ManagerFactory.processManager.Initialized += ProcessManager_Initialized;
                break;
            case ManagerStatus.Initialized:
                QueryForeground();
                break;
        }

        // prepare timer(s)
        scenarioTimer.Elapsed += ScenarioTimer_Elapsed;
        scenarioTimer.Start();

        pickTimer.Elapsed += PickTimer_Elapsed;
        pickTimer.Start();

        // enable HidHide
        HidHide.SetCloaking(true);

        // Summon an empty controller, used to feed Layout UI and receive injected inputs from keyboard/OEM chords
        // TODO: Consider refactoring this for better design
        Controllers[string.Empty] = GetDefault();
        PickTargetController();

        IsInitialized = true;
        Initialized?.Invoke();

        LogManager.LogInformation("{0} has started", "ControllerManager");
    }

    public static void Stop()
    {
        if (!IsInitialized)
            return;

        // Flushing possible JoyShocks...
        JslDisconnect();

        // unplug on close
        ClearTargetController();

        // manage events
        ManagerFactory.deviceManager.XUsbDeviceArrived -= XUsbDeviceArrived;
        ManagerFactory.deviceManager.XUsbDeviceRemoved -= XUsbDeviceRemoved;
        ManagerFactory.deviceManager.HidDeviceArrived -= HidDeviceArrived;
        ManagerFactory.deviceManager.HidDeviceRemoved -= HidDeviceRemoved;
        ManagerFactory.deviceManager.Initialized -= DeviceManager_Initialized;
        ManagerFactory.settingsManager.SettingValueChanged -= SettingsManager_SettingValueChanged;
        ManagerFactory.settingsManager.Initialized -= SettingsManager_Initialized;
        ManagerFactory.processManager.ForegroundChanged -= ProcessManager_ForegroundChanged;
        ManagerFactory.processManager.Initialized -= ProcessManager_Initialized;

        UIGamepad.GotFocus -= GamepadFocusManager_GotFocus;
        UIGamepad.LostFocus -= GamepadFocusManager_LostFocus;
        VirtualManager.Vibrated -= VirtualManager_Vibrated;
        MainWindow.uiSettings.ColorValuesChanged -= OnColorValuesChanged;

        // manage device events
        IDevice.GetCurrent().KeyPressed -= CurrentDevice_KeyPressed;
        IDevice.GetCurrent().KeyReleased -= CurrentDevice_KeyReleased;

        // stop timer
        scenarioTimer.Elapsed -= ScenarioTimer_Elapsed;
        scenarioTimer.Stop();

        bool HIDuncloakonclose = ManagerFactory.settingsManager.GetBoolean("HIDuncloakonclose");
        foreach (IController controller in GetPhysicalControllers<IController>())
        {
            // uncloak on close, if requested
            if (HIDuncloakonclose)
                controller.Unhide(false);

            // dispose controller
            // controller.Dispose();
        }

        IsInitialized = false;

        LogManager.LogInformation("{0} has stopped", "ControllerManager");
    }

    private static void OnColorValuesChanged(UISettings sender, object args)
    {
        Color _systemAccent = MainWindow.uiSettings.GetColorValue(UIColorType.AccentDark1);
        targetController?.SetLightColor(_systemAccent.R, _systemAccent.G, _systemAccent.B);
    }

    [Flags]
    private enum FocusedWindow
    {
        None,
        MainWindow,
        Quicktools
    }

    private static void GamepadFocusManager_LostFocus(string Name)
    {
        switch (Name)
        {
            default:
            case "MainWindow":
                focusedWindows &= ~FocusedWindow.MainWindow;
                break;
            case "QuickTools":
                focusedWindows &= ~FocusedWindow.Quicktools;
                break;
        }

        // check applicable scenarios
        CheckControllerScenario();
    }

    private static void GamepadFocusManager_GotFocus(string Name)
    {
        switch (Name)
        {
            default:
            case "MainWindow":
                focusedWindows |= FocusedWindow.MainWindow;
                break;
            case "QuickTools":
                focusedWindows |= FocusedWindow.Quicktools;
                break;
        }

        // check applicable scenarios
        CheckControllerScenario();
    }

    private static void ProcessManager_ForegroundChanged(ProcessEx? processEx, ProcessEx? backgroundEx)
    {
        // update current process
        foregroundProcess = processEx;

        // check applicable scenarios
        CheckControllerScenario();
    }

    private static void CurrentDevice_KeyReleased(ButtonFlags button)
    {
        // calls current controller (if connected)
        targetController?.InjectButton(button, false, true);
    }

    private static void CurrentDevice_KeyPressed(ButtonFlags button)
    {
        // calls current controller (if connected)
        targetController?.InjectButton(button, true, false);
    }

    private static void ScenarioTimer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        // set flag
        ControllerMuted = false;

        // Steam Deck specific scenario
        if (IDevice.GetCurrent() is SteamDeck steamDeck)
        {
            bool IsExclusiveMode = ManagerFactory.settingsManager.GetBoolean("SteamControllerMode");

            // Making sure current controller is embedded
            if (targetController is NeptuneController neptuneController)
            {
                // We're busy, come back later
                if (neptuneController.IsBusy)
                    return;

                if (IsExclusiveMode)
                {
                    // mode: exclusive
                    // hide embedded controller
                    if (!neptuneController.IsHidden())
                        neptuneController.Hide();
                }
                else
                {
                    // mode: hybrid
                    if (foregroundProcess?.Platform == PlatformType.Steam)
                    {
                        // application is either steam or a steam game
                        // restore embedded controller and mute virtual controller
                        if (neptuneController.IsHidden())
                            neptuneController.Unhide();

                        // set flag
                        ControllerMuted = true;
                    }
                    else
                    {
                        // application is not steam related
                        // hide embbeded controller
                        if (!neptuneController.IsHidden())
                            neptuneController.Hide();
                    }
                }

                // halt timer
                scenarioTimer.Stop();
            }
        }

        // either main window or quicktools are focused
        if (focusedWindows != FocusedWindow.None)
            ControllerMuted = true;
    }

    private static void CheckControllerScenario()
    {
        // reset timer
        scenarioTimer.Stop();
        scenarioTimer.Start();
    }

    private static void SettingsManager_SettingValueChanged(string name, object value, bool temporary)
    {
        switch (name)
        {
            case "VibrationStrength":
                uint VibrationStrength = Convert.ToUInt32(value);
                targetController?.SetVibrationStrength(VibrationStrength, ManagerFactory.settingsManager.IsReady);
                break;

            case "ControllerManagement":
                {
                    ControllerManagement = Convert.ToBoolean(value);
                    switch (ControllerManagement)
                    {
                        case true:
                            {
                                StartWatchdog();
                            }
                            break;
                        case false:
                            {
                                StopWatchdog();
                                UpdateStatus(ControllerManagerStatus.Pending);
                            }
                            break;
                    }
                }
                break;

            case "SensorSelection":
                sensorSelection = (SensorFamily)Convert.ToInt32(value);
                break;

            case "SteamControllerMode":
                CheckControllerScenario();
                break;
        }
    }

    private static void SettingsManager_Initialized()
    {
        QuerySettings();
    }

    private static void QuerySettings()
    {
        SettingsManager_SettingValueChanged("VibrationStrength", ManagerFactory.settingsManager.GetString("VibrationStrength"), false);
        SettingsManager_SettingValueChanged("ControllerManagement", ManagerFactory.settingsManager.GetString("ControllerManagement"), false);
        SettingsManager_SettingValueChanged("SensorSelection", ManagerFactory.settingsManager.GetString("SensorSelection"), false);
        SettingsManager_SettingValueChanged("SteamControllerMode", ManagerFactory.settingsManager.GetString("SteamControllerMode"), false);
    }

    private static void DeviceManager_Initialized()
    {
        QueryDevices();
    }

    private static void QueryDevices()
    {
        foreach (PnPDetails? device in ManagerFactory.deviceManager.PnPDevices.Values)
        {
            if (device.isXInput)
                XUsbDeviceArrived(device, device.InterfaceGuid);
            else if (device.isGaming)
                HidDeviceArrived(device, device.InterfaceGuid);
        }
    }

    private static void ProcessManager_Initialized()
    {
        QueryForeground();
    }

    private static void QueryForeground()
    {
        ProcessManager_ForegroundChanged(ProcessManager.GetForegroundProcess(), null);
    }

    private static bool IsOS = false;
    public static void Resume(bool OS)
    {
        // update flag
        IsOS = OS;

        if (ManagerFactory.settingsManager.GetBoolean("ControllerManagement"))
            StartWatchdog();

        PickTargetController();
    }

    public static void Suspend(bool OS)
    {
        if (ManagerFactory.settingsManager.GetBoolean("ControllerManagement"))
            StopWatchdog();

        ClearTargetController();
    }

    public static void StartWatchdog()
    {
        if (watchdogThreadRunning)
            return;

        watchdogThreadRunning = true;
        watchdogThread = new Thread(watchdogThreadLoop) { IsBackground = true };
        watchdogThread.Start();
    }

    public static void StopWatchdog()
    {
        if (watchdogThread is null)
            return;

        watchdogThreadRunning = false;
        if (watchdogThread.IsAlive)
            watchdogThread.Join();
    }

    private static void VirtualManager_Vibrated(byte LargeMotor, byte SmallMotor)
    {
        targetController?.SetVibration(LargeMotor, SmallMotor);
    }

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> DeviceLocks = new();

    private static async Task<SemaphoreSlim> GetDeviceLock(string deviceId)
    {
        return DeviceLocks.GetOrAdd(deviceId, _ => new SemaphoreSlim(1, 1));
    }

    private static void CleanupDeviceLock(string deviceId)
    {
        if (DeviceLocks.TryGetValue(deviceId, out var semaphore) && semaphore.CurrentCount == 1)
        {
            DeviceLocks.TryRemove(deviceId, out _);
            semaphore.Dispose();
        }
    }

    private static async void HidDeviceArrived(PnPDetails details, Guid InterfaceGuid)
    {
        if (!details.isGaming) return;

        var deviceLock = await GetDeviceLock(details.baseContainerDeviceInstanceId);
        await deviceLock.WaitAsync();

        try
        {
            Controllers.TryGetValue(details.baseContainerDeviceInstanceId, out IController controller);
            PowerCyclers.TryGetValue(details.baseContainerDeviceInstanceId, out bool IsPowerCycling);

            int connectedJoys = -1;
            int joyShockId = -1;
            JOY_SETTINGS settings = new();
            DateTime timeout = DateTime.Now.Add(TimeSpan.FromSeconds(4));

            while (DateTime.Now < timeout && connectedJoys == -1)
            {
                try
                {
                    connectedJoys = JslConnectDevices();
                }
                catch
                {
                    await Task.Delay(1000).ConfigureAwait(false);
                }
            }

            if (connectedJoys > 0)
            {
                int[] joysHandle = new int[connectedJoys];
                JslGetConnectedDeviceHandles(joysHandle, connectedJoys);

                foreach (int i in joysHandle)
                {
                    settings = JslGetControllerInfoAndSettings(i);
                    string joyShockpath = settings.path;
                    string detailsPath = details.devicePath;

                    if (detailsPath.Equals(joyShockpath, StringComparison.InvariantCultureIgnoreCase))
                    {
                        joyShockId = i;
                        break;
                    }
                }
            }

            if (joyShockId != -1)
            {
                settings.playerNumber = joyShockId;
                JOY_TYPE joyShockType = (JOY_TYPE)JslGetControllerType(joyShockId);

                if (controller != null)
                {
                    ((JSController)controller).AttachDetails(details);
                    ((JSController)controller).AttachJoySettings(settings);

                    if (controller.IsHidden()) controller.Hide(false);
                    IsPowerCycling = true;
                }
                else
                {
                    switch (joyShockType)
                    {
                        case JOY_TYPE.DualSense:
                            controller = new DualSenseController(settings, details);
                            break;
                        case JOY_TYPE.DualShock4:
                            controller = new DS4Controller(settings, details);
                            break;
                        case JOY_TYPE.ProController:
                            controller = new ProController(settings, details);
                            break;
                    }
                }
            }
            else
            {
                // DInput
                DirectInput directInput = new DirectInput();
                int VendorId = details.VendorID;
                int ProductId = details.ProductID;

                // initialize controller vars
                Joystick joystick = null;

                // search for the plugged controller
                foreach (DeviceInstance? deviceInstance in directInput.GetDevices(DeviceType.Gamepad, DeviceEnumerationFlags.AllDevices))
                {
                    try
                    {
                        // Instantiate the joystick
                        Joystick lookup_joystick = new Joystick(directInput, deviceInstance.InstanceGuid);
                        string SymLink = ManagerFactory.deviceManager.SymLinkToInstanceId(lookup_joystick.Properties.InterfacePath, InterfaceGuid.ToString());

                        if (SymLink.Equals(details.SymLink, StringComparison.InvariantCultureIgnoreCase))
                        {
                            joystick = lookup_joystick;
                            break;
                        }
                    }
                    catch
                    {
                    }
                }

                if (joystick is not null)
                {
                    // supported controller
                    VendorId = joystick.Properties.VendorId;
                    ProductId = joystick.Properties.ProductId;
                }
                else
                {
                    // unsupported controller
                    LogManager.LogWarning("Couldn't find matching DInput controller: VID:{0} and PID:{1}", details.GetVendorID(), details.GetProductID());
                }

                if (controller is not null)
                {
                    controller.AttachDetails(details);

                    // hide or unhide "new" InstanceID (HID)
                    if (controller.GetInstanceId() != details.deviceInstanceId)
                    {
                        if (controller.IsHidden())
                            controller.Hide(false);
                        else
                            controller.Unhide(false);
                    }

                    // force set flag
                    IsPowerCycling = true;
                    PowerCyclers[details.baseContainerDeviceInstanceId] = IsPowerCycling;
                }
                else
                {
                    // search for a supported controller
                    switch (VendorId)
                    {
                        // STEAM
                        case 0x28DE:
                            {
                                switch (ProductId)
                                {
                                    // WIRED STEAM CONTROLLER
                                    case 0x1102:
                                        // MI == 0 is virtual keyboards
                                        // MI == 1 is virtual mouse
                                        // MI == 2 is controller proper
                                        // No idea what's in case of more than one controller connected
                                        if (details.GetMI() == 2)
                                            controller = new GordonController(details);
                                        break;
                                    // WIRELESS STEAM CONTROLLER
                                    case 0x1142:
                                        // MI == 0 is virtual keyboards
                                        // MI == 1-4 are 4 controllers
                                        // TODO: The dongle registers 4 controller devices, regardless how many are
                                        // actually connected. There is no easy way to check for connection without
                                        // actually talking to each controller. Handle only the first for now.
                                        if (details.GetMI() == 1)
                                            controller = new GordonController(details);
                                        break;

                                    // STEAM DECK
                                    case 0x1205:
                                        controller = new NeptuneController(details);
                                        break;
                                }
                            }
                            break;

                        // NINTENDO
                        case 0x057E:
                            {
                                switch (ProductId)
                                {
                                    // Nintendo Wireless Gamepad
                                    case 0x2009:
                                        break;
                                }
                            }
                            break;

                        // LENOVO
                        case 0x17EF:
                            {
                                switch (ProductId)
                                {
                                    case 0x6184:
                                        break;
                                }
                            }
                            break;
                    }
                }
            }

            if (controller == null)
            {
                LogManager.LogWarning("Unsupported Generic controller: VID:{0} and PID:{1}", details.GetVendorID(), details.GetProductID());
                return;
            }

            while (!controller.IsReady && controller.IsConnected())
                await Task.Delay(250).ConfigureAwait(false);

            controller.IsBusy = false;
            string path = controller.GetContainerInstanceId();
            Controllers[path] = controller;

            LogManager.LogInformation("Generic controller {0} plugged", controller.ToString());
            ControllerPlugged?.Invoke(controller, IsPowerCycling);
            ToastManager.SendToast(controller.ToString(), "detected");

            PickTargetController();
            PowerCyclers.TryRemove(controller.GetContainerInstanceId(), out _);
        }
        finally
        {
            deviceLock.Release();
            CleanupDeviceLock(details.baseContainerDeviceInstanceId);
        }
    }

    private static async void HidDeviceRemoved(PnPDetails details, Guid InterfaceGuid)
    {
        var deviceLock = await GetDeviceLock(details.baseContainerDeviceInstanceId);
        await deviceLock.WaitAsync();

        try
        {
            IController controller = null;

            DateTime timeout = DateTime.Now.Add(TimeSpan.FromSeconds(10));
            while (DateTime.Now < timeout && controller == null)
            {
                if (Controllers.TryGetValue(details.baseContainerDeviceInstanceId, out controller))
                    break;

                await Task.Delay(100).ConfigureAwait(false);
            }

            if (controller == null) return;

            if (controller is XInputController) return;

            if (controller is JSController)
            {
                try
                {
                    JslDisconnect(controller.GetUserIndex());
                }
                catch { }
            }

            PowerCyclers.TryGetValue(details.baseContainerDeviceInstanceId, out bool IsPowerCycling);
            bool WasTarget = IsTargetController(controller.GetInstanceId());

            LogManager.LogInformation("Generic controller {0} unplugged, cycling {1}", controller.ToString(), IsPowerCycling);
            ControllerUnplugged?.Invoke(controller, IsPowerCycling, WasTarget);

            if (!IsPowerCycling)
            {
                controller.Gone();

                if (controller.IsPhysical())
                    controller.Unhide(false);

                if (WasTarget)
                {
                    ClearTargetController();
                    PickTargetController();
                }
                else
                {
                    controller.Dispose();
                }

                Controllers.TryRemove(details.baseContainerDeviceInstanceId, out _);
            }
        }
        finally
        {
            deviceLock.Release();
            CleanupDeviceLock(details.baseContainerDeviceInstanceId);
        }
    }

    private static async void XUsbDeviceArrived(PnPDetails details, Guid InterfaceGuid)
    {
        var deviceLock = await GetDeviceLock(details.baseContainerDeviceInstanceId);
        await deviceLock.WaitAsync();

        try
        {
            Controllers.TryGetValue(details.baseContainerDeviceInstanceId, out IController controller);
            PowerCyclers.TryGetValue(details.baseContainerDeviceInstanceId, out bool IsPowerCycling);

            if (details.XInputUserIndex == (byte)UserIndex.Any)
                details.XInputUserIndex = (byte)XInputController.TryGetUserIndex(details);

            if (controller != null)
            {
                ((XInputController)controller).AttachDetails(details);
                ((XInputController)controller).AttachController(details.XInputUserIndex);

                if (controller.GetInstanceId() != details.deviceInstanceId)
                {
                    if (controller.IsHidden()) controller.Hide(false);
                    else controller.Unhide(false);
                }

                IsPowerCycling = true;
                PowerCyclers[details.baseContainerDeviceInstanceId] = IsPowerCycling;
            }
            else
            {
                switch (details.GetVendorID())
                {
                    default:
                        controller = new XInputController(details);
                        break;

                    // LegionGo
                    case "0x17EF":
                        controller = new LegionController(details);
                        break;

                    // GameSir
                    case "0x3537":
                        {
                            switch (details.GetProductID())
                            {
                                // Tarantula Pro (Dongle)
                                case "0x1099":
                                case "0x103E":
                                    details.isDongle = true;
                                    goto case "0x1050";
                                // Tarantula Pro
                                default:
                                case "0x1050":
                                    controller = new TatantulaProController(details);
                                    break;
                            }
                        }
                        break;
                }
            }

            if (controller == null)
            {
                LogManager.LogWarning("Unsupported XInput controller: VID:{0} and PID:{1}", details.GetVendorID(), details.GetProductID());
                return;
            }

            while (!controller.IsReady && controller.IsConnected())
                await Task.Delay(250).ConfigureAwait(false);

            controller.IsBusy = false;
            string path = details.baseContainerDeviceInstanceId;
            Controllers[path] = controller;

            LogManager.LogInformation("XInput controller {0} plugged", controller.ToString());
            ControllerPlugged?.Invoke(controller, IsPowerCycling);
            ToastManager.SendToast(controller.ToString(), "detected");

            PickTargetController();
            PowerCyclers.TryRemove(controller.GetContainerInstanceId(), out _);
        }
        finally
        {
            deviceLock.Release();
            CleanupDeviceLock(details.baseContainerDeviceInstanceId);
        }
    }

    private static async void XUsbDeviceRemoved(PnPDetails details, Guid InterfaceGuid)
    {
        var deviceLock = await GetDeviceLock(details.baseContainerDeviceInstanceId);
        await deviceLock.WaitAsync();

        try
        {
            IController controller = null;

            DateTime timeout = DateTime.Now.Add(TimeSpan.FromSeconds(10));
            while (DateTime.Now < timeout && controller == null)
            {
                if (Controllers.TryGetValue(details.baseContainerDeviceInstanceId, out controller))
                    break;

                await Task.Delay(100).ConfigureAwait(false);
            }

            if (controller == null) return;

            PowerCyclers.TryGetValue(details.baseContainerDeviceInstanceId, out bool IsPowerCycling);
            bool WasTarget = IsTargetController(controller.GetInstanceId());

            LogManager.LogInformation("XInput controller {0} unplugged, cycling {1}", controller.ToString(), IsPowerCycling);
            ControllerUnplugged?.Invoke(controller, IsPowerCycling, WasTarget);

            if (!IsPowerCycling)
            {
                controller.Gone();

                if (controller.IsPhysical())
                    controller.Unhide(false);

                if (WasTarget)
                {
                    ClearTargetController();
                    PickTargetController();
                }
                else
                {
                    controller.Dispose();
                }

                Controllers.TryRemove(details.baseContainerDeviceInstanceId, out _);
            }
        }
        finally
        {
            deviceLock.Release();
            CleanupDeviceLock(details.baseContainerDeviceInstanceId);
        }
    }

    private static void watchdogThreadLoop(object? obj)
    {
        HashSet<byte> UserIndexes = [];
        bool XInputDrunk = false;

        // monitoring unexpected slot changes
        while (watchdogThreadRunning)
        {
            // clear array
            UserIndexes.Clear();
            XInputDrunk = false;

            foreach (XInputController xInputController in Controllers.Values.Where(controller => controller.IsXInput() && !controller.isPlaceholder))
            {
                byte UserIndex = ManagerFactory.deviceManager.GetXInputIndexAsync(xInputController.GetContainerPath(), true);

                // controller is not ready yet
                if (UserIndex == byte.MaxValue)
                    continue;

                // two controllers can't use the same slot
                if (!UserIndexes.Add(UserIndex))
                    XInputDrunk = true;

                xInputController.AttachController(UserIndex);
            }

            /*
            if (XInputDrunk)
            {
                // (re)init controller userIndex
                foreach (XInputController xInputController in Controllers.Values.Where(controller => controller.IsXInput() && !controller.isPlaceholder))
                    xInputController.AttachController(byte.MaxValue);
            }
            */

            if (XInputDrunk)
            {
                // suspend and resume virtual controller
                VirtualManager.Suspend(false);
                Thread.Sleep(1000);
                VirtualManager.Resume(IsOS);
            }

            // user is emulating an Xbox360Controller
            if (VirtualManager.HIDmode == HIDmode.Xbox360Controller && VirtualManager.HIDstatus == HIDstatus.Connected)
            {
                if (HasPhysicalController<XInputController>())
                {
                    // check if first controller is virtual
                    XInputController controller = GetControllerFromSlot<XInputController>(UserIndex.One, false);
                    if (controller is null)
                    {
                        if (ControllerManagementAttempts < ControllerManagementMaxAttempts)
                        {
                            UpdateStatus(ControllerManagerStatus.Busy);

                            bool HasBusyWireless = false;
                            bool HasCyclingController = false;

                            // do we have a pending wireless controller ?
                            XInputController wirelessController = GetPhysicalControllers<XInputController>().FirstOrDefault(controller => controller.IsBluetooth() && controller.IsBusy);
                            if (wirelessController is not null)
                            {
                                // update busy flag
                                HasBusyWireless = true;

                                // is the controller power cycling ?
                                PowerCyclers.TryGetValue(wirelessController.GetContainerInstanceId(), out HasCyclingController);
                                if (HasBusyWireless && !HasCyclingController && ControllerManagementAttempts != 0)
                                    goto Exit;
                            }

                            // suspend all physical controllers
                            foreach (XInputController xInputController in GetPhysicalControllers<XInputController>())
                                SuspendController(xInputController.GetContainerInstanceId());

                            // suspend and resume virtual controller
                            VirtualManager.Suspend(false);
                            Thread.Sleep(1000);
                            VirtualManager.Resume(IsOS);

                            // resume all physical controllers, after a few seconds
                            Thread.Sleep(1000);
                            ResumeControllers();

                            // increment attempt counter (if no wireless controller is power cycling)
                            if (!(HasBusyWireless && HasCyclingController))
                                ControllerManagementAttempts++;
                        }
                        else
                        {
                            // disable controller management if it has failed too many times
                            // resume all physical controllers
                            ResumeControllers();

                            UpdateStatus(ControllerManagerStatus.Failed);
                            ControllerManagementAttempts = 0;
                            IsOS = false;

                            ManagerFactory.settingsManager.SetProperty("ControllerManagement", false);
                        }
                    }
                    else if (managerStatus != ControllerManagerStatus.Succeeded)
                    {
                        // resume all physical controllers
                        ResumeControllers();

                        // give us one extra loop to make sure we're good
                        UpdateStatus(ControllerManagerStatus.Succeeded);
                        ControllerManagementAttempts = 0;
                        IsOS = false;
                    }
                }
            }

        Exit:
            Thread.Sleep(2000);
        }
    }

    private static void UpdateStatus(ControllerManagerStatus status)
    {
        switch (status)
        {
            case ControllerManagerStatus.Busy:
                MainWindow.GetCurrent().UpdateTaskbarState(TaskbarItemProgressState.Indeterminate);
                break;
            case ControllerManagerStatus.Succeeded:
            case ControllerManagerStatus.Failed:
                MainWindow.GetCurrent().UpdateTaskbarState(TaskbarItemProgressState.None);
                break;
            case ControllerManagerStatus.Pending:
                MainWindow.GetCurrent().UpdateTaskbarState(TaskbarItemProgressState.Paused);
                break;
        }

        managerStatus = status;
        StatusChanged?.Invoke(status, ControllerManagementAttempts);
    }

    private static void PickTimer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        lock (targetLock)
        {
            IEnumerable<IController> controllers = GetPhysicalControllers<IController>();

            // Pick the most recently arrived external or wireless controller
            IController? latestExternalController = controllers
                .Where(c => c.IsExternal() || c.IsWireless())
                .OrderByDescending(c => c.GetLastArrivalDate())
                .FirstOrDefault();

            // Pick the internal controller (built-in, non-removable)
            IController? internalController = controllers
                .FirstOrDefault(c => c.IsInternal());

            string deviceInstanceId = string.Empty;

            if (latestExternalController != null)
            {
                // Only replace the current controller if it's not a wireless (can be internal) or external controller
                if (targetController != null && (targetController.IsWireless() || targetController.IsExternal()))
                    deviceInstanceId = targetController.GetContainerInstanceId();
                else
                    deviceInstanceId = latestExternalController.GetContainerInstanceId();
            }
            // Fallback: if no external/wireless controller is available, use an internal controller (if present)
            else if (internalController != null)
            {
                deviceInstanceId = internalController.GetContainerInstanceId();
            }

            // Check if the chosen controller is power cycling
            PowerCyclers.TryGetValue(deviceInstanceId, out bool isPowerCycling);
            SetTargetController(deviceInstanceId, isPowerCycling);
        }
    }

    private static void ClearTargetController()
    {
        lock (targetLock)
        {
            // unplug previous controller
            if (targetController is not null)
            {
                targetController.InputsUpdated -= UpdateInputs;
                targetController.SetLightColor(0, 0, 0);
                targetController.Unplug();
                targetController = null;

                // update HIDInstancePath
                ManagerFactory.settingsManager.SetProperty("HIDInstancePath", string.Empty);
            }
        }
    }

    public static void PickTargetController()
    {
        pickTimer.Stop();
        pickTimer.Start();
    }

    public static void SetTargetController(string baseContainerDeviceInstanceId, bool IsPowerCycling)
    {
        lock (targetLock)
        {
            // look for new controller
            if (!Controllers.TryGetValue(baseContainerDeviceInstanceId, out IController controller))
                return;

            // already self
            if (IsTargetController(controller.GetInstanceId()))
                return;

            // clear current target
            ClearTargetController();

            // update target controller
            targetController = controller;
            targetController.InputsUpdated += UpdateInputs;
            targetController.Plug();

            Color _systemAccent = MainWindow.uiSettings.GetColorValue(UIColorType.AccentDark1);
            targetController.SetLightColor(_systemAccent.R, _systemAccent.G, _systemAccent.B);

            // update HIDInstancePath
            ManagerFactory.settingsManager.SetProperty("HIDInstancePath", baseContainerDeviceInstanceId);

            if (!IsPowerCycling && !controller.IsVirtual())
            {
                if (ManagerFactory.settingsManager.GetBoolean("HIDcloakonconnect"))
                {
                    bool powerCycle = true;

                    if (targetController is LegionController)
                    {
                        // todo:    Look for a byte within hid report that'd tend to mean both controllers are synced.
                        //          Then I guess we could try and power cycle them.
                        powerCycle = !((LegionController)targetController).IsWireless();
                    }

                    if (!targetController.IsHidden())
                        targetController.Hide(powerCycle);
                }
            }

            // check applicable scenarios
            CheckControllerScenario();

            // check if controller is about to power cycle
            PowerCyclers.TryGetValue(baseContainerDeviceInstanceId, out IsPowerCycling);

            string ManufacturerName = MotherboardInfo.Manufacturer.ToUpper();
            switch (ManufacturerName)
            {
                case "AOKZOE":
                case "ONE-NETBOOK TECHNOLOGY CO., LTD.":
                case "ONE-NETBOOK":
                    targetController.Rumble();
                    break;
                default:
                    if (ManagerFactory.settingsManager.GetBoolean("HIDvibrateonconnect") && !IsPowerCycling)
                        targetController.Rumble();
                    break;
            }

            ControllerSelected?.Invoke(targetController);
        }
    }

    public static bool SuspendController(string baseContainerDeviceInstanceId)
    {
        try
        {
            PnPDevice pnPDevice = null;

            DateTime timeout = DateTime.Now.Add(TimeSpan.FromSeconds(3));
            while (DateTime.Now < timeout && pnPDevice is null)
            {
                pnPDevice = PnPDevice.GetDeviceByInstanceId(baseContainerDeviceInstanceId);
                Task.Delay(100).Wait();
            }

            if (pnPDevice is null)
                return false;

            UsbPnPDevice usbPnPDevice = pnPDevice.ToUsbPnPDevice();
            if (usbPnPDevice is null)
                return false;

            DriverMeta pnPDriver = null;
            try
            {
                // get current driver
                pnPDriver = pnPDevice.GetCurrentDriver();
            }
            catch { }

            string enumerator = pnPDevice.GetProperty<string>(DevicePropertyKey.Device_EnumeratorName);
            switch (enumerator)
            {
                case "USB":
                    if (!string.IsNullOrEmpty(pnPDriver?.InfPath))
                    {
                        // store driver to collection
                        DriverStore.AddOrUpdateDriverStore(baseContainerDeviceInstanceId, pnPDriver.InfPath);

                        pnPDevice.InstallNullDriver(out bool rebootRequired);
                        usbPnPDevice.CyclePort();

                        PowerCyclers[baseContainerDeviceInstanceId] = true;
                    }
                    return true;
            }
        }
        catch { }

        return false;
    }

    public static bool ResumeControllers()
    {
        // loop through controllers
        foreach (string baseContainerDeviceInstanceId in DriverStore.GetPaths())
        {
            try
            {
                PnPDevice pnPDevice = null;

                DateTime timeout = DateTime.Now.Add(TimeSpan.FromSeconds(3));
                while (DateTime.Now < timeout && pnPDevice is null)
                {
                    pnPDevice = PnPDevice.GetDeviceByInstanceId(baseContainerDeviceInstanceId);
                    Task.Delay(100).Wait();
                }

                if (pnPDevice is null)
                    continue;

                DriverMeta pnPDriver = null;
                try
                {
                    // get current driver
                    pnPDriver = pnPDevice.GetCurrentDriver();
                }
                catch { }

                string enumerator = pnPDevice.GetProperty<string>(DevicePropertyKey.Device_EnumeratorName);
                switch (enumerator)
                {
                    case "USB":
                        {
                            // todo: check PnPDevice PID/VID to deploy the appropriate inf
                            string InfPath = DriverStore.GetDriverFromDriverStore(baseContainerDeviceInstanceId);
                            if (pnPDriver?.InfPath != InfPath && !string.IsNullOrEmpty(InfPath))
                            {
                                pnPDevice.RemoveAndSetup();
                                pnPDevice.InstallCustomDriver(InfPath, out bool rebootRequired);
                            }

                            // remove device from store
                            DriverStore.RemoveFromDriverStore(baseContainerDeviceInstanceId);

                            PowerCyclers[baseContainerDeviceInstanceId] = false;
                        }
                        return true;
                }
            }
            catch { }
        }

        return false;
    }

    public static IController GetTarget()
    {
        return targetController;
    }

    public static IController GetTargetOrDefault()
    {
        return targetController is not null ? targetController : GetDefault();
    }

    public static bool IsTargetController(string InstanceId)
    {
        return targetController?.GetInstanceId() == InstanceId;
    }

    public static bool HasPhysicalController<T>() where T : IController
    {
        return GetPhysicalControllers<T>().Any(c => c is T);
    }

    public static bool HasVirtualController<T>() where T : IController
    {
        return GetVirtualControllers<T>().Any(c => c is T);
    }

    public static IEnumerable<T> GetPhysicalControllers<T>() where T : IController
    {
        return Controllers.Values.Where(a => a is T && !a.IsVirtual() && !a.isPlaceholder).Cast<T>();
    }

    public static IEnumerable<T> GetVirtualControllers<T>() where T : IController
    {
        return Controllers.Values.Where(a => a is T && a.IsVirtual() && !a.isPlaceholder).Cast<T>();
    }

    public static T GetControllerFromSlot<T>(UserIndex userIndex = 0, bool physical = true) where T : IController
    {
        return Controllers.Values.FirstOrDefault(c => c is T && ((physical && c.IsPhysical()) || (!physical && c.IsVirtual())) && c.GetUserIndex() == (int)userIndex) as T;
    }

    public static IEnumerable<T> GetControllers<T>() where T : IController
    {
        return Controllers.Values.Where(a => a is T).Cast<T>();
    }

    private static ControllerState mutedState = new ControllerState();
    private static void UpdateInputs(ControllerState controllerState, Dictionary<byte, GamepadMotion> gamepadMotions, float deltaTimeSeconds, byte gamepadIndex)
    {
        // raise event
        InputsUpdated?.Invoke(controllerState);

        // get main motion
        GamepadMotion gamepadMotion = gamepadMotions[gamepadIndex];

        switch (sensorSelection)
        {
            case SensorFamily.Windows:
            case SensorFamily.SerialUSBIMU:
                gamepadMotion = IDevice.GetCurrent().GamepadMotion;
                SensorsManager.UpdateReport(controllerState, gamepadMotion, ref deltaTimeSeconds);
                break;
        }

        // compute motion
        if (gamepadMotion is not null)
        {
            MotionManager.UpdateReport(controllerState, gamepadMotion);
            MainWindow.overlayModel.UpdateReport(controllerState, gamepadMotion, deltaTimeSeconds);
        }

        // compute layout
        controllerState = ManagerFactory.layoutManager.MapController(controllerState);
        InputsUpdated2?.Invoke(controllerState);

        // controller is muted
        if (ControllerMuted)
        {
            mutedState.ButtonState[ButtonFlags.Special] = controllerState.ButtonState[ButtonFlags.Special];
            controllerState = mutedState;
        }

        DS4Touch.UpdateInputs(controllerState);
        DSUServer.UpdateInputs(controllerState, gamepadMotions);
        VirtualManager.UpdateInputs(controllerState, gamepadMotion);
    }

    public static IController GetDefault(bool profilePage = false)
    {
        // get HIDmode for the selected profile (could be different than HIDmode in settings if profile has HIDmode)
        HIDmode HIDmode = HIDmode.NoController;

        // if profile is selected, get its HIDmode
        if (profilePage)
            HIDmode = ProfilesPage.selectedProfile.HID;
        else
            HIDmode = ManagerFactory.profileManager.GetCurrent().HID;

        // if profile HID is NotSelected, use HIDmode from settings
        if (HIDmode == HIDmode.NotSelected)
            HIDmode = (HIDmode)ManagerFactory.settingsManager.GetInt("HIDmode", true);

        switch (HIDmode)
        {
            default:
            case HIDmode.NoController:
            case HIDmode.Xbox360Controller:
                return defaultXInput;

            case HIDmode.DualShock4Controller:
                return defaultDS4;
        }
    }

    public static IController GetDefaultXBOX()
    {
        return defaultXInput;
    }

    public static IController GetDefaultDualShock4()
    {
        return defaultDS4;
    }

    #region events

    public static event ControllerPluggedEventHandler ControllerPlugged;
    public delegate void ControllerPluggedEventHandler(IController Controller, bool IsPowerCycling);

    public static event ControllerUnpluggedEventHandler ControllerUnplugged;
    public delegate void ControllerUnpluggedEventHandler(IController Controller, bool IsPowerCycling, bool WasTarget);

    public static event ControllerSelectedEventHandler ControllerSelected;
    public delegate void ControllerSelectedEventHandler(IController Controller);

    /// <summary>
    /// Controller state has changed, before layout manager
    /// </summary>
    /// <param name="Inputs">The updated controller state.</param>
    public static event InputsUpdatedEventHandler InputsUpdated;
    public delegate void InputsUpdatedEventHandler(ControllerState Inputs);

    /// <summary>
    /// Controller state has changed, after layout manager
    /// </summary>
    /// <param name="Inputs">The updated controller state.</param>
    public static event InputsUpdated2EventHandler InputsUpdated2;
    public delegate void InputsUpdated2EventHandler(ControllerState Inputs);

    public static event StatusChangedEventHandler StatusChanged;
    public delegate void StatusChangedEventHandler(ControllerManagerStatus status, int attempts);

    public static event InitializedEventHandler Initialized;
    public delegate void InitializedEventHandler();

    #endregion
}
