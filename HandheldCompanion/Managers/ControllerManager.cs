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
using DriverStoreHelper = HandheldCompanion.Helpers.DriverStoreHelper;
using MediaColor = System.Windows.Media.Color;
using Timer = System.Timers.Timer;

namespace HandheldCompanion.Managers;

public static class ControllerManager
{
    private static readonly ConcurrentDictionary<uint, SDLController> SDLControllers = new();
    private static readonly ConcurrentDictionary<string, IController> Controllers = new();

    #region Network controllers
    private static readonly ConcurrentDictionary<Guid, RemoteController> NetworkControllers = new();
    private static readonly ConcurrentDictionary<Guid, byte> DisconnectedNetworkControllers = new();
    private static Timer networkTimer = new(500) { AutoReset = true };
    #endregion
    public static readonly ConcurrentDictionary<string, bool> PowerCyclers = new();

    private static readonly ConcurrentDictionary<string, Task> xusbArrivalInProgress = new();
    private static readonly ConcurrentDictionary<string, Task> xusbRemovalInProgress = new();
    private static readonly ConcurrentDictionary<string, Task> hidArrivalInProgress = new();
    private static readonly ConcurrentDictionary<string, Task> hidRemovalInProgress = new();
    private static readonly ConcurrentDictionary<uint, Task> sdlArrivalInProgress = new();
    private static readonly ConcurrentDictionary<uint, Task> sdlRemovalInProgress = new();

    /// <summary>
    /// Maximum time an arrival/removal task waits for its counterpart before
    /// proceeding anyway. Prevents deadlocks when both fire concurrently for
    /// the same device during a power cycle.
    /// </summary>
    private static readonly TimeSpan CrossWaitTimeout = TimeSpan.FromSeconds(5);

    private static Thread? pumpThread = null;
    private static bool pumpThreadRunning;

    public enum ControllerSlotManagementMode
    {
        Manual = 0,
        Automatic = 1
    }

    public enum ControllerPlugBehavior
    {
        DoNothing = 0,
        AlwaysAsk = 1,
        AutoConnect = 2,
    }

    private static readonly ControllerSlotHelper slotHelper = new(UpdateStatus, SetSlotIssueState);

    // Steam hybrid mode: temporarily overridden HIDmode when Steam process has foreground
    private static HIDmode previousHIDmode = HIDmode.NotSelected;

    private static readonly DummyXbox360Controller dummyXbox360 = new();
    private static readonly DummyDualShock4Controller dummyDualShock4 = new();
    private static readonly DummyDualSenseController dummyDualSense = new();
    private static readonly DummySteamDeckController dummySteamDeck = new();
    private static readonly DummySwitchProController dummySwitchPro = new();
    public static bool HasTargetController => GetTarget() != null;

    private static IController? targetController;
    private static ProcessEx? foregroundProcess;
    private static bool ControllerMuted;
    private static readonly object networkStreamingLock = new();
    private static readonly HashSet<Guid> streamingControllers = [];
    private static bool localBrightnessDimmedForStreaming;

    private static readonly object targetLock = new();
    private static readonly object targetTransitionLock = new();
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

        // Hints must be set before SDL_Init() — SDL reads them during subsystem initialization.
        // Disable XInput so SDL does not enumerate XInput devices as SDL gamepads; those are
        // handled separately through the XUsbDevice pipeline.
        SDL.SetHint(SDL.Hints.XInputEnabled, "0");
        // Prevent SDL from exposing the Steam virtual controller and Steam Deck built-in
        // controller through the HID API, avoiding double-enumeration with our own paths.
        SDL.SetHint(SDL.Hints.JoystickHIDAPISteam, "1");
        SDL.SetHint(SDL.Hints.JoystickHIDAPISteamdeck, "1");

        // Initialize the SDL Gamepad subsystem
        if (!SDL.Init(SDL.InitFlags.Gamepad))
            LogManager.LogError("SDL_Init Error: {0}", SDL.GetError());
        else
        {
            LogManager.LogInformation("SDL was successfully initialized");

            // Populate SDL's internal gamepad mapping table from the community database
            LoadGamepadMappings();

            // Suppress all SDL event types by default to keep the event queue clean.
            foreach (SDL.EventType eventType in Enum.GetValues<SDL.EventType>())
                SDL.SetEventEnabled((uint)eventType, false);

            // Only GamepadAdded / GamepadRemoved are processed by the pump thread loop.
            SDL.SetEventEnabled((uint)SDL.EventType.GamepadAdded, true);
            SDL.SetEventEnabled((uint)SDL.EventType.GamepadRemoved, true);
        }

        // manage pump thread
        pumpThreadRunning = true;
        pumpThread = new Thread(pumpThreadLoop)
        {
            IsBackground = true,
            Priority = ThreadPriority.AboveNormal
        };
        pumpThread.Start();

        // Start controller slot monitor (always running)
        slotHelper.Start();

        // manage events
        TimerManager.Tick += Tick;
        NetworkControllerHelper.PacketReceived += NetworkControllerHelper_PacketReceived;
        NetworkControllerHelper.VibrationReceived += NetworkControllerHelper_VibrationReceived;
        NetworkControllerHelper.StreamingChanged += NetworkControllerHelper_StreamingChanged;
        UIGamepad.GotFocus += GamepadFocusManager_FocusChanged;
        UIGamepad.LostFocus += GamepadFocusManager_FocusChanged;
        VirtualManager.Vibrated += VirtualManager_Vibrated;
        App.uiSettings.ColorValuesChanged += OnColorValuesChanged;
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

        networkTimer.Elapsed += NetworkTimer_Elapsed;
        networkTimer.Start();

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

    /// <summary>
    /// Waits for a controller to be ready or gone, retrying physical disconnection checks.
    /// Returns true if the controller is confirmed gone.
    /// </summary>
    private static async Task<bool> IsControllerGoneAsync(IController controller)
    {
        const int maxGoneAttempts = 3;
        int goneAttempts = 0;

        while (!controller.IsReady || !controller.IsConnected())
        {
            if (controller.IsConnected())
            {
                goneAttempts = 0;
            }
            else if (controller.IsVirtual())
            {
                return false;
            }
            else if (++goneAttempts >= maxGoneAttempts)
            {
                return true;
            }

            await Task.Delay(1000).ConfigureAwait(false);
        }

        return false;
    }

    private static void ToastCommandRouter(string command, IReadOnlyDictionary<string, string> args)
    {
        try
        {
            switch (command)
            {
                case "DimLocalBrightness":
                    DimLocalBrightnessForStreaming();
                    break;
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
                    slotHelper.TriggerFix(resetAttempts: true);
                    break;
                case "SlotFixIgnore":
                    // User explicitly dismissed the prompt; suppress prompts for a short period.
                    slotHelper.SetIgnoreWindow();
                    break;
            }
        }
        catch { /* ignore */ }
    }


