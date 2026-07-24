using HandheldCompanion.Misc;
using HandheldCompanion.Utils;
using iNKORE.UI.WPF.Modern.Controls;
using System.Management;
using System.Threading.Tasks;
using System.Windows;

namespace HandheldCompanion.Watchers
{
    public class CoreIsolationWatcher : ISpaceWatcher
    {
        private static WqlEventQuery HypervisorQuery = new WqlEventQuery(@"SELECT * FROM RegistryValueChangeEvent WHERE Hive = 'HKEY_LOCAL_MACHINE' AND KeyPath = 'SYSTEM\\CurrentControlSet\\Control\\DeviceGuard\\Scenarios\\HypervisorEnforcedCodeIntegrity' AND ValueName='Enabled'");
        private static WqlEventQuery VulnerableDriverQuery = new WqlEventQuery(@"SELECT * FROM RegistryValueChangeEvent WHERE Hive = 'HKEY_LOCAL_MACHINE' AND KeyPath = 'SYSTEM\\CurrentControlSet\\Control\\CI\\Config' AND ValueName='VulnerableDriverBlocklistEnable'");
        private static WqlEventQuery SmartAppControlQuery = new WqlEventQuery(@"SELECT * FROM RegistryValueChangeEvent WHERE Hive = 'HKEY_LOCAL_MACHINE' AND KeyPath = 'SYSTEM\\CurrentControlSet\\Control\\CI\\Policy' AND ValueName='VerifiedAndReputablePolicyState'");

        private ManagementEventWatcher VulnerableDriverWatcher = new ManagementEventWatcher(VulnerableDriverQuery);
        private ManagementEventWatcher HypervisorWatcher = new ManagementEventWatcher(HypervisorQuery);
        private ManagementEventWatcher SmartAppControlWatcher = new ManagementEventWatcher(SmartAppControlQuery);

        public bool HypervisorEnforcedCodeIntegrityEnabled => RegistryUtils.GetBoolean(@"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity", "Enabled");
        public bool VulnerableDriverBlocklistEnable => RegistryUtils.GetBoolean(@"SYSTEM\CurrentControlSet\Control\CI\Config", "VulnerableDriverBlocklistEnable");
        // VerifiedAndReputablePolicyState: 0 = Off, 1 = Evaluation, 2 = On
        public bool SmartAppControlEnabled => RegistryUtils.GetInt(@"SYSTEM\CurrentControlSet\Control\CI\Policy", "VerifiedAndReputablePolicyState") != 0;

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

            SetupRegistryWatcher(
                @"SYSTEM\CurrentControlSet\Control\CI\Policy",
                "VerifiedAndReputablePolicyState",
                SmartAppControlWatcher,
                SmartAppControlQuery);

            UpdateStatus(HypervisorEnforcedCodeIntegrityEnabled || VulnerableDriverBlocklistEnable || SmartAppControlEnabled);

            base.Start();
        }

        public override void Stop()
        {
            HypervisorWatcher.Stop();
            VulnerableDriverWatcher.Stop();
            SmartAppControlWatcher.Stop();

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
            bool enabled = VulnerableDriverBlocklistEnable || HypervisorEnforcedCodeIntegrityEnabled || controlFlowEnabled || SmartAppControlEnabled;

            UpdateStatus(enabled);
        }

        public async void SetSettings(bool enabled, Window? owner = null)
        {
            RegistryUtils.SetValue(@"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity", "Enabled", enabled ? 1 : 0);
            RegistryUtils.SetValue(@"SYSTEM\CurrentControlSet\Control\CI\Config", "VulnerableDriverBlocklistEnable", enabled ? 1 : 0);
            // VerifiedAndReputablePolicyState: 0 = Off, 2 = On (1 = Evaluation is treated as enabled)
            RegistryUtils.SetValue(@"SYSTEM\CurrentControlSet\Control\CI\Policy", "VerifiedAndReputablePolicyState", enabled ? 2 : 0);

            // Control Flow Guard settings
            ProcessUtils.ExecutePowerShellScript($"Set-ProcessMitigation -System {(enabled ? "-Enable" : "-Disable")} CFG");

            // Skip the restart dialog when no owner window is available (e.g. welcome flow manages restart itself).
            if (owner is null)
                return;

            Task<ContentDialogResult> dialogTask = new Dialog(owner)
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

            watcher.EventArrived -= HandleEvent;
            watcher.EventArrived += HandleEvent;
            watcher.Start();
        }

        public override void Dispose()
        {
            HypervisorWatcher.EventArrived -= HandleEvent;
            VulnerableDriverWatcher.EventArrived -= HandleEvent;
            SmartAppControlWatcher.EventArrived -= HandleEvent;
            base.Dispose(); // calls Stop() which stops all three ManagementEventWatchers
            HypervisorWatcher.Dispose();
            VulnerableDriverWatcher.Dispose();
            SmartAppControlWatcher.Dispose();
        }
    }
}
