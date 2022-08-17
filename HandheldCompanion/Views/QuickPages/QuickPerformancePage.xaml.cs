using ControllerCommon;
using ControllerCommon.Processor;
using HandheldCompanion.Views.Windows;
using System.Windows;
using System.Windows.Controls;

namespace HandheldCompanion.Views.QuickPages
{
    /// <summary>
    /// Interaction logic for QuickPerformancePage.xaml
    /// </summary>
    public partial class QuickPerformancePage : Page
    {
        private bool Initialized;
        private bool CanChangeTDP, CanChangeGPU;

        public QuickPerformancePage()
        {
            InitializeComponent();

            MainWindow.powerManager.ProcessorStatusChanged += PowerManager_StatusChanged;
            // MainWindow.powerManager.PowerLimitChanged += PowerManager_LimitChanged;
            // MainWindow.powerManager.PowerValueChanged += PowerManager_ValueChanged;

            MainWindow.profileManager.Updated += ProfileManager_Updated;
            MainWindow.profileManager.Applied += ProfileManager_Applied;
            MainWindow.profileManager.Discarded += ProfileManager_Discarded;

            // define slider(s) min and max values based on device specifications
            var cTDPdown = Properties.Settings.Default.ConfigurableTDPOverride ? Properties.Settings.Default.ConfigurableTDPOverrideDown : MainWindow.handheldDevice.cTDP[0];
            var cTDPup = Properties.Settings.Default.ConfigurableTDPOverride ? Properties.Settings.Default.ConfigurableTDPOverrideUp : MainWindow.handheldDevice.cTDP[1];
            TDPBoostSlider.Minimum = TDPSustainedSlider.Minimum = cTDPdown;
            TDPBoostSlider.Maximum = TDPSustainedSlider.Maximum = cTDPup;

            // pull PowerMode settings
            var PowerMode = Properties.Settings.Default.QuickToolsPowerModeValue;
            if (PowerMode >= PowerModeSlider.Minimum && PowerMode <= PowerModeSlider.Maximum)
            {
                PowerModeSlider.Value = PowerMode;
                PowerModeSlider_ValueChanged(null, null); // force call, dirty
            }

            // pull CPU settings
            var TDPdown = Properties.Settings.Default.QuickToolsPerformanceTDPEnabled ? Properties.Settings.Default.QuickToolsPerformanceTDPSustainedValue : 0;
            var TDPup = Properties.Settings.Default.QuickToolsPerformanceTDPEnabled ? Properties.Settings.Default.QuickToolsPerformanceTDPBoostValue : 0;
            TDPdown = TDPdown != 0 ? TDPdown : MainWindow.handheldDevice.nTDP[(int)PowerType.Slow];
            TDPup = TDPup != 0 ? TDPup : MainWindow.handheldDevice.nTDP[(int)PowerType.Fast];

            if (TDPSustainedSlider.Minimum <= TDPdown && TDPSustainedSlider.Maximum >= TDPdown)
                TDPSustainedSlider.Value = TDPdown;

            if (TDPBoostSlider.Minimum <= TDPup && TDPBoostSlider.Maximum >= TDPup)
                TDPBoostSlider.Value = TDPup;

            // pull GPU settings
            var GPU = Properties.Settings.Default.QuickToolsPerformanceGPUValue;

            if (GPUSlider.Minimum <= GPU && GPUSlider.Maximum >= GPU)
                GPUSlider.Value = GPU;

            // pull TDP and GPU toggle settings
            TDPToggle.IsOn = Properties.Settings.Default.QuickToolsPerformanceTDPEnabled;
            GPUToggle.IsOn = Properties.Settings.Default.QuickToolsPerformanceGPUEnabled;

            // we're all set !
            Initialized = true;
        }

        public void SettingsPage_SettingValueChanged(string name, object value)
        {
            switch (name)
            {
                case "configurabletdp_down":
                    TDPBoostSlider.Minimum = TDPSustainedSlider.Minimum = (double)value;
                    break;
                case "configurabletdp_up":
                    TDPBoostSlider.Maximum = TDPSustainedSlider.Maximum = (double)value;
                    break;
            }
        }

        private void ProfileManager_Updated(Profile profile, bool backgroundtask, bool isCurrent)
        {
            if (!isCurrent)
                return;

            LockTDP(profile);
        }

        private void ProfileManager_Discarded(Profile profile, bool isCurrent)
        {
            if (!isCurrent)
                return;

            UnlockTDP(profile);
        }

        private void ProfileManager_Applied(Profile profile)
        {
            LockTDP(profile);
        }

        private void LockTDP(Profile profile)
        {
            this.Dispatcher.Invoke(() =>
            {
                TDPToggle.IsEnabled = !profile.TDP_override;
                TDPSustainedSlider.IsEnabled = !profile.TDP_override;
                TDPBoostSlider.IsEnabled = !profile.TDP_override;
                TDPWarning.Visibility = profile.TDP_override ? Visibility.Visible : Visibility.Collapsed;
            });
        }

        private void UnlockTDP(Profile profile)
        {
            this.Dispatcher.Invoke(() =>
            {
                TDPToggle.IsEnabled = CanChangeTDP;
                TDPSustainedSlider.IsEnabled = CanChangeTDP;
                TDPBoostSlider.IsEnabled = CanChangeTDP;
                TDPWarning.Visibility = Visibility.Collapsed;
            });
        }

