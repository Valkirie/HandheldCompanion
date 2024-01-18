using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Processors;
using HandheldCompanion.Utils;
using HandheldCompanion.Views;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Diagnostics;
using System.Windows;

namespace HandheldCompanion.Controls.Hints
{
    public class Hint_AMD_IntegerScalingCheck : IHint
    {
        public Hint_AMD_IntegerScalingCheck() : base()
        {
            // default state
            this.HintActionButton.Visibility = Visibility.Visible;

            this.HintTitle.Text = Properties.Resources.Hint_AMD_IntegerScalingCheck;
            this.HintDescription.Text = Properties.Resources.Hint_AMD_IntegerScalingCheckDesc;
            this.HintReadMe.Text = Properties.Resources.Hint_AMD_IntegerScalingCheckReadme;

            this.HintActionButton.Content = Properties.Resources.Hint_AMD_IntegerScalingCheckAction;

            // manage events
            PerformanceManager.Initialized += PerformanceManager_Initialized;
        }

        private void PerformanceManager_Initialized()
        {
            Processor processor = PerformanceManager.GetProcessor();

            if (processor is not null && processor is AMDProcessor)
                CheckSettings();
        }

        private void CheckSettings()
        {
            // read OS specific values
            int EmbeddedIntegerScalingSupport = RegistryUtils.GetInt(@"SYSTEM\ControlSet001\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0000", "DalEmbeddedIntegerScalingSupport");

            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                this.Visibility = EmbeddedIntegerScalingSupport != 1 ? Visibility.Visible : Visibility.Collapsed;
            });
        }

        protected override async void HintActionButton_Click(object sender, RoutedEventArgs e)
        {
            RegistryUtils.SetValue(@"SYSTEM\ControlSet001\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0000", "DalEmbeddedIntegerScalingSupport", 1);

            var result = Dialog.ShowAsync($"{Properties.Resources.Dialog_ForceRestartTitle}",
                $"{Properties.Resources.Dialog_ForceRestartDesc}",
                ContentDialogButton.Primary, null,
                $"{Properties.Resources.Dialog_Yes}",
                $"{Properties.Resources.Dialog_No}", MainWindow.GetCurrent());
            
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
    }
}
