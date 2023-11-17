using HandheldCompanion.Controllers;
using HandheldCompanion.Controls;
using HandheldCompanion.Inputs;
using HandheldCompanion.Platforms;
using HandheldCompanion.Utils;
using HandheldCompanion.Views;
using HandheldCompanion.Views.Classes;
using Nefarius.Utilities.DeviceManagement.Drivers;
using Nefarius.Utilities.DeviceManagement.Extensions;
using Nefarius.Utilities.DeviceManagement.PnP;
using SharpDX.DirectInput;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Windows.UI.ViewManagement;
using static HandheldCompanion.Utils.DeviceUtils;
using static JSL;
using DeviceType = SharpDX.DirectInput.DeviceType;

namespace HandheldCompanion.Managers;

public static class ControllerManager
{
    private static readonly Dictionary<string, IController> Controllers = new();
    public static readonly Dictionary<string, bool> PowerCyclers = new();

    private static readonly XInputController? emptyXInput = new();
    private static readonly DS4Controller? emptyDS4 = new();

    private static IController? targetController;
    private static FocusedWindow focusedWindows = FocusedWindow.None;
    private static ProcessEx? foregroundProcess;
    private static bool ControllerMuted;

    public static bool IsInitialized;

    private static bool virtualControllerCreated;

    public static void Start()
    {
        // Flushing possible JoyShocks...
        JslDisconnectAndDisposeAll();

        DeviceManager.XUsbDeviceArrived += XUsbDeviceArrived;
        DeviceManager.XUsbDeviceRemoved += XUsbDeviceRemoved;

        DeviceManager.HidDeviceArrived += HidDeviceArrived;
        DeviceManager.HidDeviceRemoved += HidDeviceRemoved;

        DeviceManager.Initialized += DeviceManager_Initialized;

        SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

        GamepadFocusManager.GotFocus += GamepadFocusManager_GotFocus;
        GamepadFocusManager.LostFocus += GamepadFocusManager_LostFocus;

        ProcessManager.ForegroundChanged += ProcessManager_ForegroundChanged;

        VirtualManager.ControllerSelected += VirtualManager_ControllerSelected;
        VirtualManager.Vibrated += VirtualManager_Vibrated;

        MainWindow.CurrentDevice.KeyPressed += CurrentDevice_KeyPressed;
        MainWindow.CurrentDevice.KeyReleased += CurrentDevice_KeyReleased;

        MainWindow.uiSettings.ColorValuesChanged += OnColorValuesChanged;

        // enable HidHide
        HidHide.SetCloaking(true);

        IsInitialized = true;
        Initialized?.Invoke();

        // summon an empty controller, used to feed Layout UI
        // todo: improve me
        ControllerSelected?.Invoke(GetEmulatedController());

        LogManager.LogInformation("{0} has started", "ControllerManager");
    }

    private static void OnColorValuesChanged(UISettings sender, object args)
    {
        var _systemBackground = MainWindow.uiSettings.GetColorValue(UIColorType.Background);
        var _systemAccent = MainWindow.uiSettings.GetColorValue(UIColorType.Accent);

        targetController?.SetLightColor(_systemAccent.R, _systemAccent.G, _systemAccent.B);
    }

    [Flags]
    private enum FocusedWindow
    {
        None,
        MainWindow,
        Quicktools
    }

    private static void GamepadFocusManager_LostFocus(Control control)
    {
        GamepadWindow gamepadWindow = (GamepadWindow)control;

        switch (gamepadWindow.Title)
        {
            case "QuickTools":
                focusedWindows &= ~FocusedWindow.Quicktools;
                break;
            default:
                focusedWindows &= ~FocusedWindow.MainWindow;
                break;
        }

        // check applicable scenarios
        CheckControllerScenario();
    }

    private static void GamepadFocusManager_GotFocus(Control control)
    {
        GamepadWindow gamepadWindow = (GamepadWindow)control;
        switch (gamepadWindow.Title)
        {
            case "QuickTools":
                focusedWindows |= FocusedWindow.Quicktools;
                break;
            default:
                focusedWindows |= FocusedWindow.MainWindow;
                break;
        }

        // check applicable scenarios
        CheckControllerScenario();
    }

