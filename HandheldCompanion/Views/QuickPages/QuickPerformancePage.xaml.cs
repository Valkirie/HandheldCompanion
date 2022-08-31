using ControllerCommon;
using ControllerCommon.Processor;
using HandheldCompanion.Managers;
using HandheldCompanion.Views.Windows;
using System;
using System.Windows;
using System.Windows.Controls;

namespace HandheldCompanion.Views.QuickPages
{
    /// <summary>
    /// Interaction logic for QuickPerformancePage.xaml
    /// </summary>
    public partial class QuickPerformancePage : Page
    {
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
            SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
        }

        private void SettingsManager_SettingValueChanged(string name, object value)
        {
            this.Dispatcher.Invoke(() =>
            {
                switch (name)
                {
                    case "QuickToolsPowerModeValue":
                        PowerModeSlider.Value = Convert.ToInt32(value);
                        break;
                    case "QuickToolsPerformanceTDPEnabled":
                        TDPToggle.IsOn = Convert.ToBoolean(value);
                        break;
                    case "QuickToolsPerformanceGPUEnabled":
                        GPUToggle.IsOn = Convert.ToBoolean(value);
                        break;
                    case "QuickToolsPerformanceTDPSustainedValue":
                        {
                            double TDP = Convert.ToDouble(value);

                            if (TDPSustainedSlider.Minimum <= TDP && TDPSustainedSlider.Maximum >= TDP)
                                TDPSustainedSlider.Value = TDP;
                        }
                        break;
                    case "QuickToolsPerformanceTDPBoostValue":
                        {
                            double TDP = Convert.ToDouble(value);

                            if (TDPBoostSlider.Minimum <= TDP && TDPBoostSlider.Maximum >= TDP)
                                TDPBoostSlider.Value = TDP;
                        }
                        break;
                    case "QuickToolsPerformanceGPUValue":
                        {
                            double Clock = Convert.ToDouble(value);

                            if (GPUSlider.Minimum <= Clock && GPUSlider.Maximum >= Clock)
                                GPUSlider.Value = Clock;
                        }
                        break;
                    case "ConfigurableTDPOverrideUp":
                        TDPSustainedSlider.Maximum = Convert.ToInt32(value);
                        TDPBoostSlider.Maximum = Convert.ToInt32(value);
                        break;
                    case "ConfigurableTDPOverrideDown":
                        TDPSustainedSlider.Minimum = Convert.ToInt32(value);
                        TDPBoostSlider.Minimum = Convert.ToInt32(value);
                        break;
                }
            });
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

        private void TDPSustainedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!SettingsManager.GetBoolean("QuickToolsPerformanceTDPEnabled"))
                return;

            MainWindow.powerManager.RequestTDP(PowerType.Slow, TDPSustainedSlider.Value);
            MainWindow.powerManager.RequestTDP(PowerType.Stapm, TDPSustainedSlider.Value);

            if (!SettingsManager.IsInitialized)
                return;

            SettingsManager.SetProperty("QuickToolsPerformanceTDPSustainedValue", TDPSustainedSlider.Value);
        }

        private void TDPBoostSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!SettingsManager.GetBoolean("QuickToolsPerformanceTDPEnabled"))
                return;

            MainWindow.powerManager.RequestTDP(PowerType.Fast, TDPBoostSlider.Value);

            if (!SettingsManager.IsInitialized)
                return;

            SettingsManager.SetProperty("QuickToolsPerformanceTDPBoostValue", TDPBoostSlider.Value);
        }

        private void TDPToggle_Toggled(object sender, RoutedEventArgs e)
        {
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

            if (!SettingsManager.IsInitialized)
                return;

            SettingsManager.SetProperty("QuickToolsPerformanceTDPEnabled", TDPToggle.IsOn);
        }

        private void GPUToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!GPUToggle.IsOn)
            {
                // restore default GPU clock
                MainWindow.powerManager.RequestGPUClock(255 * 50);
                return;
            }

            MainWindow.powerManager.RequestGPUClock(GPUSlider.Value);

            if (!SettingsManager.IsInitialized)
                return;

            SettingsManager.SetProperty("QuickToolsPerformanceGPUEnabled", GPUToggle.IsOn);
        }

        private void PowerModeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // update settings
            int value = (int)PowerModeSlider.Value;

            // update UI
            this.Dispatcher.Invoke(() =>
            {
                foreach (TextBlock tb in PowerModeGrid.Children)
                    tb.SetResourceReference(Control.ForegroundProperty, "SystemControlForegroundBaseMediumBrush");

                TextBlock TextBlock = (TextBlock)PowerModeGrid.Children[value];
                TextBlock.SetResourceReference(Control.ForegroundProperty, "AccentButtonBackground");
            });

            MainWindow.powerManager.RequestPowerMode((int)PowerModeSlider.Value);

            if (!SettingsManager.IsInitialized)
                return;

            SettingsManager.SetProperty("QuickToolsPowerModeValue", value);
        }

        private void GPUSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!SettingsManager.GetBoolean("QuickToolsPerformanceGPUEnabled"))
                return;

            MainWindow.powerManager.RequestGPUClock(GPUSlider.Value);

            if (!SettingsManager.IsInitialized)
                return;

            SettingsManager.SetProperty("QuickToolsPerformanceGPUValue", GPUSlider.Value);
        }
    }
}
