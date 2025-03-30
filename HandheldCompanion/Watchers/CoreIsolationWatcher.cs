using HandheldCompanion.Utils;
using iNKORE.UI.WPF.Modern.Controls;
using System.Management;

namespace HandheldCompanion.Watchers
{
    public class CoreIsolationWatcher : ISpaceWatcher
    {
        private static WqlEventQuery HypervisorQuery = new WqlEventQuery(@"SELECT * FROM RegistryValueChangeEvent WHERE Hive = 'HKEY_LOCAL_MACHINE' AND KeyPath = 'SYSTEM\\CurrentControlSet\\Control\\DeviceGuard\\Scenarios' AND ValueName='HypervisorEnforcedCodeIntegrity'");
        private static WqlEventQuery VulnerableDriverQuery = new WqlEventQuery(@"SELECT * FROM RegistryValueChangeEvent WHERE Hive = 'HKEY_LOCAL_MACHINE' AND KeyPath = 'SYSTEM\\CurrentControlSet\\Control\\CI\\Config' AND ValueName='VulnerableDriverBlocklistEnable'");

        private ManagementEventWatcher VulnerableDriverWatcher = new ManagementEventWatcher(VulnerableDriverQuery);
        private ManagementEventWatcher HypervisorWatcher = new ManagementEventWatcher(HypervisorQuery);

        public bool HypervisorEnforcedCodeIntegrityEnabled => RegistryUtils.GetBoolean(@"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios", "HypervisorEnforcedCodeIntegrity");
        public bool VulnerableDriverBlocklistEnable => RegistryUtils.GetBoolean(@"SYSTEM\CurrentControlSet\Control\CI\Config", "VulnerableDriverBlocklistEnable");

        public CoreIsolationWatcher()
        {
            // set notification
            notification = new(
                Properties.Resources.Hint_CoreIsolationCheck,
                Properties.Resources.Hint_CoreIsolationCheckDesc,
                string.Empty,
                InfoBarSeverity.Warning);
        }

        public override void Start()
        {
            // Ensure registry keys exist and set up watchers.
            SetupRegistryWatcher(
                @"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios",
                "HypervisorEnforcedCodeIntegrity",
                HypervisorWatcher,
                HypervisorQuery);

            SetupRegistryWatcher(
                @"SYSTEM\CurrentControlSet\Control\CI\Config",
                "VulnerableDriverBlocklistEnable",
                VulnerableDriverWatcher,
                VulnerableDriverQuery);

            UpdateStatus(HypervisorEnforcedCodeIntegrityEnabled || VulnerableDriverBlocklistEnable);

            base.Start();
        }

        public override void Stop()
        {
            HypervisorWatcher.Stop();
            VulnerableDriverWatcher.Stop();

            base.Stop();
        }

        private void HandleEvent(object sender, EventArrivedEventArgs e)
        {
            // Access the event details from NewEvent
            ManagementBaseObject registryEvent = e.NewEvent;

            // Pull the registry hive, key path, and value name.
            string hive = registryEvent["Hive"]?.ToString();
            string keyPath = registryEvent["KeyPath"]?.ToString();
            string valueName = registryEvent["ValueName"]?.ToString();

            bool value = false;
            switch (valueName)
            {
                case "VulnerableDriverBlocklistEnable":
                    value = VulnerableDriverBlocklistEnable;
                    break;
                case "HypervisorEnforcedCodeIntegrityEnabled":
                    value = HypervisorEnforcedCodeIntegrityEnabled;
                    break;
            }

            UpdateStatus(value);
        }

        public void SetSettings(bool enabled)
        {
            RegistryUtils.SetValue(@"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios", "HypervisorEnforcedCodeIntegrity", enabled ? 1 : 0);
            RegistryUtils.SetValue(@"SYSTEM\CurrentControlSet\Control\CI\Config", "VulnerableDriverBlocklistEnable", enabled ? 1 : 0);
        }

        private void SetupRegistryWatcher(string regPath, string valueName, ManagementEventWatcher watcher, WqlEventQuery query)
        {
            if (!RegistryUtils.KeyExists(regPath, valueName))
                RegistryUtils.CreateKey(regPath);

            watcher.EventArrived += new EventArrivedEventHandler(HandleEvent);
            watcher.Start();
        }
    }
}
