﻿using ControllerCommon;
using ControllerCommon.Controllers;
using ControllerCommon.Managers;
using ControllerCommon.Platforms;
using ControllerCommon.Devices;
using HandheldCompanion.Controllers;
using HandheldCompanion.Views;
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

        private static IController? targetController;
        private static ProcessEx? foregroundProcess;

        // temporary, improve me
        public static Dictionary<ControllerButtonFlags, ControllerButtonFlags> buttonMaps = new();

        private static bool IsInitialized;

        public static void Start()
        {
            DeviceManager.XUsbDeviceArrived += XUsbDeviceArrived;
            DeviceManager.XUsbDeviceRemoved += XUsbDeviceRemoved;

            DeviceManager.HidDeviceArrived += HidDeviceArrived;
            DeviceManager.HidDeviceRemoved += HidDeviceRemoved;

            DeviceManager.Initialized += SystemManager_Initialized;

            SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

            ProcessManager.ForegroundChanged += ProcessManager_ForegroundChanged;

            PipeClient.Connected += OnClientConnected;

            // cloak on start, if requested
            bool HIDcloaked = SettingsManager.GetBoolean("HIDcloaked");
            HidHide.SetCloaking(HIDcloaked);

            // apply vibration strength
            double HIDstrength = SettingsManager.GetDouble("HIDstrength");
            SetHIDStrength(HIDstrength);

            IsInitialized = true;
            Initialized?.Invoke();

            LogManager.LogInformation("{0} has started", "ControllerManager");
        }

        private static void ProcessManager_ForegroundChanged(ProcessEx processEx, ProcessEx backgroundEx)
        {
            foregroundProcess = processEx;
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
            bool HIDuncloakonclose = SettingsManager.GetBoolean("HIDuncloakonclose");
            foreach (IController controller in Controllers.Values)
                controller.Unhide();
            // HidHide.SetCloaking(!HIDuncloakonclose);

            LogManager.LogInformation("{0} has stopped", "ControllerManager");
        }

        private static void SettingsManager_SettingValueChanged(string name, object value)
        {
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                switch (name)
                {
                    case "HIDstrength":
                        double HIDstrength = Convert.ToDouble(value);
                        SetHIDStrength(HIDstrength);
                        break;

                    case "SteamDeckLizardMouse":
                    case "SteamDeckLizardButtons":
                        {
                            IController target = GetTargetController();
                            if (target is null)
                                return;

                            if (typeof(NeptuneController) != target.GetType())
                                return;

                            bool LizardMode = Convert.ToBoolean(value);

                            switch (name)
                            {
                                case "SteamDeckLizardMouse":
                                    ((NeptuneController)target).SetLizardMouse(LizardMode);
                                    break;
                                case "SteamDeckLizardButtons":
                                    ((NeptuneController)target).SetLizardButtons(LizardMode);
                                    break;
                            }
                        }
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
                }
            }));
        }

        private static void SystemManager_Initialized()
        {
            // search for last known controller and connect
            string path = SettingsManager.GetString("HIDInstancePath");

            if (Controllers.ContainsKey(path))
            {
                SetTargetController(path);
            }
            else if (Controllers.Count != 0)
            {
                // no known controller, connect to the first available
                path = Controllers.Keys.FirstOrDefault();
                SetTargetController(path);
            }
        }

        private static void SetHIDStrength(double value)
        {
            IController target = GetTargetController();
            if (target is null)
                return;

            target.SetVibrationStrength(value);
        }

        private static void HidDeviceArrived(PnPDetails details)
        {
            DirectInput directInput = new DirectInput();

            // use dispatcher because we're drawing UI elements when initializing the controller object
            Application.Current.Dispatcher.Invoke(new Action(() =>
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
                        joystick = new Joystick(directInput, deviceInstance.InstanceGuid);

                        // IG_ means it is an XInput controller and therefore is handled elsewhere
                        if (joystick.Properties.InterfacePath.Contains("IG_", StringComparison.InvariantCultureIgnoreCase))
                            continue;

                        if (!joystick.Properties.InterfacePath.Equals(details.SymLink, StringComparison.InvariantCultureIgnoreCase))
                            continue;
                    }
                    catch { }
                }

                // search for a supported controller
                switch (details.attributes.VendorID)
                {
                    // SONY
                    case 1356:
                        {
                            switch (details.attributes.ProductID)
                            {
                                // DualShock4
                                case 2508:
                                    controller = new DS4Controller(joystick, details);
                                    break;
                            }
                        }
                        break;

                    // STEAM
                    case 0x28DE:
                        {
                            switch (details.attributes.ProductID)
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
                            switch (details.attributes.ProductID)
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

                if (controller.IsVirtual())
                    return;

                // update or create controller
                string path = controller.GetInstancePath();
                Controllers[path] = controller;

                // raise event
                ControllerPlugged?.Invoke(controller);
            }));
        }

        private static void HidDeviceRemoved(PnPDetails details)
        {
            if (!Controllers.ContainsKey(details.deviceInstanceId))
                return;

            IController controller = Controllers[details.deviceInstanceId];

            if (!controller.IsConnected())
                return;

            if (controller.IsVirtual())
                return;

            // XInput controller are handled elsewhere
            if (controller.GetType() == typeof(XInputController))
                return;

            // controller was unplugged
            Controllers.Remove(details.deviceInstanceId);

            // raise event
            ControllerUnplugged?.Invoke(controller);
        }

        private static void XUsbDeviceArrived(PnPDetails details)
        {
            // trying to guess XInput behavior...
            // get first available slot
            UserIndex slot = UserIndex.One;
            Controller _controller = new(slot);

            for (slot = UserIndex.One; slot <= UserIndex.Three; slot++)
            {
                _controller = new(slot);

                // check if controller is connected and slot free
                if (_controller.IsConnected && XUsbControllers[slot])
                    break;
            }

            // use dispatcher because we're drawing UI elements when initializing the controller object
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                XInputController controller = new(_controller);

                // failed to initialize
                if (controller.Details is null)
                    return;

                if (!controller.IsConnected())
                    return;

                // slot is now busy
                XUsbControllers[slot] = false;

                if (controller.IsVirtual())
                    return;

                // update or create controller
                string path = controller.GetInstancePath();
                Controllers[path] = controller;

                // raise event
                ControllerPlugged?.Invoke(controller);
            }));
        }

        private static void XUsbDeviceRemoved(PnPDetails details)
        {
            if (!Controllers.ContainsKey(details.deviceInstanceId))
                return;

            XInputController controller = (XInputController)Controllers[details.deviceInstanceId];

            if (controller.IsConnected())
                return;

            // slot is now free
            UserIndex slot = (UserIndex)controller.GetUserIndex();
            XUsbControllers[slot] = true;

            if (controller.IsVirtual())
                return;

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
            if (!Controllers.ContainsKey(baseContainerDeviceInstancePath))
                return;

            IController controller = Controllers[baseContainerDeviceInstancePath];
            if (controller is null)
                return;

            // update target controller
            targetController = controller;

            targetController.InputsUpdated += UpdateInputs;
            targetController.MovementsUpdated += UpdateMovements;

            targetController.Plug();
            targetController.Rumble(targetController.GetUserIndex() + 1);

            if (targetController.HideOnHook)
                targetController.Hide();

            // update settings
            SettingsManager.SetProperty("HIDInstancePath", baseContainerDeviceInstancePath);

            // warn service a new controller has arrived
            PipeClient.SendMessage(new PipeClientControllerConnect(targetController.ToString(), targetController.Capacities));
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

        private static void UpdateInputs(ControllerInputs Inputs)
        {
            // pass inputs to InputsManager
            InputsManager.UpdateReport(Inputs.Buttons);
            MainWindow.overlayModel.UpdateReport(Inputs);

            // todo: pass inputs to (re)mapper
            // todo: filter inputs if part of shortcut
            ControllerInputs filtered = new(Inputs);
            foreach (var pair in buttonMaps)
            {
                ControllerButtonFlags origin = pair.Key;
                ControllerButtonFlags substitute = pair.Value;

                if (!filtered.Buttons.HasFlag(origin))
                    continue;

                filtered.Buttons &= ~origin;
                filtered.Buttons |= substitute;
            }

            // Neptune controller specific scenarios
            if (targetController is not null)
                if (targetController.GetType() == typeof(NeptuneController))
                {
                    NeptuneController neptuneController = (NeptuneController)targetController;

                    // mute controller if lizard buttons mode is enabled
                    if (neptuneController.IsLizardButtonsEnabled())
                        return;

                    // mute virtual controller if foreground process is Steam or Steam-related and user a toggle the mute setting
                    if (foregroundProcess is not null)
                    {
                        switch (foregroundProcess.Platform)
                        {
                            case PlatformType.Steam:
                                {
                                    if (neptuneController.IsVirtualMuted())
                                        return;
                                }
                                break;
                        }
                    }
                }

            // pass inputs to service
            PipeClient.SendMessage(new PipeClientInputs(filtered));
        }

        private static void UpdateMovements(ControllerMovements Movements)
        {
            // pass movements to service
            PipeClient.SendMessage(new PipeClientMovements(Movements));
        }
    }
}
