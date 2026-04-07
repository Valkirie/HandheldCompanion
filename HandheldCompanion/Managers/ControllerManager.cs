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
using System.IO;
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
using MediaColor = System.Windows.Media.Color;
using Timer = System.Timers.Timer;

namespace HandheldCompanion.Managers;

public static class ControllerManager
{
    private static readonly ConcurrentDictionary<uint, SDLController> SDLControllers = new();
    private static readonly ConcurrentDictionary<string, IController> Controllers = new();
    public static readonly ConcurrentDictionary<string, bool> PowerCyclers = new();

    private static readonly ConcurrentDictionary<string, Task> xusbArrivalInProgress = new();
    private static readonly ConcurrentDictionary<string, Task> xusbRemovalInProgress = new();
    private static readonly ConcurrentDictionary<string, Task> hidArrivalInProgress = new();
    private static readonly ConcurrentDictionary<string, Task> hidRemovalInProgress = new();
    private static readonly ConcurrentDictionary<uint, Task> sdlArrivalInProgress = new();
    private static readonly ConcurrentDictionary<uint, Task> sdlRemovalInProgress = new();

    private static Thread watchdogThread;
    private static bool watchdogThreadRunning;
    private static readonly object watchdogLock = new();
    private static int watchdogStarted;
    private static Thread pumpThread;
    private static bool pumpThreadRunning;

    // Slot monitor runs independently from the actual slot manipulation logic.
    // It is intentionally always running (light polling) so HC can detect slot issues
    // and either auto-fix (Automatic) or prompt the user (Manual).
    private static Thread slotMonitorThread;
    private static bool slotMonitorThreadRunning;
    private static readonly object slotMonitorLock = new();
    private static int slotMonitorStarted;

    // Ensures that probing/manipulating slot state is serialized across monitor/event/fix run.
    private static readonly SemaphoreSlim slotStateSemaphore = new(1, 1);

    public enum ControllerSlotManagementMode
    {
        Manual = 0,
        Automatic = 1
    }

    private static ControllerSlotManagementMode slotManagementMode = ControllerSlotManagementMode.Manual;

    // Manual prompting (debounce / ignore)
    private static DateTime slotFixIgnoreUntilUtc = DateTime.MinValue;
    private static DateTime slotFixLastPromptUtc = DateTime.MinValue;

    // Slot issue state (for UI + manual fallback)
    public static bool HasSlotIssue { get; private set; }
    public static bool HasVirtualSlot1Issue { get; private set; }
    public static string SlotIssueReason { get; private set; } = string.Empty;

    // Status toast debounce
    private static DateTime slotFixLastStatusToastUtc = DateTime.MinValue;
    private static ControllerManagerStatus slotFixLastStatusToast = ControllerManagerStatus.Pending;
    private static int ControllerManagementAttempts = 0;
    private const int ControllerManagementMaxAttempts = 4;

    // Consecutive watchdog failure tracking – prevents infinite retry loops at boot
    // when XInput slot assignments are not yet stable. After MaxConsecutiveWatchdogFailures
    // failed runs the mode is switched to Manual, which never auto-triggers the watchdog.
    private static int consecutiveWatchdogFailures = 0;
    private const int MaxConsecutiveWatchdogFailures = 3;

    private static readonly DummyXbox360Controller dummyXbox360 = new();
    private static readonly DummyDualShock4Controller dummyDualShock4 = new();
    public static bool HasTargetController => GetTarget() != null;

    private static IController? targetController;
    private static ProcessEx? foregroundProcess;
    private static bool ControllerMuted;
    private static SensorFamily sensorSelection = SensorFamily.None;

    private static object targetLock = new object();
    public static ControllerManagerStatus managerStatus = ControllerManagerStatus.Pending;

    private static Timer scenarioTimer = new(100) { AutoReset = false };
    private static Timer pickTimer = new(500) { AutoReset = false };

    #region settings
    private static bool HIDuncloakonclose => ManagerFactory.settingsManager.GetBoolean("HIDuncloakonclose");
    private static bool HIDuncloakondisconnect => ManagerFactory.settingsManager.GetBoolean("HIDuncloakondisconnect");
    #endregion

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

        SDL.SetJoystickEventsEnabled(true);
        SDL.SetGamepadEventsEnabled(true);

        foreach (SDL.EventType eventType in Enum.GetValues<SDL.EventType>())
            SDL.SetEventEnabled((uint)eventType, false);

        // gamepad pipeline used by SDLController.PumpEvent()
        SDL.SetEventEnabled((uint)SDL.EventType.GamepadAdded, true);
        SDL.SetEventEnabled((uint)SDL.EventType.GamepadRemoved, true);

        // manage pump thread
        pumpThreadRunning = true;
        pumpThread = new Thread(pumpThreadLoop)
        {
            IsBackground = true,
            Priority = ThreadPriority.AboveNormal
        };
        pumpThread.Start();

        // Start controller slot monitor (always running)
        StartSlotMonitor();

        // manage events
        TimerManager.Tick += Tick;
        UIGamepad.GotFocus += GamepadFocusManager_FocusChanged;
        UIGamepad.LostFocus += GamepadFocusManager_FocusChanged;
        VirtualManager.Vibrated += VirtualManager_Vibrated;
        MainWindow.uiSettings.ColorValuesChanged += OnColorValuesChanged;
        ToastManager.CommandReceived += ToastCommandRouter;

        // Trigger slot-fix prompt (Manual) or auto-fix (Automatic) only when controller topology changes.
        // The slot monitor remains running to keep HasSlotIssue up-to-date and to support Automatic mode.
        ControllerPlugged += ControllerManager_ControllerPlugged;
        ControllerUnplugged += ControllerManager_ControllerUnplugged;

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

