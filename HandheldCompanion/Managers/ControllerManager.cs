using HandheldCompanion.Controllers;
using HandheldCompanion.Controls;
using HandheldCompanion.Devices;
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Windows.UI;
using Windows.UI.ViewManagement;
using static HandheldCompanion.Utils.DeviceUtils;
using static JSL;
using DeviceType = SharpDX.DirectInput.DeviceType;

namespace HandheldCompanion.Managers;

public static class ControllerManager
{
    private static readonly ConcurrentDictionary<string, IController> Controllers = new();
    public static readonly ConcurrentDictionary<string, bool> PowerCyclers = new();

    private static Thread watchdogThread;
    private static bool watchdogThreadRunning;
    private static bool ControllerManagement;

    private static bool ControllerManagementSuccess = false;
    private static int ControllerManagementAttempts = 0;

    private static readonly XInputController? emptyXInput = new();
    private static readonly DS4Controller? emptyDS4 = new();

    private static IController? targetController;
    private static FocusedWindow focusedWindows = FocusedWindow.None;
    private static ProcessEx? foregroundProcess;
    private static bool ControllerMuted;

    public static bool IsInitialized;

    static ControllerManager()
    {
        watchdogThread = new Thread(watchdogThreadLoop);
        watchdogThread.IsBackground = true;
    }

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

                case "ControllerManagement":
                    {
                        ControllerManagement = Convert.ToBoolean(value);
                        switch(ControllerManagement)
                        {
                            case true:
                                {
                                    if (!watchdogThreadRunning)
                                    {
                                        watchdogThreadRunning = true;

                                        watchdogThread = new Thread(watchdogThreadLoop);
                                        watchdogThread.IsBackground = true;
                                        watchdogThread.Start();
                                    }
                                }
                                break;
                            case false:
                                {
                                    if (watchdogThreadRunning)
                                    {
                                        watchdogThreadRunning = false;
                                        watchdogThread.Join();
                                    }
                                }
                                break;
                        }
                    }
                    break;

