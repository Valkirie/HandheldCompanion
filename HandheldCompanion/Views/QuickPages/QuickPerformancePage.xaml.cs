using ControllerCommon.Devices;
using HandheldCompanion.Managers;
using HandheldCompanion.Managers.Desktop;
using ModernWpf.Controls;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Views.QuickPages
{
    /// <summary>
    /// Interaction logic for QuickPerformancePage.xaml
    /// </summary>
    public partial class QuickPerformancePage : Page
    {
        public QuickPerformancePage()
        {
            InitializeComponent();

            MainWindow.performanceManager.PowerModeChanged += PerformanceManager_PowerModeChanged;
            MainWindow.performanceManager.PerfBoostModeChanged += PerformanceManager_PerfBoostModeChanged;

            SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

            SystemManager.PrimaryScreenChanged += DesktopManager_PrimaryScreenChanged;
            SystemManager.DisplaySettingsChanged += DesktopManager_DisplaySettingsChanged;

            // todo: move me ?
            SettingsManager.SetProperty("QuietModeEnabled", MainWindow.CurrentDevice.Capacities.HasFlag(DeviceCapacities.FanControl));
        }

        private void DesktopManager_PrimaryScreenChanged(DesktopScreen screen)
        {
            ComboBoxResolution.Items.Clear();
            foreach (ScreenResolution resolution in screen.resolutions)
                ComboBoxResolution.Items.Add(resolution);
        }

        private void DesktopManager_DisplaySettingsChanged(ScreenResolution resolution)
        {
            ComboBoxResolution.SelectedItem = resolution;
            ComboBoxFrequency.SelectedItem = SystemManager.GetDesktopScreen().GetFrequency();
        }

        private void PerformanceManager_PowerModeChanged(int idx)
        {
            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                PowerModeSlider.Value = idx;
            });
        }

        private void PerformanceManager_PerfBoostModeChanged(bool value)
        {
            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                CPUBoostToggle.IsOn = value;
            });
        }

        private void SettingsManager_SettingValueChanged(string name, object value)
        {
            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                switch (name)
                {
                    case "QuietModeToggled":
                        QuietModeToggle.IsOn = Convert.ToBoolean(value);
                        break;
                    case "QuietModeEnabled":
                        QuietModeToggle.IsEnabled = Convert.ToBoolean(value);
                        break;
                    case "QuietModeDuty":
                        QuietModeSlider.Value = Convert.ToDouble(value);
                        break;
                    case "OnScreenDisplayLevel":
                        ComboBoxOverlayDisplayLevel.SelectedIndex = Convert.ToInt32(value);
                        break;
                }
            });
        }

        private void PowerModeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // update settings
            int idx = (int)PowerModeSlider.Value;

            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                foreach (TextBlock tb in PowerModeGrid.Children)
                    tb.SetResourceReference(Control.ForegroundProperty, "SystemControlForegroundBaseMediumBrush");

                TextBlock TextBlock = (TextBlock)PowerModeGrid.Children[idx];
                TextBlock.SetResourceReference(Control.ForegroundProperty, "AccentButtonBackground");
            });

            if (!IsLoaded)
                return;

            MainWindow.performanceManager.RequestPowerMode(idx);
        }

        private void ComboBoxResolution_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ComboBoxResolution.SelectedItem is null)
                return;

            ScreenResolution resolution = (ScreenResolution)ComboBoxResolution.SelectedItem;

            ComboBoxFrequency.Items.Clear();
            foreach (ScreenFrequency frequency in resolution.frequencies.Values)
                ComboBoxFrequency.Items.Add(frequency);

            ComboBoxFrequency.SelectedItem = SystemManager.GetDesktopScreen().GetFrequency();

            SetResolution();
        }

        private void ComboBoxFrequency_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ComboBoxFrequency.SelectedItem is null)
                return;

            SetResolution();
        }

        private void SetResolution()
        {
            if (ComboBoxResolution.SelectedItem is null)
                return;

            if (ComboBoxFrequency.SelectedItem is null)
                return;

            ScreenResolution resolution = (ScreenResolution)ComboBoxResolution.SelectedItem;
            ScreenFrequency frequency = (ScreenFrequency)ComboBoxFrequency.SelectedItem;

            // update current screen resolution
            SystemManager.SetResolution(resolution.width, resolution.height, (int)frequency.GetValue(Frequency.Full));
        }

        private async void QuietModeToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded)
                return;

            bool Disclosure = SettingsManager.GetBoolean("QuietModeDisclosure");
            if (QuietModeToggle.IsOn && !Disclosure)
            {
                // todo: localize me !
                Task<ContentDialogResult> result = Dialog.ShowAsync(
                    "Warning",
                    "Altering fan duty cycle might cause instabilities and overheating. It might also trigger anti cheat systems and get you banned. Product warranties may not apply if you operate your device beyond its specifications. Use at your own risk.",
                    ContentDialogButton.Primary, "Cancel", Properties.Resources.ProfilesPage_OK);

                await result; // sync call

                switch (result.Result)
                {
                    case ContentDialogResult.Primary:
                        // save state
                        SettingsManager.SetProperty("QuietModeDisclosure", true);
                        break;
                    default:
                    case ContentDialogResult.None:
                        // restore previous state
                        QuietModeToggle.IsOn = false;
                        return;
                }
            }

            SettingsManager.SetProperty("QuietModeToggled", QuietModeToggle.IsOn);
        }

        private void QuietModeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            double value = QuietModeSlider.Value;
            if (double.IsNaN(value))
                return;

            if (!IsLoaded)
                return;

            SettingsManager.SetProperty("QuietModeDuty", value);
        }

        private void ComboBoxOverlayDisplayLevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded)
                return;

            SettingsManager.SetProperty("OnScreenDisplayLevel", ComboBoxOverlayDisplayLevel.SelectedIndex);
        }

        private void CPUBoostToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded)
                return;

            bool value = CPUBoostToggle.IsOn;
            MainWindow.performanceManager.RequestPerfBoostMode(value);
        }
    }
}