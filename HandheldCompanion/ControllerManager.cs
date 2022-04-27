using ControllerCommon;
using Microsoft.Extensions.Logging;
using Nefarius.Utilities.DeviceManagement.PnP;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Management;
using System.Runtime.InteropServices;
using System.Timers;

namespace HandheldCompanion
{
    public class ControllerManager
    {
        #region import
        [DllImport("hid.dll", EntryPoint = "HidD_GetHidGuid")]
        static internal extern void HidD_GetHidGuidMethod(out Guid hidGuid);
        #endregion

        private ILogger logger;

        private ManagementEventWatcher insertWatcher;
        private ManagementEventWatcher removeWatcher;
        private Timer watcherTimer;

        private Dictionary<UserIndex, ControllerEx> controllers;
        private List<bool> controllersStatus;

        private readonly Guid hidClassInterfaceGuid;
        private List<PnPDevice> devices = new();

        public event ControllerPluggedEventHandler ControllerPlugged;
        public delegate void ControllerPluggedEventHandler(UserIndex idx, ControllerEx controller);

        public event ControllerUnpluggedEventHandler ControllerUnplugged;
        public delegate void ControllerUnpluggedEventHandler(UserIndex idx, ControllerEx controller);

        public ControllerManager(ILogger logger)
        {
            this.logger = logger;
            this.controllers = new();
            this.controllersStatus = new() { false, false, false, false };

            // initialize timers
            watcherTimer = new Timer(1000) { AutoReset = false };
            watcherTimer.Elapsed += WatcherTimer_Tick;

            // initialize hid
            HidD_GetHidGuidMethod(out var interfaceGuid);
            hidClassInterfaceGuid = interfaceGuid;
        }

        public ControllerEx ElementAt(UserIndex idx)
        {
            return this.controllers[idx];
        }

        public void Start()
        {
            insertWatcher = new ManagementEventWatcher(new WqlEventQuery("SELECT * FROM Win32_DeviceChangeEvent WHERE EventType = 2"));
            insertWatcher.EventArrived += new EventArrivedEventHandler(DeviceEvent);
            insertWatcher.Start();

            removeWatcher = new ManagementEventWatcher(new WqlEventQuery("SELECT * FROM Win32_DeviceChangeEvent WHERE EventType = 3"));
            removeWatcher.EventArrived += new EventArrivedEventHandler(DeviceEvent);
            removeWatcher.Start();

            WatcherTimer_Tick(null, null);
        }

        private void WatcherTimer_Tick(object? sender, EventArgs e)
        {
            // refresh devices
            int deviceIndex = 0;
            devices.Clear();

            while (Devcon.Find(hidClassInterfaceGuid, out var path, out var instanceId, deviceIndex++))
                devices.Add(PnPDevice.GetDeviceByInterfaceId(path));

            for (int idx = 0; idx < 4; idx++)
            {
                UserIndex userIndex = (UserIndex)idx;
                ControllerEx controllerEx = new ControllerEx(userIndex, null, ref devices);

                bool isConnected = controllerEx.IsConnected();
                bool wasConnected = controllersStatus[idx];

                // update status
                controllers[userIndex] = controllerEx;
                controllersStatus[idx] = controllers[userIndex].IsConnected();

                if (controllerEx.isVirtual)
                    return;

                if (isConnected)
                {
                    if (!wasConnected)
                    {
                        // controller was plugged
                        ControllerPlugged?.Invoke(userIndex, controllerEx);
                    }
                }
                else if (wasConnected)
                {
                    // controller was unplugged
                    ControllerUnplugged?.Invoke(userIndex, controllerEx);
                }
            }
        }

        private void DeviceEvent(object sender, EventArrivedEventArgs e)
        {
            watcherTimer.Stop();
            watcherTimer.Start();
        }

        public void Stop()
        {
            insertWatcher.Stop();
            removeWatcher.Stop();
            watcherTimer.Stop();
        }
    }
}
