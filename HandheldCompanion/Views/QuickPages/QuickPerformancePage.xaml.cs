using ControllerCommon;
using ControllerCommon.Devices;
using ControllerCommon.Processor;
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
        private double[] frequency_slider = new double[4] { 15, 20, 30, 60 };

        public QuickPerformancePage()
        {
            InitializeComponent();

            SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

            SystemManager.PrimaryScreenChanged += DesktopManager_PrimaryScreenChanged;
            SystemManager.DisplaySettingsChanged += DesktopManager_DisplaySettingsChanged;

            // todo: move me ?
            SettingsManager.SetProperty("QuietModeEnabled", MainWindow.CurrentDevice.Capacities.HasFlag(DeviceCapacities.FanControl));
            SettingsManager.SetProperty("QuickToolsPerformanceFramerateEnabled", PlatformManager.RTSS.IsInstalled);
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
            ComboBoxFrequency.SelectedItem = SystemManager.GetScreenFrequency();

            double frequency_current = SystemManager.GetScreenFrequency().frequency;
            frequency_slider[0] = frequency_current / 4.0d;
            frequency_slider[1] = frequency_current / 3.0d;
            frequency_slider[2] = frequency_current / 2.0d;
            frequency_slider[3] = frequency_current;

            FramerateQuarter.Text = String.Format("{0:0.#}", frequency_slider[0]);
            FramerateThird.Text = String.Format("{0:0.#}", frequency_slider[1]);
            FramerateHalf.Text = String.Format("{0:0.#}", frequency_slider[2]);
            FramerateFull.Text = String.Format("{0:0.#}", frequency_slider[3]);

            UpdateFrequency();
        }

        private void SettingsManager_SettingValueChanged(string name, object value)
        {
            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                switch (name)
                {
                    case "QuickToolsPowerModeValue":
                        PowerModeSlider.Value = Convert.ToDouble(value);
                        break;
                    case "QuickToolsPerformanceFramerateToggled":
                        FramerateToggle.IsOn = Convert.ToBoolean(value);
                        break;
                    case "QuickToolsPerformanceFramerateEnabled":
                        FramerateToggle.IsEnabled = Convert.ToBoolean(value);
                        break;
                    case "QuickToolsPerformanceFramerateValue":
                        FramerateSlider.Value = Convert.ToDouble(value);
                        break;
                    case "QuietModeToggled":
                        QuietModeToggle.IsOn = Convert.ToBoolean(value);
                        break;
                    case "QuietModeEnabled":
                        QuietModeToggle.IsEnabled = Convert.ToBoolean(value);
                        break;
                    case "QuietModeDuty":
                        QuietModeSlider.Value = Convert.ToDouble(value);
                        break;
                }
            });
        }

        private void PowerModeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // update settings
            int value = (int)PowerModeSlider.Value;

            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                foreach (TextBlock tb in PowerModeGrid.Children)
                    tb.SetResourceReference(Control.ForegroundProperty, "SystemControlForegroundBaseMediumBrush");

                TextBlock TextBlock = (TextBlock)PowerModeGrid.Children[value];
                TextBlock.SetResourceReference(Control.ForegroundProperty, "AccentButtonBackground");
            });

            if (!IsLoaded)
                return;

            SettingsManager.SetProperty("QuickToolsPowerModeValue", value);
        }

        private void ComboBoxResolution_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ComboBoxResolution.SelectedItem is null)
                return;

            ScreenResolution resolution = (ScreenResolution)ComboBoxResolution.SelectedItem;

            ComboBoxFrequency.Items.Clear();
            foreach (ScreenFrequency frequency in resolution.frequencies)
                ComboBoxFrequency.Items.Add(frequency);

            // pick current frequency, if available
            ComboBoxFrequency.SelectedItem = SystemManager.GetScreenFrequency();

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
            SystemManager.SetResolution(resolution.width, resolution.height, frequency.frequency);
        }

        private void FramerateToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded)
                return;

            // restore default value is toggled off
            if (!FramerateToggle.IsOn)
                PlatformManager.RTSS.RequestFPS(0);
            else
            {
                double framerate = SettingsManager.GetDouble("QuickToolsPerformanceFramerateValue");
                PlatformManager.RTSS.RequestFPS(framerate);
            }

            SettingsManager.SetProperty("QuickToolsPerformanceFramerateToggled", FramerateToggle.IsOn);
        }

        private void FramerateSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // update settings
            int value = (int)FramerateSlider.Value;

            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                foreach (TextBlock tb in FramerateModeGrid.Children)
                    tb.SetResourceReference(Control.ForegroundProperty, "SystemControlForegroundBaseMediumBrush");

                TextBlock TextBlock = (TextBlock)FramerateModeGrid.Children[value];
                TextBlock.SetResourceReference(Control.ForegroundProperty, "AccentButtonBackground");
            });

            UpdateFrequency();
        }

        private void UpdateFrequency()
        {
            if (!SettingsManager.GetBoolean("QuickToolsPerformanceFramerateToggled"))
                return;

            double frequency = frequency_slider[(int)FramerateSlider.Value];
            PlatformManager.RTSS.RequestFPS(frequency);

            if (!IsLoaded)
                return;

            SettingsManager.SetProperty("QuickToolsPerformanceFramerateValue", FramerateSlider.Value);
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
    }
}