    private static void Tick(long ticks, float delta)
    {
        RemoveTimedOutNetworkControllers();

        IController? tc;
        lock (targetLock)
            tc = targetController;

        if (tc is null)
            return;

        tc.Tick(ticks, delta);

        // snapshot inputs; bail if not ready
        ControllerState controllerState = tc.Inputs;
        if (controllerState is null)
            return;

        // snapshot motions; button-only controllers may not have a motion sensor
        Dictionary<byte, GamepadMotion> motions = tc.gamepadMotions;

        // get main motion, falling back to the device sensor for button-only controllers
        byte gamepadIndex = tc.gamepadIndex;
        if (!motions.TryGetValue(gamepadIndex, out GamepadMotion? gamepadMotion) || gamepadMotion is null)
            gamepadMotion = IDevice.GetCurrent().GamepadMotion;

        // sensor override
        switch (SensorsManager.ActiveSensorFamily)
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

        if (tc is not RemoteController)
        {
            // Publish local controller state to an authorized peer after the selected sensor has updated it.
            NetworkControllerHelper.Publish(tc);

            // Do not allow a controller to be used remotely if it is already broadcasting to a peer.
            if (NetworkControllerHelper.IsBroadcasting(tc))
                return;
        }

        // raise event, before layout mapping
        InputsUpdated?.Invoke(controllerState, false);

        // Update motion consumers (null-safe)
        MotionManager.UpdateReport(controllerState, gamepadMotion, delta);
        App.overlayModel?.UpdateReport(controllerState, gamepadMotion, delta);

        // compute layout (null-safe mapping)
        ControllerState mapped = ManagerFactory.layoutManager?.MapController(controllerState, delta) ?? controllerState;
        InputsUpdatedEventHandler? inputsUpdated = InputsUpdated;
        if (inputsUpdated is not null)
        {
            if (ReferenceEquals(mapped, controllerState))
                inputsUpdated(mapped, true);
            else
                EventHelper.RaiseInputsUpdatedAsync(inputsUpdated, mapped, true);
        }

        // controller is muted
        if (ControllerMuted)
            mapped = mutedState;

        // Auto-raise pad touch flags when pad axes exceed deadzone, so downstream consumers don't require an explicit touch button mapping from the user.
        // Use deadzone to prevent noise/drift from triggering unwanted input (e.g., phantom scrolling on DualShock4)
        const short PAD_TOUCH_DEADZONE = 500;
        mapped.ButtonState[ButtonFlags.LeftPadTouch] |= Math.Abs((int)mapped.AxisState[AxisFlags.LeftPadX]) > PAD_TOUCH_DEADZONE || Math.Abs((int)mapped.AxisState[AxisFlags.LeftPadY]) > PAD_TOUCH_DEADZONE;
        mapped.ButtonState[ButtonFlags.RightPadTouch] |= Math.Abs((int)mapped.AxisState[AxisFlags.RightPadX]) > PAD_TOUCH_DEADZONE || Math.Abs((int)mapped.AxisState[AxisFlags.RightPadY]) > PAD_TOUCH_DEADZONE;

        DS4Touch.UpdateInputs(mapped);
        VirtualManager.UpdateInputs(mapped, gamepadMotion);
        DSUServer.UpdateInputs(mapped, motions);
        DSUServer.Tick(ticks, delta);
    }

    #region Network controller transport

    private static void NetworkTimer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        IController? controller;
        lock (targetLock)
            controller = targetController;

