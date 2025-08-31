using HandheldCompanion.Controllers;
using HandheldCompanion.Controllers.Dummies;
using HandheldCompanion.Controllers.GameSir;
using HandheldCompanion.Controllers.Lenovo;
using HandheldCompanion.Controllers.MSI;
using HandheldCompanion.Controllers.SDL;
using HandheldCompanion.Controllers.Steam;
using HandheldCompanion.Controllers.Zotac;
using HandheldCompanion.Devices;
using HandheldCompanion.Helpers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Misc;
using HandheldCompanion.Notifications;
using HandheldCompanion.Platforms;
using HandheldCompanion.Shared;
using HandheldCompanion.Utils;
using HandheldCompanion.Views;
using HandheldCompanion.Views.Pages;
using Nefarius.Utilities.DeviceManagement.Drivers;
using Nefarius.Utilities.DeviceManagement.Extensions;
using Nefarius.Utilities.DeviceManagement.PnP;
using SDL3;
using SharpDX.XInput;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Shell;
using Windows.UI;
using Windows.UI.ViewManagement;
using static HandheldCompanion.Misc.ProcessEx;
using static HandheldCompanion.Utils.DeviceUtils;
using DriverStore = HandheldCompanion.Helpers.DriverStore;
using Timer = System.Timers.Timer;

namespace HandheldCompanion.Managers;

public static class ControllerManager
{
    private static readonly ConcurrentDictionary<uint, SDLController> SDLControllers = new();
    private static readonly ConcurrentDictionary<string, IController> Controllers = new();
    public static readonly ConcurrentDictionary<string, bool> PowerCyclers = new();

    private static Thread watchdogThread;
    private static bool watchdogThreadRunning;
    private static Thread pumpThread;
    private static bool pumpThreadRunning;

    private static bool ControllerManagement;

    private static int ControllerManagementAttempts = 0;
    private const int ControllerManagementMaxAttempts = 4;

    private static readonly DummyXbox360Controller? dummyXbox360 = new();
    private static readonly DummyDualShock4Controller? dummyDualShock4 = new();
    public static bool HasTargetController => GetTarget() != null;

    private static IController? targetController;
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

        // disable XInput from SDL
        SDL.SetHint(SDL.Hints.XInputEnabled, "0");
        SDL.SetHint(SDL.Hints.JoystickHIDAPISteam, "0");
        SDL.SetHint(SDL.Hints.JoystickHIDAPISteamdeck, "0");

        // load SDL game controller database
        // https://github.com/mdqinc/SDL_GameControllerDB
        int loaded = SDL.AddGamepadMappingsFromFile("gamecontrollerdb.txt");

        // Initialize SDL Gamepad
        if (!SDL.Init(SDL.InitFlags.Gamepad))
            LogManager.LogError("SDL_Init Error: {0}", SDL.GetError());
        else
            LogManager.LogInformation("SDL was successfully initialized with {0} gamepad supported", loaded);

        // manage pump thread
        pumpThreadRunning = true;
        pumpThread = new Thread(pumpThreadLoop)
        {
            IsBackground = true,
            Priority = ThreadPriority.Highest
        };
        pumpThread.Start();

        // manage events
        TimerManager.Tick += Tick;
        UIGamepad.GotFocus += GamepadFocusManager_FocusChanged;
        UIGamepad.LostFocus += GamepadFocusManager_FocusChanged;
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

    private static void Tick(long ticks, float delta)
    {
        // Snapshot to avoid races after HasTargetController check
        IController? tc = targetController;
        if (!HasTargetController || tc is null)
            return;

        // pull controller
        tc.Tick(ticks, delta);

        // snapshot inputs; bail if not ready
        ControllerState controllerState = tc.Inputs;
        if (controllerState is null)
            return;

        // snapshot motions; bail if not ready
        Dictionary<byte, GamepadMotion>? motions = tc.gamepadMotions;
        if (motions is null || motions.Count == 0)
            return;

        // raise event, before layout mapping
        EventHelper.RaiseAsync(InputsUpdated, controllerState, false);

        // get main motion safely
        byte gamepadIndex = tc.gamepadIndex;
        if (!motions.TryGetValue(gamepadIndex, out GamepadMotion gamepadMotion) || gamepadMotion is null)
            return;

        // sensor override
        switch (sensorSelection)
        {
            case SensorFamily.Windows:
            case SensorFamily.SerialUSBIMU:
                {
                    IDevice dev = IDevice.GetCurrent();
                    GamepadMotion? devMotion = dev?.GamepadMotion;
                    if (devMotion is null)
                        break; // keep existing gamepadMotion if device motion not ready

                    gamepadMotion = devMotion;
                    SensorsManager.UpdateReport(controllerState, gamepadMotion, ref delta);
                    break;
                }
        }

        // Update motion consumers (null-safe)
        MotionManager.UpdateReport(controllerState, gamepadMotion);
        MainWindow.overlayModel?.UpdateReport(controllerState, gamepadMotion, delta);

        // compute layout (null-safe mapping)
        ControllerState mapped = ManagerFactory.layoutManager?.MapController(controllerState) ?? controllerState;
        EventHelper.RaiseAsync(InputsUpdated, mapped, true);

        // controller is muted
        if (ControllerMuted)
        {
            // keep only Special passthrough
            mutedState.ButtonState[ButtonFlags.Special] = mapped.ButtonState[ButtonFlags.Special];
            mapped = mutedState;
        }

        DS4Touch.UpdateInputs(mapped);
        VirtualManager.UpdateInputs(mapped, gamepadMotion);
        DSUServer.UpdateInputs(mapped, motions);
        DSUServer.Tick(ticks, delta);
    }

