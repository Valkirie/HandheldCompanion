using ControllerCommon;
using HandheldCompanion.Views;
using SharpDX.XInput;
using System.Collections.Generic;
using System.Linq;

namespace HandheldCompanion.Managers
{
    public class ControllerManager
    {
        private Dictionary<string, ControllerEx> controllers;
        private List<PnPDeviceEx> devices = new();

        public event ControllerPluggedEventHandler ControllerPlugged;
        public delegate void ControllerPluggedEventHandler(ControllerEx controller);

        public event ControllerUnpluggedEventHandler ControllerUnplugged;
        public delegate void ControllerUnpluggedEventHandler(ControllerEx controller);

        public ControllerManager()
        {
            controllers = new();

            MainWindow.systemManager.XInputArrived += SystemManager_XInputUpdated;
            MainWindow.systemManager.XInputRemoved += SystemManager_XInputUpdated;
        }

        public void StopListen()
        {
            MainWindow.systemManager.XInputArrived -= SystemManager_XInputUpdated;
            MainWindow.systemManager.XInputRemoved -= SystemManager_XInputUpdated;
        }

        public void StartListen()
        {
            lock (devices)
            {
                devices = MainWindow.systemManager.GetDeviceExs();

                // rely on device Last arrival date
                devices = devices.OrderBy(a => a.arrivalDate).ThenBy(a => a.isVirtual).ToList();

                for (int idx = 0; idx < 4; idx++)
                {
                    UserIndex userIndex = (UserIndex)idx;
                    ControllerEx controllerEx = new ControllerEx(userIndex, ref devices);

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
                devices = MainWindow.systemManager.GetDeviceExs();

                // rely on device Last arrival date
                devices = devices.OrderBy(a => a.arrivalDate).ThenBy(a => a.isVirtual).ToList();

                for (int idx = 0; idx < 4; idx++)
                {
                    UserIndex userIndex = (UserIndex)idx;
                    ControllerEx controllerEx = new ControllerEx(userIndex, ref devices);

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
