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
        private bool CanChangeTDP, CanChangeGPU;
        private Profile currentProfile;

        public QuickPerformancePage()
        {
            InitializeComponent();

            MainWindow.performanceManager.ProcessorStatusChanged += PowerManager_StatusChanged;
            // MainWindow.powerManager.PowerLimitChanged += PowerManager_LimitChanged;
            // MainWindow.powerManager.PowerValueChanged += PowerManager_ValueChanged;

            ProfileManager.Updated += ProfileManager_Updated;
            ProfileManager.Applied += ProfileManager_Applied;
            ProfileManager.Discarded += ProfileManager_Discarded;

            SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

            HotkeysManager.CommandExecuted += HotkeysManager_CommandExecuted;

            SystemManager.PrimaryScreenChanged += DesktopManager_PrimaryScreenChanged;
            SystemManager.DisplaySettingsChanged += DesktopManager_DisplaySettingsChanged;

            GPUSlider.Minimum = MainWindow.CurrentDevice.GfxClock[0];
            GPUSlider.Maximum = MainWindow.CurrentDevice.GfxClock[1];

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
            ComboBoxFrequency.SelectedItem = SystemManager.GetScreenFrequency();
        }

        private void HotkeysManager_CommandExecuted(string listener)
        {
            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                switch (listener)
                {
                    case "increaseTDP":
                        {
                            if (!SettingsManager.GetBoolean("QuickToolsPerformanceTDPEnabled") || currentProfile.TDPOverrideEnabled)
                                return;

                            TDPBoostSlider.Value++;
                            TDPSustainedSlider.Value++;
                        }
                        break;
                    case "decreaseTDP":
                        {
                            if (!SettingsManager.GetBoolean("QuickToolsPerformanceTDPEnabled") || currentProfile.TDPOverrideEnabled)
                                return;

                            TDPSustainedSlider.Value--;
                            TDPBoostSlider.Value--;
                        }
                        break;
                }
            });
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
                    case "QuickToolsPerformanceTDPEnabled":
                        TDPToggle.IsOn = Convert.ToBoolean(value);
                        break;
                    case "QuickToolsPerformanceGPUEnabled":
                        GPUToggle.IsOn = Convert.ToBoolean(value);
                        break;
                    case "QuickToolsPerformanceFramerateEnabled":
                        FramerateToggle.IsOn = Convert.ToBoolean(value);
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

        private void ProfileManager_Updated(Profile profile, ProfileUpdateSource source, bool isCurrent)
        {
            if (!isCurrent)
                return;

            currentProfile = profile;

            UpdateControls();
        }

        private void ProfileManager_Discarded(Profile profile, bool isCurrent, bool isUpdate)
        {
            if (!isCurrent)
                return;

            currentProfile = null;

            UpdateControls();
        }

        private void ProfileManager_Applied(Profile profile)
        {
            currentProfile = profile;

            UpdateControls();
        }

        private void UpdateControls()
        {
            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                if (currentProfile is not null)
                {
                    TDPToggle.IsEnabled = TDPSustainedSlider.IsEnabled = TDPBoostSlider.IsEnabled = CanChangeTDP && !currentProfile.TDPOverrideEnabled;
                    TDPWarning.Visibility = currentProfile.TDPOverrideEnabled ? Visibility.Visible : Visibility.Collapsed;
                }
                else
                {
                    TDPToggle.IsEnabled = TDPSustainedSlider.IsEnabled = TDPBoostSlider.IsEnabled = CanChangeTDP;
                    TDPWarning.Visibility = Visibility.Collapsed;
                }

                GPUToggle.IsEnabled = CanChangeGPU;
                GPUSlider.IsEnabled = CanChangeGPU;
            });
        }

        private void PowerManager_StatusChanged(bool CanChangeTDP, bool CanChangeGPU)
        {
            this.CanChangeTDP = CanChangeTDP;
            this.CanChangeGPU = CanChangeGPU;

            UpdateControls();
        }

        private void PowerManager_LimitChanged(PowerType type, int limit)
        {
            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
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
            if (!TDPSustainedSlider.IsInitialized || !TDPBoostSlider.IsInitialized)
                return;

            if (!SettingsManager.GetBoolean("QuickToolsPerformanceTDPEnabled"))
                return;

            MainWindow.performanceManager.RequestTDP(PowerType.Slow, TDPSustainedSlider.Value);
            MainWindow.performanceManager.RequestTDP(PowerType.Stapm, TDPSustainedSlider.Value);

            // set boost slider minimum value to sustained current value
            TDPBoostSlider.Minimum = TDPSustainedSlider.Value;

            if (!IsLoaded)
                return;

            SettingsManager.SetProperty("QuickToolsPerformanceTDPSustainedValue", TDPSustainedSlider.Value);
        }

        private void TDPBoostSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!TDPSustainedSlider.IsInitialized || !TDPBoostSlider.IsInitialized)
                return;

            if (!SettingsManager.GetBoolean("QuickToolsPerformanceTDPEnabled"))
                return;

            MainWindow.performanceManager.RequestTDP(PowerType.Fast, TDPBoostSlider.Value);

            // set sustained slider maximum value to boost current value
            TDPSustainedSlider.Maximum = TDPBoostSlider.Value;

            if (!IsLoaded)
                return;

            SettingsManager.SetProperty("QuickToolsPerformanceTDPBoostValue", TDPBoostSlider.Value);
        }

        private void TDPToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (TDPToggle.IsOn)
            {
                MainWindow.performanceManager.RequestTDP(PowerType.Slow, TDPSustainedSlider.Value);
                MainWindow.performanceManager.RequestTDP(PowerType.Stapm, TDPSustainedSlider.Value);
                MainWindow.performanceManager.RequestTDP(PowerType.Fast, TDPBoostSlider.Value);

                MainWindow.performanceManager.StartTDPWatchdog();
            }
            else
            {
                // restore default TDP and halt watchdog
                MainWindow.performanceManager.RequestTDP(MainWindow.CurrentDevice.nTDP);

                MainWindow.performanceManager.StopTDPWatchdog();
            }

            if (!IsLoaded)
                return;

            SettingsManager.SetProperty("QuickToolsPerformanceTDPEnabled", TDPToggle.IsOn);
        }

        private void GPUToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (GPUToggle.IsOn)
            {
                MainWindow.performanceManager.RequestGPUClock(GPUSlider.Value);
                MainWindow.performanceManager.StartGPUWatchdog();
            }
            else
            {
                // restore default GPU clock and halt watchdog
                MainWindow.performanceManager.RequestGPUClock(255 * 50);
                MainWindow.performanceManager.StopGPUWatchdog();
            }

            if (!IsLoaded)
                return;

            SettingsManager.SetProperty("QuickToolsPerformanceGPUEnabled", GPUToggle.IsOn);
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

            MainWindow.performanceManager.RequestPowerMode((int)PowerModeSlider.Value);

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

            SettingsManager.SetProperty("QuickToolsPerformanceFramerateEnabled", FramerateToggle.IsOn);
        }

        private void FramerateSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!SettingsManager.GetBoolean("QuickToolsPerformanceFramerateEnabled"))
                return;

            PlatformManager.RTSS.RequestFPS(FramerateSlider.Value);

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

        private void GPUSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!SettingsManager.GetBoolean("QuickToolsPerformanceGPUEnabled"))
                return;

            MainWindow.performanceManager.RequestGPUClock(GPUSlider.Value);

            if (!IsLoaded)
                return;

            SettingsManager.SetProperty("QuickToolsPerformanceGPUValue", GPUSlider.Value);
        }
    }
}