    private static void ProcessManager_ForegroundChanged(ProcessEx processEx, ProcessEx backgroundEx)
    {
        foregroundProcess = processEx;

        // check applicable scenarios
        CheckControllerScenario();
    }

    private static void CurrentDevice_KeyReleased(ButtonFlags button)
    {
        // calls current controller (if connected)
        var controller = GetTargetController();
        controller?.InjectButton(button, false, true);
    }

    private static void CurrentDevice_KeyPressed(ButtonFlags button)
    {
        // calls current controller (if connected)
        var controller = GetTargetController();
        controller?.InjectButton(button, true, false);
    }

    private static void CheckControllerScenario()
    {
        ControllerMuted = false;

        // platform specific scenarios
        if (foregroundProcess?.Platform == PlatformType.Steam)
        {
            // mute virtual controller if foreground process is Steam or Steam-related and user a toggle the mute setting
            // Controller specific scenarios
            if (targetController is SteamController)
            {
                SteamController steamController = (SteamController)targetController;
                if (steamController.IsVirtualMuted())
                    ControllerMuted = true;
            }
        }

        // either main window or quicktools are focused
        if (focusedWindows != FocusedWindow.None)
            ControllerMuted = true;
    }

    public static void Stop()
    {
        if (!IsInitialized)
            return;

        IsInitialized = false;

        // unplug on close
        ClearTargetController();

        DeviceManager.XUsbDeviceArrived -= XUsbDeviceArrived;
        DeviceManager.XUsbDeviceRemoved -= XUsbDeviceRemoved;

        DeviceManager.HidDeviceArrived -= HidDeviceArrived;
        DeviceManager.HidDeviceRemoved -= HidDeviceRemoved;

        SettingsManager.SettingValueChanged -= SettingsManager_SettingValueChanged;

        // uncloak on close, if requested
        if (SettingsManager.GetBoolean("HIDuncloakonclose"))
            foreach (var controller in GetPhysicalControllers())
                controller.Unhide(false);

        // Flushing possible JoyShocks...
        JslDisconnectAndDisposeAll();

        LogManager.LogInformation("{0} has stopped", "ControllerManager");
    }

