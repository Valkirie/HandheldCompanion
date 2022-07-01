using HandheldCompanion.Managers;
using HandheldCompanion.Views.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace HandheldCompanion.Views.QuickPages
{
    /// <summary>
    /// Interaction logic for PerformancePage.xaml
    /// </summary>
    public partial class PerformancePage : Page
    {
        private Timer cpuTimer;
        private Timer gpuTimer;

        private bool Initialized;

        public PerformancePage()
        {
            InitializeComponent();
            Initialized = true;

            cpuTimer = new Timer() { Interval = 3000, AutoReset = false, Enabled = false };
            cpuTimer.Elapsed += cpuTimer_Elapsed;

            gpuTimer = new Timer() { Interval = 3000, AutoReset = false, Enabled = false };
            gpuTimer.Elapsed += gpuTimer_Elapsed;

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

            // pull PowerMode settings
            var PowerMode = Properties.Settings.Default.QuickToolsPowerModeValue;
            if (PowerMode >= PowerModeSlider.Minimum && PowerMode <= PowerModeSlider.Maximum)
            {
                PowerModeSlider.Value = PowerMode;
                PowerModeSlider_ValueChanged(null, null); // force call, dirty
            }
        }

        private void cpuTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            this.Dispatcher.Invoke(() =>
            {
                if (!TDPToggle.IsOn)
                    return;

                QuickTools.powerManager.RequestTDP(TDPSlider.Value);
            });
        }

        private void gpuTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            this.Dispatcher.Invoke(() =>
            {
                if (!GPUToggle.IsOn)
                    return;

                QuickTools.powerManager.RequestGPUClock(GPUSlider.Value);
            });
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

            // we use a timer to prevent too many calls from happening
            cpuTimer.Stop();
            cpuTimer.Start();
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

            // we use a timer to prevent too many calls from happening
            cpuTimer.Stop();
            cpuTimer.Start();
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

            // we use a timer to prevent too many calls from happening
            gpuTimer.Stop();
            gpuTimer.Start();
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

            // we use a timer to prevent too many calls from happening
            gpuTimer.Stop();
            gpuTimer.Start();
        }
    }
}
