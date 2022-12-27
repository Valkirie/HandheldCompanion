using ControllerCommon;
using ControllerCommon.Controllers;
using ControllerCommon.Managers;
using HandheldCompanion.Controllers;
using HandheldCompanion.Views;
using SharpDX.DirectInput;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using DeviceType = SharpDX.DirectInput.DeviceType;

namespace HandheldCompanion.Managers
{
    public static class ControllerManager
    {
        private static Dictionary<string, IController> Controllers = new();
        private static IController? targetController;

        private static DirectInput directInput = new DirectInput();

        // temporary, improve me
        public static Dictionary<ControllerButtonFlags, ControllerButtonFlags> buttonMaps = new();

        public static event ControllerPluggedEventHandler ControllerPlugged;
        public delegate void ControllerPluggedEventHandler(IController Controller);

        public static event ControllerUnpluggedEventHandler ControllerUnplugged;
        public delegate void ControllerUnpluggedEventHandler(IController Controller);

        public static event InitializedEventHandler Initialized;
        public delegate void InitializedEventHandler();

        private static bool IsInitialized;

        public static void Start()
        {
            SystemManager.XUsbDeviceArrived += XUsbDeviceUpdated;
            SystemManager.XUsbDeviceRemoved += XUsbDeviceUpdated;

            SystemManager.HidDeviceArrived += HidDeviceUpdated;
            SystemManager.HidDeviceRemoved += HidDeviceUpdated;

            SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

            // cloak on start, if requested
            bool HIDcloaked = SettingsManager.GetBoolean("HIDcloaked");
            HidHide.SetCloaking(HIDcloaked);

            // apply vibration strength
            double HIDstrength = SettingsManager.GetDouble("HIDstrength");
            SetHIDStrength(HIDstrength);

            IsInitialized = true;
            Initialized?.Invoke();
        }

        public static void Stop()
        {
            if (!IsInitialized)
                return;

            IsInitialized = false;

            SystemManager.XUsbDeviceArrived -= XUsbDeviceUpdated;
            SystemManager.XUsbDeviceRemoved -= XUsbDeviceUpdated;

            SettingsManager.SettingValueChanged -= SettingsManager_SettingValueChanged;

            // uncloak on close, if requested
            bool HIDuncloakonclose = SettingsManager.GetBoolean("HIDuncloakonclose");
            HidHide.SetCloaking(!HIDuncloakonclose);
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
                }
            }));
        }

        private static void SetHIDStrength(double value)
        {
            IController target = GetTargetController();
            if (target is null)
                return;

            target.SetVibrationStrength(value);
        }

        private static void HidDeviceUpdated(PnPDetails details)
        {
            Joystick joystick = null;
            foreach (var deviceInstance in directInput.GetDevices(DeviceType.Gamepad, DeviceEnumerationFlags.AllDevices))
            {
                try
                {
                    // Instantiate the joystick
                    joystick = new Joystick(directInput, deviceInstance.InstanceGuid);

                    if (!joystick.Properties.InterfacePath.Equals(details.SymLink, StringComparison.InvariantCultureIgnoreCase))
                        continue;

                    // is an XInput controller, handled elsewhere
                    if (joystick.Properties.InterfacePath.Contains("IG_", StringComparison.InvariantCultureIgnoreCase))
                        continue;
                }
                catch { }
            }

            IController controller = null;
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

            if (controller.IsVirtual())
                return;

            if (controller.IsConnected())
            {
                // update or create controller
                string path = controller.GetInstancePath();
                Controllers[path] = controller;

                // raise event
                ControllerPlugged?.Invoke(controller);
            }

            // search for unplugged controllers
            string[] keys = Controllers.Keys.ToArray();
            foreach (string key in keys)
            {
                controller = Controllers[key];
                if (!controller.IsConnected())
                {
                    // controller was unplugged
                    Controllers.Remove(key);

                    // raise event
                    ControllerUnplugged?.Invoke(controller);
                }
            }
        }

        private static void XUsbDeviceUpdated(PnPDetails details)
        {
            for (int idx = 0; idx < 4; idx++)
            {
                XInputController controller = new(idx);

                if (!controller.IsConnected())
                    continue;

                if (controller.IsVirtual())
                    continue;

                // update or create controller
                string path = controller.GetInstancePath();
                Controllers[path] = controller;

                // raise event
                ControllerPlugged?.Invoke(controller);
            }

            // search for unplugged controllers
            string[] keys = Controllers.Keys.ToArray();
            foreach (string key in keys)
            {
                IController controller = Controllers[key];
                if (!controller.IsConnected())
                {
                    // controller was unplugged
                    Controllers.Remove(key);

                    // raise event
                    ControllerUnplugged?.Invoke(controller);
                }
            }
        }

        public static void SetTargetController(string baseContainerDeviceInstancePath)
        {
            // dispose from previous controller
            ClearTargetController();

            // update target controller
            targetController = Controllers[baseContainerDeviceInstancePath];
            targetController.Updated += UpdateReport;

            targetController.Plug();
            targetController.Hide();

            // rumble current controller
            targetController.Rumble(targetController.GetUserIndex() + 1);

            // warn service
            SendTargetController();
        }

        public static void ClearTargetController()
        {
            if (targetController is null)
                return;

            targetController.Unplug();
            targetController.Unhide();

            targetController = null;

            // warn service
            SendTargetController();
        }

        public static void SendTargetController()
        {
            if (targetController is null)
                PipeClient.SendMessage(new PipeClientControllerDisconnect());
            else
                PipeClient.SendMessage(new PipeClientControllerConnect(targetController.ToString(), targetController.Capacities));
        }

        public static IController GetTargetController()
        {
            return targetController;
        }

        private static void UpdateReport(ControllerInput Inputs)
        {
            // pass inputs to InputsManager
            InputsManager.UpdateReport(Inputs.Buttons);
            MainWindow.overlayModel.UpdateReport(Inputs);

            // todo: pass inputs to (re)mapper
            // todo: filter inputs if part of shortcut
            ControllerInput filtered = new(Inputs);
            foreach (var pair in buttonMaps)
            {
                ControllerButtonFlags origin = pair.Key;
                ControllerButtonFlags substitute = pair.Value;

                if (!filtered.Buttons.HasFlag(origin))
                    continue;

                filtered.Buttons &= ~origin;
                filtered.Buttons |= substitute;
            }

            // pass inputs to service
            PipeClient.SendMessage(new PipeClientInput(filtered));
        }
    }
}
