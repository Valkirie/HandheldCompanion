using HandheldCompanion.Misc;
using HandheldCompanion.Utils;
using HandheldCompanion.Views;
using iNKORE.UI.WPF.Modern.Controls;
using System.Management;
using System.Threading.Tasks;

namespace HandheldCompanion.Watchers
{
    public class CoreIsolationWatcher : ISpaceWatcher
    {
        private static WqlEventQuery HypervisorQuery = new WqlEventQuery(@"SELECT * FROM RegistryValueChangeEvent WHERE Hive = 'HKEY_LOCAL_MACHINE' AND KeyPath = 'SYSTEM\\CurrentControlSet\\Control\\DeviceGuard\\Scenarios\\HypervisorEnforcedCodeIntegrity' AND ValueName='Enabled'");
        private static WqlEventQuery VulnerableDriverQuery = new WqlEventQuery(@"SELECT * FROM RegistryValueChangeEvent WHERE Hive = 'HKEY_LOCAL_MACHINE' AND KeyPath = 'SYSTEM\\CurrentControlSet\\Control\\CI\\Config' AND ValueName='VulnerableDriverBlocklistEnable'");

        private ManagementEventWatcher VulnerableDriverWatcher = new ManagementEventWatcher(VulnerableDriverQuery);
        private ManagementEventWatcher HypervisorWatcher = new ManagementEventWatcher(HypervisorQuery);

        public bool HypervisorEnforcedCodeIntegrityEnabled => RegistryUtils.GetBoolean(@"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity", "Enabled");
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
                @"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity",
                "Enabled",
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
            /*
            // Access the event details from NewEvent
            ManagementBaseObject registryEvent = e.NewEvent;

            // Pull the registry hive, key path, and value name.
            string hive = registryEvent["Hive"]?.ToString();
            string keyPath = registryEvent["KeyPath"]?.ToString();
            string valueName = registryEvent["ValueName"]?.ToString();
            */

            // Control Flow Guard settings
            string output = ProcessUtils.ExecutePowerShellScript("Get-ProcessMitigation -System");
            bool controlFlowEnabled = output.Contains("ON");

            // Get status
            bool enabled = VulnerableDriverBlocklistEnable || HypervisorEnforcedCodeIntegrityEnabled || controlFlowEnabled;

            UpdateStatus(enabled);
        }

        public async void SetSettings(bool enabled)
        {
            RegistryUtils.SetValue(@"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity", "Enabled", enabled ? 1 : 0);
            RegistryUtils.SetValue(@"SYSTEM\CurrentControlSet\Control\CI\Config", "VulnerableDriverBlocklistEnable", enabled ? 1 : 0);

            // Control Flow Guard settings
            ProcessUtils.ExecutePowerShellScript($"Set-ProcessMitigation -System {(enabled ? "-Enable" : "-Disable")} CFG");

            Task<ContentDialogResult> dialogTask = new Dialog(MainWindow.GetCurrent())
            {
                Title = Properties.Resources.Dialog_ForceRestartTitle,
                Content = Properties.Resources.Dialog_ForceRestartDesc,
                DefaultButton = ContentDialogButton.Close,
                CloseButtonText = Properties.Resources.Dialog_No,
                PrimaryButtonText = Properties.Resources.Dialog_Yes
            }.ShowAsync();

            await dialogTask; // sync call

            switch (dialogTask.Result)
            {
                case ContentDialogResult.Primary:
                    DeviceUtils.RestartComputer();
                    break;
                case ContentDialogResult.Secondary:
                    break;
            }
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
