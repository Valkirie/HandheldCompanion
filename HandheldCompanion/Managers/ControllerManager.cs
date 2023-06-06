using ControllerCommon;
using ControllerCommon.Controllers;
using ControllerCommon.Inputs;
using ControllerCommon.Managers;
using ControllerCommon.Pipes;
using ControllerCommon.Platforms;
using ControllerCommon.Utils;
using HandheldCompanion.Controllers;
using HandheldCompanion.Controls;
using HandheldCompanion.Views;
using Nefarius.Utilities.DeviceManagement.PnP;
using SharpDX.DirectInput;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using DeviceType = SharpDX.DirectInput.DeviceType;

namespace HandheldCompanion.Managers
{
    public static class ControllerManager
    {
        #region events
        public static event ControllerPluggedEventHandler ControllerPlugged;
        public delegate void ControllerPluggedEventHandler(IController Controller);

        public static event ControllerUnpluggedEventHandler ControllerUnplugged;
        public delegate void ControllerUnpluggedEventHandler(IController Controller);

        public static event ControllerSelectedEventHandler ControllerSelected;
        public delegate void ControllerSelectedEventHandler(IController Controller);

        public static event InitializedEventHandler Initialized;
        public delegate void InitializedEventHandler();
        #endregion

        private static Dictionary<string, IController> Controllers = new();
        private static Dictionary<UserIndex, bool> XUsbControllers = new()
        {
            { UserIndex.One, true },
            { UserIndex.Two, true },
            { UserIndex.Three, true },
            { UserIndex.Four, true },
        };

        private static XInputController? emptyXInput = new();
        private static DS4Controller? emptyDS4 = new();

        private static IController? targetController;
        private static ProcessEx? foregroundProcess;
        private static bool ControllerMuted;

        private static bool IsInitialized;