                case "LegionControllerPassthrough":
                    {
                        IController target = GetTargetController();
                        if (target is null)
                            return;

                        if (target is not LegionController)
                            return;

                        bool enabled = Convert.ToBoolean(value);
                        ((LegionController)target).SetPassthrough(enabled);
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

    private static async void HidDeviceArrived(PnPDetails details, DeviceEventArgs obj)
    {
        if (!details.isGaming)
            return;

        Controllers.TryGetValue(details.baseContainerDeviceInstanceId, out IController controller);

        // are we power cycling ?
        PowerCyclers.TryGetValue(details.baseContainerDeviceInstanceId, out bool IsPowerCycling);

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
                string detailsPath = details.devicePath;

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

                if (controller is not null)
                {
                    ((JSController)controller).AttachDetails(details);
                    ((JSController)controller).AttachJoySettings(settings);

                    // hide new InstanceID (HID)
                    if (controller.IsHidden())
                        controller.Hide(false);

                    IsPowerCycling = true;
                }
                else
                {
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
            int VendorId = details.VendorID;
            int ProductId = details.ProductID;

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

            if (controller is not null)
            {
                controller.AttachDetails(details);

                // hide new InstanceID (HID)
                if (controller.IsHidden())
                    controller.Hide(false);

                IsPowerCycling = true;
            }
            else
            {
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
        }

        // unsupported controller
        if (controller is null)
        {
            LogManager.LogError("Unsupported Generic controller: VID:{0} and PID:{1}", details.GetVendorID(),
                details.GetProductID());
            return;
        }

        while (!controller.IsReady && controller.IsConnected())
            await Task.Delay(250);

        // set (un)busy
        controller.IsBusy = false;

        // update or create controller
        var path = controller.GetContainerInstancePath();
        Controllers[path] = controller;

        LogManager.LogDebug("Generic controller {0} plugged", controller.ToString());

        // raise event
        ControllerPlugged?.Invoke(controller, IsPowerCycling);

        ToastManager.SendToast(controller.ToString(), "detected");

        // remove controller from powercyclers
        PowerCyclers.TryRemove(controller.GetContainerInstancePath(), out _);

        // new controller logic
        if (DeviceManager.IsInitialized)
        {
            if (controller.IsPhysical() && targetController is null)
                SetTargetController(controller.GetContainerInstancePath(), IsPowerCycling);

            if (targetController is not null)
            {
                Color _systemBackground = MainWindow.uiSettings.GetColorValue(UIColorType.Background);
                Color _systemAccent = MainWindow.uiSettings.GetColorValue(UIColorType.Accent);
                targetController.SetLightColor(_systemAccent.R, _systemAccent.G, _systemAccent.B);
            }
        }
    }

    private static async void HidDeviceRemoved(PnPDetails details, DeviceEventArgs obj)
    {
        IController controller = null;

        DateTime timeout = DateTime.Now.Add(TimeSpan.FromSeconds(8));
        while (DateTime.Now < timeout && !Controllers.TryGetValue(details.baseContainerDeviceInstanceId, out controller))
            await Task.Delay(100);

        if (controller is null)
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
        {
            controller.Unhide(false);

            // unplug controller, if needed
            if (GetTargetController()?.GetContainerInstancePath() == details.baseContainerDeviceInstanceId)
                ClearTargetController();

            // controller was unplugged
            Controllers.TryRemove(details.baseContainerDeviceInstanceId, out _);
        }

        LogManager.LogDebug("Generic controller {0} unplugged", controller.ToString());

        // raise event
        ControllerUnplugged?.Invoke(controller, IsPowerCycling);
    }

    private static void watchdogThreadLoop(object? obj)
    {
        while(watchdogThreadRunning)
        {
            // monitoring unexpected slot changes
            HashSet<byte> UserIndexes = new();
            bool XInputDrunk = false;

            foreach (XInputController xInputController in Controllers.Values.Where(c => c.Details is not null && c.Details.isXInput))
            {
                byte UserIndex = DeviceManager.GetXInputIndexAsync(xInputController.Details.baseContainerDevicePath);

                // controller is not ready yet
                if (UserIndex == byte.MaxValue)
                    continue;

                // that's not possible, XInput is drunk
                if (!UserIndexes.Add(UserIndex))
                    XInputDrunk = true;

                xInputController.AttachController(UserIndex);
            }

            if (XInputDrunk)
            {
                foreach (XInputController xInputController in Controllers.Values.Where(c => c.Details is not null && c.Details.isXInput))
                    xInputController.AttachController(byte.MaxValue);
            }

            if (VirtualManager.HIDmode == HIDmode.Xbox360Controller && VirtualManager.HIDstatus == HIDstatus.Connected)
            {
                if (HasVirtualController())
                {
                    // check if it is first controller
                    IController controller = GetControllerFromSlot(UserIndex.One, false);
                    if (controller is null)
                    {
                        // disable that setting if we failed three times
                        if (ControllerManagementAttempts == 3)
                        {
                            // resume all physical controllers
                            StringCollection deviceInstanceIds = SettingsManager.GetStringCollection("SuspendedControllers");
                            if (deviceInstanceIds is not null && deviceInstanceIds.Count != 0)
                                ResumeControllers();

                            ControllerManagementSuccess = false;
                            ControllerManagementAttempts = 0;

                            Working?.Invoke(2);

                            // suspend watchdog
                            watchdogThreadRunning = false;
                            watchdogThread.Join();
                        }
                        else
                        {
                            Working?.Invoke(0);
                            ControllerManagementSuccess = false;
                            ControllerManagementAttempts++;

                            // suspend virtual controller
                            VirtualManager.Suspend();

                            // suspend all physical controllers
                            foreach (XInputController xInputController in GetPhysicalControllers())
                                SuspendController(xInputController.Details.baseContainerDeviceInstanceId);

                            // resume virtual controller
                            VirtualManager.Resume();

                            // resume all physical controllers
                            StringCollection deviceInstanceIds = SettingsManager.GetStringCollection("SuspendedControllers");
                            if (deviceInstanceIds is not null && deviceInstanceIds.Count != 0)
                                ResumeControllers();

                            // suspend and resume virtual controller
                            VirtualManager.Suspend();
                            VirtualManager.Resume();
                        }
                    }
                    else
                    {
                        // resume all physical controllers
                        StringCollection deviceInstanceIds = SettingsManager.GetStringCollection("SuspendedControllers");
                        if (deviceInstanceIds is not null && deviceInstanceIds.Count != 0)
                            ResumeControllers();

                        // give us one extra loop to make sure we're good
                        if (!ControllerManagementSuccess)
                        {
                            ControllerManagementSuccess = true;
                            Working?.Invoke(1);
                        }
                        else
                            ControllerManagementAttempts = 0;
                    }
                }
            }

            Thread.Sleep(2000);
        }
    }

    private static async void XUsbDeviceArrived(PnPDetails details, DeviceEventArgs obj)
    {
        Controllers.TryGetValue(details.baseContainerDeviceInstanceId, out IController controller);

        // are we power cycling ?
        PowerCyclers.TryGetValue(details.baseContainerDeviceInstanceId, out bool IsPowerCycling);

        // get details passed UserIndex
        UserIndex userIndex = (UserIndex)details.XInputUserIndex;

        // device manager failed to retrieve actual userIndex
        // use backup method
        if (userIndex == UserIndex.Any)
            userIndex = XInputController.TryGetUserIndex(details);

        if (controller is not null)
        {
            ((XInputController)controller).AttachDetails(details);
            ((XInputController)controller).AttachController((byte)userIndex);

            // hide new InstanceID (HID)
            if (controller.IsHidden())
                controller.Hide(false);

            IsPowerCycling = true;
        }
        else
        {
            Application.Current.Dispatcher.Invoke(() =>
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
                }
            });
        }

        while (!controller.IsReady && controller.IsConnected())
            await Task.Delay(250);

        // set (un)busy
        controller.IsBusy = false;

        // update or create controller
        string path = details.baseContainerDeviceInstanceId;
        Controllers[path] = controller;

        LogManager.LogDebug("XInput controller {0} plugged", controller.ToString());

        // raise event
        ControllerPlugged?.Invoke(controller, IsPowerCycling);

        ToastManager.SendToast(controller.ToString(), "detected");

        // remove controller from powercyclers
        PowerCyclers.TryRemove(controller.GetContainerInstancePath(), out _);

        // new controller logic
        if (DeviceManager.IsInitialized)
        {
            if (controller.IsPhysical() && targetController is null)
                SetTargetController(controller.GetContainerInstancePath(), IsPowerCycling);

            if (targetController is not null)
            {
                Color _systemBackground = MainWindow.uiSettings.GetColorValue(UIColorType.Background);
                Color _systemAccent = MainWindow.uiSettings.GetColorValue(UIColorType.Accent);
                targetController.SetLightColor(_systemAccent.R, _systemAccent.G, _systemAccent.B);

                var ManufacturerName = MotherboardInfo.Manufacturer.ToUpper();
                switch(ManufacturerName)
                {
                    case "AOKZOE":
                    case "ONE-NETBOOK TECHNOLOGY CO., LTD.":
                    case "ONE-NETBOOK":
                        targetController.Rumble();
                        break;
                }
            }
        }
    }

