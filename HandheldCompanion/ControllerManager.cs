using ControllerCommon;
using Microsoft.Extensions.Logging;
using Nefarius.Utilities.DeviceManagement.PnP;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;

namespace HandheldCompanion
{
    public class ControllerManager
    {
        private ILogger logger;

        private SystemManager systemManager;

        private Dictionary<string, ControllerEx> controllers;
        private List<PnPDeviceEx> devices = new();

        public event ControllerPluggedEventHandler ControllerPlugged;
        public delegate void ControllerPluggedEventHandler(ControllerEx controller);

        public event ControllerUnpluggedEventHandler ControllerUnplugged;
        public delegate void ControllerUnpluggedEventHandler(ControllerEx controller);

        public ControllerManager(ILogger logger)
        {
            this.logger = logger;
            this.controllers = new();

            // initialize manager(s)
            systemManager = new SystemManager();
        }

        public void StartListen()
        {
            systemManager.XInputArrived += SystemManager_XInputUpdated;
            systemManager.XInputRemoved += SystemManager_XInputUpdated;

            lock (devices)
            {
                devices = systemManager.GetDeviceExs();

                // rely on device Last arrival date
                devices = devices.OrderBy(a => a.arrivalDate).ThenBy(a => a.isVirtual).ToList();

                for (int idx = 0; idx < 4; idx++)
                {
                    UserIndex userIndex = (UserIndex)idx;
                    ControllerEx controllerEx = new ControllerEx(userIndex, null, ref devices);

                    controllers[controllerEx.baseContainerDeviceInstancePath] = controllerEx;

                    if (controllerEx.isVirtual)
                        continue;

                    if (!controllerEx.IsConnected())
                        continue;

                    // raise event
                    ControllerPlugged?.Invoke(controllerEx);
                }
            }
        }

        private void SystemManager_XInputRemoved(PnPDeviceEx device)
        {
            // todo: implement me
        }

        private void SystemManager_XInputArrived(PnPDeviceEx device)
        {
            // todo: implement me
        }

        private void SystemManager_XInputUpdated(PnPDeviceEx device)
        {
            lock (devices)
            {
                devices = systemManager.GetDeviceExs();

                // rely on device Last arrival date
                devices = devices.OrderBy(a => a.arrivalDate).ThenBy(a => a.isVirtual).ToList();

                for (int idx = 0; idx < 4; idx++)
                {
                    UserIndex userIndex = (UserIndex)idx;
                    ControllerEx controllerEx = new ControllerEx(userIndex, null, ref devices);

                    controllers[controllerEx.baseContainerDeviceInstancePath] = controllerEx;

                    if (controllerEx.isVirtual)
                        continue;

                    if (!controllerEx.IsConnected())
                        continue;

                    // raise event
                    ControllerPlugged?.Invoke(controllerEx);
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