    private static void pumpThreadLoop(object? obj)
    {
        while (pumpThreadRunning)
        {
            if (SDL.WaitEventTimeout(out SDL.Event e, TimerManager.GetPeriod()))
            {
                switch ((SDL.EventType)e.Type)
                {
                    case SDL.EventType.GamepadAdded:
                        SDL_GamepadAdded(e.GDevice.Which);
                        break;

                    case SDL.EventType.GamepadRemoved:
                        SDL_GamepadRemoved(e.GDevice.Which);
                        break;

                    default:
                        if (SDLControllers.TryGetValue(e.GDevice.Which, out SDLController controller))
                            controller.PumpEvent(e);
                        break;
                }
            }
        }
    }

    private static async void SDL_GamepadAdded(uint deviceIndex)
    {
        if (!SDL.IsGamepad(deviceIndex))
        {
            LogManager.LogError("Controller at index: {0} is not a recognized game controller", deviceIndex);
            return;
        }

        nint gamepad = SDL.OpenGamepad(deviceIndex);
        if (gamepad == IntPtr.Zero)
        {
            LogManager.LogError($"Failed to open controller {deviceIndex}: {SDL.GetError()}");
        }
        else
        {
            string? name = SDL.GetGamepadName(gamepad);
            string? path = SDL.GetGamepadPath(gamepad);
            uint userIndex = (uint)SDL.GetGamepadPlayerIndex(gamepad);

            if (string.IsNullOrEmpty(path))
                return;

            if (path.Contains("XInput"))
                path = DeviceManager.GetPathFromUserIndex(userIndex);

            // get cleared path
            if (DeviceManager.TryExtractInterfaceGuid(path, out Guid interfaceGuid))
                path = DeviceManager.SymLinkToInstanceId(path, interfaceGuid.ToString());

            // prepare PnPDetails
            PnPDetails? details = DeviceManager.GetDeviceFromInstanceId(path);
            if (details is null)
            {
                LogManager.LogError($"Failed to retrieve PnPDetails for controller {deviceIndex}");
                return;
            }

            try
            {
                Controllers.TryGetValue(details.baseContainerDeviceInstanceId, out IController controller);
                PowerCyclers.TryGetValue(details.baseContainerDeviceInstanceId, out bool IsPowerCycling);

                if (controller != null)
                {
                    // skip XInput controller
                    if (controller is XInputController) return;

                    // skip DInput controller
                    if (controller is DInputController) return;

                    // controller is known
                    // set power cycling
                    IsPowerCycling = true;
                    PowerCyclers[details.baseContainerDeviceInstanceId] = IsPowerCycling;

                    if (controller is SDLController SDLController)
                    {
                        SDLController.gamepad = gamepad;
                        SDLController.deviceIndex = deviceIndex;
                    }

                    // update PnPDetails
                    controller.AttachDetails(details);

                    // check if HID path has changed
                    if (controller.GetInstanceId() != details.deviceInstanceId)
                    {
                        // refresh cloaking
                        if (controller.IsHidden()) controller.Hide(false);
                        else controller.Unhide(false);
                    }
                }
                else
                {
                    // SDL
                    SDL.GamepadType type = SDL.GetGamepadType(gamepad);
                    switch (type)
                    {
                        default:
                        case SDL.GamepadType.Unknown:
                        case SDL.GamepadType.Standard:
                            controller = new SDLController(gamepad, deviceIndex, details);
                            break;

                        case SDL.GamepadType.Xbox360:
                        case SDL.GamepadType.XboxOne:
                            controller = new Xbox360Controller(gamepad, deviceIndex, details);
                            break;

                        case SDL.GamepadType.PS3:
                        case SDL.GamepadType.PS4:
                        case SDL.GamepadType.PS5:
                            controller = new DualShock4Controller(gamepad, deviceIndex, details);
                            break;

                        case SDL.GamepadType.GameCube:
                        case SDL.GamepadType.NintendoSwitchPro:
                            controller = new NintendoSwitchProController(gamepad, deviceIndex, details);
                            break;
                    }
                }

                if (controller == null)
                {
                    LogManager.LogWarning("Unsupported SDL controller: VID:{0} and PID:{1}", details.GetVendorID(), details.GetProductID());
                    return;
                }

                while (!controller.IsReady && controller.IsConnected())
                    await Task.Delay(250).ConfigureAwait(false);

                if (!controller.IsConnected())
                {
                    LogManager.LogWarning("SDL controller: VID:{0} and PID:{1} was unplugged before being ready", details.GetVendorID(), details.GetProductID());
                    return;
                }

                // controller is ready
                controller.IsBusy = false;

                // store controller
                Controllers[details.baseContainerDeviceInstanceId] = controller;
                SDLControllers[deviceIndex] = (SDLController)controller;

                LogManager.LogInformation("SDL controller {0} plugged", controller.ToString());
                ControllerPlugged?.Invoke(controller, false);

                // let's not flood the toaster
                if (!IsPowerCycling && !controller.IsVirtual())
                    ToastManager.SendToast(controller.ToString(), "detected");

                PickTargetController();
                PowerCyclers.TryRemove(controller.GetContainerInstanceId(), out _);
            }
            finally
            {
            }
        }
    }

