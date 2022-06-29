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
    /// Interaction logic for QuickToolsPage4.xaml
    /// </summary>
    public partial class QuickToolsPage4 : Page
    {
        private Timer updateTimer;
        private bool Initialized;

        public QuickToolsPage4()
        {
            InitializeComponent();
            Initialized = true;

            updateTimer = new Timer() { Interval = 1000, AutoReset = false, Enabled = false };
            updateTimer.Elapsed += UpdateTimer_Elapsed;

            QuickTools.powerManager.StatusChanged += PowerManager_StatusChanged;
            QuickTools.powerManager.LimitChanged += PowerManager_LimitChanged;
            QuickTools.powerManager.ValueChanged += PowerManager_ValueChanged;

            // pull GPU settings
            GPUSlider.Value = Properties.Settings.Default.QuickToolsPerformanceGPUValue;

            // pull TDP settings
            var TDP = Properties.Settings.Default.QuickToolsPerformanceTDPValue;
            if (TDP == 0)
            {
                Properties.Settings.Default.QuickToolsPerformanceTDPValue = MainWindow.handheldDevice.DefaultTDP;
                Properties.Settings.Default.Save();
            }
            TDPSlider.Value = Properties.Settings.Default.QuickToolsPerformanceTDPValue;
        }

        private void UpdateTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            this.Dispatcher.Invoke(() =>
            {
                if (!TDPToggle.IsOn)
                    return;

                QuickTools.powerManager.RequestTDP(TDPSlider.Value);
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
            updateTimer.Stop();
            updateTimer.Start();
        }

        private void TDPToggle_Toggled(object sender, RoutedEventArgs e)
        {
            // update settings
            Properties.Settings.Default.QuickToolsPerformanceTDPEnabled = TDPToggle.IsOn;
            Properties.Settings.Default.Save();

            // we use a timer to prevent too many calls from happening
            updateTimer.Stop();
            updateTimer.Start();
        }

        private void GPUToggle_Toggled(object sender, RoutedEventArgs e)
        {
            // update settings
            Properties.Settings.Default.QuickToolsPerformanceGPUEnabled = GPUToggle.IsOn;
            Properties.Settings.Default.Save();

            if (!GPUToggle.IsOn)
                return;

            // do something
        }
    }
}
