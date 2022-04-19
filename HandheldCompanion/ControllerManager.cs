using ControllerCommon;
using Microsoft.Extensions.Logging;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace HandheldCompanion
{
    public class ControllerManager
    {
        private ILogger logger;

        private ManagementEventWatcher insertWatcher;
        private ManagementEventWatcher removeWatcher;
        private Timer watcherTimer;

        private Dictionary<UserIndex, ControllerEx> controllers;
        private Dictionary<UserIndex, bool> controllersStatus;

        public event ControllerPluggedEventHandler ControllerPlugged;
        public delegate void ControllerPluggedEventHandler(UserIndex idx, ControllerEx controller);

        public event ControllerUnpluggedEventHandler ControllerUnplugged;
        public delegate void ControllerUnpluggedEventHandler(UserIndex idx, ControllerEx controller);

        public ControllerManager(ILogger logger)
        {
            this.logger = logger;
            this.controllers = new();
            this.controllersStatus = new();

            // initialize timers
            watcherTimer = new Timer(1000) { AutoReset = false };
            watcherTimer.Elapsed += WatcherTimer_Tick;
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

            foreach (UserIndex idx in (UserIndex[])Enum.GetValues(typeof(UserIndex)))
            {
                if (idx == UserIndex.Any)
                    break;

                ControllerEx controllerEx = new ControllerEx(idx);
                controllers[idx] = controllerEx;
                controllersStatus[idx] = controllers[idx].IsConnected();

                if (controllerEx.IsConnected())
                {
                    controllerEx.PullCapabilitiesEx();
                    ControllerPlugged?.Invoke(idx, controllerEx);
                }
            }
        }

        private void WatcherTimer_Tick(object? sender, EventArgs e)
        {
            foreach (UserIndex idx in (UserIndex[])Enum.GetValues(typeof(UserIndex)))
            {
                if (idx == UserIndex.Any)
                    break;

                Controller controller = new Controller(idx);
                ControllerEx controllerEx = controllers[idx];
                bool WasConnected = controllersStatus[idx];

                if (controllerEx.IsConnected())
                {
                    if (WasConnected)
                    {
                        // do nothing
                    }
                    else
                    {
                        // controller was plugged
                        controllerEx.PullCapabilitiesEx();
                        ControllerPlugged?.Invoke(idx, controllerEx);
                    }
                }
                else if (WasConnected)
                {
                    // controller was unplugged
                    ControllerUnplugged?.Invoke(idx, controllerEx);
                }

                // update status
                controllers[idx] = new ControllerEx(idx);
                controllersStatus[idx] = controllers[idx].IsConnected();
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
