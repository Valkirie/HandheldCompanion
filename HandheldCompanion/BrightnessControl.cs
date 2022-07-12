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

        public BrightnessControl()
        {
            // connecting the scope
            scope = new ManagementScope(@"\\.\root\wmi");
            scope.Connect();

            // creating the watcher
            watcher = new ManagementEventWatcher(scope, new EventQuery("Select * From WmiMonitorBrightnessEvent"));
            watcher.EventArrived += new EventArrivedEventHandler(this.onWMIEvent);
            watcher.Start();
        }

        private void onWMIEvent(object sender, EventArrivedEventArgs e)
        {
            BrightnessChanged?.Invoke(Convert.ToInt32(e.NewEvent.Properties["Brightness"].Value));
        }

        public void SetBrightness(double brightness)
        {
            ManagementClass WmiMonitorBrightnessMethods = new ManagementClass("root/wmi", "WmiMonitorBrightnessMethods", null);

            foreach (ManagementObject mo in WmiMonitorBrightnessMethods.GetInstances())
            {
                ManagementBaseObject inParams = mo.GetMethodParameters("WmiSetBrightness");
                inParams["Brightness"] = brightness;
                inParams["Timeout"] = 5;
                mo.InvokeMethod("WmiSetBrightness", inParams, null);
            }
        }

        public int GetBrightness()
        {
            ManagementScope s = new ManagementScope("root\\WMI");
            SelectQuery q = new SelectQuery("WmiMonitorBrightness");
            ManagementObjectSearcher mos = new ManagementObjectSearcher(s, q);
            ManagementObjectCollection moc = mos.Get();

            //store result
            byte curBrightness = 0;
            foreach (ManagementObject o in moc)
            {
                curBrightness = (byte)o.GetPropertyValue("CurrentBrightness");
                break; //only work on the first object
            }

            moc.Dispose();
            mos.Dispose();

            return curBrightness;
        }

        internal void Dispose()
        {
            this.watcher?.Dispose();
        }
    }
}