    private static void SettingsManager_SettingValueChanged(string name, object value)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            switch (name)
            {
                case "VibrationStrength":
                    uint VibrationStrength = Convert.ToUInt32(value);
                    targetController?.SetVibrationStrength(VibrationStrength, MainWindow.GetCurrent().IsLoaded);
                    break;

                case "SteamControllerMute":
                    {
                        IController target = GetTargetController();
                        if (target is null)
                            return;

                        if (target is not SteamController)
                            return;

                        bool Muted = Convert.ToBoolean(value);
                        ((SteamController)target).SetVirtualMuted(Muted);
                    }
                    break;
            }
        });
    }

    private static void DeviceManager_Initialized()
    {
        // search for last known controller and connect
        var path = SettingsManager.GetString("HIDInstancePath");

        if (Controllers.ContainsKey(path))
        {
            // last known controller still is plugged, set as target
            SetTargetController(path, false);
        }
        else if (HasPhysicalController())
        {
            // no known controller, connect to first available
            path = GetPhysicalControllers().FirstOrDefault().GetContainerInstancePath();
            SetTargetController(path, false);
        }
    }

    private static void VirtualManager_Vibrated(byte LargeMotor, byte SmallMotor)
    {
        targetController?.SetVibration(LargeMotor, SmallMotor);
    }

    private static void HidDeviceArrived(PnPDetails details, DeviceEventArgs obj)
    {
        if (!details.isGaming)
            return;

        // initialize controller vars
        IController controller = null;

        // JoyShockLibrary
        int connectedJoys = JslConnectDevices();
        if (connectedJoys != 0)
        {
            int[] joysHandle = new int[connectedJoys];
            JslGetConnectedDeviceHandles(joysHandle, connectedJoys);

            // scroll handles until we find matching device path
            int joyShockId = -1;
            JOY_SETTINGS settings = new();

            foreach (int i in joysHandle)
            {
                settings = JslGetControllerInfoAndSettings(i);

                string joyShockpath = settings.path;
                string detailsPath = details.Path;

                if (detailsPath.Equals(joyShockpath, StringComparison.InvariantCultureIgnoreCase))
                {
                    joyShockId = i;
                    break;
                }
            }

            // device found
            if (joyShockId != -1)
            {
                // use handle
                settings.playerNumber = joyShockId;

                JOY_TYPE joyShockType = (JOY_TYPE)JslGetControllerType(joyShockId);

                // UI thread (sync)
                Application.Current.Dispatcher.Invoke(() =>
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
                });
            }
            else
            {
                // unsupported controller
                LogManager.LogError("Couldn't find matching JoyShock controller: VID:{0} and PID:{1}",
                    details.GetVendorID(), details.GetProductID());
            }
        }
        else
        {

            // DInput
            var directInput = new DirectInput();
            int VendorId = details.attributes.VendorID;
            int ProductId = details.attributes.ProductID;

            // initialize controller vars
            Joystick joystick = null;

            // search for the plugged controller
            foreach (var deviceInstance in directInput.GetDevices(DeviceType.Gamepad, DeviceEnumerationFlags.AllDevices))
            {
                try
                {
                    // Instantiate the joystick
                    var lookup_joystick = new Joystick(directInput, deviceInstance.InstanceGuid);
                    var SymLink = DeviceManager.SymLinkToInstanceId(lookup_joystick.Properties.InterfacePath,
                        obj.InterfaceGuid.ToString());

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
                LogManager.LogError("Couldn't find matching DInput controller: VID:{0} and PID:{1}",
                    details.GetVendorID(), details.GetProductID());
            }

            // UI thread (sync)
            Application.Current.Dispatcher.Invoke(() =>
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
            });
        }

        // unsupported controller
        if (controller is null)
        {
            LogManager.LogError("Unsupported Generic controller: VID:{0} and PID:{1}", details.GetVendorID(),
                details.GetProductID());
            return;
        }

        // failed to initialize
        if (controller.Details is null)
            return;

        if (!controller.IsConnected())
            return;

        // update or create controller
        var path = controller.GetContainerInstancePath();
        Controllers[path] = controller;

        // are we power cycling ?
        PowerCyclers.TryGetValue(details.baseContainerDeviceInstanceId, out bool IsPowerCycling);

        // power cycling logic
        // hide new InstanceID (HID)
        if (IsPowerCycling && controller.IsHidden())
            controller.HideHID();

        LogManager.LogDebug("Generic controller {0} plugged", controller.ToString());

        // raise event
        ControllerPlugged?.Invoke(controller, IsPowerCycling);

        ToastManager.SendToast(controller.ToString(), "detected");

        // remove controller from powercyclers
        PowerCyclers.Remove(controller.GetContainerInstancePath());

        // first controller logic
        if (!controller.IsVirtual() && GetTargetController() is null && DeviceManager.IsInitialized)
            SetTargetController(controller.GetContainerInstancePath(), IsPowerCycling);
    }

    private static void HidDeviceRemoved(PnPDetails details, DeviceEventArgs obj)
    {
        if (!Controllers.TryGetValue(details.baseContainerDeviceInstanceId, out IController controller))
            return;

        // XInput controller are handled elsewhere
        if (controller is XInputController)
            return;

        if (controller is JSController)
            JslDisconnect(controller.GetUserIndex());

        // are we power cycling ?
        PowerCyclers.TryGetValue(details.baseContainerDeviceInstanceId, out bool IsPowerCycling);

        // unhide on remove 
        if (!IsPowerCycling)
            controller.UnhideHID();

        // controller was unplugged
        Controllers.Remove(details.baseContainerDeviceInstanceId);

        // unplug controller, if needed
        if (GetTargetController()?.GetContainerInstancePath() == details.baseContainerDeviceInstanceId)
            ClearTargetController();

        LogManager.LogDebug("Generic controller {0} unplugged", controller.ToString());

        // raise event
        ControllerUnplugged?.Invoke(controller, IsPowerCycling);
    }

    private static async void XUsbDeviceArrived(PnPDetails details, DeviceEventArgs obj)
    {
        if (Controllers.TryGetValue(details.baseContainerDeviceInstanceId, out _))
            return;

        // get details passed UserIndex
        UserIndex userIndex = (UserIndex)details.XInputUserIndex;

        // device manager failed to retrieve actual userIndex
        // use backup method
        if (userIndex == UserIndex.Any)
            userIndex = XInputController.TryGetUserIndex(details);

        if (details.isPhysical)
        {
            // wait for virtual manager to wake up
            if (VirtualManager.HIDmode == HIDmode.Xbox360Controller && VirtualManager.HIDstatus == HIDstatus.Connected)
            {
                while (!HasVirtualController())
                    await Task.Delay(250);

                // physical controller arrived and is taking first slot, suspend it
                if (details.XInputUserIndex == (int)UserIndex.One)
                {
                    if (SuspendController(details.baseContainerDeviceInstanceId))
                    {
                        // suspended physical controller
                        return;
                    }
                    else
                    {
                        // failed to suspend physical controller
                    }
                }
            }
        }
        else
        {
            bool resumed = false;

            // do we have a suspended controller
            string baseContainerDeviceInstanceId = SettingsManager.GetString("SuspendedController");
            if (!string.IsNullOrEmpty(baseContainerDeviceInstanceId))
            {
                // (re)enable physical controller(s) after virtual controller to ensure first order
                int attempts = 0;

                // give the controller 10 seconds to come back to half-life
                while (!resumed && attempts < 10)
                {
                    await Task.Delay(1000);
                    resumed = ResumeController();
                    attempts++;
                }
            }
            
            if (!resumed)
            {
                IController controller = GetFirstController();
                if (controller is not null && controller.IsPhysical())
                {
                    // force controller as busy, disable UI
                    controller.IsBusy = true;

                    SettingsManager.SetProperty("SuspendedController", string.Empty);
                    if (SuspendController(controller.Details.baseContainerDeviceInstanceId))
                    {
                        // suspended physical controller
                        return;
                    }
                    else
                    {
                        // failed to suspend physical controller
                    }
                }
            }
        }

        // A XInput controller
        Controller _controller = new(userIndex);

        // UI thread (async)
        _ = Application.Current.Dispatcher.BeginInvoke(async () =>
        {
            XInputController controller;
            switch (details.GetVendorID())
            {
                default:
                    controller = new XInputController(_controller, details);
                    break;

                // LegionGo
                case "0x17EF":
                    controller = new LegionController(_controller, details);
                    break;
            }

            while (!controller.IsReady && _controller.IsConnected)
                await Task.Delay(250);

            // update or create controller
            string path = controller.GetContainerInstancePath();
            Controllers[path] = controller;

            // are we power cycling ?
            PowerCyclers.TryGetValue(details.baseContainerDeviceInstanceId, out bool IsPowerCycling);

            // power cycling logic
            // hide new InstanceID (HID)
            if (IsPowerCycling && controller.IsHidden())
                controller.HideHID();

            LogManager.LogDebug("XInput controller {0} plugged", controller.ToString());

            // raise event
            ControllerPlugged?.Invoke(controller, IsPowerCycling);

            ToastManager.SendToast(controller.ToString(), "detected");

            // remove controller from powercyclers
            PowerCyclers.Remove(controller.GetContainerInstancePath());

            if (controller.IsPhysical())
            {
                // first controller logic
                if (GetTargetController() is null && DeviceManager.IsInitialized)
                    SetTargetController(controller.GetContainerInstancePath(), IsPowerCycling);
            }
        });
    }

    private static async void XUsbDeviceRemoved(PnPDetails details, DeviceEventArgs obj)
    {
        // do we have a controller pending revival ?
        string baseContainerDeviceInstanceId = SettingsManager.GetString("SuspendedController");
        if (details.baseContainerDeviceInstanceId == baseContainerDeviceInstanceId)
        {
            if (VirtualManager.HIDmode == HIDmode.Xbox360Controller && VirtualManager.HIDstatus == HIDstatus.Connected)
            {
                // restart virtual controller
                VirtualManager.Pause();
                VirtualManager.Resume();
            }
        }

        if (!Controllers.TryGetValue(details.baseContainerDeviceInstanceId, out IController controller))
            return;

        // are we power cycling ?
        PowerCyclers.TryGetValue(details.baseContainerDeviceInstanceId, out bool IsPowerCycling);

        // unhide on remove
        if (!IsPowerCycling)
        {
            if (controller.IsReady)
                controller.UnhideHID();
        }

        // controller was unplugged
        Controllers.Remove(details.baseContainerDeviceInstanceId);

        // unplug controller, if needed
        if (GetTargetController()?.GetContainerInstancePath() == controller.GetContainerInstancePath())
            ClearTargetController();

        LogManager.LogDebug("XInput controller {0} unplugged", controller.ToString());

        // raise event
        ControllerUnplugged?.Invoke(controller, IsPowerCycling);
    }

    private static void ClearTargetController()
    {
        // unplug previous controller
        if (targetController is not null)
        {
            targetController.InputsUpdated -= UpdateInputs;
            targetController.SetLightColor(0, 0, 0);
            targetController.Cleanup();
            targetController.Unplug();
            targetController = null;

            // update HIDInstancePath
            SettingsManager.SetProperty("HIDInstancePath", string.Empty);
        }
    }

    public static void SetTargetController(string baseContainerDeviceInstanceId, bool IsPowerCycling)
    {
        // look for new controller
        if (!Controllers.TryGetValue(baseContainerDeviceInstanceId, out IController controller))
            return;

        if (controller is null)
            return;

        if (controller.IsVirtual())
            return;

        // clear current target
        ClearTargetController();

        // update target controller
        targetController = controller;
        targetController.InputsUpdated += UpdateInputs;
        targetController.Plug();

        var _systemBackground = MainWindow.uiSettings.GetColorValue(UIColorType.Background);
        var _systemAccent = MainWindow.uiSettings.GetColorValue(UIColorType.Accent);
        targetController.SetLightColor(_systemAccent.R, _systemAccent.G, _systemAccent.B);

        // update HIDInstancePath
        SettingsManager.SetProperty("HIDInstancePath", baseContainerDeviceInstanceId);

        if (!IsPowerCycling)
        {
            if (SettingsManager.GetBoolean("HIDcloakonconnect"))
                if (!targetController.IsHidden())
                    targetController.Hide();
        }

        // check applicable scenarios
        CheckControllerScenario();

        // check if controller is about to power cycle
        PowerCyclers.TryGetValue(baseContainerDeviceInstanceId, out IsPowerCycling);

        if (!IsPowerCycling)
        {
            if (SettingsManager.GetBoolean("HIDvibrateonconnect"))
                targetController.Rumble();
        }
        else
        {
            // stop listening to device while it's power cycled
            // only usefull for Xbox One bluetooth controllers
            targetController.Unplug();
        }
        
        ControllerSelected?.Invoke(targetController);
    }

    public static IController GetTargetController()
    {
        return targetController;
    }

    public static bool HasPhysicalController()
    {
        return GetPhysicalControllers().Count() != 0;
    }

    public static bool HasVirtualController()
    {
        return GetVirtualControllers().Count() != 0;
    }

    public static IEnumerable<IController> GetPhysicalControllers()
    {
        return Controllers.Values.Where(a => !a.IsVirtual()).ToList();
    }

    public static IEnumerable<IController> GetVirtualControllers()
    {
        return Controllers.Values.Where(a => a.IsVirtual()).ToList();
    }

    public static XInputController GetFirstController()
    {
        return Controllers.Values.FirstOrDefault(c => c is XInputController && c.GetUserIndex() == 0) as XInputController;
    }

    public static List<IController> GetControllers()
    {
        return Controllers.Values.ToList();
    }

    public static bool SuspendController(string baseContainerDeviceInstanceId)
    {
        Controller.SetReporting(false);
        PowerCyclers[baseContainerDeviceInstanceId] = true;

        // raise event
        Working?.Invoke(true);

        try
        {
            PnPDevice pnPDevice = PnPDevice.GetDeviceByInstanceId(baseContainerDeviceInstanceId);
            UsbPnPDevice usbPnPDevice = pnPDevice.ToUsbPnPDevice();

            string enumerator = pnPDevice.GetProperty<string>(DevicePropertyKey.Device_EnumeratorName);
            switch (enumerator)
            {
                case "USB":
                    pnPDevice.InstallNullDriver(out bool rebootRequired);
                    usbPnPDevice.CyclePort();

                    SettingsManager.SetProperty("SuspendedController", baseContainerDeviceInstanceId);
                    return true;
            }
        }
        catch { }

        return false;
    }

    public static bool ResumeController()
    {
        Controller.SetReporting(true);
        bool success = false;

        // disable physical controllers when shutting down to ensure we can give the first order to virtual controller on next boot
        string baseContainerDeviceInstanceId = SettingsManager.GetString("SuspendedController");

        if (string.IsNullOrEmpty(baseContainerDeviceInstanceId))
            return true;

        PowerCyclers.Remove(baseContainerDeviceInstanceId);

        try
        {
            PnPDevice pnPDevice = PnPDevice.GetDeviceByInstanceId(baseContainerDeviceInstanceId);
            UsbPnPDevice usbPnPDevice = pnPDevice.ToUsbPnPDevice();
            DriverMeta pnPDriver = null;

            try
            {
                pnPDevice.GetCurrentDriver();
            }
            catch { }

            string enumerator = pnPDevice.GetProperty<string>(DevicePropertyKey.Device_EnumeratorName);
            switch (enumerator)
            {
                case "USB":
                    if (pnPDriver is null || pnPDriver.InfPath != "xusb22.inf")
                        pnPDevice.InstallCustomDriver("xusb22.inf", out bool rebootRequired);

                    SettingsManager.SetProperty("SuspendedController", string.Empty);
                    success = true;
                    break;
            }
        }
        catch { }

        // raise event
        Working?.Invoke(false);

        return success;
    }

    private static void UpdateInputs(ControllerState controllerState)
    {
        ButtonState buttonState = controllerState.ButtonState.Clone() as ButtonState;

        // raise event
        InputsUpdated?.Invoke(controllerState);

        // pass inputs to Inputs manager
        InputsManager.UpdateReport(buttonState);

        // pass to SensorsManager for sensors value reading
        SensorFamily sensorSelection = (SensorFamily)SettingsManager.GetInt("SensorSelection");
        switch (sensorSelection)
        {
            case SensorFamily.Windows:
            case SensorFamily.SerialUSBIMU:
                SensorsManager.UpdateReport(controllerState);
                break;
        }

        // pass to MotionManager for calculations
        MotionManager.UpdateReport(controllerState);

        // pass inputs to Overlay Model
        MainWindow.overlayModel.UpdateReport(controllerState);

        // pass inputs to Layout manager
        controllerState = LayoutManager.MapController(controllerState);

        // controller is muted
        if (ControllerMuted)
            return;

        VirtualManager.UpdateInputs(controllerState);
    }

    internal static IController GetEmulatedController()
    {
        var HIDmode = (HIDmode)SettingsManager.GetInt("HIDmode", true);
        switch (HIDmode)
        {
            default:
            case HIDmode.NoController:
            case HIDmode.Xbox360Controller:
                return emptyXInput;

            case HIDmode.DualShock4Controller:
                return emptyDS4;
        }
    }

    private static void VirtualManager_ControllerSelected(HIDmode mode)
    {
        virtualControllerCreated = true;
    }

    #region events

    public static event ControllerPluggedEventHandler ControllerPlugged;
    public delegate void ControllerPluggedEventHandler(IController Controller, bool IsPowerCycling);

    public static event ControllerUnpluggedEventHandler ControllerUnplugged;
    public delegate void ControllerUnpluggedEventHandler(IController Controller, bool IsPowerCycling);

    public static event ControllerSelectedEventHandler ControllerSelected;
    public delegate void ControllerSelectedEventHandler(IController Controller);

    public static event InputsUpdatedEventHandler InputsUpdated;
    public delegate void InputsUpdatedEventHandler(ControllerState Inputs);

    public static event WorkingEventHandler Working;
    public delegate void WorkingEventHandler(bool busy);

    public static event InitializedEventHandler Initialized;
    public delegate void InitializedEventHandler();

    #endregion
}