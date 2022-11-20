﻿using ControllerCommon;
using ControllerCommon.Controllers;
using ControllerCommon.Managers;
using HandheldCompanion.Views;
using SharpDX.DirectInput;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace HandheldCompanion.Managers
{
    public static class ControllerManager
    {
        private static Dictionary<string, IController> Controllers = new();
        private static IController targetController;

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
            SystemManager.XInputDeviceArrived += XInputUpdated;
            SystemManager.XInputDeviceRemoved += XInputUpdated;

            SystemManager.DInputDeviceArrived += DInputUpdated;
            SystemManager.DInputDeviceRemoved += DInputUpdated;

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

            SystemManager.XInputDeviceArrived -= XInputUpdated;
            SystemManager.XInputDeviceRemoved -= XInputUpdated;

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

        private static void DInputUpdated(PnPDetails details)
        {
            foreach (var deviceInstance in directInput.GetDevices(DeviceType.Gamepad, DeviceEnumerationFlags.AllDevices))
            {
                // Instantiate the joystick
                var joystick = new Joystick(directInput, deviceInstance.InstanceGuid);

                if (!joystick.Properties.InterfacePath.Equals(details.SymLink, StringComparison.InvariantCultureIgnoreCase))
                    continue;

                // is an XInput controller, handled elsewhere
                if (joystick.Properties.InterfacePath.Contains("IG_", StringComparison.InvariantCultureIgnoreCase))
                    continue;

                IController controller = null;
                switch (joystick.Properties.VendorId)
                {
                    // SONY
                    case 1356:
                        {
                            switch (joystick.Properties.ProductId)
                            {
                                // DualShock4
                                case 2508:
                                    controller = new DS4Controller(joystick, details);
                                    break;
                            }
                        }
                        break;
                }

                // unsupported DInput controller
                if (controller is null)
                    continue;

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

        private static void XInputUpdated(PnPDetails details)
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
            targetController.Rumble();
        }

        public static void ClearTargetController()
        {
            if (targetController is null)
                return;

            targetController.Unplug();
            targetController.Unhide();

            targetController = null;
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
            ControllerInput filtered = new()
            {
                Buttons = Inputs.Buttons,
                Timestamp = Inputs.Timestamp,
                LeftThumbX = Inputs.LeftThumbX,
                LeftThumbY = Inputs.LeftThumbY,
                RightThumbX = Inputs.RightThumbX,
                RightThumbY = Inputs.RightThumbY,
                RightTrigger = Inputs.RightTrigger,
                LeftTrigger = Inputs.LeftTrigger,
            };

            foreach (var pair in buttonMaps)
            {
                ControllerButtonFlags origin = pair.Key;
                ControllerButtonFlags substitute = pair.Value;

                if (!filtered.Buttons.HasFlag(origin))
                    continue;

                filtered.Buttons &= ~origin;
                filtered.Buttons |= substitute;
            }

            // todo: filter inputs if part of shortcut

            // pass inputs to service
            PipeClient.SendMessage(new PipeClientInput(filtered));
        }
    }
}
