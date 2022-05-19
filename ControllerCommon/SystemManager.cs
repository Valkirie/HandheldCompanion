using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Timers;
using System.Diagnostics;

namespace ControllerCommon
{
    public enum DeviceChangeEnum
    {
        ConfigurationChanged = 1,
        DeviceArrived = 2,
        DeviceRemoved = 3,
        Docked = 4
    }

    public class SystemManager
    {
        private ILogger logger;

        private ManagementEventWatcher eventWatcher;
        private Timer eventTimer;

        // event handler(s)
        private Timer ConfigurationChangedTimer;
        public event ConfigurationChangedEventHandler ConfigurationChanged;
        public delegate void ConfigurationChangedEventHandler();

        private Timer DeviceArrivedTimer;
        public event DeviceArrivedEventHandler DeviceArrived;
        public delegate void DeviceArrivedEventHandler();

        private Timer DeviceRemovedTimer;
        public event DeviceRemovedEventHandler DeviceRemoved;
        public delegate void DeviceRemovedEventHandler();

        private Timer DockedTimer;
        public event DockedEventHandler Docked;
        public delegate void DockedEventHandler();

        public SystemManager()
        {
            this.logger = logger;

            eventWatcher = new ManagementEventWatcher(new WqlEventQuery("SELECT * FROM Win32_DeviceChangeEvent"));
            eventWatcher.EventArrived += new EventArrivedEventHandler( HandleEvent);
            eventWatcher.Start();

            ConfigurationChangedTimer = new Timer() { Interval = 500, AutoReset = false };
            ConfigurationChangedTimer.Elapsed += (object sender, ElapsedEventArgs e) =>
            {
                ConfigurationChanged?.Invoke();
            };

            DeviceArrivedTimer = new Timer() { Interval = 500, AutoReset = false };
            DeviceArrivedTimer.Elapsed += (object sender, ElapsedEventArgs e) =>
            {
                DeviceArrived?.Invoke();
            };

            DeviceRemovedTimer = new Timer() { Interval = 500, AutoReset = false };
            DeviceRemovedTimer.Elapsed += (object sender, ElapsedEventArgs e) =>
            {
                DeviceRemoved?.Invoke();
            };

            DockedTimer = new Timer() { Interval = 500, AutoReset = false };
            DockedTimer.Elapsed += (object sender, ElapsedEventArgs e) =>
            {
                Docked?.Invoke();
            };
        }

        private void HandleEvent(object sender, EventArrivedEventArgs e)
        {
            int EventType = Convert.ToInt32(e.NewEvent.GetPropertyValue("EventType"));
            DeviceChangeEnum value = (DeviceChangeEnum)EventType;

            // we use timers because events are triggered in chains
            switch (value)
            {
                case DeviceChangeEnum.ConfigurationChanged:
                    ConfigurationChangedTimer.Stop();
                    ConfigurationChangedTimer.Start();
                    break;
                case DeviceChangeEnum.DeviceArrived:
                    DeviceArrivedTimer.Stop();
                    DeviceArrivedTimer.Start();
                    break;
                case DeviceChangeEnum.DeviceRemoved:
                    DeviceRemovedTimer.Stop();
                    DeviceRemovedTimer.Start();
                    break;
                case DeviceChangeEnum.Docked:
                    DockedTimer.Stop();
                    DockedTimer.Start();
                    break;
            }
        }
    }
}