    private static async void XUsbDeviceRemoved(PnPDetails details, DeviceEventArgs obj)
    {
        IController controller = null;

        DateTime timeout = DateTime.Now.Add(TimeSpan.FromSeconds(8));
        while (DateTime.Now < timeout && !Controllers.TryGetValue(details.baseContainerDeviceInstanceId, out controller))
            await Task.Delay(100);

        if (controller is null)
            return;

        // are we power cycling ?
        PowerCyclers.TryGetValue(details.baseContainerDeviceInstanceId, out bool IsPowerCycling);

        // controller was unplugged
        if (!IsPowerCycling)
        {
            controller.Unhide(false);
            Controllers.TryRemove(details.baseContainerDeviceInstanceId, out _);

            // controller is current target
            if (targetController?.GetContainerInstancePath() == details.baseContainerDeviceInstanceId)
                ClearTargetController();
        }

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

        if (controller.IsVirtual())
            return;

        // clear current target
        ClearTargetController();

        // update target controller
        targetController = controller;
        targetController.InputsUpdated += UpdateInputs;
        targetController.Plug();
        
        Color _systemBackground = MainWindow.uiSettings.GetColorValue(UIColorType.Background);
        Color _systemAccent = MainWindow.uiSettings.GetColorValue(UIColorType.Accent);
        targetController.SetLightColor(_systemAccent.R, _systemAccent.G, _systemAccent.B);

        // update HIDInstancePath
        SettingsManager.SetProperty("HIDInstancePath", baseContainerDeviceInstanceId);

        if (!IsPowerCycling)
        {
            if (SettingsManager.GetBoolean("HIDcloakonconnect"))
            {
                bool powerCycle = true;

                if (targetController is LegionController)
                {
                    // todo:    Look for a byte within hid report that'd tend to mean both controllers are synced.
                    //          Then I guess we could try and power cycle them.
                    powerCycle = !((LegionController)targetController).IsWireless;
                }

                if (!targetController.IsHidden())
                    targetController.Hide(powerCycle);
            }
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
        
        ControllerSelected?.Invoke(targetController);
    }

    public static bool SuspendController(string baseContainerDeviceInstanceId)
    {
        // PnPUtil.StartPnPUtil(@"/delete-driver C:\Windows\INF\xusb22.inf /uninstall /force");
        StringCollection deviceInstanceIds = SettingsManager.GetStringCollection("SuspendedControllers");

        if (deviceInstanceIds is null)
            deviceInstanceIds = new();

        try
        {
            PnPDevice pnPDevice = PnPDevice.GetDeviceByInstanceId(baseContainerDeviceInstanceId);
            UsbPnPDevice usbPnPDevice = pnPDevice.ToUsbPnPDevice();
            DriverMeta pnPDriver = null;

            try
            {
                pnPDriver = pnPDevice.GetCurrentDriver();
            }
            catch { }

            string enumerator = pnPDevice.GetProperty<string>(DevicePropertyKey.Device_EnumeratorName);
            switch (enumerator)
            {
                case "USB":
                    if (pnPDriver is not null)
                    {
                        pnPDevice.InstallNullDriver(out bool rebootRequired);
                            usbPnPDevice.CyclePort();
                    }

                    if (!deviceInstanceIds.Contains(baseContainerDeviceInstanceId))
                        deviceInstanceIds.Add(baseContainerDeviceInstanceId);

                    SettingsManager.SetProperty("SuspendedControllers", deviceInstanceIds);
                    PowerCyclers[baseContainerDeviceInstanceId] = true;
                    return true;
            }
        }
        catch { }


        return false;
    }

    public static bool ResumeControllers()
    {
        // PnPUtil.StartPnPUtil(@"/add-driver C:\Windows\INF\xusb22.inf /install");
        StringCollection deviceInstanceIds = SettingsManager.GetStringCollection("SuspendedControllers");

        if (deviceInstanceIds is null || deviceInstanceIds.Count == 0)
            return true;

        foreach (string baseContainerDeviceInstanceId in deviceInstanceIds)
        {
            try
            {
                PnPDevice pnPDevice = PnPDevice.GetDeviceByInstanceId(baseContainerDeviceInstanceId);
                UsbPnPDevice usbPnPDevice = pnPDevice.ToUsbPnPDevice();
                DriverMeta pnPDriver = null;

                try
                {
                    pnPDriver = pnPDevice.GetCurrentDriver();
                }
                catch { }

                string enumerator = pnPDevice.GetProperty<string>(DevicePropertyKey.Device_EnumeratorName);
                switch (enumerator)
                {
                    case "USB":
                        if (pnPDriver is null || pnPDriver.InfPath != "xusb22.inf")
                        {
                            pnPDevice.RemoveAndSetup();
                            pnPDevice.InstallCustomDriver("xusb22.inf", out bool rebootRequired);
                        }

                        if (deviceInstanceIds.Contains(baseContainerDeviceInstanceId))
                            deviceInstanceIds.Remove(baseContainerDeviceInstanceId);

                        SettingsManager.SetProperty("SuspendedControllers", deviceInstanceIds);
                        PowerCyclers.TryRemove(baseContainerDeviceInstanceId, out _);
                        return true;
                }
            }
            catch { }
        }

        return false;
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

    public static XInputController GetControllerFromSlot(UserIndex userIndex = 0, bool physical = true)
    {
        return Controllers.Values.FirstOrDefault(c => c is XInputController && ((physical && c.IsPhysical()) || !physical && c.IsVirtual()) && c.GetUserIndex() == (int)userIndex) as XInputController;
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
        {
            ControllerState emptyState = new ControllerState();
            emptyState.ButtonState[ButtonFlags.Special] = controllerState.ButtonState[ButtonFlags.Special];

            controllerState = emptyState;
        }

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
    public delegate void WorkingEventHandler(int status);

    public static event InitializedEventHandler Initialized;
    public delegate void InitializedEventHandler();

    #endregion
}