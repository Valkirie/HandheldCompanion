using System;
using System.Management;

namespace HandheldCompanion
{
    public class BrightnessControl
    {
        private readonly ManagementEventWatcher watcher;
        private readonly ManagementScope scope;

        public event BrightnessChangedHandler BrightnessChanged;
        public delegate void BrightnessChangedHandler(int brightness);

        private readonly bool Supported;

        public BrightnessControl()
        {
            // connecting the scope
            scope = new ManagementScope(@"\\.\root\wmi");
            scope.Connect();

            // creating the watcher
            watcher = new ManagementEventWatcher(scope, new EventQuery("Select * From WmiMonitorBrightnessEvent"));
            watcher.EventArrived += new EventArrivedEventHandler(this.onWMIEvent);
            watcher.Start();

            try
            {
                GetBrightness();
                Supported = true;
            }
            catch (Exception)
            {
                Supported = false;
            }
        }

        private void onWMIEvent(object sender, EventArrivedEventArgs e)
        {
            BrightnessChanged?.Invoke(Convert.ToInt32(e.NewEvent.Properties["Brightness"].Value));
        }

        public void SetBrightness(double brightness)
        {
            if (!Supported)
                return;

            using (var mclass = new ManagementClass("WmiMonitorBrightnessMethods"))
            {
                mclass.Scope = new ManagementScope(@"\\.\root\wmi");
                using (var instances = mclass.GetInstances())
                {
                    foreach (ManagementObject instance in instances)
                    {
                        object[] args = new object[] { 1, brightness };
                        instance.InvokeMethod("WmiSetBrightness", args);
                    }
                }
            }
        }

        public ushort GetBrightness()
        {
            if (!Supported)
                return 100;

            using (var mclass = new ManagementClass("WmiMonitorBrightness"))
            {
                mclass.Scope = new ManagementScope(@"\\.\root\wmi");
                using (var instances = mclass.GetInstances())
                {
                    foreach (ManagementObject instance in instances)
                    {
                        return (byte)instance.GetPropertyValue("CurrentBrightness");
                    }
                }
            }
            return 0;
        }

        internal void Dispose()
        {
            this.watcher?.Dispose();
        }
    }
}
