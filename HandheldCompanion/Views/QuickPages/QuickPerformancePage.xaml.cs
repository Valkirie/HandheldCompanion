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
            MainWindow.powerManager.PowerLimitChanged += PowerManager_LimitChanged;
            MainWindow.powerManager.PowerValueChanged += PowerManager_ValueChanged;

            MainWindow.profileManager.Updated += ProfileManager_Updated;
            MainWindow.profileManager.Applied += ProfileManager_Applied;
            MainWindow.profileManager.Discarded += ProfileManager_Discarded;

            // define slider(s) min and max values based on device specifications
            TDPBoostSlider.Minimum = TDPSustainedSlider.Minimum = MainWindow.handheldDevice.cTDP[0];
            TDPBoostSlider.Maximum = TDPSustainedSlider.Maximum = MainWindow.handheldDevice.cTDP[1];

            // pull PowerMode settings
            var PowerMode = Properties.Settings.Default.QuickToolsPowerModeValue;
            if (PowerMode >= PowerModeSlider.Minimum && PowerMode <= PowerModeSlider.Maximum)
            {
                PowerModeSlider.Value = PowerMode;
                PowerModeSlider_ValueChanged(null, null); // force call, dirty
            }

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

        private void ProfileManager_Updated(Profile profile, bool backgroundtask, bool isCurrent)
        {
            if (!isCurrent)
                return;

            this.Dispatcher.Invoke(() =>
            {
                TDPToggle.IsEnabled = !profile.TDP_override;
                TDPSustainedSlider.IsEnabled = !profile.TDP_override;
                TDPBoostSlider.IsEnabled = !profile.TDP_override;
                TDPWarning.Visibility = profile.TDP_override ? Visibility.Visible : Visibility.Collapsed;
            });
        }

        private void ProfileManager_Discarded(Profile profile)
        {
            this.Dispatcher.Invoke(() =>
            {
                TDPToggle.IsEnabled = CanChangeTDP;
                TDPSustainedSlider.IsEnabled = CanChangeTDP;
                TDPBoostSlider.IsEnabled = CanChangeTDP;
                TDPWarning.Visibility = Visibility.Collapsed;
            });
        }

        private void ProfileManager_Applied(Profile profile)
        {
            // do something
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
                    default:
                    case PowerType.Long:
                        if (TDPSustainedSlider.Minimum <= limit && TDPSustainedSlider.Maximum >= limit)
                            TDPSustainedSlider.Value = limit;
                        break;
                    case PowerType.Short:
                        if (TDPBoostSlider.Minimum <= limit && TDPBoostSlider.Maximum >= limit)
                            TDPBoostSlider.Value = limit;
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

            MainWindow.powerManager.RequestTDP(PowerType.Long, TDPSustainedSlider.Value);
        }

        private void TDPBoostSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!Initialized)
                return;

            // update settings
            Properties.Settings.Default.QuickToolsPerformanceTDPBoostValue = TDPBoostSlider.Value;
            Properties.Settings.Default.Save();

            MainWindow.powerManager.RequestTDP(PowerType.Short, TDPBoostSlider.Value);
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
                MainWindow.powerManager.RequestTDP(PowerType.Long, TDPSustainedSlider.Value);
                MainWindow.powerManager.RequestTDP(PowerType.Short, TDPBoostSlider.Value);
            }
            else
            {
                // restore default GPU clock
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

            MainWindow.powerManager.RequestGPUClock(GPUSlider.Value);
        }
    }
}