    private static async void SDL_GamepadRemoved(uint deviceIndex)
    {
        if (SDLControllers.TryGetValue(deviceIndex, out SDLController controller))
        {
            string path = controller.GetContainerInstanceId();

            try
            {
                // close controller
                SDL.CloseGamepad(controller.gamepad);

                PowerCyclers.TryGetValue(path, out bool IsPowerCycling);
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

                    Controllers.TryRemove(path, out _);
                    SDLControllers.TryRemove(deviceIndex, out _);
                }
            }
            finally
            {
            }
        }
    }

    private static async void HidDeviceArrived(PnPDetails details, Guid InterfaceGuid)
    {
        if (!details.isGaming) return;

        try
        {
            Controllers.TryGetValue(details.baseContainerDeviceInstanceId, out IController controller);
            PowerCyclers.TryGetValue(details.baseContainerDeviceInstanceId, out bool IsPowerCycling);

            // DInput
            if (controller is not null)
            {
                // skip XInput controller
                if (controller is XInputController) return;

                // skip SDL HID controller
                if (controller is SDLController) return;

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
                int VendorId = details.VendorID;
                int ProductId = details.ProductID;

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
                                        try { controller = new GordonController(details); } catch { }
                                    break;
                                // WIRELESS STEAM CONTROLLER
                                case 0x1142:
                                    // MI == 0 is virtual keyboards
                                    // MI == 1-4 are 4 controllers
                                    // TODO: The dongle registers 4 controller devices, regardless how many are
                                    // actually connected. There is no easy way to check for connection without
                                    // actually talking to each controller.
                                    try { controller = new GordonController(details); } catch { }
                                    break;

                                // STEAM DECK
                                case 0x1205:
                                    try { controller = new NeptuneController(details); } catch { }
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
                                // Legion Go
                                case 0x6183:    // DInput
                                case 0x6184:    // Dual DInput
                                case 0x61EC:    // DInput (new firmware)
                                case 0x61ED:    // Dual DInput (new firmware)
                                    break;

                                // Legion Go S
                                case 0xE311:
                                    break;
                            }
                        }
                        break;

                    // MSI
                    case 0x0DB0:
                        {
                            switch (ProductId)
                            {
                                case 0x1902:
                                case 0x1903:
                                    try
                                    {
                                        controller = new DClawController(details);
                                        // hacky: MSI will eventually create an controller for a few milliseconds, then delete it
                                        await Task.Delay(2000).ConfigureAwait(false);
                                    }
                                    catch { }
                                    break;
                            }
                        }
                        break;
                }
            }

            if (controller == null)
            {
                LogManager.LogWarning("Unsupported Generic controller: VID:{0} and PID:{1}", details.GetVendorID(), details.GetProductID());
                return;
            }

            while (!controller.IsReady && controller.IsConnected())
                await Task.Delay(250).ConfigureAwait(false);

            if (!controller.IsConnected())
            {
                LogManager.LogWarning("Generic controller: VID:{0} and PID:{1} was unplugged before being ready", details.GetVendorID(), details.GetProductID());
                return;
            }

            // controller is ready
            controller.IsBusy = false;

            // store controller
            string path = controller.GetContainerInstanceId();
            Controllers[path] = controller;

            LogManager.LogInformation("Generic controller {0} plugged", controller.ToString());
            ControllerPlugged?.Invoke(controller, IsPowerCycling);

            // let's not flood the toaster
            if (!IsPowerCycling && !controller.IsVirtual())
                ToastManager.SendToast(controller.ToString(), "detected");

            PickTargetController();
            PowerCyclers.TryRemove(controller.GetContainerInstanceId(), out _);
        }
        catch { }
        finally
        {
        }
    }

    private static async void HidDeviceRemoved(PnPDetails details, Guid InterfaceGuid)
    {
        try
        {
            IController controller = null;

            Task timeout = Task.Delay(TimeSpan.FromSeconds(10));
            while (!timeout.IsCompleted && controller == null)
            {
                if (Controllers.TryGetValue(details.baseContainerDeviceInstanceId, out controller))
                    break;

                await Task.Delay(100).ConfigureAwait(false);
            }

            if (controller == null) return;

            // skip XInput controller
            if (controller is XInputController) return;

            // skip SDL HID controller
            if (controller is SDLController) return;

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
        catch { }
        finally
        {
        }
    }

    private static async void XUsbDeviceArrived(PnPDetails details, Guid InterfaceGuid)
    {
        try
        {
            Controllers.TryGetValue(details.baseContainerDeviceInstanceId, out IController controller);
            PowerCyclers.TryGetValue(details.baseContainerDeviceInstanceId, out bool IsPowerCycling);

            if (controller != null)
            {
                // skip DInput controller
                if (controller is DInputController) return;

                // skip SDL HID controller
                if (controller is SDLController) return;

                ((XInputController)controller).AttachDetails(details);

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
                        try { controller = new XInputController(details); } catch { }
                        break;

                    // Lenovo
                    case "0x17EF":
                    case "0x1A86":
                        {
                            switch (details.GetProductID())
                            {
                                // Legion Go
                                case "0x6182":  // old firmware
                                case "0x61EB":  // new firmware
                                    try { controller = new LegionController(details); } catch { }
                                    break;

                                // Legion Go S
                                case "0xE310":
                                    try { controller = new LegionControllerS(details); } catch { }
                                    break;

                                default:
                                    try { controller = new XInputController(details); } catch { }
                                    break;
                            }
                        }
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
                                    try { controller = new TarantulaProController(details); } catch { }
                                    break;
                            }
                        }
                        break;

                    // MSI
                    case "0x0DB0":
                        {
                            switch (details.GetProductID())
                            {
                                case "0x1901":
                                    try
                                    {
                                        controller = new XClawController(details);
                                        // hacky: MSI will eventually create an controller for a few milliseconds, then delete it
                                        await Task.Delay(2000).ConfigureAwait(false);
                                    }
                                    catch { }
                                    break;
                            }
                        }
                        break;

                    // ZOTAC
                    case "0x1EE9":
                        {
                            switch (details.GetProductID())
                            {
                                case "0x1590":
                                    try { controller = new ZoneController(details); } catch { }
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

            if (!controller.IsConnected())
            {
                LogManager.LogWarning("XInput controller: VID:{0} and PID:{1} was unplugged before being ready", details.GetVendorID(), details.GetProductID());
                return;
            }

            // controller is ready
            controller.IsBusy = false;

            // store controller
            string path = details.baseContainerDeviceInstanceId;
            Controllers[path] = controller;

            LogManager.LogInformation("XInput controller {0} plugged", controller.ToString());
            ControllerPlugged?.Invoke(controller, IsPowerCycling);

            // let's not flood the toaster
            if (!IsPowerCycling && !controller.IsVirtual())
                ToastManager.SendToast(controller.ToString(), "detected");

            PickTargetController();
            PowerCyclers.TryRemove(controller.GetContainerInstanceId(), out _);
        }
        catch { }
        finally
        {
        }
    }

    private static async void XUsbDeviceRemoved(PnPDetails details, Guid InterfaceGuid)
    {
        try
        {
            IController controller = null;

            Task timeout = Task.Delay(TimeSpan.FromSeconds(10));
            while (!timeout.IsCompleted && controller == null)
            {
                if (Controllers.TryGetValue(details.baseContainerDeviceInstanceId, out controller))
                    break;

                await Task.Delay(100).ConfigureAwait(false);
            }

            if (controller == null) return;

            // skip DInput controller
            if (controller is DInputController) return;

            // skip SDL HID controller
            if (controller is SDLController) return;

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
        catch { }
        finally
        {
        }
    }

    public static void Stop()
    {
        if (!IsInitialized)
            return;

        // kill pump thread
        if (pumpThread is not null)
        {
            pumpThreadRunning = false;
            // Ensure the thread has finished execution
            if (pumpThread.IsAlive)
                pumpThread.Join(3000);
            pumpThread = null;
        }

        // Cleanup SDL3 controllers
        foreach (SDLController controller in SDLControllers.Values)
            SDL.CloseGamepad(controller.gamepad);

        SDL.Quit();

        // manage events
        TimerManager.Tick -= Tick;
        ManagerFactory.deviceManager.XUsbDeviceArrived -= XUsbDeviceArrived;
        ManagerFactory.deviceManager.XUsbDeviceRemoved -= XUsbDeviceRemoved;
        ManagerFactory.deviceManager.HidDeviceArrived -= HidDeviceArrived;
        ManagerFactory.deviceManager.HidDeviceRemoved -= HidDeviceRemoved;
        ManagerFactory.deviceManager.Initialized -= DeviceManager_Initialized;
        ManagerFactory.settingsManager.SettingValueChanged -= SettingsManager_SettingValueChanged;
        ManagerFactory.settingsManager.Initialized -= SettingsManager_Initialized;
        ManagerFactory.processManager.ForegroundChanged -= ProcessManager_ForegroundChanged;
        ManagerFactory.processManager.Initialized -= ProcessManager_Initialized;
        UIGamepad.GotFocus -= GamepadFocusManager_FocusChanged;
        UIGamepad.LostFocus -= GamepadFocusManager_FocusChanged;
        VirtualManager.Vibrated -= VirtualManager_Vibrated;
        MainWindow.uiSettings.ColorValuesChanged -= OnColorValuesChanged;

        // manage device events
        IDevice.GetCurrent().KeyPressed -= CurrentDevice_KeyPressed;
        IDevice.GetCurrent().KeyReleased -= CurrentDevice_KeyReleased;

        // halt controller manager and unplug on close
        // todo: we might need to use lock (targetLock) within Tick event.
        Suspend(true);

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

    private static void GamepadFocusManager_FocusChanged(string Name)
    {
        // check applicable scenarios
        CheckControllerScenario();
    }

    private static void ProcessManager_ForegroundChanged(ProcessEx? processEx, ProcessEx? backgroundEx, ProcessFilter filter)
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
        if (UIGamepad.HasFocus())
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
        // manage events
        ManagerFactory.settingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

        // raise events
        SettingsManager_SettingValueChanged("VibrationStrength", ManagerFactory.settingsManager.GetString("VibrationStrength"), false);
        SettingsManager_SettingValueChanged("ControllerManagement", ManagerFactory.settingsManager.GetString("ControllerManagement"), false);
        SettingsManager_SettingValueChanged("SensorSelection", ManagerFactory.settingsManager.GetString("SensorSelection"), false);
        SettingsManager_SettingValueChanged("SteamControllerMode", ManagerFactory.settingsManager.GetString("SteamControllerMode"), false);
    }

    private static void DeviceManager_Initialized()
    {
        QueryDevices();
    }

    public static void QueryDevices()
    {
        // manage events
        ManagerFactory.deviceManager.XUsbDeviceArrived += XUsbDeviceArrived;
        ManagerFactory.deviceManager.XUsbDeviceRemoved += XUsbDeviceRemoved;
        ManagerFactory.deviceManager.HidDeviceArrived += HidDeviceArrived;
        ManagerFactory.deviceManager.HidDeviceRemoved += HidDeviceRemoved;

        foreach (PnPDetails? device in ManagerFactory.deviceManager.PnPDevices.Values)
        {
            if (device.isXInput)
                XUsbDeviceArrived(device, DeviceInterfaceIds.XUsbDevice);
            else if (device.isGaming)
                HidDeviceArrived(device, DeviceInterfaceIds.HidDevice);
        }

        // Reopen all SDL gamepads
        uint[] gamepads = SDL.GetGamepads(out int count);
        foreach (uint gamepad in gamepads)
            SDL_GamepadAdded(gamepad);
    }

    private static void ProcessManager_Initialized()
    {
        QueryForeground();
    }

    private static void QueryForeground()
    {
        // manage events
        ManagerFactory.processManager.ForegroundChanged += ProcessManager_ForegroundChanged;

        ProcessEx processEx = ProcessManager.GetCurrent();
        if (processEx is not null)
        {
            ProcessFilter filter = ProcessManager.GetFilter(processEx.Executable, processEx.Path);
            ProcessManager_ForegroundChanged(processEx, null, filter);
        }
    }

    public static void Resume(bool OS)
    {
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
            watchdogThread.Join(3000);
    }

    private static void VirtualManager_Vibrated(byte LargeMotor, byte SmallMotor)
    {
        targetController?.SetVibration(LargeMotor, SmallMotor);
    }

    public static async void Unplug(IController controller)
    {
        string baseContainerDeviceInstanceId = controller.GetContainerInstanceId();

        try
        {
            bool WasTarget = IsTargetController(controller.GetInstanceId());

            LogManager.LogInformation("XInput controller {0} force unplugged", controller.ToString());
            ControllerUnplugged?.Invoke(controller, false, WasTarget);

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

            PowerCyclers.TryRemove(controller.GetInstanceId(), out _);
            Controllers.TryRemove(baseContainerDeviceInstanceId, out _);
        }
        catch { }
        finally
        {
        }
    }

    private static HashSet<byte> UserIndexes = new();
    private static List<XInputController> InvalidSlotAssignments = new();
    private static bool HasInvalidController => InvalidSlotAssignments.Any();

    private static async void watchdogThreadLoop(object? obj)
    {
        while (watchdogThreadRunning)
        {
            await Task.Delay(1000);

            HashSet<byte> currentSlots = new();
            InvalidSlotAssignments.Clear();

            // Track UserIndexes assignment
            IEnumerable<Task> tasks = GetControllers<XInputController>()
                .Where(controller => !controller.IsDummy())
                .Select(async controller =>
                {
                    byte index = await DeviceManager.GetXInputIndexAsync(controller.GetContainerPath(), true);
                    if (index == byte.MaxValue)
                        index = (byte)XInputController.TryGetUserIndex(controller.Details);

                    lock (UserIndexes)
                    {
                        if (!currentSlots.Add(index))
                            InvalidSlotAssignments.Add(controller);
                    }

                    controller.AttachController(index);
                });

            await Task.WhenAll(tasks);

            bool hasInvalidVirtual = InvalidSlotAssignments.Any(c => c.IsVirtual());
            if (hasInvalidVirtual)
            {
                VirtualManager.Suspend(false);
                await Task.Delay(1000);
                VirtualManager.Resume(false);
            }

            foreach (IController controller in InvalidSlotAssignments)
                if (!controller.IsVirtual())
                    controller.CyclePort();

            // Ensure virtual controller occupies slot 1
            if (VirtualManager.HIDmode == HIDmode.Xbox360Controller && VirtualManager.HIDstatus == HIDstatus.Connected)
            {
                if (HasPhysicalController<XInputController>())
                {
                    var vController = GetControllerFromSlot<XInputController>(UserIndex.One, false);
                    if (vController is null)
                    {
                        XInputController? pController = null;
                        for (int idx = 0; idx <= 4; idx++)
                        {
                            if (idx == 4)
                                idx = byte.MaxValue;

                            pController = GetControllerFromSlot<XInputController>((UserIndex)idx, true);
                            if (pController is not null)
                                break;
                        }

                        if (pController is null)
                            continue;

                        // ushort vendorId = pController.GetVendorID();
                        // ushort productId = pController.GetProductID();

                        if (!ShouldAttemptControllerManagement())
                            continue;

                        SuspendController(pController.GetContainerInstanceId());

                        bool hasBusyWireless = false;
                        bool hasCyclingController = false;

                        var wireless = GetPhysicalControllers<XInputController>().FirstOrDefault(c => c.IsBluetooth() && c.IsBusy);
                        if (wireless is not null)
                        {
                            hasBusyWireless = true;
                            PowerCyclers.TryGetValue(wireless.GetContainerInstanceId(), out hasCyclingController);
                            if (hasBusyWireless && !hasCyclingController && ControllerManagementAttempts != 0)
                                return;
                        }

                        // Remove current virtual controller
                        VirtualManager.SetControllerMode(HIDmode.NoController);
                        Task timeout = Task.Delay(TimeSpan.FromSeconds(4));
                        while (!timeout.IsCompleted && GetVirtualControllers<XInputController>().Any())
                            await Task.Delay(100);

                        // Create temporary virtual controllers
                        // VirtualManager.VendorId = vendorId;
                        // VirtualManager.ProductId = productId;
                        int usedSlots = VirtualManager.CreateTemporaryControllers();

                        // Wait for virtual controllers to appear
                        timeout = Task.Delay(TimeSpan.FromSeconds(4));
                        while (!timeout.IsCompleted && GetVirtualControllers<XInputController>().Count() < usedSlots)
                            await Task.Delay(100);

                        // Dispose temporary
                        VirtualManager.DisposeTemporaryControllers();
                        timeout = Task.Delay(TimeSpan.FromSeconds(4));
                        while (!timeout.IsCompleted && GetVirtualControllers<XInputController>().Count() > usedSlots)
                            await Task.Delay(100);

                        // Resume main virtual controller
                        VirtualManager.SetControllerMode(HIDmode.Xbox360Controller);
                        timeout = Task.Delay(TimeSpan.FromSeconds(4));
                        while (!timeout.IsCompleted && !GetVirtualControllers<XInputController>().Any())
                            await Task.Delay(100);
                    }
                    else if (managerStatus != ControllerManagerStatus.Succeeded)
                    {
                        MarkControllerManagementSuccess();
                    }
                    else
                    {
                        ResumeControllers();
                    }
                }
                else if (HasVirtualController<XInputController>())
                {
                    var vController = GetControllerFromSlot<XInputController>(UserIndex.One, false);
                    if (vController is null && ShouldAttemptControllerManagement())
                    {
                        VirtualManager.Suspend(false);
                        await Task.Delay(1000);
                        VirtualManager.Resume(false);

                        Task timeout = Task.Delay(TimeSpan.FromSeconds(4));
                        while (!timeout.IsCompleted && !GetVirtualControllers<XInputController>(VirtualManager.VendorId, VirtualManager.ProductId).Any())
                            await Task.Delay(100);
                    }
                    else if (managerStatus != ControllerManagerStatus.Succeeded)
                    {
                        MarkControllerManagementSuccess();
                    }
                }
                else if (ControllerManagementAttempts != 0)
                {
                    ResumeControllers();
                }
            }
        }
    }

    private static bool ShouldAttemptControllerManagement()
    {
        if (ControllerManagementAttempts < ControllerManagementMaxAttempts)
        {
            ControllerManagementAttempts++;
            UpdateStatus(ControllerManagerStatus.Busy);
            return true;
        }

        // Max attempts reached: disable controller management
        ResumeControllers();
        UpdateStatus(ControllerManagerStatus.Failed);
        ControllerManagementAttempts = 0;

        ManagerFactory.settingsManager.SetProperty("ControllerManagement", false);
        return false;
    }

    private static void MarkControllerManagementSuccess()
    {
        ResumeControllers();
        UpdateStatus(ControllerManagerStatus.Succeeded);
        ControllerManagementAttempts = 0;
    }

    private static Notification ManagerBusy = new("Controller Manager", "Controllers order is being adjusted, your gamepad might be come irresponsive for a few seconds.") { IsInternal = true };

    private static void UpdateStatus(ControllerManagerStatus status)
    {
        switch (status)
        {
            case ControllerManagerStatus.Busy:
                ManagerFactory.notificationManager.Add(ManagerBusy);
                MainWindow.GetCurrent().UpdateTaskbarState(TaskbarItemProgressState.Indeterminate);
                break;
            case ControllerManagerStatus.Succeeded:
            case ControllerManagerStatus.Failed:
                MainWindow.GetCurrent().UpdateTaskbarState(TaskbarItemProgressState.None);
                ManagerFactory.notificationManager.Discard(ManagerBusy);
                break;
            case ControllerManagerStatus.Pending:
                MainWindow.GetCurrent().UpdateTaskbarState(TaskbarItemProgressState.Paused);
                break;
        }

        managerStatus = status;
        StatusChanged?.Invoke(status, ControllerManagementAttempts);
    }

    private static bool ConnectOnPlug => ManagerFactory.settingsManager.GetBoolean("ConnectOnPlug");
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

            // If user has disabled auto-connect and we already have a real controller, keep it
            if (!ConnectOnPlug && targetController is not null && !targetController.IsDummy())
            {
                deviceInstanceId = targetController.GetContainerInstanceId();
            }
            // If we have an external controller plugged-in and user wants controller to connect when plugged
            else if (latestExternalController is not null && ConnectOnPlug)
            {
                if (targetController is null || targetController.IsDummy())
                {
                    // If no previous controller or dummy, pick external
                    deviceInstanceId = latestExternalController.GetContainerInstanceId();
                }
                else if (targetController.IsWireless() || targetController.IsExternal())
                {
                    deviceInstanceId = targetController.GetContainerInstanceId();
                }
            }
            else if (internalController is not null)
            {
                // Fallback: if no external/wireless controller is available, use an internal controller (if present)
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
            ClearTargetControllerInternal();
        }
    }

    private static void ClearTargetControllerInternal()
    {
        if (targetController is null)
            return;

        targetController.SetLightColor(0, 0, 0);
        targetController.Unplug();
        targetController = null;
        ManagerFactory.settingsManager.SetProperty("HIDInstancePath", string.Empty);
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
            ClearTargetControllerInternal();

            // update target controller
            targetController = controller;
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

            Task timeout = Task.Delay(TimeSpan.FromSeconds(3));
            while (!timeout.IsCompleted && pnPDevice is null)
            {
                try { pnPDevice = PnPDevice.GetDeviceByInstanceId(baseContainerDeviceInstanceId); } catch { }
                Task.Delay(1000).Wait();
            }

            if (pnPDevice is null)
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

                        // install empty drivers
                        pnPDevice.InstallNullDriver(out bool rebootRequired);
                    }
                    break;
            }

            // cycle controller
            if (Controllers.TryGetValue(baseContainerDeviceInstanceId, out IController controller))
                return controller.CyclePort();
        }
        catch { }

        return false;
    }

    public static void SuspendControllers()
    {
        foreach (XInputController xInputController in GetPhysicalControllers<XInputController>())
            SuspendController(xInputController.GetContainerInstanceId());
    }

    public static bool ResumeController(string baseContainerDeviceInstanceId)
    {
        try
        {
            PnPDevice pnPDevice = null;

            Task timeout = Task.Delay(TimeSpan.FromSeconds(3));
            while (!timeout.IsCompleted && pnPDevice is null)
            {
                try { pnPDevice = PnPDevice.GetDeviceByInstanceId(baseContainerDeviceInstanceId); } catch { }
                Task.Delay(1000).Wait();
            }

            if (pnPDevice is null)
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
                    {
                        string InfPath = DriverStore.GetDriverFromDriverStore(baseContainerDeviceInstanceId);
                        if (!string.IsNullOrEmpty(InfPath) && pnPDriver?.InfPath != InfPath)
                        {
                            // restore drivers
                            pnPDevice.RemoveAndSetup();
                            pnPDevice.InstallCustomDriver(InfPath, out bool rebootRequired);

                            // remove device from store
                            DriverStore.RemoveFromDriverStore(baseContainerDeviceInstanceId);

                            return true;
                        }
                    }
                    break;
            }
        }
        catch { }

        return false;
    }

    public static void ResumeControllers()
    {
        // loop through controllers
        foreach (string baseContainerDeviceInstanceId in DriverStore.GetPaths())
            ResumeController(baseContainerDeviceInstanceId);

        /*
        if (HostRadioDisabled)
        {
            using (HostRadio hostRadio = new())
            {
                hostRadio.EnableRadio();
                HostRadioDisabled = false;
            }
        }
        */
    }

    public static IController? GetTarget()
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
        return GetPhysicalControllers<T>().Any(controller => typeof(T).IsAssignableFrom(controller.GetType()));
    }

    public static bool HasVirtualController<T>() where T : IController
    {
        return GetVirtualControllers<T>().Any(controller => typeof(T).IsAssignableFrom(controller.GetType()));
    }

    public static IEnumerable<T> GetPhysicalControllers<T>(ushort vendorId = 0, ushort productId = 0) where T : IController
    {
        return Controllers.Values
            .Where(controller => typeof(T).IsAssignableFrom(controller.GetType()) && controller.IsPhysical() && !controller.IsDummy()
                && (vendorId == 0 || controller.GetVendorID() == vendorId)
                && (productId == 0 || controller.GetProductID() == productId))
            .Cast<T>();
    }

    public static IEnumerable<T> GetVirtualControllers<T>(ushort vendorId = 0, ushort productId = 0) where T : IController
    {
        return Controllers.Values
            .Where(controller => typeof(T).IsAssignableFrom(controller.GetType()) && controller.IsVirtual() && !controller.IsDummy()
                && (vendorId == 0 || controller.GetVendorID() == vendorId)
                && (productId == 0 || controller.GetProductID() == productId))
            .Cast<T>();
    }

    public static T? GetControllerFromSlot<T>(UserIndex userIndex = 0, bool physical = true) where T : IController
    {
        return Controllers.Values.FirstOrDefault(controller => typeof(T).IsAssignableFrom(controller.GetType()) && ((physical && controller.IsPhysical()) || (!physical && controller.IsVirtual())) && controller.GetUserIndex() == (int)userIndex) as T;
    }

    public static IEnumerable<T> GetControllers<T>() where T : IController
    {
        return Controllers.Values.Where(controller => typeof(T).IsAssignableFrom(controller.GetType())).Cast<T>();
    }

    private static ControllerState mutedState = new ControllerState();

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
                return dummyXbox360;

            case HIDmode.DualShock4Controller:
                return dummyDualShock4;
        }
    }

    public static IController GetDefaultXBOX()
    {
        return dummyXbox360;
    }

    public static IController GetDefaultDualShock4()
    {
        return dummyDualShock4;
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
    public delegate void InputsUpdatedEventHandler(ControllerState Inputs, bool IsMapped);

    public static event StatusChangedEventHandler StatusChanged;
    public delegate void StatusChangedEventHandler(ControllerManagerStatus status, int attempts);

    public static event InitializedEventHandler Initialized;
    public delegate void InitializedEventHandler();

    #endregion
}