        public static void Start()
        {
            DeviceManager.XUsbDeviceArrived += XUsbDeviceArrived;
            DeviceManager.XUsbDeviceRemoved += XUsbDeviceRemoved;

            DeviceManager.HidDeviceArrived += HidDeviceArrived;
            DeviceManager.HidDeviceRemoved += HidDeviceRemoved;

            DeviceManager.Initialized += DeviceManager_Initialized;

            SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

            ProcessManager.ForegroundChanged += ProcessManager_ForegroundChanged;

            PipeClient.Connected += OnClientConnected;

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

        private static void CurrentDevice_KeyReleased(ButtonFlags button)
        {
            // calls current controller (if connected)
            IController controller = ControllerManager.GetTargetController();
            controller?.InjectButton(button, false, true);
        }

        private static void CurrentDevice_KeyPressed(ButtonFlags button)
        {
            // calls current controller (if connected)
            IController controller = ControllerManager.GetTargetController();
            controller?.InjectButton(button, true, false);
        }

        private static void CheckControllerScenario()
        {
            ControllerMuted = false;

            // controller specific scenarios
            if (targetController?.GetType() == typeof(NeptuneController))
            {
                NeptuneController neptuneController = (NeptuneController)targetController;

                // mute virtual controller if foreground process is Steam or Steam-related and user a toggle the mute setting
                if (foregroundProcess?.Platform == PlatformType.Steam)
                {
                    if (neptuneController.IsVirtualMuted())
                    {
                        ControllerMuted = true;
                        return;
                    }
                }
            }
        }

        private static void ProcessManager_ForegroundChanged(ProcessEx processEx, ProcessEx backgroundEx)
        {
            foregroundProcess = processEx;

            // check applicable scenarios
            CheckControllerScenario();
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
                foreach (IController controller in Controllers.Values)
                    controller.Unhide();

            // unplug on close
            IController target = GetTargetController();
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
                    case "HIDstrength":
                        double HIDstrength = Convert.ToDouble(value);
                        SetHIDStrength(HIDstrength);
                        break;

                    case "SteamDeckMuteController":
                        {
                            IController target = GetTargetController();
                            if (target is null)
                                return;

                            if (typeof(NeptuneController) != target.GetType())
                                return;

                            bool Muted = Convert.ToBoolean(value);
                            ((NeptuneController)target).SetVirtualMuted(Muted);
                        }
                        break;

                    case "SteamDeckHDRumble":
                        {
                            IController target = GetTargetController();
                            if (target is null)
                                return;

                            if (typeof(NeptuneController) != target.GetType())
                                return;

                            bool HDRumble = Convert.ToBoolean(value);
                            ((NeptuneController)target).SetHDRumble(HDRumble);
                        }
                        break;
                }
            });
        }

        private static void DeviceManager_Initialized()
        {
            // search for last known controller and connect
            string path = SettingsManager.GetString("HIDInstancePath");

            if (Controllers.ContainsKey(path))
            {
                SetTargetController(path);
            }
            else if (HasPhysicalController())
            {
                // no known controller, connect to the first available
                path = GetPhysicalControllers().FirstOrDefault().GetInstancePath();
                SetTargetController(path);
            }
        }

        private static void SetHIDStrength(double value)
        {
            IController target = GetTargetController();
            target?.SetVibrationStrength(value, SettingsManager.IsInitialized);
        }

        private static void HidDeviceArrived(PnPDetails details, DeviceEventArgs obj)
        {
            DirectInput directInput = new DirectInput();
            int VendorId = details.attributes.VendorID;
            int ProductId = details.attributes.ProductID;

            // UI thread (synchronous)
            // We need to wait for each controller to initialize and take (or not) its slot in the array
            Application.Current.Dispatcher.Invoke(() =>
            {
                // initialize controller vars
                Joystick joystick = null;
                IController controller = null;

                // search for the plugged controller
                foreach (var deviceInstance in directInput.GetDevices(DeviceType.Gamepad, DeviceEnumerationFlags.AllDevices))
                {
                    try
                    {
                        // Instantiate the joystick
                        var lookup_joystick = new Joystick(directInput, deviceInstance.InstanceGuid);
                        string SymLink = DeviceManager.PathToInstanceId(lookup_joystick.Properties.InterfacePath, obj.InterfaceGuid.ToString());

                        // IG_ means it is an XInput controller and therefore is handled elsewhere
                        if (lookup_joystick.Properties.InterfacePath.Contains("IG_", StringComparison.InvariantCultureIgnoreCase))
                            continue;

                        if (SymLink.Equals(details.SymLink, StringComparison.InvariantCultureIgnoreCase))
                        {
                            joystick = lookup_joystick;
                            break;
                        }
                    }
                    catch { }
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
                    LogManager.LogError("Couldn't find matching DInput controller: VID:{0} and PID:{1}", details.GetVendorID(), details.GetProductID());
                }

                // search for a supported controller
                switch (VendorId)
                {
                    // SONY
                    case 0x054C:
                        {
                            switch (ProductId)
                            {
                                case 0x0268:    // DualShock 3
                                case 0x05C4:    // DualShock 4
                                case 0x09CC:    // DualShock 4 (2nd Gen)
                                case 0x0CE6:    // DualSense
                                    controller = new DS4Controller(joystick, details);
                                    break;
                            }
                        }
                        break;

                    // STEAM
                    case 0x28DE:
                        {
                            switch (ProductId)
                            {
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

                // unsupported controller
                if (controller is null)
                {
                    LogManager.LogError("Unsupported DInput controller: VID:{0} and PID:{1}", details.GetVendorID(), details.GetProductID());
                    return;
                }

                // failed to initialize
                if (controller.Details is null)
                    return;

                if (!controller.IsConnected())
                    return;

                /*
                if (controller.IsVirtual())
                    return;
                */

                // update or create controller
                string path = controller.GetInstancePath();
                Controllers[path] = controller;

                // raise event
                ControllerPlugged?.Invoke(controller);
                ToastManager.SendToast(controller.ToString(), "detected");
            });
        }

        private static void HidDeviceRemoved(PnPDetails details, DeviceEventArgs obj)
        {
            if (!Controllers.TryGetValue(details.deviceInstanceId, out IController controller))
                return;

            if (!controller.IsConnected())
                return;

            /*
            if (controller.IsVirtual())
                return;
            */

            // XInput controller are handled elsewhere
            if (controller.GetType() == typeof(XInputController))
                return;

            // controller was unplugged
            Controllers.Remove(details.deviceInstanceId);

            // raise event
            ControllerUnplugged?.Invoke(controller);
        }

        private static void XUsbDeviceArrived(PnPDetails details, DeviceEventArgs obj)
        {
            // trying to guess XInput behavior...
            // get first available slot
            UserIndex slot = UserIndex.One;
            Controller _controller = new(slot);

            for (slot = UserIndex.One; slot <= UserIndex.Four; slot++)
            {
                _controller = new(slot);

                // check if controller is connected and slot free
                if (_controller.IsConnected && XUsbControllers[slot])
                    break;
            }

            // UI thread (synchronous)
            // We need to wait for each controller to initialize and take (or not) its slot in the array
            Application.Current.Dispatcher.Invoke(() =>
            {
                XInputController controller = new(_controller);

                // failed to initialize
                if (controller.Details is null)
                    return;

                if (!controller.IsConnected())
                    return;

                // slot is now busy
                XUsbControllers[slot] = false;

                /*
                if (controller.IsVirtual())
                    return;
                */

                // update or create controller
                string path = controller.GetInstancePath();
                Controllers[path] = controller;

                // raise event
                ControllerPlugged?.Invoke(controller);
                ToastManager.SendToast(controller.ToString(), "detected");
            });
        }

        private static void XUsbDeviceRemoved(PnPDetails details, DeviceEventArgs obj)
        {
            if (!Controllers.TryGetValue(details.deviceInstanceId, out IController controller))
                return;

            if (controller.IsConnected())
                return;

            // slot is now free
            UserIndex slot = (UserIndex)controller.GetUserIndex();
            XUsbControllers[slot] = true;

            /*
            if (controller.IsVirtual())
                return;
            */

            // controller was unplugged
            Controllers.Remove(details.deviceInstanceId);

            // raise event
            ControllerUnplugged?.Invoke(controller);
        }

        public static void SetTargetController(string baseContainerDeviceInstancePath)
        {
            // unplug previous controller
            if (targetController is not null)
            {
                targetController.InputsUpdated -= UpdateInputs;
                targetController.MovementsUpdated -= UpdateMovements;
                targetController.Unplug();
            }

            // warn service the current controller has been unplugged
            PipeClient.SendMessage(new PipeClientControllerDisconnect());

            // look for new controller
            if (!Controllers.TryGetValue(baseContainerDeviceInstancePath, out IController controller))
                return;

            if (controller is null)
                return;

            /*
            if (controller.IsVirtual())
                return;
            */

            // update target controller
            targetController = controller;

            targetController.InputsUpdated += UpdateInputs;
            targetController.MovementsUpdated += UpdateMovements;

            targetController.Plug();

            if (SettingsManager.GetBoolean("HIDvibrateonconnect"))
                targetController.Rumble(targetController.GetUserIndex() + 1);

            if (SettingsManager.GetBoolean("HIDcloakonconnect"))
                targetController.Hide();

            // update settings
            SettingsManager.SetProperty("HIDInstancePath", baseContainerDeviceInstancePath);

            // warn service a new controller has arrived
            PipeClient.SendMessage(new PipeClientControllerConnect(targetController.ToString(), targetController.Capacities));

            // check applicable scenarios
            CheckControllerScenario();

            // raise event
            ControllerSelected?.Invoke(targetController);
        }

        private static void OnClientConnected()
        {
            // warn service a new controller has arrived
            if (targetController is null)
                return;

            PipeClient.SendMessage(new PipeClientControllerConnect(targetController.ToString(), targetController.Capacities));
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
            ButtonState InputsState = controllerState.ButtonState.Clone() as ButtonState;

            // pass inputs to Inputs manager
            InputsManager.UpdateReport(InputsState);

            // pass inputs to Overlay Model
            MainWindow.overlayModel.UpdateReport(controllerState);

            // pass inputs to Layout manager
            controllerState = LayoutManager.MapController(controllerState);

            // controller is muted
            if (ControllerMuted)
                return;

            // check if motion trigger is pressed
            Profile currentProfile = ProfileManager.GetCurrent();
            controllerState.MotionTriggered = (currentProfile.MotionMode == MotionMode.Off && InputsState.ContainsTrue(currentProfile.MotionTrigger)) ||
                (currentProfile.MotionMode == MotionMode.On && !InputsState.ContainsTrue(currentProfile.MotionTrigger));

            // pass inputs to service
            PipeClient.SendMessage(new PipeClientInputs(controllerState));
        }

        private static void UpdateMovements(ControllerMovements Movements)
        {
            // pass movements to service
            PipeClient.SendMessage(new PipeClientMovements(Movements));
        }

        internal static IController GetEmulatedController()
        {
            HIDmode HIDmode = (HIDmode)SettingsManager.GetInt("HIDmode", true);
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
    }
}