        private void PowerManager_StatusChanged(bool CanChangeTDP, bool CanChangeGPU)
        {
            this.CanChangeTDP = CanChangeTDP;
            this.CanChangeGPU = CanChangeGPU;

            this.Dispatcher.Invoke(() =>
            {
                TDPToggle.IsEnabled = CanChangeTDP;
                TDPSustainedSlider.IsEnabled = CanChangeTDP;
                TDPBoostSlider.IsEnabled = CanChangeTDP;

                GPUToggle.IsEnabled = CanChangeGPU;
                GPUSlider.IsEnabled = CanChangeGPU;
            });
        }

        private void PowerManager_LimitChanged(PowerType type, int limit)
        {
            this.Dispatcher.Invoke(() =>
            {
                // do something
                switch (type)
                {
                    case PowerType.Slow:
                        {
                            if (!TDPSustainedSlider.IsEnabled)
                                return;

                            if (TDPSustainedSlider.Minimum <= limit && TDPSustainedSlider.Maximum >= limit)
                                TDPSustainedSlider.Value = limit;
                        }
                        break;
                    case PowerType.Fast:
                        {
                            if (!TDPBoostSlider.IsEnabled)
                                return;

                            if (TDPBoostSlider.Minimum <= limit && TDPBoostSlider.Maximum >= limit)
                                TDPBoostSlider.Value = limit;
                        }
                        break;
                    case PowerType.Stapm:
                    case PowerType.MsrSlow:
                    case PowerType.MsrFast:
                        // do nothing
                        break;
                }
            });
        }

        private void PowerManager_ValueChanged(PowerType type, float value)
        {
            // do something
        }

        private void Scrolllock_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            QuickTools.scrollLock = true;
        }

        private void Scrolllock_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            QuickTools.scrollLock = false;
        }

        private void TDPSustainedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!Initialized)
                return;

            // update settings
            Properties.Settings.Default.QuickToolsPerformanceTDPSustainedValue = TDPSustainedSlider.Value;
            Properties.Settings.Default.Save();

            if (!Properties.Settings.Default.QuickToolsPerformanceTDPEnabled)
                return;

            MainWindow.powerManager.RequestTDP(PowerType.Slow, TDPSustainedSlider.Value);
            MainWindow.powerManager.RequestTDP(PowerType.Stapm, TDPSustainedSlider.Value);
        }

        private void TDPBoostSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!Initialized)
                return;

            // update settings
            Properties.Settings.Default.QuickToolsPerformanceTDPBoostValue = TDPBoostSlider.Value;
            Properties.Settings.Default.Save();

            if (!Properties.Settings.Default.QuickToolsPerformanceTDPEnabled)
                return;

            MainWindow.powerManager.RequestTDP(PowerType.Fast, TDPBoostSlider.Value);
        }

        private void TDPToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!Initialized)
                return;

            // update settings
            Properties.Settings.Default.QuickToolsPerformanceTDPEnabled = TDPToggle.IsOn;
            Properties.Settings.Default.Save();

            if (TDPToggle.IsOn)
            {
                MainWindow.powerManager.RequestTDP(PowerType.Slow, TDPSustainedSlider.Value);
                MainWindow.powerManager.RequestTDP(PowerType.Stapm, TDPSustainedSlider.Value);
                MainWindow.powerManager.RequestTDP(PowerType.Fast, TDPBoostSlider.Value);
            }
            else
            {
                // restore default TDP
                MainWindow.powerManager.RequestTDP(MainWindow.handheldDevice.nTDP);
            }
        }

        private void GPUToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!Initialized)
                return;

            // update settings
            Properties.Settings.Default.QuickToolsPerformanceGPUEnabled = GPUToggle.IsOn;
            Properties.Settings.Default.Save();

            if (!GPUToggle.IsOn)
            {
                // restore default GPU clock
                MainWindow.powerManager.RequestGPUClock(255 * 50);
                return;
            }

            MainWindow.powerManager.RequestGPUClock(GPUSlider.Value);
        }

        private void PowerModeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // update settings
            int value = (int)PowerModeSlider.Value;
            Properties.Settings.Default.QuickToolsPowerModeValue = value;
            Properties.Settings.Default.Save();

            this.Dispatcher.Invoke(() =>
            {
                foreach (TextBlock tb in PowerModeGrid.Children)
                    tb.SetResourceReference(Control.ForegroundProperty, "SystemControlForegroundBaseMediumBrush");

                TextBlock TextBlock = (TextBlock)PowerModeGrid.Children[value];
                TextBlock.SetResourceReference(Control.ForegroundProperty, "AccentButtonBackground");
            });

            MainWindow.powerManager.RequestPowerMode((int)PowerModeSlider.Value);
        }

        private void GPUSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!Initialized)
                return;

            // update settings
            Properties.Settings.Default.QuickToolsPerformanceGPUValue = GPUSlider.Value;
            Properties.Settings.Default.Save();

            if (!Properties.Settings.Default.QuickToolsPerformanceTDPEnabled)
                return;

            MainWindow.powerManager.RequestGPUClock(GPUSlider.Value);
        }
    }
}
