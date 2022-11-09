using ControllerCommon;
using ControllerCommon.Controllers;
using ControllerCommon.Managers;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HandheldCompanion.Managers
{
    public static class ControllerManager
    {
        private static Dictionary<string, IController> controllers = new();
        private static IController targetController;

        public static event ControllerPluggedEventHandler ControllerPlugged;
        public delegate void ControllerPluggedEventHandler(IController Controller);

        public static event ControllerUnpluggedEventHandler ControllerUnplugged;
        public delegate void ControllerUnpluggedEventHandler(IController Controller);

        private static bool IsInitialized;

        public static void Start()
        {
            SystemManager.XInputArrived += SystemManager_XInputUpdated;
            SystemManager.XInputRemoved += SystemManager_XInputUpdated;
        }

        public static void Stop()
        {
            if (!IsInitialized)
                return;

            SystemManager.XInputArrived -= SystemManager_XInputUpdated;
            SystemManager.XInputRemoved -= SystemManager_XInputUpdated;
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
            if (targetController != null)
                targetController.Dispose();
            
            // update target controller
            targetController = controllers[baseContainerDeviceInstancePath];
            targetController.Updated += UpdateReport;

            // rumble current controller
            targetController.Rumble();
        }

        private static void UpdateReport(ControllerInput Inputs)
        {
            // pass inputs to InputsManager
            InputsManager.UpdateReport(Inputs.Buttons);

            // is part of a hotkey
            if (Inputs.Buttons.HasFlag(ControllerButtonFlags.Special))
                return;
        }
    }
}
