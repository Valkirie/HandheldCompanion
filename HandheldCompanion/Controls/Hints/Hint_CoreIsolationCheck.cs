using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Platforms;
using HandheldCompanion.Utils;
using Inkore.UI.WPF.Modern.Controls;
using Microsoft.Extensions.FileSystemGlobbing;
using SharpDX.Direct3D9;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Linq;

namespace HandheldCompanion.Controls.Hints
{
    public class Hint_CoreIsolationCheck : IHint
    {
        private static WqlEventQuery HypervisorQuery = new WqlEventQuery(@"SELECT * FROM RegistryValueChangeEvent WHERE Hive = 'HKEY_LOCAL_MACHINE' AND KeyPath = 'SYSTEM\\CurrentControlSet\\Control\\DeviceGuard\\Scenarios' AND ValueName='HypervisorEnforcedCodeIntegrity'");
        private static WqlEventQuery VulnerableDriverQuery = new WqlEventQuery(@"SELECT * FROM RegistryValueChangeEvent WHERE Hive = 'HKEY_LOCAL_MACHINE' AND KeyPath = 'SYSTEM\\CurrentControlSet\\Control\\CI\\Config' AND ValueName='VulnerableDriverBlocklistEnable'");

        private static ManagementEventWatcher VulnerableDriverWatcher = new ManagementEventWatcher(VulnerableDriverQuery);
        private static ManagementEventWatcher HypervisorWatcher = new ManagementEventWatcher(HypervisorQuery);

        bool HypervisorEnforcedCodeIntegrityEnabled = true;
        bool VulnerableDriverBlocklistEnable = true;

        public Hint_CoreIsolationCheck() : base()
        {
            HypervisorWatcher.EventArrived += new EventArrivedEventHandler(HandleEvent);
            VulnerableDriverWatcher.EventArrived += new EventArrivedEventHandler(HandleEvent);

            // Start listening for events.
            HypervisorWatcher.Start();
            VulnerableDriverWatcher.Start();

            // default state
            this.HintActionButton.Visibility = Visibility.Visible;

            this.HintTitle.Text = Properties.Resources.Hint_CoreIsolationCheck;
            this.HintDescription.Text = Properties.Resources.Hint_CoreIsolationCheckDesc;
            this.HintReadMe.Text = Properties.Resources.Hint_CoreIsolationCheckReadme;

            this.HintActionButton.Content = Properties.Resources.Hint_CoreIsolationCheckAction;

            CheckSettings();
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

            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                this.Visibility = HypervisorEnforcedCodeIntegrityEnabled || VulnerableDriverBlocklistEnable ? Visibility.Visible : Visibility.Collapsed;
            });
        }

        protected override async void HintActionButton_Click(object sender, RoutedEventArgs e)
        {
            RegistryUtils.SetValue(@"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios", "HypervisorEnforcedCodeIntegrity", 0);
            RegistryUtils.SetValue(@"SYSTEM\CurrentControlSet\Control\CI\Config", "VulnerableDriverBlocklistEnable", 0);

            var result = Dialog.ShowAsync($"{Properties.Resources.Dialog_ForceRestartTitle}",
                $"{Properties.Resources.Dialog_ForceRestartDesc}",
                ContentDialogButton.Primary, null,
                $"{Properties.Resources.Dialog_Yes}",
                $"{Properties.Resources.Dialog_No}");
            
            await result;

            switch (result.Result)
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
            HypervisorWatcher.Stop();
            VulnerableDriverWatcher.Stop();

            base.Stop();
        }
    }
}
