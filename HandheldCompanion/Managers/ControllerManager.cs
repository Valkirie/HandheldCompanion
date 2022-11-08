using ControllerCommon;
using ControllerCommon.Controllers;
using ControllerCommon.Managers;
using SharpDX.XInput;
using System.Collections.Generic;
using System.Linq;

namespace HandheldCompanion.Managers
{
    public class ControllerManager : Manager
    {
        private Dictionary<string, ControllerEx> controllers;
        private List<PnPDetails> devices = new();

        public event ControllerPluggedEventHandler ControllerPlugged;
        public delegate void ControllerPluggedEventHandler(ControllerEx controller);

        public event ControllerUnpluggedEventHandler ControllerUnplugged;
        public delegate void ControllerUnpluggedEventHandler(ControllerEx controller);

        public ControllerManager() : base()
        {
            controllers = new();
        }

        public override void Start()
        {
            SystemManager.XInputArrived += SystemManager_XInputUpdated;
            SystemManager.XInputRemoved += SystemManager_XInputUpdated;

            base.Start();
        }

        public override void Stop()
        {
            if (!IsInitialized)
                return;

            SystemManager.XInputArrived -= SystemManager_XInputUpdated;
            SystemManager.XInputRemoved -= SystemManager_XInputUpdated;

            base.Stop();
        }

        private void SystemManager_XInputRemoved(PnPDetails device)
        {
            // todo: implement me
        }

        private void SystemManager_XInputArrived(PnPDetails device)
        {
            // todo: implement me
        }

        private void SystemManager_XInputUpdated(PnPDetails device)
        {
            lock (devices)
            {
                devices = SystemManager.GetDeviceExs();

                // rely on device Last arrival date
                devices = devices.OrderBy(a => a.arrivalDate).ThenBy(a => a.isVirtual).ToList();

                for (int idx = 0; idx < 4; idx++)
                {
                    XInputController test = new XInputController(idx);

                    /*
                    UserIndex userIndex = (UserIndex)idx;
                    ControllerEx controllerEx = new ControllerEx(userIndex, ref devices);

                    controllers[controllerEx.baseContainerDeviceInstancePath] = controllerEx;

                    if (controllerEx.isVirtual)
                        controllerEx.DeviceDesc += " (Virtual)";

                    if (!controllerEx.IsConnected())
                        continue;

                    // raise event
                    ControllerPlugged?.Invoke(controllerEx);
                    */
                }

                string[] keys = controllers.Keys.ToArray();

                foreach (string key in keys)
                {
                    ControllerEx controllerEx = controllers[key];
                    if (!controllerEx.IsConnected())
                    {
                        // controller was unplugged
                        controllers.Remove(key);

                        // raise event
                        ControllerUnplugged?.Invoke(controllerEx);
                    }
                }
            }
        }
    }
}