        if (controller is RemoteController remoteController && !remoteController.IsTimedOut)
            NetworkControllerHelper.RequestController(remoteController.NetworkId);
    }

    private static void pumpThreadLoop(object? obj)
    {
        while (pumpThreadRunning)
        {
            // check controller events every 1000ms; this is used by SDLController to detect disconnections and hotplug events
            if (SDL.WaitEvent(out SDL.Event e))
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

        bool showActions = PlugBehavior == ControllerPlugBehavior.AlwaysAsk;

        Color winColor = App.uiSettings.GetColorValue(UIColorType.Foreground);

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
            Title = "Controller connected",
            Content = $"{controller.ToString()} #{controller.GetUserIndex() + 1}",
            ActivationCommand = "OpenControllerPage",
            Actions = showActions ? actions : new(),
        });
    }

    #region SDL
    private static void SDL_GamepadAdded(uint deviceIndex)
    {
        var addTask = Task.Run(async () =>
        {
            // If a removal is running for this SDL slot, wait it out first (with timeout to avoid deadlock)
            if (sdlRemovalInProgress.TryGetValue(deviceIndex, out var pendingRemove))
                try { await pendingRemove.WaitAsync(CrossWaitTimeout).ConfigureAwait(false); } catch { /* swallow */ }

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

                    PnPDetails? details = await DeviceManager.GetDeviceFromInstanceIdAsync(path).ConfigureAwait(false);
                    if (details is null)
                    {
                        LogManager.LogError("Failed to retrieve PnPDetails for controller {0}", deviceIndex);
                        return;
                    }

                    try
                    {
                        Controllers.TryGetValue(details.baseContainerDeviceInstanceId, out IController? controller);
                        PowerCyclers.TryGetValue(details.baseContainerDeviceInstanceId, out bool IsPowerCycling);

                        if (controller != null)
                        {
                            if (controller is XInputController or LegionControllerXInput) return;
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
                                    {
                                        int VendorId = details.VendorID;
                                        int ProductId = details.ProductID;

                                        switch (VendorId)
                                        {
                                            case 0x28DE:
                                                switch (ProductId)
                                                {
                                                    case 0x1102:
                                                    case 0x1142:
                                                    case 0x1205: // Steam Deck Controller (Neptune)
                                                    case 0x12f0: // SteamOS Handheld Controller
                                                        break;

                                                    case 0x1302: // Steam Controller 2026 (Wired)
                                                    case 0x1304: // Steam Controller 2026 (Wireless)
                                                        controller = new SteamController2026(gamepad, deviceIndex, details);
                                                        break;
                                                }
                                                break;

                                            default:
                                                controller = new Xbox360Controller(gamepad, deviceIndex, details);
                                                break;
                                        }
                                    }
                                    break;

                                case SDL.GamepadType.Xbox360:
                                case SDL.GamepadType.XboxOne:
                                    // XInput controllers are handled exclusively by the XInput pipeline (XUsbDeviceArrived).
                                    // SDL detection is expected; skip silently and let XInput manage it.
                                    return;

                                case SDL.GamepadType.PS3:
                                case SDL.GamepadType.PS4:
                                    controller = new DualShock4Controller(gamepad, deviceIndex, details);
                                    break;
                                case SDL.GamepadType.PS5:
                                    controller = new DualSenseController(gamepad, deviceIndex, details);
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

                        // controller is gone ?
                        if (await IsControllerGoneAsync(controller))
                        {
                            LogManager.LogWarning("SDL controller: VID:{0} and PID:{1} was gone while being added", details.GetVendorID(), details.GetProductID());
                            controller.Gone();
                            return;
                        }

                        string baseContainerDeviceInstanceId = details.baseContainerDeviceInstanceId;
                        bool wasPowerCycling = PowerCyclers.TryGetValue(baseContainerDeviceInstanceId, out var powerCycling) && powerCycling;

                        controller.IsBusy = false;

                        Controllers[baseContainerDeviceInstanceId] = controller;
                        SDLControllers[deviceIndex] = (SDLController)controller;

                        LogManager.LogInformation("SDL controller {0} plugged", controller.ToString());
                        ControllerPlugged?.Invoke(controller, wasPowerCycling);

                        bool isPhysical = controller.IsPhysical();
                        if (isPhysical)
                        {
                            if (!wasPowerCycling && PlugBehavior == ControllerPlugBehavior.AlwaysAsk)
                                ShowDetectedToast(controller, wasPowerCycling);

                            PickTargetController();
                        }
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
            // If add is still running, wait before removing (with timeout to avoid deadlock)
            if (sdlArrivalInProgress.TryGetValue(deviceIndex, out var pendingAdd))
                try { await pendingAdd.WaitAsync(CrossWaitTimeout).ConfigureAwait(false); } catch { }

            try
            {
                if (SDLControllers.TryGetValue(deviceIndex, out SDLController? controller))
                {
                    string path = controller.GetContainerInstanceId();

                    try
                    {
                        SDL.CloseGamepad(controller.gamepad);

                        PowerCyclers.TryGetValue(path, out bool IsPowerCycling);
                        bool WasTarget = IsTargetController(controller.GetInstanceId());

                        LogManager.LogInformation("SDL controller {0} unplugged, cycling {1}", controller.ToString(), IsPowerCycling);
                        ControllerUnplugged?.Invoke(controller, IsPowerCycling, WasTarget);

                        if (!IsPowerCycling)
                        {
                            Controllers.TryRemove(path, out _);
                            SDLControllers.TryRemove(deviceIndex, out _);

                            bool isPhysical = controller.IsPhysical();

                            controller.Gone();

                            if (isPhysical && HIDuncloakondisconnect)
                                controller.Unhide(false);

                            if (isPhysical && ClearTargetIfMatch(controller.GetInstanceId()))
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
            // If a removal is running for this device, wait it out (with timeout to avoid deadlock)
            if (hidRemovalInProgress.TryGetValue(key, out var pendingRemove))
                try { await pendingRemove.WaitAsync(CrossWaitTimeout).ConfigureAwait(false); } catch { }

            try
            {
                if (!details.isGaming) return;

                try
                {
                    Controllers.TryGetValue(details.baseContainerDeviceInstanceId, out IController? controller);
                    PowerCyclers.TryGetValue(details.baseContainerDeviceInstanceId, out bool IsPowerCycling);

                    if (controller is not null)
                    {
                        if (controller is XInputController or LegionControllerXInput) return;
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
                                        if (details.GetMI() == 2) // Steam Controller has a 3-interface composite HID device, with the Valve feature-report controller surface on interface 2
                                            try { controller = new GordonController(details); } catch { }
                                        break;
                                    case 0x1142:
                                        try { controller = new GordonController(details); } catch { }
                                        break;
                                    case 0x1205: // Steam Deck Controller (Neptune)
                                    case 0x12f0: // SteamOS Handheld Controller
                                        try { controller = new NeptuneController(details); } catch { }
                                        break;
                                    case 0x1302: // Steam Controller 2026 (Wired)
                                    case 0x1304: // Steam Controller 2026 (Wireless)
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
                                    case 0x6184: // dual_dinput
                                    case 0x61ED: // dual_dinput (2025 FW)
                                        if (details.GetMI() == 2)
                                        {
                                            details.isDongle = true;
                                            try { controller = new LegionControllerDInput(details); } catch { }
                                        }
                                        break;
                                    case 0x6183: // dinput
                                    case 0x61EC: // dinput (2025 FW)
                                        try { controller = new LegionControllerDInput(details); } catch { }
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

                    // controller is gone ?
                    if (await IsControllerGoneAsync(controller))
                    {
                        LogManager.LogWarning("Generic controller: VID:{0} and PID:{1} was gone while being added", details.GetVendorID(), details.GetProductID());
                        controller.Gone();
                        return;
                    }

                    string baseContainerDeviceInstanceId = controller.GetContainerInstanceId();
                    bool wasPowerCycling = PowerCyclers.TryGetValue(baseContainerDeviceInstanceId, out var powerCycling) && powerCycling;

                    controller.IsBusy = false;

                    Controllers[baseContainerDeviceInstanceId] = controller;

                    LogManager.LogInformation("Generic controller {0} plugged", controller.ToString());
                    ControllerPlugged?.Invoke(controller, wasPowerCycling);

                    bool isPhysical = controller.IsPhysical();
                    if (isPhysical)
                    {
                        if (!wasPowerCycling)
                            ShowDetectedToast(controller, wasPowerCycling);

                        PickTargetController();
                    }
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
            // If add is still running for this HID device, wait before removing (with timeout to avoid deadlock)
            if (hidArrivalInProgress.TryGetValue(key, out var pendingAdd))
                try { await pendingAdd.WaitAsync(CrossWaitTimeout).ConfigureAwait(false); } catch { }

            try
            {
                try
                {
                    IController? controller = null;

                    Task timeout = Task.Delay(TimeSpan.FromSeconds(10));
                    while (!timeout.IsCompleted && controller == null)
                    {
                        if (Controllers.TryGetValue(details.baseContainerDeviceInstanceId, out controller))
                            break;

                        await Task.Delay(100).ConfigureAwait(false);
                    }

                    if (controller == null) return;
                    if (controller is XInputController or LegionControllerXInput) return;
                    if (controller is SDLController) return;

                    PowerCyclers.TryGetValue(details.baseContainerDeviceInstanceId, out bool IsPowerCycling);
                    bool WasTarget = IsTargetController(controller.GetInstanceId());

                    LogManager.LogInformation("Generic controller {0} unplugged, cycling {1}", controller.ToString(), IsPowerCycling);
                    ControllerUnplugged?.Invoke(controller, IsPowerCycling, WasTarget);

                    if (!IsPowerCycling)
                    {
                        Controllers.TryRemove(details.baseContainerDeviceInstanceId, out _);

                        bool isPhysical = controller.IsPhysical();

                        controller.Gone();

                        if (isPhysical && HIDuncloakondisconnect)
                            controller.Unhide(false);

                        if (isPhysical && ClearTargetIfMatch(controller.GetInstanceId()))
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
            // If a removal is running for this controller, wait first (with timeout to avoid deadlock)
            if (xusbRemovalInProgress.TryGetValue(key, out var pendingRemove))
                try { await pendingRemove.WaitAsync(CrossWaitTimeout).ConfigureAwait(false); } catch { }

            try
            {
                try
                {
                    Controllers.TryGetValue(details.baseContainerDeviceInstanceId, out IController? controller);
                    PowerCyclers.TryGetValue(details.baseContainerDeviceInstanceId, out bool IsPowerCycling);

                    if (controller != null)
                    {
                        if (controller is DInputController) return;
                        if (controller is SDLController) return;
                        if (controller is not IXInputController) return;

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

                            // Lenovo
                            case "0x17EF":
                            case "0x1A86":
                                switch (details.GetProductID())
                                {
                                    case "0x6182":
                                    case "0x61EB":
                                        try { controller = new LegionControllerXInput(details); } catch { }
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
                            controller = IDevice.GetCurrent().CreateController(details) ?? new XInputController(details);
                        }
                        catch
                        {
                            LogManager.LogWarning("Unsupported XInput controller: VID:{0} and PID:{1}", details.GetVendorID(), details.GetProductID());
                            return;
                        }
                    }

                    // controller is gone ?
                    if (await IsControllerGoneAsync(controller))
                    {
                        LogManager.LogWarning("XInput controller: VID:{0} and PID:{1} was gone while being added", details.GetVendorID(), details.GetProductID());
                        controller.Gone();
                        return;
                    }

                    string baseContainerDeviceInstanceId = details.baseContainerDeviceInstanceId;
                    bool wasPowerCycling = PowerCyclers.TryGetValue(baseContainerDeviceInstanceId, out var powerCycling) && powerCycling;

                    controller.IsBusy = false;

                    Controllers[baseContainerDeviceInstanceId] = controller;

                    LogManager.LogInformation("XInput controller {0} plugged", controller.ToString());
                    ControllerPlugged?.Invoke(controller, wasPowerCycling);

                    bool isPhysical = controller.IsPhysical();
                    if (isPhysical)
                    {
                        if (!wasPowerCycling)
                            ShowDetectedToast(controller, wasPowerCycling);

                        PickTargetController();
                    }
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
            // If add is still running for this controller, wait before removing (with timeout to avoid deadlock)
            if (xusbArrivalInProgress.TryGetValue(key, out var pendingAdd))
                try { await pendingAdd.WaitAsync(CrossWaitTimeout).ConfigureAwait(false); } catch { }

            try
            {
                try
                {
                    IController? controller = null;

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

                        bool isPhysical = controller.IsPhysical();

                        controller.Gone();

                        if (isPhysical && HIDuncloakondisconnect)
                            controller.Unhide(false);

                        // Atomically check-and-clear under targetLock to avoid clearing a
                        // controller that SetTargetController just switched to on another thread.
                        if (isPhysical && ClearTargetIfMatch(controller.GetInstanceId()))
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

    public static void LoadGamepadMappings()
    {
        int loaded = SDL.AddGamepadMappingsFromFile(App.GameControllerDbPath);
        LogManager.LogInformation("SDL gamepad mappings loaded: {0} mappings loaded", loaded);
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

        // Stop slot monitor
        slotHelper.Stop();

        // Cleanup SDL3 controllers
        foreach (SDLController controller in SDLControllers.Values)
            SDL.CloseGamepad(controller.gamepad);

        SDL.Quit();

        // manage events
        TimerManager.Tick -= Tick;
        NetworkControllerHelper.PacketReceived -= NetworkControllerHelper_PacketReceived;
        NetworkControllerHelper.VibrationReceived -= NetworkControllerHelper_VibrationReceived;
        StopNetworkControllers();
        NetworkControllerHelper.StreamingChanged -= NetworkControllerHelper_StreamingChanged;
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
        App.uiSettings.ColorValuesChanged -= OnColorValuesChanged;
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

        networkTimer.Elapsed -= NetworkTimer_Elapsed;
        networkTimer.Stop();

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

    private static void NetworkControllerHelper_VibrationReceived(Guid id, byte largeMotor, byte smallMotor)
    {
        IController? controller = Controllers.Values.FirstOrDefault(candidate =>
            candidate is not RemoteController && NetworkControllerHelper.GetNetworkControllerId(candidate.GetInstanceId()) == id);
        controller?.SetVibration(largeMotor, smallMotor);
    }

    public static void Unplug(IController controller)
    {
        string containerId = controller.GetContainerInstanceId();
        try
        {
            bool wasTarget = IsTargetController(controller.GetInstanceId());
            ControllerUnplugged?.Invoke(controller, false, wasTarget);
            PowerCyclers.TryRemove(containerId, out _);
            Controllers.TryRemove(containerId, out _);
            bool isPhysical = controller.IsPhysical();
            controller.Gone();
            if (isPhysical && HIDuncloakondisconnect)
                controller.Unhide(false);
            if (isPhysical && ClearTargetIfMatch(controller.GetInstanceId()))
                PickTargetController();
            else
                controller.Dispose();
        }
        catch { }
    }

    public static void DisconnectNetworkController(IController controller)
    {
        NetworkControllerHelper.Disconnect(controller);
    }

    public static void RemoteControllerSessionClosed(Guid id)
    {
        if (!NetworkControllers.TryRemove(id, out RemoteController? controller))
            return;

        Controllers.TryRemove(controller.GetContainerInstanceId(), out _);
        bool wasTarget = IsTargetController(controller.GetInstanceId());
        ControllerUnplugged?.Invoke(controller, false, wasTarget);
        if (wasTarget)
            ClearTargetIfMatch(controller.GetInstanceId());
        controller.Dispose();
    }

    #endregion

    private static void OnColorValuesChanged(UISettings sender, object args)
    {
        Color _systemAccent = App.uiSettings.GetColorValue(UIColorType.AccentDark1);
        IController? controller;
        lock (targetLock)
            controller = targetController;

        controller?.SetLightColor(_systemAccent.R, _systemAccent.G, _systemAccent.B);
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

    private static void CurrentDevice_KeyReleased(IDevice sender, ButtonFlags button)
    {
        // calls current controller (if connected)
        IController? controller;
        lock (targetLock)
            controller = targetController;

        controller?.InjectButton(button, false, true);
    }

    private static void CurrentDevice_KeyPressed(IDevice sender, ButtonFlags button)
    {
        // calls current controller (if connected)
        IController? controller;
        lock (targetLock)
            controller = targetController;

        controller?.InjectButton(button, true, false);
    }

    private static void ScenarioTimer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        // reset flag
        ControllerMuted = false;

        // Steam Deck specific scenario
        if (IDevice.GetCurrent() is SteamDeck steamDeck)
        {
            bool IsExclusiveMode = ManagerFactory.settingsManager.GetInt("SteamControllerMode") == 1;

            // Making sure current controller is embedded
            IController? controller;
            lock (targetLock)
                controller = targetController;

            if (controller is NeptuneController neptuneController)
            {
                // We're busy, come back later
                if (neptuneController.IsBusy)
                    return;

                if (IsExclusiveMode)
                {
                    // do nothing
                }
                else
                {
                    // mode: hybrid
                    // Temporarily override HIDmode to SteamDeckController when Steam has foreground
                    if (foregroundProcess?.Platform == GamePlatform.Steam)
                    {
                        // application is either steam or a steam game
                        // save current HIDmode before override (if not already saved)
                        if (previousHIDmode == HIDmode.NotSelected)
                            previousHIDmode = (HIDmode)ManagerFactory.settingsManager.GetInt("HIDmode", true);

                        // set to SteamDeck only if not already set
                        HIDmode currentHIDmode = (HIDmode)ManagerFactory.settingsManager.GetInt("HIDmode", true);
                        if (currentHIDmode != HIDmode.SteamDeckController)
                            ManagerFactory.settingsManager.SetProperty("HIDmode", (int)HIDmode.SteamDeckController);

                        // notify UI if we've activated Steam hybrid override
                        SteamHybridModeOverride?.Invoke(true);
                    }
                    else
                    {
                        // application is not steam related
                        // restore previous HIDmode if we had overridden it
                        if (previousHIDmode != HIDmode.NotSelected)
                        {
                            ManagerFactory.settingsManager.SetProperty("HIDmode", (int)previousHIDmode);

                            // notify UI that Steam hybrid override is no longer active
                            SteamHybridModeOverride?.Invoke(false);
                        }
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

    private static void SettingsManager_SettingValueChanged(string name, object? value, bool temporary, bool initializing)
    {
        switch (name)
        {
            case "VibrationStrength":
                uint VibrationStrength = Convert.ToUInt32(value);
                IController? controller;
                lock (targetLock)
                    controller = targetController;

                controller?.SetVibrationStrength(VibrationStrength, ManagerFactory.settingsManager.IsReady);
                break;

            case "ControllerSlotManagementMode":
                {
                    int modeInt = 0;
                    if (value is not null && int.TryParse(value.ToString(), out int parsed))
                        modeInt = parsed;

                    slotHelper.SetMode((ControllerSlotManagementMode)Math.Max(0, Math.Min(1, modeInt)));
                }
                break;

            case "SteamControllerMode":
                CheckControllerScenario();
                break;

            case "NetworkControllersEnabled":
                bool networkControllersEnabled = Convert.ToBoolean(value);
                LogManager.LogInformation("Network controllers {0}", networkControllersEnabled ? "enabled" : "disabled");
                if (networkControllersEnabled)
                    NetworkControllerHelper.Start();
                else
                    StopNetworkControllers();
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
        SettingsManager_SettingValueChanged("VibrationStrength", ManagerFactory.settingsManager.GetString("VibrationStrength"), false, true);
        SettingsManager_SettingValueChanged("ControllerSlotManagementMode", ManagerFactory.settingsManager.GetString("ControllerSlotManagementMode"), false, true);
        SettingsManager_SettingValueChanged("SteamControllerMode", ManagerFactory.settingsManager.GetString("SteamControllerMode"), false, true);
        SettingsManager_SettingValueChanged("NetworkControllersEnabled", ManagerFactory.settingsManager.GetBoolean("NetworkControllersEnabled"), false, true);
    }

    #region Network controller lifecycle

    private static void NetworkControllerHelper_StreamingChanged(Guid id, bool streaming)
    {
        bool showPrompt = false;
        bool restoreScreen = false;

        lock (networkStreamingLock)
        {
            if (streaming)
            {
                bool wasEmpty = streamingControllers.Count == 0;
                bool added = streamingControllers.Add(id);
                showPrompt = wasEmpty && added;
            }
            else if (streamingControllers.Remove(id) && streamingControllers.Count == 0 && localBrightnessDimmedForStreaming)
            {
                localBrightnessDimmedForStreaming = false;
                restoreScreen = true;
            }
        }

        if (showPrompt)
            ShowNetworkStreamingToast();

        if (restoreScreen)
        {
            ManagerFactory.multimediaManager.RestoreStreamingBrightness();
            ManagerFactory.powerProfileManager.SetStreamingPowerOverride(false);
        }
    }

    private static void ShowNetworkStreamingToast()
    {
        ToastManager.SendToast(new ToastRequest
        {
            Title = "Controller is being streamed",
            Content = "Dim the screen and use the profile’s on-battery power preset while streaming the controller to preserve battery life?",
            Actions =
            {
                new ToastAction
                {
                    Label = "Dim brightness and save power",
                    Command = "DimLocalBrightness",
                    Callback = _ => DimLocalBrightnessForStreaming()
                },
                new ToastAction
                {
                    Label = "Keep current brightness and power mode",
                    Command = "KeepLocalScreenOn",
                    Callback = _ => { }
                }
            }
        });
    }

    private static void DimLocalBrightnessForStreaming()
    {
        bool dimBrightness = false;
        lock (networkStreamingLock)
        {
            if (streamingControllers.Count == 0)
                return;

            if (!localBrightnessDimmedForStreaming)
            {
                localBrightnessDimmedForStreaming = true;
                dimBrightness = true;
            }
        }

        if (dimBrightness)
        {
            ManagerFactory.multimediaManager.DimForStreaming();
            ManagerFactory.powerProfileManager.SetStreamingPowerOverride(true);
        }
    }

    private static void NetworkControllerHelper_PacketReceived(NetworkControllerPacket packet)
    {
        if (!ManagerFactory.settingsManager.GetBoolean("NetworkControllersEnabled"))
            return;

        if (DisconnectedNetworkControllers.ContainsKey(packet.Id))
            return;

        RemoveTimedOutNetworkControllers();

        RemoteController controller = NetworkControllers.GetOrAdd(packet.Id, id =>
        {
            RemoteController created = new(id, packet.Name, packet.UserIndex);

            Controllers[created.GetContainerInstanceId()] = created;

            LogManager.LogInformation("Network controller connected: {0}", created.ToString());

            ControllerPlugged?.Invoke(created, false);

            if (PlugBehavior == ControllerPlugBehavior.AlwaysAsk)
                ShowDetectedToast(created, false);

            PickTargetController();
            return created;
        });

        // manage packet opcodes
        switch (packet.OpCode)
        {
            case NetworkControllerOpCode.ControllerState:
                controller.Update(packet.Name, packet.UserIndex, packet.Sequence, packet.State);
                break;

            case NetworkControllerOpCode.Metadata:
                if (packet.Metadata is not null)
                    controller.ApplyMetadata(packet.Metadata);
                break;

            default:
                controller.RefreshAdvertisement(packet.Name, packet.UserIndex);
                break;
        }
    }

    private static void RemoveTimedOutNetworkControllers()
    {
        foreach (var stale in NetworkControllers.Where(p => p.Value.IsTimedOut).ToArray())
        {
            if (DisconnectedNetworkControllers.ContainsKey(stale.Key))
                continue;

            if (!NetworkControllers.TryRemove(stale.Key, out RemoteController? removed))
                continue;

            LogManager.LogInformation("Network controller timed out: {0}", removed.ToString());
            Controllers.TryRemove(removed.GetContainerInstanceId(), out _);

            bool wasTarget = IsTargetController(removed.GetInstanceId());

            ControllerUnplugged?.Invoke(removed, false, wasTarget);

            if (wasTarget)
                ClearTargetIfMatch(removed.GetInstanceId());

            removed.Dispose();
        }
    }

    private static void StopNetworkControllers()
    {
        NetworkControllerHelper.Stop();
        DisconnectedNetworkControllers.Clear();

        foreach (var entry in NetworkControllers.ToArray())
        {
            RemoteController controller = entry.Value;

            NetworkControllers.TryRemove(entry.Key, out _);

            Controllers.TryRemove(controller.GetContainerInstanceId(), out _);

            LogManager.LogInformation("Network controller removed: {0}", controller.ToString());

            bool wasTarget = IsTargetController(controller.GetInstanceId());

            ControllerUnplugged?.Invoke(controller, false, wasTarget);

            if (wasTarget)
                ClearTargetIfMatch(controller.GetInstanceId());

            controller.Dispose();
        }
        NetworkControllers.Clear();
    }

    #endregion

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

        // raise events
        foreach (PnPDetails details in ManagerFactory.deviceManager.GetGamingDevices(false))
            HidDeviceArrived(details, details.InterfaceGuid);

        // raise events
        foreach (PnPDetails details in ManagerFactory.deviceManager.GetGamingDevices(true))
            XUsbDeviceArrived(details, details.InterfaceGuid);

        // raise events
        ReopenSDLGamepads();

        // Device enumeration can complete after the initial picker timer was started.
        // Schedule one more pass so cold-start controllers are selected once all
        // currently connected devices have been registered.
        PickTargetController();
    }

    public static void Rescan()
    {
        ManagerFactory.deviceManager.RefreshDInput();
        ManagerFactory.deviceManager.RefreshXInput();

        ReopenSDLGamepads();
    }

    private static void ReopenSDLGamepads()
    {
        uint[]? gamepads = SDL.GetGamepads(out int count);
        if (gamepads != null)
        {
            foreach (uint gamepad in gamepads)
                SDL_GamepadAdded(gamepad);
        }
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
        // Clear input state from all controllers before hibernation to prevent stuck state
        foreach (var controller in Controllers.Values)
        {
            try
            {
                controller.ClearInputState();
            }
            catch { }
        }

        // Stop any in-flight slot fix to avoid manipulating devices during suspend/shutdown.
        slotHelper.StopWatchdog();

        ClearTargetController();
    }

    public enum SlotFixTrigger
    {
        Manual = 0,
        Automatic = 1
    }

    public static bool HasSlotIssue => slotHelper.HasSlotIssue;
    public static bool HasVirtualSlot1Issue => slotHelper.HasVirtualSlot1Issue;
    public static string SlotIssueReason => slotHelper.SlotIssueReason;

    private static void ControllerManager_ControllerPlugged(IController controller, bool isPowerCycling)
    {
        if (!isPowerCycling)
            slotHelper.HandleTopologyChanged();
    }

    private static void ControllerManager_ControllerUnplugged(IController controller, bool isPowerCycling, bool wasTarget)
    {
        if (!isPowerCycling)
            slotHelper.HandleTopologyChanged();
    }

    public static void TriggerSlotFix(bool resetAttempts) => slotHelper.TriggerFix(resetAttempts);
    public static void StartWatchdog() => slotHelper.TriggerFix(resetAttempts: false);
    public static void StopWatchdog() => slotHelper.StopWatchdog();
    public static bool AssignXInputSlot(XInputController controller, byte targetSlot) => slotHelper.AssignXInputSlot(controller, targetSlot);

    private static void SetSlotIssueState(bool hasIssue, string reason) => SlotIssueChanged?.Invoke(hasIssue, reason);

    private static void UpdateStatus(ControllerManagerStatus status, int attempts)
    {
        if (status == ControllerManagerStatus.Succeeded && managerStatus != ControllerManagerStatus.Busy)
            return;
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
        }

        managerStatus = status;
        StatusChanged?.Invoke(status, attempts);
    }

    private static readonly Notification ManagerBusy = new("Controller Manager", "Controllers order is being adjusted, your gamepad might become irresponsive for a few seconds.") { IsInternal = true };

    private static void VirtualManager_Vibrated(byte largeMotor, byte smallMotor)
    {
        IController? controller;
        lock (targetLock)
            controller = targetController;
        controller?.SetVibration(largeMotor, smallMotor);
    }

    private static ControllerPlugBehavior PlugBehavior => (ControllerPlugBehavior)ManagerFactory.settingsManager.GetInt("ControllerPlugBehavior");
    private static void PickTimer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        IController? current;
        lock (targetLock)
            current = targetController;
        IEnumerable<IController> controllers = GetPhysicalControllers<IController>();

        // Pick the most recently arrived external or wireless controller
        IController? latestExternalController = controllers
            .Where(c => !c.IsNetwork() && (c.IsExternal() || c.IsWireless()))
            .OrderByDescending(c => c.GetLastArrivalDate())
            .FirstOrDefault();

        // Pick the internal controller (built-in, non-removable)
        IController? internalController = controllers.FirstOrDefault(c => c.IsInternal());

        // Default: keep current target (reassigned below if a better candidate exists)
        string deviceInstanceId = current?.GetContainerInstanceId() ?? string.Empty;

        // AlwaysAsk and DoNothing start on the internal controller. After startup, preserve an
        // explicitly selected real target instead of switching it when another controller arrives.
        if (PlugBehavior != ControllerPlugBehavior.AutoConnect)
        {
            if (current is not null && !current.IsDummy())
            {
                // deviceInstanceId already holds the current target — nothing to do
            }
            else if (internalController is not null)
            {
                deviceInstanceId = internalController.GetContainerInstanceId();
            }
            else if (internalController is null && latestExternalController is not null)
            {
                deviceInstanceId = latestExternalController.GetContainerInstanceId();
            }
        }
        // Auto-connect to the most recently arrived external/wireless controller.
        else if (latestExternalController is not null)
        {
            // If the current target is already an external/wireless controller, keep it —
            // we don't want to switch away when a second external controller is plugged in.
            if (current is not null && (current.IsNetwork() || current.IsWireless() || current.IsExternal()))
                deviceInstanceId = current.GetContainerInstanceId();
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

        // Clear power-cycling flags for controllers that have finished their cycle
        // (IsBusy == false) or whose entry is orphaned (no longer in Controllers).
        foreach (string key in PowerCyclers.Keys)
        {
            if (!Controllers.TryGetValue(key, out IController? controller) || !controller.IsBusy)
                PowerCyclers.TryRemove(key, out _);
        }

    }

    private static void ClearTargetController()
    {
        lock (targetTransitionLock)
        {
            IController? controller;
            lock (targetLock)
            {
                controller = targetController;
                targetController = null;

            }

            ClearTargetController(controller, manualDisconnect: false);
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
        lock (targetTransitionLock)
        {
            IController? controller;
            lock (targetLock)
            {
                if (targetController?.GetInstanceId() != instanceId)
                    return false;

                controller = targetController;
                targetController = null;

            }

            ClearTargetController(controller);
            return true;
        }
    }

    private static void ClearTargetController(IController? controller, bool manualDisconnect = true)
    {
        if (controller is null)
            return;

        controller.SetLightColor(0, 0, 0);
        controller.StopRumble(waitForCompletion: false);
        if (controller is RemoteController remoteController && !manualDisconnect)
            remoteController.UnplugFromRemoteSession();
        else
            controller.Unplug();
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
        IController? controllerToHide = null;
        bool hideWithPowerCycle = false;

        lock (targetTransitionLock)
        {
            // look for new controller
            if (!Controllers.TryGetValue(baseContainerDeviceInstanceId, out IController? controller))
                return;

            if (controller is RemoteController remoteController)
                DisconnectedNetworkControllers.TryRemove(remoteController.NetworkId, out _);

            // already self
            bool isCurrentTarget;
            IController? previousController;
            lock (targetLock)
            {
                isCurrentTarget = targetController?.GetInstanceId() == controller.GetInstanceId();
                previousController = targetController;

                if (!isCurrentTarget)
                    targetController = controller;
            }

            if (isCurrentTarget)
            {
                controller.Plug();
                return;
            }

            ClearTargetController(previousController);
            controller.Plug();

            Color _systemAccent = App.uiSettings.GetColorValue(UIColorType.AccentDark1);
            controller.SetLightColor(_systemAccent.R, _systemAccent.G, _systemAccent.B);

            // update HIDInstancePath
            ManagerFactory.settingsManager.SetProperty("HIDInstancePath", baseContainerDeviceInstanceId);

            if (!IsPowerCycling && !controller.IsVirtual())
            {
                if (ManagerFactory.settingsManager.GetBoolean("HIDcloakonconnect"))
                {
                    bool powerCycle = true;

                    if (controller is LegionController legionController)
                    {
                        // todo:    Look for a byte within hid report that'd tend to mean both controllers are synced.
                        //          Then I guess we could try and power cycle them.
                        powerCycle = !legionController.IsWireless();
                    }

                    // Capture for post-transition call: Hide() -> CyclePort() can block for seconds
                    // and fires IsBusy which dispatches to the UI thread — invoking it while
                    // holding targetLock deadlocks if the UI thread is also waiting for the lock.
                    if (!controller.IsHidden())
                    {
                        controllerToHide = controller;
                        hideWithPowerCycle = powerCycle;
                    }
                }
            }

            // check applicable scenarios
            CheckControllerScenario();

            // check if controller is about to power cycle
            PowerCyclers.TryGetValue(baseContainerDeviceInstanceId, out IsPowerCycling);

            // stop any ongoing rumble
            controller.StopRumble(waitForCompletion: false);

            // vibrate on connect, except when controller is power cycling
            if (ManagerFactory.settingsManager.GetBoolean("HIDvibrateonconnect") && !IsPowerCycling)
                controller.Rumble();

            // Never invoke external code while holding targetLock.
            // Subscribers may touch UI / managers that also take locks during shutdown.
            selectedController = controller;
            selectedHandlers = ControllerSelected;
        }

        // Hide() -> CyclePort() blocks for up to 3 s (Bluetooth) and fires IsBusy/StateChanged
        // which marshals OnPropertyChanged to the UI thread. Must run after releasing targetTransitionLock.
        controllerToHide?.Hide(hideWithPowerCycle);

        try
        {
            selectedHandlers?.Invoke(selectedController);
        }
        catch { }
    }

    public static void DisconnectTargetController(string instanceId)
    {
        IController? controller;

        lock (targetTransitionLock)
        {
            lock (targetLock)
            {
                if (targetController?.GetInstanceId() != instanceId)
                    return;

                controller = targetController;
                targetController = null;

            }

            ClearTargetController(controller);
        }

        if (controller is not null)
            ControllerUnplugged?.Invoke(controller, true, true);
    }

    public static bool SuspendController(string baseContainerDeviceInstanceId)
    {
        try
        {
            PnPDevice? pnPDevice = null;

            Task timeout = Task.Delay(TimeSpan.FromSeconds(3));
            while (!timeout.IsCompleted && pnPDevice is null)
            {
                try { pnPDevice = PnPDevice.GetDeviceByInstanceId(baseContainerDeviceInstanceId); } catch { }
                Task.Delay(1000).Wait();
            }

            if (pnPDevice is null)
                return false;

            DriverMeta? pnPDriver = null;
            try
            {
                // get current driver
                pnPDriver = pnPDevice.GetCurrentDriver();
            }
            catch { }

            // get controller
            if (Controllers.TryGetValue(baseContainerDeviceInstanceId, out IController? controller))
            {
                // Mark as power-cycling BEFORE installing null driver or cycling the port.
                // InstallNullDriver can trigger PnP removal events; without this flag the
                // removal handler would destroy the controller entry prematurely.
                controller.IsBusy = true;
                PowerCyclers[baseContainerDeviceInstanceId] = true;

                if (controller is XboxAdaptiveController xboxController)
                {
                    return xboxController.Disable();
                }
                else
                {
                    string enumerator = pnPDevice.GetProperty<string>(DevicePropertyKey.Device_EnumeratorName) ?? string.Empty;
                    switch (enumerator)
                    {
                        case "USB":
                            if (!string.IsNullOrEmpty(pnPDriver?.InfPath))
                            {
                                // store driver to collection
                                DriverStoreHelper.AddOrUpdateDriverStore(baseContainerDeviceInstanceId, pnPDriver.InfPath);

                                // install empty drivers
                                pnPDevice.InstallNullDriver(out bool rebootRequired);
                            }
                            break;
                    }
                }

                // cycle controller
                return controller.CyclePort();
            }
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
        return ResumeController(baseContainerDeviceInstanceId, TimeSpan.FromSeconds(3), false);
    }

    private static bool ResumeController(string baseContainerDeviceInstanceId, TimeSpan timeout, bool requireStoredDriver)
    {
        try
        {
            PnPDevice? pnPDevice = null;

            Task discoveryTimeout = Task.Delay(timeout);
            while (!discoveryTimeout.IsCompleted && pnPDevice is null)
            {
                try { pnPDevice = PnPDevice.GetDeviceByInstanceId(baseContainerDeviceInstanceId); } catch { }

                if (pnPDevice is null)
                    Task.Delay(1000).Wait();
            }

            if (pnPDevice is null)
                return false;

            DriverMeta? pnPDriver = null;
            try
            {
                // get current driver
                pnPDriver = pnPDevice.GetCurrentDriver();
            }
            catch { }

            string? enumerator = pnPDevice.GetProperty<string>(DevicePropertyKey.Device_EnumeratorName);
            switch (enumerator)
            {
                case "USB":
                    {
                        string InfPath;
                        if (requireStoredDriver)
                        {
                            if (!DriverStoreHelper.TryGetDriverFromDriverStore(baseContainerDeviceInstanceId, out InfPath))
                            {
                                LogManager.LogWarning("No stored OEM driver found for {0}", baseContainerDeviceInstanceId);
                                return false;
                            }
                        }
                        else
                        {
                            InfPath = DriverStoreHelper.GetDriverFromDriverStore(baseContainerDeviceInstanceId);
                        }

                        if (!string.IsNullOrEmpty(InfPath))
                        {
                            if (pnPDriver?.InfPath != InfPath)
                            {
                                // restore drivers
                                pnPDevice.RemoveAndSetup();
                                pnPDevice.InstallCustomDriver(InfPath, out bool rebootRequired);
                            }

                            // remove device from store
                            DriverStoreHelper.RemoveFromDriverStore(baseContainerDeviceInstanceId);

                            return true;
                        }
                    }
                    break;
            }
        }
        catch { }

        return false;
    }

    public static bool RestoreAllControllersForUninstall(Action<string>? reportStatus = null)
    {
        bool settled = WaitForControllerActivityToSettle(TimeSpan.FromSeconds(10), reportStatus);

        reportStatus?.Invoke("Disabling HidHide cloaking...");
        HidHide.SetCloaking(false);

        List<string> hiddenDevices = HidHide.GetRegisteredDevices()
            .Where(instanceId => !string.IsNullOrWhiteSpace(instanceId))
            .Distinct()
            .ToList();

        int unhideFailures = 0;
        if (hiddenDevices.Count != 0)
        {
            reportStatus?.Invoke($"Unhiding {hiddenDevices.Count} device(s)...");

            foreach (string instanceId in hiddenDevices)
            {
                if (!HidHide.UnhidePath(instanceId))
                    unhideFailures++;
            }
        }

        List<string> driverPaths = DriverStoreHelper.GetPaths().ToList();
        int restored = 0;
        int restoreFailures = 0;

        if (driverPaths.Count == 0)
        {
            reportStatus?.Invoke("No stored OEM drivers to restore.");
        }
        else
        {
            for (int index = 0; index < driverPaths.Count; index++)
            {
                string baseContainerDeviceInstanceId = driverPaths[index];
                reportStatus?.Invoke($"Restoring controller drivers {index + 1}/{driverPaths.Count}...");

                if (ResumeController(baseContainerDeviceInstanceId, TimeSpan.FromSeconds(10), true))
                    restored++;
                else
                    restoreFailures++;
            }
        }

        bool success = settled && unhideFailures == 0 && restoreFailures == 0;

        LogManager.LogInformation(
            "Uninstall restore completed. Settled: {0}, hidden devices: {1}, unhide failures: {2}, drivers restored: {3}, restore failures: {4}",
            settled,
            hiddenDevices.Count,
            unhideFailures,
            restored,
            restoreFailures);

        reportStatus?.Invoke(success ? "Restore complete." : "Restore completed with issues.");
        return success;
    }

    private static bool WaitForControllerActivityToSettle(TimeSpan timeout, Action<string>? reportStatus)
    {
        DateTime deadline = DateTime.UtcNow.Add(timeout);

        while (DateTime.UtcNow < deadline)
        {
            int pendingOperations = GetPendingControllerActivityCount();
            if (pendingOperations == 0)
                return true;

            reportStatus?.Invoke($"Waiting for controller activity to settle ({pendingOperations})...");
            Task.Delay(250).Wait();
        }

        int remainingOperations = GetPendingControllerActivityCount();
        if (remainingOperations != 0)
            LogManager.LogWarning("Controller activity still pending during uninstall restore: {0}", remainingOperations);

        return remainingOperations == 0;
    }

    private static int GetPendingControllerActivityCount()
    {
        int pendingOperations = 0;

        pendingOperations += xusbArrivalInProgress.Values.Count(task => !task.IsCompleted);
        pendingOperations += xusbRemovalInProgress.Values.Count(task => !task.IsCompleted);
        pendingOperations += hidArrivalInProgress.Values.Count(task => !task.IsCompleted);
        pendingOperations += hidRemovalInProgress.Values.Count(task => !task.IsCompleted);
        pendingOperations += sdlArrivalInProgress.Values.Count(task => !task.IsCompleted);
        pendingOperations += sdlRemovalInProgress.Values.Count(task => !task.IsCompleted);
        pendingOperations += PowerCyclers.Values.Count(isCycling => isCycling);

        return pendingOperations;
    }

    public static void ResumeControllers()
    {
        // loop through controllers
        foreach (string baseContainerDeviceInstanceId in DriverStoreHelper.GetPaths())
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
        lock (targetLock)
            return targetController;
    }

    public static IController GetTargetOrDefault()
    {
        IController? controller;
        lock (targetLock)
            controller = targetController;

        return controller ?? GetDefault();
    }

    public static bool IsTargetController(string InstanceId)
    {
        lock (targetLock)
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
        return (physical ? GetPhysicalControllers<T>() : GetVirtualControllers<T>())
            .FirstOrDefault(controller => controller.GetUserIndex() == (int)userIndex);
    }

    public static IEnumerable<T> GetControllers<T>() where T : IController
    {
        return Controllers.Values.Where(controller => typeof(T).IsAssignableFrom(controller.GetType()) && !controller.IsDummy()).Cast<T>();
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
                return dummyXbox360;

            case HIDmode.DualShock4Controller:
                return dummyDualShock4;

            case HIDmode.DualSenseController:
                return dummyDualSense;

            case HIDmode.SteamDeckController:
                return dummySteamDeck;

            case HIDmode.SteamController:
                return dummySteamDeck;

            case HIDmode.SwitchProController:
                return dummySwitchPro;
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

    public static event ControllerPluggedEventHandler? ControllerPlugged;
    public delegate void ControllerPluggedEventHandler(IController Controller, bool WasPowerCycling);

    public static event ControllerUnpluggedEventHandler? ControllerUnplugged;
    public delegate void ControllerUnpluggedEventHandler(IController Controller, bool IsPowerCycling, bool WasTarget);

    public static event ControllerSelectedEventHandler? ControllerSelected;
    public delegate void ControllerSelectedEventHandler(IController Controller);

    /// <summary>
    /// Controller state has changed, before layout manager
    /// </summary>
    /// <param name="Inputs">The updated controller state.</param>
    public static event InputsUpdatedEventHandler? InputsUpdated;
    public delegate void InputsUpdatedEventHandler(ControllerState Inputs, bool IsMapped);

    public static event StatusChangedEventHandler? StatusChanged;
    public delegate void StatusChangedEventHandler(ControllerManagerStatus status, int attempts);

    public static event SlotIssueChangedEventHandler? SlotIssueChanged;
    public delegate void SlotIssueChangedEventHandler(bool hasIssue, string reason);

    /// <summary>
    /// Raised when Steam hybrid mode temporarily overrides HIDmode.
    /// Allows UI to disable controller selection during Steam foreground.
    /// </summary>
    public static event SteamHybridModeOverrideEventHandler? SteamHybridModeOverride;
    public delegate void SteamHybridModeOverrideEventHandler(bool isOverridden);

    public static event InitializedEventHandler? Initialized;
    public delegate void InitializedEventHandler();

    #endregion
}