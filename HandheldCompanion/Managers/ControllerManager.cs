using HandheldCompanion.Controllers;
using HandheldCompanion.Controls;
using HandheldCompanion.Inputs;
using HandheldCompanion.Platforms;
using HandheldCompanion.Utils;
using HandheldCompanion.Views;
using HandheldCompanion.Views.Classes;
using Nefarius.Utilities.DeviceManagement.PnP;
using SharpDX.DirectInput;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
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

        // enable HidHide
        HidHide.SetCloaking(true);

        IsInitialized = true;
        Initialized?.Invoke();

        // summon an empty controller, used to feed Layout UI
        // todo: improve me
        ControllerSelected?.Invoke(GetEmulatedController());

        LogManager.LogInformation("{0} has started", "ControllerManager");
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

        DeviceManager.XUsbDeviceArrived -= XUsbDeviceArrived;
        DeviceManager.XUsbDeviceRemoved -= XUsbDeviceRemoved;

        DeviceManager.HidDeviceArrived -= HidDeviceArrived;
        DeviceManager.HidDeviceRemoved -= HidDeviceRemoved;

        SettingsManager.SettingValueChanged -= SettingsManager_SettingValueChanged;

        // uncloak on close, if requested
        if (SettingsManager.GetBoolean("HIDuncloakonclose"))
            foreach (var controller in Controllers.Values)
                controller.Unhide();

        // unplug on close
        var target = GetTargetController();
        target?.Unplug();

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
                    var SymLink = DeviceManager.PathToInstanceId(lookup_joystick.Properties.InterfacePath,
                        obj.InterfaceGuid.ToString());

                    // IG_ means it is an XInput controller and therefore is handled elsewhere
                    if (lookup_joystick.Properties.InterfacePath.Contains("IG_",
                            StringComparison.InvariantCultureIgnoreCase))
                        continue;

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
        if (IsPowerCycling)
        {
            // hide new InstanceID (HID)
            if (controller.IsHidden())
                HidHide.HidePath(details.deviceInstanceId);
        }

        // first controller logic
        if (!controller.IsVirtual() && GetTargetController() is null && DeviceManager.IsInitialized)
            SetTargetController(controller.GetContainerInstancePath(), IsPowerCycling);

        LogManager.LogDebug("Generic controller {0} plugged", controller.ToString());

        // raise event
        ControllerPlugged?.Invoke(controller, IsHCVirtualController(controller), IsPowerCycling);

        // remove controller from powercyclers
        PowerCyclers[details.baseContainerDeviceInstanceId] = false;

        ToastManager.SendToast(controller.ToString(), "detected");
    }

    private static void HidDeviceRemoved(PnPDetails details, DeviceEventArgs obj)
    {
        if (!Controllers.TryGetValue(details.baseContainerDeviceInstanceId, out IController controller))
            return;

        // XInput controller are handled elsewhere
        if (controller.GetType() == typeof(XInputController))
            return;

        // are we power cycling ?
        PowerCyclers.TryGetValue(details.baseContainerDeviceInstanceId, out bool IsPowerCycling);

        // controller was unplugged
        Controllers.Remove(details.baseContainerDeviceInstanceId);

        // unplug controller, if needed
        if (GetTargetController()?.GetContainerInstancePath() == details.baseContainerDeviceInstanceId)
            ClearTargetController();

        LogManager.LogDebug("Generic controller {0} unplugged", controller.ToString());

        // raise event
        ControllerUnplugged?.Invoke(controller, IsPowerCycling);
    }

    private static void XUsbDeviceArrived(PnPDetails details, DeviceEventArgs obj)
    {
        // get details passed UserIndex
        UserIndex userIndex = (UserIndex)details.XInputUserIndex;

        // device manager failed to retrieve actual userIndex
        // use backup method
        if (userIndex == UserIndex.Any)
            userIndex = XInputController.TryGetUserIndex(details);

        // A XInput controller
        Controller _controller = new(userIndex);

        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            XInputController controller = new(_controller, details);

            // failed to initialize
            if (controller.Details is null)
                return;

            if (!controller.IsConnected())
                return;

            // update or create controller
            string path = controller.GetContainerInstancePath();
            Controllers[path] = controller;

            // are we power cycling ?
            PowerCyclers.TryGetValue(details.baseContainerDeviceInstanceId, out bool IsPowerCycling);

            // power cycling logic
            if (IsPowerCycling)
            {
                // hide new InstanceID (HID)
                if (controller.IsHidden())
                    HidHide.HidePath(details.deviceInstanceId);
            }

            // first controller logic
            if (!controller.IsVirtual() && GetTargetController() is null && DeviceManager.IsInitialized)
                SetTargetController(controller.GetContainerInstancePath(), IsPowerCycling);

            LogManager.LogDebug("XInput controller {0} plugged", controller.ToString());

            // raise event
            ControllerPlugged?.Invoke(controller, IsHCVirtualController(controller), IsPowerCycling);

            // remove controller from powercyclers
            PowerCyclers[details.baseContainerDeviceInstanceId] = false;

            ToastManager.SendToast(controller.ToString(), "detected");
        });
    }

    private static void XUsbDeviceRemoved(PnPDetails details, DeviceEventArgs obj)
    {
        if (!Controllers.TryGetValue(details.baseContainerDeviceInstanceId, out IController controller))
            return;

        // are we power cycling ?
        PowerCyclers.TryGetValue(details.baseContainerDeviceInstanceId, out bool IsPowerCycling);

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
            targetController.Cleanup();
            targetController.Unplug();
            targetController = null;
        }
    }

    public static void SetTargetController(string baseContainerDeviceInstanceId, bool IsPowerCycling)
    {
        // unplug current controller
        if (targetController is not null && targetController.IsPlugged())
        {
            string targetPath = targetController.GetContainerInstancePath();

            ClearTargetController();

            // if we're setting currently selected, it's unplugged, there is none plugged
            // reset the UI to the default controller and stop
            if (targetPath == baseContainerDeviceInstanceId)
            {
                // reset layout UI
                ControllerSelected?.Invoke(GetEmulatedController());
                return;
            }
        }

        // look for new controller
        if (!Controllers.TryGetValue(baseContainerDeviceInstanceId, out IController controller))
            return;

        if (controller is null)
            return;

        if (controller.IsVirtual())
            return;

        // update target controller
        targetController = controller;
        targetController.InputsUpdated += UpdateInputs;
        targetController.Plug();

        if (!IsPowerCycling)
        {
            if (SettingsManager.GetBoolean("HIDvibrateonconnect"))
                targetController.Rumble();

            if (SettingsManager.GetBoolean("HIDcloakonconnect"))
            {
                // we shouldn't hide steam controller on connect
                if (targetController is not SteamController)
                    targetController.Hide();
            }

            // update settings
            SettingsManager.SetProperty("HIDInstancePath", baseContainerDeviceInstanceId);
        }

        // check applicable scenarios
        CheckControllerScenario();

        // raise event
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

    public static List<IController> GetControllers()
    {
        return Controllers.Values.ToList();
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

    private static bool IsHCVirtualController(XInputController controller)
    {
        if (controller.IsVirtual() && virtualControllerCreated)
        {
            virtualControllerCreated = false;
            return true;
        }
        return false;
    }

    private static bool IsHCVirtualController(IController controller)
    {
        if (controller.IsVirtual() && virtualControllerCreated)
        {
            virtualControllerCreated = false;
            return true;
        }
        return false;
    }

    private static void VirtualManager_ControllerSelected(IController Controller)
    {
        virtualControllerCreated = true;
    }

    #region events

    public static event ControllerPluggedEventHandler ControllerPlugged;

    public delegate void ControllerPluggedEventHandler(IController Controller, bool isHCVirtualController, bool IsPowerCycling);

    public static event ControllerUnpluggedEventHandler ControllerUnplugged;

    public delegate void ControllerUnpluggedEventHandler(IController Controller, bool IsPowerCycling);

    public static event ControllerSelectedEventHandler ControllerSelected;

    public delegate void ControllerSelectedEventHandler(IController Controller);

    public static event InputsUpdatedEventHandler InputsUpdated;

    public delegate void InputsUpdatedEventHandler(ControllerState Inputs);

    public static event InitializedEventHandler Initialized;

    public delegate void InitializedEventHandler();

    #endregion
}