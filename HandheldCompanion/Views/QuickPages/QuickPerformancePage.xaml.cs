using HandheldCompanion.Views.Windows;
using System.Timers;
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

        public QuickPerformancePage()
        {
            InitializeComponent();
            Initialized = true;

            QuickTools.powerManager.StatusChanged += PowerManager_StatusChanged;
            QuickTools.powerManager.LimitChanged += PowerManager_LimitChanged;
            QuickTools.powerManager.ValueChanged += PowerManager_ValueChanged;

            // pull GPU settings
            var GPU = Properties.Settings.Default.QuickToolsPerformanceGPUValue;
            if (GPU >= GPUSlider.Minimum && GPU <= GPUSlider.Maximum)
                GPUSlider.Value = GPU;

            // pull TDP settings
            var TDP = Properties.Settings.Default.QuickToolsPerformanceTDPValue;
            if (TDP >= TDPSlider.Minimum && TDP <= TDPSlider.Maximum)
                TDPSlider.Value = TDP;
            else
                TDPSlider.Value = MainWindow.handheldDevice.DefaultTDP;

            // pull PowerMode settings
            var PowerMode = Properties.Settings.Default.QuickToolsPowerModeValue;
            if (PowerMode >= PowerModeSlider.Minimum && PowerMode <= PowerModeSlider.Maximum)
            {
                PowerModeSlider.Value = PowerMode;
                PowerModeSlider_ValueChanged(null, null); // force call, dirty
            }
        }

        private void PowerManager_StatusChanged(bool CanChangeTDP, bool CanChangeGPU)
        {
            this.Dispatcher.Invoke(() =>
            {
                TDPToggle.IsEnabled = CanChangeTDP;
                TDPToggle.IsOn = Properties.Settings.Default.QuickToolsPerformanceTDPEnabled;

                GPUToggle.IsEnabled = CanChangeGPU;
                GPUToggle.IsOn = Properties.Settings.Default.QuickToolsPerformanceGPUEnabled;
            });
        }

        private void PowerManager_LimitChanged(string type, int limit)
        {
            // do something
        }

        private void PowerManager_ValueChanged(string type, float value)
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

        private void TDPSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!Initialized)
                return;

            // update settings
            Properties.Settings.Default.QuickToolsPerformanceTDPValue = TDPSlider.Value;
            Properties.Settings.Default.Save();

            QuickTools.powerManager.RequestTDP(TDPSlider.Value);
        }

        private void TDPToggle_Toggled(object sender, RoutedEventArgs e)
        {
            // update settings
            Properties.Settings.Default.QuickToolsPerformanceTDPEnabled = TDPToggle.IsOn;
            Properties.Settings.Default.Save();

            if (!TDPToggle.IsOn)
            {
                // restore default GPU clock
                QuickTools.powerManager.RequestTDP(MainWindow.handheldDevice.DefaultTDP);
                return;
            }

            QuickTools.powerManager.RequestTDP(TDPSlider.Value);
        }

        private void GPUToggle_Toggled(object sender, RoutedEventArgs e)
        {
            // update settings
            Properties.Settings.Default.QuickToolsPerformanceGPUEnabled = GPUToggle.IsOn;
            Properties.Settings.Default.Save();

            if (!GPUToggle.IsOn)
            {
                // restore default GPU clock
                QuickTools.powerManager.RequestGPUClock(255 * 50);
                return;
            }

            QuickTools.powerManager.RequestGPUClock(GPUSlider.Value);
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

            QuickTools.powerManager.RequestPowerMode((int)PowerModeSlider.Value);
        }

        private void GPUSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!Initialized)
                return;

            // update settings
            Properties.Settings.Default.QuickToolsPerformanceGPUValue = GPUSlider.Value;
            Properties.Settings.Default.Save();

            QuickTools.powerManager.RequestGPUClock(GPUSlider.Value);
        }
    }
}
