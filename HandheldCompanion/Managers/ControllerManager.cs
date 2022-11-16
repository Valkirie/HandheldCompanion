using ControllerCommon;
using ControllerCommon.Controllers;
using ControllerCommon.Managers;
using HandheldCompanion.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace HandheldCompanion.Managers
{
    public static class ControllerManager
    {
        private static Dictionary<string, IController> controllers = new();
        private static IController targetController;

        public static Dictionary<ControllerButtonFlags, ControllerButtonFlags> buttonMaps = new();

        public static event ControllerPluggedEventHandler ControllerPlugged;
        public delegate void ControllerPluggedEventHandler(IController Controller);

        public static event ControllerUnpluggedEventHandler ControllerUnplugged;
        public delegate void ControllerUnpluggedEventHandler(IController Controller);

        private static bool IsInitialized;

        public static void Start()
        {
            SystemManager.XInputArrived += SystemManager_XInputUpdated;
            SystemManager.XInputRemoved += SystemManager_XInputUpdated;

            SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

            // cloak on start, if requested
            bool HIDcloaked = SettingsManager.GetBoolean("HIDcloaked");
            HidHide.SetCloaking(HIDcloaked);

            // apply vibration strength
            double HIDstrength = SettingsManager.GetDouble("HIDstrength");
            SetHIDStrength(HIDstrength);

            IsInitialized = true;
        }

        public static void Stop()
        {
            if (!IsInitialized)
                return;

            SystemManager.XInputArrived -= SystemManager_XInputUpdated;
            SystemManager.XInputRemoved -= SystemManager_XInputUpdated;

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

        private static void SystemManager_XInputRemoved(PnPDetails device)
        {
            // todo: implement me
        }

        private static void SystemManager_XInputArrived(PnPDetails device)
        {
            // todo: implement me
        }

        private static void SystemManager_XInputUpdated(PnPDetails device)
        {
            for (int idx = 0; idx < 4; idx++)
            {
                XInputController controller = new XInputController(idx);

                if (!controller.IsConnected())
                    continue;

                if (controller.IsVirtual())
                    continue;

                // update or create controller
                controllers[controller.GetContainerInstancePath()] = controller;

                // raise event
                ControllerPlugged?.Invoke(controller);
            }

            string[] keys = controllers.Keys.ToArray();

            foreach (string key in keys)
            {
                IController controllerEx = controllers[key];
                if (!controllerEx.IsConnected())
                {
                    // controller was unplugged
                    controllers.Remove(key);

                    // raise event
                    ControllerUnplugged?.Invoke(controllerEx);
                }
            }
        }

        public static void SetTargetController(string baseContainerDeviceInstancePath)
        {
            // dispose from previous controller
            ClearTargetController();

            // update target controller
            targetController = controllers[baseContainerDeviceInstancePath];
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

            // todo: pass inputs to (re)mapper
            foreach (var pair in buttonMaps)
            {
                ControllerButtonFlags origin = pair.Key;
                ControllerButtonFlags substitute = pair.Value;

                if (!Inputs.Buttons.HasFlag(origin))
                    continue;

                Inputs.Buttons &= ~origin;
                Inputs.Buttons |= substitute;
            }

            MainWindow.overlayModel.UpdateReport(Inputs);

            // todo: filter inputs if part of shortcut

            // pass inputs to service
            PipeClient.SendMessage(new PipeClientInput(Inputs));
        }
    }
}
