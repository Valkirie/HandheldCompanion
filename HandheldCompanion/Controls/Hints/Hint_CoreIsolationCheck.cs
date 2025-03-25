using HandheldCompanion.Helpers;
using HandheldCompanion.Misc;
using HandheldCompanion.Utils;
using HandheldCompanion.Views;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Diagnostics;
using System.Management;
using System.Threading.Tasks;
using System.Windows;

namespace HandheldCompanion.Controls.Hints
{
    public class Hint_CoreIsolationCheck : IHint
    {
        private static WqlEventQuery HypervisorQuery = new WqlEventQuery(@"SELECT * FROM RegistryValueChangeEvent WHERE Hive = 'HKEY_LOCAL_MACHINE' AND KeyPath = 'SYSTEM\\CurrentControlSet\\Control\\DeviceGuard\\Scenarios' AND ValueName='HypervisorEnforcedCodeIntegrity'");
        private static WqlEventQuery VulnerableDriverQuery = new WqlEventQuery(@"SELECT * FROM RegistryValueChangeEvent WHERE Hive = 'HKEY_LOCAL_MACHINE' AND KeyPath = 'SYSTEM\\CurrentControlSet\\Control\\CI\\Config' AND ValueName='VulnerableDriverBlocklistEnable'");

        private ManagementEventWatcher VulnerableDriverWatcher = new ManagementEventWatcher(VulnerableDriverQuery);
        private ManagementEventWatcher HypervisorWatcher = new ManagementEventWatcher(HypervisorQuery);

        bool HypervisorEnforcedCodeIntegrityEnabled = true;
        bool VulnerableDriverBlocklistEnable = true;

        public Hint_CoreIsolationCheck() : base()
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

            // default state
            this.HintActionButton.Visibility = Visibility.Visible;

            this.HintTitle.Text = Properties.Resources.Hint_CoreIsolationCheck;
            this.HintDescription.Text = Properties.Resources.Hint_CoreIsolationCheckDesc;
            this.HintReadMe.Text = Properties.Resources.Hint_CoreIsolationCheckReadme;

            this.HintActionButton.Content = Properties.Resources.Hint_CoreIsolationCheckAction;

            CheckSettings();
        }

        private void SetupRegistryWatcher(string regPath, string valueName, ManagementEventWatcher watcher, WqlEventQuery query)
        {
            if (!RegistryUtils.KeyExists(regPath, valueName))
                RegistryUtils.CreateKey(regPath);

            watcher.EventArrived += new EventArrivedEventHandler(HandleEvent);
            watcher.Start();
        }

        private void HandleEvent(object sender, EventArrivedEventArgs e)
        {
            CheckSettings();
        }

        private void CheckSettings()
        {
            // read OS specific values
            HypervisorEnforcedCodeIntegrityEnabled = RegistryUtils.GetBoolean(@"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios", "HypervisorEnforcedCodeIntegrity");
            VulnerableDriverBlocklistEnable = RegistryUtils.GetBoolean(@"SYSTEM\CurrentControlSet\Control\CI\Config", "VulnerableDriverBlocklistEnable");

            // UI thread
            UIHelper.TryInvoke(() =>
            {
                this.Visibility = (HypervisorEnforcedCodeIntegrityEnabled || VulnerableDriverBlocklistEnable) ? Visibility.Visible : Visibility.Collapsed;
            });
        }

        protected override async void HintActionButton_Click(object sender, RoutedEventArgs e)
        {
            // UI thread
            UIHelper.TryInvoke(() =>
            {
                this.IsEnabled = false;
            });

            RegistryUtils.SetValue(@"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios", "HypervisorEnforcedCodeIntegrity", 0);
            RegistryUtils.SetValue(@"SYSTEM\CurrentControlSet\Control\CI\Config", "VulnerableDriverBlocklistEnable", 0);

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
                    using (Process shutdown = new())
                    {
                        shutdown.StartInfo.FileName = "shutdown.exe";
                        shutdown.StartInfo.Arguments = "-r -t 3";

                        shutdown.StartInfo.UseShellExecute = false;
                        shutdown.StartInfo.CreateNoWindow = true;
                        shutdown.Start();
                    }
                    break;
                case ContentDialogResult.Secondary:
                    break;
            }
        }

        public override void Stop()
        {
            HypervisorWatcher?.Stop();
            VulnerableDriverWatcher?.Stop();

            base.Stop();
        }
    }
}