    private static void ToastCommandRouter(string command, IReadOnlyDictionary<string, string> args)
    {
        try
        {
            switch (command)
            {
                case "SetTarget":
                    if (args.TryGetValue("deviceId", out string? baseContainerDeviceInstanceId) && !string.IsNullOrEmpty(baseContainerDeviceInstanceId))
                    {
                        bool powerCycle = args.TryGetValue("powerCycle", out string? pc) && bool.TryParse(pc, out bool pC) && pC;
                        SetTargetController(baseContainerDeviceInstanceId, powerCycle);
                    }
                    break;
                case "IgnoreTarget":
                    break;

                case "SlotFixReset":
                    // Manual user action: force a fresh run (reset attempts)
                    StartWatchdog(SlotFixTrigger.Manual, resetAttempts: true);
                    break;
                case "SlotFixIgnore":
                    // User explicitly dismissed the prompt; suppress prompts for a short period.
                    slotFixIgnoreUntilUtc = DateTime.UtcNow.AddMinutes(5);
                    break;
            }
        }
        catch { /* ignore */ }
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
        EventHelper.RaiseInputsUpdatedAsync(InputsUpdated, controllerState, false);

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
        MotionManager.UpdateReport(controllerState, gamepadMotion, delta);
        MainWindow.overlayModel?.UpdateReport(controllerState, gamepadMotion, delta);

        // compute layout (null-safe mapping)
        ControllerState mapped = ManagerFactory.layoutManager?.MapController(controllerState, delta) ?? controllerState;
        EventHelper.RaiseInputsUpdatedAsync(InputsUpdated, mapped, true);

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
            // check controller events every 1000ms; this is used by SDLController to detect disconnections and hotplug events
            if (SDL.WaitEventTimeout(out SDL.Event e, 1000))
            {
                switch ((SDL.EventType)e.Type)
                {
                    case SDL.EventType.GamepadAdded:
                        SDL_GamepadAdded(e.GDevice.Which);
                        break;

                    case SDL.EventType.GamepadRemoved:
                        SDL_GamepadRemoved(e.GDevice.Which);
                        break;
                }
            }
        }
    }

    private static void ShowDetectedToast(IController controller, bool isCycling)
    {
        // mute on virtual controller
        if (controller.IsVirtual())
            return;

        // don't show toast if this is the only controller 
        var physicalControllers = GetPhysicalControllers<IController>();
        if (physicalControllers.Count() == 1)
            return;

        Color winColor = MainWindow.uiSettings.GetColorValue(UIColorType.Foreground);

        string iconFile = ToastIconHelper.RenderGlyphPng(
            glyph: "\ue7fc",
            outputPath: Path.Combine(Path.GetTempPath(), "connect_to_app.png"),
            foreground: MediaColor.FromArgb(winColor.A, winColor.R, winColor.G, winColor.B));

        List<ToastAction> actions =
        [
            new ToastAction
            {
                Label = "Connect",
                // IconPath = iconFile,
                Command = "SetTarget",
                Parameters = new() { { "deviceId", controller.GetContainerInstanceId() }, { "powerCycle", isCycling.ToString() } },
                Callback = p => SetTargetController(p["deviceId"], isCycling)
            },
            new ToastAction
            {
                Label = "Ignore",
                // IconPath = iconFile,
                Command = "IgnoreTarget",
                Parameters = new(),
                Callback = p => SetTargetController(string.Empty, false)
            },
        ];

        ToastManager.SendToast(new ToastRequest
        {
            Title = controller.ToString(),
            Content = "detected",
            Actions = actions,
        });
    }

    #region SDL
    private static void SDL_GamepadAdded(uint deviceIndex)
    {
        var addTask = Task.Run(async () =>
        {
            // If a removal is running for this SDL slot, wait it out first
            if (sdlRemovalInProgress.TryGetValue(deviceIndex, out var pendingRemove))
                try { await pendingRemove.ConfigureAwait(false); } catch { /* swallow */ }

            try
            {
                if (!SDL.IsGamepad(deviceIndex))
                {
                    LogManager.LogError("Controller at index: {0} is not a recognized game controller", deviceIndex);
                    return;
                }

                nint gamepad = SDL.OpenGamepad(deviceIndex);
                if (gamepad == IntPtr.Zero)
                {
                    LogManager.LogError("Failed to open controller {0}: {1}", deviceIndex, SDL.GetError());
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

                    if (DeviceManager.TryExtractInterfaceGuid(path, out Guid interfaceGuid))
                        path = DeviceManager.SymLinkToInstanceId(path, interfaceGuid.ToString());

                    PnPDetails? details = DeviceManager.GetDeviceFromInstanceId(path);
                    if (details is null)
                    {
                        LogManager.LogError("Failed to retrieve PnPDetails for controller {0}", deviceIndex);
                        return;
                    }

                    try
                    {
                        Controllers.TryGetValue(details.baseContainerDeviceInstanceId, out IController controller);
                        PowerCyclers.TryGetValue(details.baseContainerDeviceInstanceId, out bool IsPowerCycling);

                        if (controller != null)
                        {
                            if (controller is XInputController) return;
                            if (controller is DInputController) return;

                            IsPowerCycling = true;
                            PowerCyclers[details.baseContainerDeviceInstanceId] = IsPowerCycling;

                            if (controller is SDLController SDLController)
                            {
                                SDLController.gamepad = gamepad;
                                SDLController.deviceIndex = deviceIndex;
                            }

                            controller.AttachDetails(details);

                            if (controller.GetInstanceId() != details.deviceInstanceId)
                            {
                                if (controller.IsHidden())
                                    controller.Hide(false);
                                else
                                    controller.Unhide(false);
                            }
                        }
                        else
                        {
                            SDL.GamepadType type = SDL.GetGamepadType(gamepad);
                            switch (type)
                            {
                                default:
                                case SDL.GamepadType.Unknown:
                                case SDL.GamepadType.Standard:
                                    controller = new Xbox360Controller(gamepad, deviceIndex, details);
                                    break;

                                case SDL.GamepadType.Xbox360:
                                case SDL.GamepadType.XboxOne:
                                    // controller = new Xbox360Controller(gamepad, deviceIndex, details);
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

                        // controller is gone ?
                        if (!controller.IsConnected() && !controller.IsVirtual())
                        {
                            controller.Gone();
                            return;
                        }

                        string baseContainerDeviceInstanceId = details.baseContainerDeviceInstanceId;
                        bool wasPowerCycling = PowerCyclers.TryGetValue(baseContainerDeviceInstanceId, out var powerCycling) && powerCycling;

                        controller.IsBusy = false;

                        Controllers[baseContainerDeviceInstanceId] = controller;
                        SDLControllers[deviceIndex] = (SDLController)controller;

                        LogManager.LogInformation("SDL controller {0} plugged", controller.ToString());
                        ControllerPlugged?.Invoke(controller, false);

                        if (!wasPowerCycling)
                            ShowDetectedToast(controller, wasPowerCycling);

                        PickTargetController();
                        PowerCyclers.TryRemove(baseContainerDeviceInstanceId, out _);
                    }
                    finally { }
                }
            }
            finally
            {
                sdlArrivalInProgress.TryRemove(deviceIndex, out _);
            }
        });

        sdlArrivalInProgress[deviceIndex] = addTask;
    }

    private static void SDL_GamepadRemoved(uint deviceIndex)
    {
        var removeTask = Task.Run(async () =>
        {
            // If add is still running, wait before removing
            if (sdlArrivalInProgress.TryGetValue(deviceIndex, out var pendingAdd))
                try { await pendingAdd.ConfigureAwait(false); } catch { }

            try
            {
                if (SDLControllers.TryGetValue(deviceIndex, out SDLController controller))
                {
                    string path = controller.GetContainerInstanceId();

                    try
                    {
                        SDL.CloseGamepad(controller.gamepad);

                        PowerCyclers.TryGetValue(path, out bool IsPowerCycling);
                        bool WasTarget = IsTargetController(controller.GetInstanceId());

                        LogManager.LogInformation("XInput controller {0} unplugged, cycling {1}", controller.ToString(), IsPowerCycling);
                        ControllerUnplugged?.Invoke(controller, IsPowerCycling, WasTarget);

                        if (!IsPowerCycling)
                        {
                            Controllers.TryRemove(path, out _);
                            SDLControllers.TryRemove(deviceIndex, out _);

                            controller.Gone();

                            if (controller.IsPhysical() && HIDuncloakondisconnect)
                                controller.Unhide(false);

                            if (ClearTargetIfMatch(controller.GetInstanceId()))
                                PickTargetController();
                            else
                                controller.Dispose();
                        }
                    }
                    finally { }
                }
            }
            finally
            {
                sdlRemovalInProgress.TryRemove(deviceIndex, out _);
            }
        });

        sdlRemovalInProgress[deviceIndex] = removeTask;
    }
    #endregion

    #region HidDevice
    private static void HidDeviceArrived(PnPDetails details, Guid InterfaceGuid)
    {
        var key = details.baseContainerDeviceInstanceId;

        var addTask = Task.Run(async () =>
        {
            // If a removal is running for this device, wait it out
            if (hidRemovalInProgress.TryGetValue(key, out var pendingRemove))
                try { await pendingRemove.ConfigureAwait(false); } catch { }

            try
            {
                if (!details.isGaming) return;

                try
                {
                    Controllers.TryGetValue(details.baseContainerDeviceInstanceId, out IController controller);
                    PowerCyclers.TryGetValue(details.baseContainerDeviceInstanceId, out bool IsPowerCycling);

                    if (controller is not null)
                    {
                        if (controller is XInputController) return;
                        if (controller is SDLController) return;

                        controller.AttachDetails(details);

                        if (controller.GetInstanceId() != details.deviceInstanceId)
                        {
                            if (controller.IsHidden())
                                controller.Hide(false);
                            else
                                controller.Unhide(false);
                        }

                        IsPowerCycling = true;
                        PowerCyclers[details.baseContainerDeviceInstanceId] = IsPowerCycling;
                    }
                    else
                    {
                        int VendorId = details.VendorID;
                        int ProductId = details.ProductID;

                        switch (VendorId)
                        {
                            case 0x28DE:
                                switch (ProductId)
                                {
                                    case 0x1102:
                                        if (details.GetMI() == 2)
                                            try { controller = new GordonController(details); } catch { }
                                        break;
                                    case 0x1142:
                                        try { controller = new GordonController(details); } catch { }
                                        break;
                                    case 0x1205:
                                        try { controller = new NeptuneController(details); } catch { }
                                        break;
                                }
                                break;

                            case 0x057E:
                                switch (ProductId)
                                {
                                    case 0x2009:
                                        break;
                                }
                                break;

                            case 0x17EF:
                                switch (ProductId)
                                {
                                    case 0x6183:
                                    case 0x6184:
                                    case 0x61EC:
                                    case 0x61ED:
                                        break;
                                    case 0xE311:
                                        break;
                                }
                                break;

                            case 0x0DB0:
                                switch (ProductId)
                                {
                                    case 0x1902:
                                    case 0x1903:
                                        try { controller = new DClawController(details); } catch { }
                                        break;
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

                    // controller is gone ?
                    if (!controller.IsConnected() && !controller.IsVirtual())
                    {
                        controller.Gone();
                        return;
                    }

                    string baseContainerDeviceInstanceId = controller.GetContainerInstanceId();
                    bool wasPowerCycling = PowerCyclers.TryGetValue(baseContainerDeviceInstanceId, out var powerCycling) && powerCycling;

                    controller.IsBusy = false;

                    Controllers[baseContainerDeviceInstanceId] = controller;

                    LogManager.LogInformation("Generic controller {0} plugged", controller.ToString());
                    ControllerPlugged?.Invoke(controller, false);

                    if (!wasPowerCycling)
                        ShowDetectedToast(controller, wasPowerCycling);

                    PickTargetController();
                    PowerCyclers.TryRemove(baseContainerDeviceInstanceId, out _);
                }
                catch { }
                finally { }
            }
            finally
            {
                hidArrivalInProgress.TryRemove(key, out _);
            }
        });

        hidArrivalInProgress[key] = addTask;
    }

    private static void HidDeviceRemoved(PnPDetails details, Guid InterfaceGuid)
    {
        var key = details.baseContainerDeviceInstanceId;

        var removeTask = Task.Run(async () =>
        {
            // If add is still running for this HID device, wait before removing
            if (hidArrivalInProgress.TryGetValue(key, out var pendingAdd))
                try { await pendingAdd.ConfigureAwait(false); } catch { }

            try
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
                    if (controller is XInputController) return;
                    if (controller is SDLController) return;

                    PowerCyclers.TryGetValue(details.baseContainerDeviceInstanceId, out bool IsPowerCycling);
                    bool WasTarget = IsTargetController(controller.GetInstanceId());

                    LogManager.LogInformation("Generic controller {0} unplugged, cycling {1}", controller.ToString(), IsPowerCycling);
                    ControllerUnplugged?.Invoke(controller, IsPowerCycling, WasTarget);

                    if (!IsPowerCycling)
                    {
                        Controllers.TryRemove(details.baseContainerDeviceInstanceId, out _);

                        controller.Gone();

                        if (controller.IsPhysical() && HIDuncloakondisconnect)
                            controller.Unhide(false);

                        if (ClearTargetIfMatch(controller.GetInstanceId()))
                            PickTargetController();
                        else
                            controller.Dispose();
                    }
                }
                catch { }
                finally { }
            }
            finally
            {
                hidRemovalInProgress.TryRemove(key, out _);
            }
        });

        hidRemovalInProgress[key] = removeTask;
    }
    #endregion

    #region XUsbDevice
    private static void XUsbDeviceArrived(PnPDetails details, Guid InterfaceGuid)
    {
        var key = details.baseContainerDeviceInstanceId;

        var addTask = Task.Run(async () =>
        {
            // If a removal is running for this controller, wait first
            if (xusbRemovalInProgress.TryGetValue(key, out var pendingRemove))
                try { await pendingRemove.ConfigureAwait(false); } catch { }

            try
            {
                try
                {
                    Controllers.TryGetValue(details.baseContainerDeviceInstanceId, out IController controller);
                    PowerCyclers.TryGetValue(details.baseContainerDeviceInstanceId, out bool IsPowerCycling);

                    if (controller != null)
                    {
                        if (controller is DInputController) return;
                        if (controller is SDLController) return;

                        ((XInputController)controller).AttachDetails(details);

                        if (controller.GetInstanceId() != details.deviceInstanceId)
                        {
                            if (controller.IsHidden())
                                controller.Hide(false);
                            else
                                controller.Unhide(false);
                        }

                        IsPowerCycling = true;
                        PowerCyclers[details.baseContainerDeviceInstanceId] = IsPowerCycling;
                    }
                    else
                    {
                        switch (details.GetVendorID())
                        {
                            // Asus
                            case "0x0B05":
                                {
                                    switch (details.GetProductID())
                                    {
                                        case "0x1ABE": // ASUS Xbox Adaptive Controller
                                        case "0x1B4C": // ASUS Xbox Adaptive Controller
                                            try { controller = new XboxAdaptiveController(details); } catch { }
                                            break;
                                    }
                                }
                                break;

                            case "0x17EF":
                            case "0x1A86":
                                switch (details.GetProductID())
                                {
                                    case "0x6182":
                                    case "0x61EB":
                                        try { controller = new LegionController(details); } catch { }
                                        break;

                                    case "0xE310":
                                        try { controller = new LegionControllerS(details); } catch { }
                                        break;

                                    default:
                                        try { controller = new XInputController(details); } catch { }
                                        break;
                                }
                                break;

                            case "0x3537":
                                switch (details.GetProductID())
                                {
                                    case "0x1099":
                                    case "0x103E":
                                        details.isDongle = true;
                                        goto case "0x1050";
                                    default:
                                    case "0x1050":
                                        try { controller = new TarantulaProController(details); } catch { }
                                        break;
                                }
                                break;

                            case "0x0DB0":
                                switch (details.GetProductID())
                                {
                                    case "0x1901":
                                        try { controller = new XClawController(details); } catch { }
                                        break;
                                }
                                break;

                            case "0x1EE9":
                                switch (details.GetProductID())
                                {
                                    case "0x1590":
                                        try { controller = new ZoneController(details); } catch { }
                                        break;
                                }
                                break;
                        }
                    }

                    if (controller is null)
                    {                        
                        try 
                        { 
                            controller = new XInputController(details); 
                        }
                        catch 
                        {
                            LogManager.LogWarning("Unsupported XInput controller: VID:{0} and PID:{1}", details.GetVendorID(), details.GetProductID());
                            return;
                        }
                    }

                    while (!controller.IsReady && controller.IsConnected())
                        await Task.Delay(250).ConfigureAwait(false);

                    // controller is gone ?
                    if (!controller.IsConnected() && !controller.IsVirtual())
                    {
                        controller.Gone();
                        return;
                    }

                    string baseContainerDeviceInstanceId = details.baseContainerDeviceInstanceId;
                    bool wasPowerCycling = PowerCyclers.TryGetValue(baseContainerDeviceInstanceId, out var powerCycling) && powerCycling;

                    controller.IsBusy = false;

                    Controllers[baseContainerDeviceInstanceId] = controller;

                    LogManager.LogInformation("XInput controller {0} plugged", controller.ToString());
                    ControllerPlugged?.Invoke(controller, false);

                    if (!wasPowerCycling)
                        ShowDetectedToast(controller, wasPowerCycling);

                    PickTargetController();
                    PowerCyclers.TryRemove(baseContainerDeviceInstanceId, out _);
                }
                catch { }
                finally { }
            }
            finally
            {
                xusbArrivalInProgress.TryRemove(key, out _);
            }
        });

        xusbArrivalInProgress[key] = addTask;
    }

    private static void XUsbDeviceRemoved(PnPDetails details, Guid InterfaceGuid)
    {
        var key = details.baseContainerDeviceInstanceId;

        var removeTask = Task.Run(async () =>
        {
            // If add is still running for this controller, wait before removing
            if (xusbArrivalInProgress.TryGetValue(key, out var pendingAdd))
                try { await pendingAdd.ConfigureAwait(false); } catch { }

            try
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
                    if (controller is DInputController) return;
                    if (controller is SDLController) return;

                    PowerCyclers.TryGetValue(details.baseContainerDeviceInstanceId, out bool IsPowerCycling);
                    bool WasTarget = IsTargetController(controller.GetInstanceId());

                    LogManager.LogInformation("XInput controller {0} unplugged, cycling {1}", controller.ToString(), IsPowerCycling);
                    ControllerUnplugged?.Invoke(controller, IsPowerCycling, WasTarget);

                    if (!IsPowerCycling)
                    {
                        // Remove from the dictionary first so PickTargetController and any
                        // callbacks triggered by Gone()/Dispose() never see this controller.
                        Controllers.TryRemove(details.baseContainerDeviceInstanceId, out _);

                        controller.Gone();

                        if (controller.IsPhysical() && HIDuncloakondisconnect)
                            controller.Unhide(false);

                        // Atomically check-and-clear under targetLock to avoid clearing a
                        // controller that SetTargetController just switched to on another thread.
                        if (ClearTargetIfMatch(controller.GetInstanceId()))
                            PickTargetController();
                        else
                            controller.Dispose();
                    }
                }
                catch { }
                finally { }
            }
            finally
            {
                xusbRemovalInProgress.TryRemove(key, out _);
            }
        });

        xusbRemovalInProgress[key] = removeTask;
    }
    #endregion

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

        // Stop slot monitor
        StopSlotMonitor();

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
        ToastManager.CommandReceived -= ToastCommandRouter;

        ControllerPlugged -= ControllerManager_ControllerPlugged;
        ControllerUnplugged -= ControllerManager_ControllerUnplugged;

        // manage device events
        IDevice.GetCurrent().KeyPressed -= CurrentDevice_KeyPressed;
        IDevice.GetCurrent().KeyReleased -= CurrentDevice_KeyReleased;

        // halt controller manager and unplug on close
        // todo: we might need to use lock (targetLock) within Tick event.
        Suspend(true);

        // stop timer(s)
        scenarioTimer.Elapsed -= ScenarioTimer_Elapsed;
        scenarioTimer.Stop();

        pickTimer.Elapsed -= PickTimer_Elapsed;
        pickTimer.Stop();

        foreach (IController controller in GetPhysicalControllers<IController>())
        {
            // uncloak on close, if requested
            if (HIDuncloakonclose)
                controller.Unhide(!controller.IsBluetooth());

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
                    if (foregroundProcess?.Platform == GamePlatform.Steam)
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

            case "ControllerSlotManagementMode":
                {
                    int modeInt = 0;
                    if (value is not null && int.TryParse(value.ToString(), out int parsed))
                        modeInt = parsed;

                    slotManagementMode = (ControllerSlotManagementMode)Math.Max(0, Math.Min(1, modeInt));

                    // Reset failure counter so re-enabling Automatic mode gets fresh attempts.
                    if (slotManagementMode == ControllerSlotManagementMode.Automatic)
                        consecutiveWatchdogFailures = 0;
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
        SettingsManager_SettingValueChanged("ControllerSlotManagementMode", ManagerFactory.settingsManager.GetString("ControllerSlotManagementMode"), false);
        SettingsManager_SettingValueChanged("SensorSelection", ManagerFactory.settingsManager.GetString("SensorSelection"), false);
        SettingsManager_SettingValueChanged("SteamControllerMode", ManagerFactory.settingsManager.GetString("SteamControllerMode"), false);
    }

    private static void DeviceManager_Initialized()
    {
        QueryDevices();
    }

    private static void QueryDevices()
    {
        // manage events
        ManagerFactory.deviceManager.XUsbDeviceArrived += XUsbDeviceArrived;
        ManagerFactory.deviceManager.XUsbDeviceRemoved += XUsbDeviceRemoved;
        ManagerFactory.deviceManager.HidDeviceArrived += HidDeviceArrived;
        ManagerFactory.deviceManager.HidDeviceRemoved += HidDeviceRemoved;

        Rescan();
    }

    public static void Rescan()
    {
        ManagerFactory.deviceManager.RefreshDInputAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        ManagerFactory.deviceManager.RefreshXInputAsync().ConfigureAwait(false).GetAwaiter().GetResult();

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
        // Slot monitor is always running; on resume we simply re-evaluate the target controller.
        PickTargetController();
    }

    public static void Suspend(bool OS)
    {
        // Stop any in-flight slot fix to avoid manipulating devices during suspend/shutdown.
        StopWatchdog();

        ClearTargetController();
    }

    public enum SlotFixTrigger
    {
        Manual = 0,
        Automatic = 1
    }

    /// <summary>
    /// Update the public slot-issue state used by the UI (manual fallback button).
    /// This must be low-noise: only raise when state/reason actually changes.
    /// </summary>
    private static void SetSlotIssueState(bool hasIssue, string reason)
    {
        reason ??= string.Empty;

        if (HasSlotIssue == hasIssue && string.Equals(SlotIssueReason, reason, StringComparison.Ordinal))
            return;

        HasSlotIssue = hasIssue;
        HasVirtualSlot1Issue = hasIssue && reason == "Virtual controller is not occupying slot 1.";
        SlotIssueReason = reason;
        SlotIssueChanged?.Invoke(hasIssue, reason);
    }

    private static void ControllerManager_ControllerPlugged(IController controller, bool isPowerCycling)
    {
        // Events caused by our own power-cycling (watchdog / hide) are not real topology changes.
        if (isPowerCycling)
            return;

        _ = Task.Run(HandleControllerTopologyChangedAsync);
    }

    private static void ControllerManager_ControllerUnplugged(IController controller, bool isPowerCycling, bool wasTarget)
    {
        // Events caused by our own power-cycling (watchdog / hide) are not real topology changes.
        if (isPowerCycling)
            return;

        _ = Task.Run(HandleControllerTopologyChangedAsync);
    }

    /// <summary>
    /// A controller was plugged/unplugged. This is the only moment we proactively prompt the user (Manual mode).
    /// </summary>
    private static async Task HandleControllerTopologyChangedAsync()
    {
        // Let Windows settle XInput assignments.
        await Task.Delay(500).ConfigureAwait(false);

        // If a fix run is already active, let it finish; the monitor will update state.
        if (watchdogThreadRunning)
            return;

        SlotProbeResult probe = await ProbeSlotsAsync().ConfigureAwait(false);
        SetSlotIssueState(probe.NeedsFix, probe.Reason);

        if (!probe.NeedsFix)
            return;

        switch (slotManagementMode)
        {
            case ControllerSlotManagementMode.Automatic:
                StartWatchdog(SlotFixTrigger.Automatic, resetAttempts: false);
                break;

            case ControllerSlotManagementMode.Manual:
            default:
                TrySendSlotFixPromptToast(probe.Reason);
                break;
        }
    }

    /// <summary>
    /// Starts the controller slot monitor thread (always running). The monitor detects invalid slot state and either
    /// auto-fixes (Automatic) or prompts the user (Manual).
    /// </summary>
    private static void StartSlotMonitor()
    {
        if (Interlocked.Exchange(ref slotMonitorStarted, 1) == 1)
            return;

        lock (slotMonitorLock)
        {
            slotMonitorThreadRunning = true;
            slotMonitorThread = new Thread(SlotMonitorLoop)
            {
                IsBackground = true,
                Name = "ControllerSlotMonitor"
            };
            slotMonitorThread.Start();
        }
    }

    private static void StopSlotMonitor()
    {
        if (Interlocked.Exchange(ref slotMonitorStarted, 0) == 0)
            return;

        slotMonitorThreadRunning = false;
        if (slotMonitorThread?.IsAlive == true)
            slotMonitorThread.Join(2000);
        slotMonitorThread = null;
    }

    /// <summary>
    /// Public entrypoint for Manual mode UI/button and toast action.
    /// Starts a finite slot-fix run that will stop automatically once completed.
    /// </summary>
    public static void TriggerSlotFix(bool resetAttempts)
    {
        StartWatchdog(SlotFixTrigger.Manual, resetAttempts);
    }

    /// <summary>
    /// Starts a slot-fix run using the current settings (defaults to Automatic trigger). The thread will automatically stop
    /// once the run completes (success or failure).
    /// </summary>
    public static void StartWatchdog()
    {
        StartWatchdog(SlotFixTrigger.Automatic, resetAttempts: false);
    }

    private static void StartWatchdog(SlotFixTrigger trigger, bool resetAttempts)
    {
        if (resetAttempts)
        {
            Interlocked.Exchange(ref ControllerManagementAttempts, 0);
            consecutiveWatchdogFailures = 0;
        }

        // If a run is already active, do not start another one.
        if (Interlocked.Exchange(ref watchdogStarted, 1) == 1)
            return;

        lock (watchdogLock)
        {
            watchdogThreadRunning = true;
            watchdogThread = new Thread(() => WatchdogLoop(trigger))
            {
                IsBackground = true,
                Name = "ControllerSlotFix"
            };
            watchdogThread.Start();
        }
    }

    public static void StopWatchdog()
    {
        if (Interlocked.Exchange(ref watchdogStarted, 0) == 0)
            return;

        watchdogThreadRunning = false;
        if (watchdogThread?.IsAlive == true)
            watchdogThread.Join(3000);
        watchdogThread = null;
    }

    private static void VirtualManager_Vibrated(byte LargeMotor, byte SmallMotor)
    {
        targetController?.SetVibration(LargeMotor, SmallMotor);
    }

    public static void Unplug(IController controller)
    {
        string baseContainerDeviceInstanceId = controller.GetContainerInstanceId();

        try
        {
            bool WasTarget = IsTargetController(controller.GetInstanceId());

            LogManager.LogInformation("XInput controller {0} force unplugged", controller.ToString());
            ControllerUnplugged?.Invoke(controller, false, WasTarget);

            PowerCyclers.TryRemove(baseContainerDeviceInstanceId, out _);
            Controllers.TryRemove(baseContainerDeviceInstanceId, out _);

            controller.Gone();

            if (controller.IsPhysical() && HIDuncloakondisconnect)
                controller.Unhide(false);

            if (ClearTargetIfMatch(controller.GetInstanceId()))
                PickTargetController();
            else
                controller.Dispose();
        }
        catch { }
    }

    private static List<XInputController> InvalidSlotAssignments = new();

    private sealed record SlotProbeResult(
        bool NeedsFix,
        bool EnsureVirtualSlot1,
        bool VirtualInSlot1,
        bool HasInvalidControllers,
        bool HasInvalidVirtual,
        string Reason)
    {
        public static readonly SlotProbeResult Healthy =
            new(false, false, true, false, false, string.Empty);
    }

    private static void SlotMonitorLoop()
    {
        // A small polling loop that detects invalid slot assignment.
        // It MUST NOT proactively prompt (toast) in Manual mode; prompting is event-driven on plug/unplug.
        while (slotMonitorThreadRunning)
        {
            Thread.Sleep(1000);

            // If a slot-fix run is active, do not interfere.
            if (watchdogThreadRunning)
                continue;

            // Update UI state continuously so the Manual fallback button is available even if a toast was ignored.
            SlotProbeResult probe = ProbeSlotsAsync().GetAwaiter().GetResult();
            SetSlotIssueState(probe.NeedsFix, probe.Reason);

            if (!probe.NeedsFix)
            {
                MarkControllerManagementSuccess();
                continue;
            }

            // Automatic mode can still self-heal from polling (no toast).
            if (slotManagementMode == ControllerSlotManagementMode.Automatic)
                StartWatchdog(SlotFixTrigger.Automatic, resetAttempts: false);
        }
    }

    private static async Task<SlotProbeResult> ProbeSlotsAsync()
    {
        await slotStateSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            var slotOwners = new Dictionary<byte, XInputController>();
            var newInvalid = new List<XInputController>();

            var tasks = GetControllers<XInputController>()
                .Where(c => !c.IsDummy() && !c.IsBusy)
                .Select(async controller =>
                {
                    byte index = await DeviceManager.GetXInputIndexAsync(controller.GetContainerPath()).ConfigureAwait(false);
                    if (index == byte.MaxValue)
                        index = (byte)XInputController.TryGetUserIndex(controller.Details);

                    // Skip controllers whose slot could not be determined —
                    // they must not be attached (UserIndex.Any cross-talk) or
                    // counted as duplicates (false-positive triggering FixDuplicateSlots).
                    if (index == byte.MaxValue)
                        return;

                    controller.AttachController(index);

                    lock (slotOwners)
                    {
                        if (slotOwners.TryGetValue(index, out var firstOwner))
                        {
                            // Mark both the original occupant and the new arrival as invalid.
                            lock (newInvalid)
                            {
                                if (!newInvalid.Contains(firstOwner))
                                    newInvalid.Add(firstOwner);
                                newInvalid.Add(controller);
                            }
                        }
                        else
                        {
                            slotOwners[index] = controller;
                        }
                    }
                });

            await Task.WhenAll(tasks).ConfigureAwait(false);
            InvalidSlotAssignments = newInvalid;

            bool hasInvalidControllers = newInvalid.Count > 0;
            bool hasInvalidVirtual = newInvalid.Any(c => c.IsVirtual());

            bool ensureVirtualSlot1 =
                VirtualManager.HIDmode == HIDmode.Xbox360Controller &&
                VirtualManager.HIDstatus == HIDstatus.Connected &&
                (HasPhysicalController<XInputController>() || HasVirtualController<XInputController>());

            bool virtualInSlot1 = !ensureVirtualSlot1 ||
                GetControllerFromSlot<XInputController>(UserIndex.One, false) is not null;

            bool needsFix = hasInvalidControllers || !virtualInSlot1;

            string reason = hasInvalidControllers ? "Duplicate controller slot assignment detected."
                : !virtualInSlot1 ? "Virtual controller is not occupying slot 1."
                : string.Empty;

            return new SlotProbeResult(needsFix, ensureVirtualSlot1, virtualInSlot1, hasInvalidControllers, hasInvalidVirtual, reason);
        }
        finally
        {
            slotStateSemaphore.Release();
        }
    }

    private static void TrySendSlotFixPromptToast(string reason)
    {
        // Ignore window
        if (DateTime.UtcNow < slotFixIgnoreUntilUtc)
            return;

        // Debounce prompts
        if ((DateTime.UtcNow - slotFixLastPromptUtc) < TimeSpan.FromSeconds(30))
            return;

        slotFixLastPromptUtc = DateTime.UtcNow;

        ToastManager.SendToast(new ToastRequest
        {
            Title = "Controller slot management",
            Content = string.IsNullOrWhiteSpace(reason)
                ? "A controller slot issue was detected. Click Fix to attempt a reset."
                : $"A controller slot issue was detected: {reason} Click Fix to attempt a reset.",
            Actions =
        {
            new ToastAction
            {
                Label = "Adjust order",
                Command = "SlotFixReset",
                Callback = _ => TriggerSlotFix(resetAttempts: true)
            },
            new ToastAction
            {
                Label = "Ignore",
                Command = "SlotFixIgnore",
                Callback = _ => slotFixIgnoreUntilUtc = DateTime.UtcNow.AddMinutes(5)
            }
        }
        });
    }

    private static void WatchdogLoop(SlotFixTrigger trigger)
    {
        try
        {
            Interlocked.Exchange(ref ControllerManagementAttempts, 0);
            UpdateStatus(ControllerManagerStatus.Busy);

            for (int i = 0; i < ControllerManagementMaxAttempts && watchdogThreadRunning; i++)
            {
                Interlocked.Exchange(ref ControllerManagementAttempts, i + 1);
                UpdateStatus(ControllerManagerStatus.Busy);

                SlotProbeResult probe = ProbeSlotsAsync().GetAwaiter().GetResult();
                if (!probe.NeedsFix)
                {
                    MarkControllerManagementSuccess();
                    return;
                }

                if (probe.HasInvalidControllers || (probe.EnsureVirtualSlot1 && !probe.VirtualInSlot1))
                {
                    /*
                    if (OpenXInput.IsAvailable)
                    {
                        XInputController? virtualController = GetVirtualControllers<XInputController>().FirstOrDefault();
                        if (virtualController is not null && AssignXInputSlot(virtualController, 0))
                        {
                            MarkControllerManagementSuccess();
                            return;
                        }
                    }
                    else
                    {
                        if (probe.HasInvalidControllers)
                            FixDuplicateSlots(probe);

                        if (probe.EnsureVirtualSlot1 && !probe.VirtualInSlot1)
                        {
                            bool shouldContinue = FixVirtualSlot(probe);
                            if (!shouldContinue)
                                break;
                        }
                    }
                    */

                    if (probe.HasInvalidControllers)
                        FixDuplicateSlots(probe);

                    if (probe.EnsureVirtualSlot1 && !probe.VirtualInSlot1)
                    {
                        bool shouldContinue = FixVirtualSlot(probe);
                        if (!shouldContinue)
                            break;
                    }
                }

                // Give Windows a moment to settle, then re-probe.
                Thread.Sleep(1000);

                probe = ProbeSlotsAsync().GetAwaiter().GetResult();
                if (!probe.NeedsFix)
                {
                    MarkControllerManagementSuccess();
                    return;
                }
            }

            FinalizeFailedRun();
        }
        catch
        {
            FinalizeFailedRun();
        }
        finally
        {
            watchdogThreadRunning = false;
            Interlocked.Exchange(ref watchdogStarted, 0);
            watchdogThread = null;
        }
    }

    /// <summary>
    /// Assigns <paramref name="controller"/> to <paramref name="targetSlot"/> via OpenXInput,
    /// then power-cycles the moved controller and any controller displaced from that slot
    /// so that running applications refresh their XInput slot bookkeeping.
    /// </summary>
    /// <returns>True on success; false if the SetUserIndex call failed.</returns>
    public static bool AssignXInputSlot(XInputController controller, byte targetSlot)
    {
        if (controller.UserIndex == targetSlot)
            return true;

        // Snapshot the current occupant of targetSlot before the swap.
        XInputController? displaced =
            GetControllerFromSlot<XInputController>((UserIndex)targetSlot, true) ??
            GetControllerFromSlot<XInputController>((UserIndex)targetSlot, false);

        uint result = OpenXInput.SetUserIndex(controller.GetContainerPath(), targetSlot, false);
        if (result != OpenXInput.ERROR_SUCCESS)
            return false;

        // Cycle the displaced controller first so it vacates the target slot on the bus
        // before the moved controller reconnects under the new index.
        if (displaced is not null && !ReferenceEquals(displaced, controller))
            displaced.CyclePort();

        // Cycle the moved controller — forces running apps to see it in its new slot.
        controller.CyclePort();

        return true;
    }

    /// <summary>
    /// Handles duplicate slot assignments: restarts the virtual stack if involved,
    /// then cycles the physical controllers that are in conflict.
    /// </summary>
    private static void FixDuplicateSlots(SlotProbeResult probe)
    {
        if (probe.HasInvalidVirtual)
        {
            VirtualManager.Suspend(false);
            Thread.Sleep(1000);
            VirtualManager.Resume(false);

            // Wait for the virtual controller to actually reconnect before
            // cycling physical controllers — otherwise the freed slot can
            // be immediately reclaimed by a physical device.
            WaitUntil(
                () => GetVirtualControllers<XInputController>().Any(),
                TimeSpan.FromSeconds(4));
        }

        foreach (IController controller in InvalidSlotAssignments)
        {
            if (!controller.IsVirtual())
            {
                controller.CyclePort();

                // Allow the device to fully disappear before cycling the next
                // one — rapid back-to-back cycles can cause re-enumeration
                // collisions in the USB stack.
                Thread.Sleep(500);
            }
        }
    }

    /// <summary>
    /// Ensures the virtual Xbox 360 controller occupies slot 1.
    /// Returns false if the run should be aborted (e.g. a busy wireless controller is blocking).
    /// </summary>
    private static bool FixVirtualSlot(SlotProbeResult probe)
    {
        if (!HasPhysicalController<XInputController>())
        {
            // No physical XInput controller — just cycle the virtual controller.
            if (HasVirtualController<XInputController>() &&
                GetControllerFromSlot<XInputController>(UserIndex.One, false) is null)
            {
                VirtualManager.Suspend(false);
                Thread.Sleep(1000);
                VirtualManager.Resume(false);

                WaitUntil(
                    () => GetVirtualControllers<XInputController>(VirtualManager.VendorId, VirtualManager.ProductId).Any(),
                    TimeSpan.FromSeconds(4));
            }
            return true;
        }

        // Physical XInput controller present — find any physical controller, prioritizing lower slots.
        XInputController? pController = null;
        foreach (UserIndex slot in new[] { UserIndex.One, UserIndex.Two, UserIndex.Three, UserIndex.Four, UserIndex.Any })
        {
            pController = GetControllerFromSlot<XInputController>(slot, true);
            if (pController is not null)
                break;
        }

        if (pController is null)
            return false;

        // Abort if a wireless controller is currently busy and not already power-cycling.
        var busyWireless = GetPhysicalControllers<XInputController>().FirstOrDefault(c => c.IsBluetooth() && c.IsBusy);
        if (busyWireless is not null && !PowerCyclers.TryGetValue(busyWireless.GetContainerInstanceId(), out _))
            return false;

        SuspendController(pController.GetContainerInstanceId());

        // Wait for the suspended controller to actually vacate its slot before
        // manipulating the virtual controller — otherwise it may still occupy the
        // slot when the virtual controller is recreated.
        byte suspendedSlot = pController.UserIndex;
        if (suspendedSlot != byte.MaxValue)
        {
            WaitUntil(
                () => GetControllerFromSlot<XInputController>((UserIndex)suspendedSlot, true) is null,
                TimeSpan.FromSeconds(4));
        }

        // Remove virtual controller to free slot 1.
        VirtualManager.SetControllerMode(HIDmode.NoController);
        WaitUntil(() => !GetVirtualControllers<XInputController>().Any(), TimeSpan.FromSeconds(4));

        // Temporarily fill slots so the physical controller cannot reclaim slot 1.
        int usedSlots = VirtualManager.CreateTemporaryControllers();
        WaitUntil(() => GetVirtualControllers<XInputController>().Count() >= usedSlots, TimeSpan.FromSeconds(4));

        VirtualManager.DisposeTemporaryControllers();
        WaitUntil(() => GetVirtualControllers<XInputController>().Count() <= usedSlots, TimeSpan.FromSeconds(4));

        // Re-register the main virtual controller; it should now claim slot 1.
        VirtualManager.SetControllerMode(HIDmode.Xbox360Controller);
        WaitUntil(() => GetVirtualControllers<XInputController>().Any(), TimeSpan.FromSeconds(4));

        return true;
    }

    /// <summary>
    /// Blocks the current (dedicated background) thread until <paramref name="condition"/> is true
    /// or <paramref name="timeout"/> elapses, polling every 100 ms.
    /// </summary>
    private static void WaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline && !condition())
            Thread.Sleep(100);
    }

    private static void MarkControllerManagementSuccess()
    {
        ResumeControllers();

        // Successful slot fix: clear the issue flag used by UI/manual mode.
        SetSlotIssueState(false, string.Empty);

        consecutiveWatchdogFailures = 0;

        UpdateStatus(ControllerManagerStatus.Succeeded);
        Interlocked.Exchange(ref ControllerManagementAttempts, 0);
    }

    private static void FinalizeFailedRun()
    {
        ResumeControllers();

        Interlocked.Increment(ref consecutiveWatchdogFailures);

        // After repeated failures, fall back to Manual mode to stop the automatic retry loop.
        // The user can re-enable Automatic mode from the UI once the system has settled.
        if (consecutiveWatchdogFailures >= MaxConsecutiveWatchdogFailures &&
            slotManagementMode == ControllerSlotManagementMode.Automatic)
        {
            slotManagementMode = ControllerSlotManagementMode.Manual;
            ManagerFactory.settingsManager.SetProperty("ControllerSlotManagementMode", (int)ControllerSlotManagementMode.Manual);
        }

        try
        {
            SlotProbeResult finalProbe = ProbeSlotsAsync().GetAwaiter().GetResult();
            SetSlotIssueState(finalProbe.NeedsFix, finalProbe.Reason);
        }
        catch { }

        UpdateStatus(ControllerManagerStatus.Failed);
        Interlocked.Exchange(ref ControllerManagementAttempts, 0);
    }

    private static Notification ManagerBusy = new("Controller Manager", "Controllers order is being adjusted, your gamepad might become irresponsive for a few seconds.") { IsInternal = true };

    private static void UpdateStatus(ControllerManagerStatus status)
    {
        // skip if already correct
        if (managerStatus == status)
            return;

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

        TrySendSlotFixStatusToast(status);
    }

    private static void TrySendSlotFixStatusToast(ControllerManagerStatus status)
    {
        if (status == ControllerManagerStatus.Pending)
            return;

        DateTime now = DateTime.UtcNow;

        // Avoid spamming "Busy" toasts while the run loops.
        if (status == ControllerManagerStatus.Busy)
        {
            if (slotFixLastStatusToast == ControllerManagerStatus.Busy &&
                (now - slotFixLastStatusToastUtc) < TimeSpan.FromSeconds(20))
                return;
        }
        else
        {
            // For terminal statuses, only de-duplicate exact repeats in short intervals.
            if (slotFixLastStatusToast == status &&
                (now - slotFixLastStatusToastUtc) < TimeSpan.FromSeconds(5))
                return;
        }

        slotFixLastStatusToast = status;
        slotFixLastStatusToastUtc = now;

        string content = status switch
        {
            ControllerManagerStatus.Busy => ManagerBusy.Message,
            ControllerManagerStatus.Succeeded => "Controllers order was sucessfully adjusted.",
            ControllerManagerStatus.Failed => "Controllers order could not be adjusted. You can retry using the Manual action.",
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(content))
            return;

        ToastManager.SendToast(new ToastRequest
        {
            Title = "Controller management",
            Content = content
        });

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
            IController? internalController = controllers.FirstOrDefault(c => c.IsInternal());

            // Default: keep current target (reassigned below if a better candidate exists)
            string deviceInstanceId = targetController?.GetContainerInstanceId() ?? string.Empty;

            // If the user disabled auto-connect and already has a real controller, don't change anything.
            // (branches below are skipped via else-if chain)
            if (!ConnectOnPlug && targetController is not null && !targetController.IsDummy())
            {
                // deviceInstanceId already holds the current target — nothing to do
            }
            // Auto-connect to the most recently arrived external/wireless controller.
            // ConnectOnPlug is intentionally not checked here: if we reach this branch,
            // targetController is null or a dummy (branch 1 already guards the "keep current" case),
            // so we must always pick the best available replacement regardless of ConnectOnPlug.
            else if (latestExternalController is not null)
            {
                // If the current target is already an external/wireless controller, keep it —
                // we don't want to switch away when a second external controller is plugged in.
                if (targetController is not null && (targetController.IsWireless() || targetController.IsExternal()))
                    deviceInstanceId = targetController.GetContainerInstanceId();
                else
                    deviceInstanceId = latestExternalController.GetContainerInstanceId();
            }
            // Fallback: use the internal (built-in) controller if no external is available
            else if (internalController is not null)
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
            ClearTargetControllerInternal();
        }
    }

    /// <summary>
    /// Atomically clears the target only if it still matches <paramref name="instanceId"/>.
    /// Prevents a race where SetTargetController switches to a new controller between the
    /// unlocked WasTarget read and the subsequent ClearTargetController call.
    /// Returns true if the target was cleared.
    /// </summary>
    private static bool ClearTargetIfMatch(string instanceId)
    {
        lock (targetLock)
        {
            if (targetController?.GetInstanceId() != instanceId)
                return false;

            ClearTargetControllerInternal();
            return true;
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
        IController? selectedController = null;
        ControllerSelectedEventHandler? selectedHandlers = null;

        lock (targetLock)
        {
            // look for new controller
            if (!Controllers.TryGetValue(baseContainerDeviceInstanceId, out IController controller))
                return;

            // already self
            if (IsTargetController(controller.GetInstanceId()))
            {
                controller.Plug();
                return;
            }

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

            // Never invoke external code while holding targetLock.
            // Subscribers may touch UI / managers that also take locks during shutdown.
            selectedController = targetController;
            selectedHandlers = ControllerSelected;
        }

        try
        {
            selectedHandlers?.Invoke(selectedController);
        }
        catch { }
    }

    public static bool SuspendController(string baseContainerDeviceInstanceId)
    {
        try
        {
            // get controller
            if (Controllers.TryGetValue(baseContainerDeviceInstanceId, out IController controller))
            {
                // edge-case
                if (controller is XboxAdaptiveController xboxController)
                {
                    // set status
                    controller.IsBusy = true;
                    PowerCyclers[baseContainerDeviceInstanceId] = true;

                    return xboxController.Disable();
                }
            }

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
            return controller?.CyclePort() ?? false;
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
                        if (!string.IsNullOrEmpty(InfPath))
                        {
                            if (pnPDriver?.InfPath != InfPath)
                            {
                                // restore drivers
                                pnPDevice.RemoveAndSetup();
                                pnPDevice.InstallCustomDriver(InfPath, out bool rebootRequired);
                            }

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

        // edge case
        foreach (XboxAdaptiveController xboxAdaptiveController in GetPhysicalControllers<XboxAdaptiveController>())
            xboxAdaptiveController.Enable();

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
        if (profilePage && ProfilesPage.selectedProfile is not null)
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
            case HIDmode.DInputController:
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

    public static event SlotIssueChangedEventHandler SlotIssueChanged;
    public delegate void SlotIssueChangedEventHandler(bool hasIssue, string reason);

    public static event InitializedEventHandler Initialized;
    public delegate void InitializedEventHandler();

    #endregion
}