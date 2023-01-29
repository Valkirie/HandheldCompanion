﻿using ControllerCommon;
using ControllerCommon.Processor;
using HandheldCompanion.Managers;
using HandheldCompanion.Managers.Desktop;
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

            DesktopManager.PrimaryScreenChanged += DesktopManager_PrimaryScreenChanged;
            DesktopManager.DisplaySettingsChanged += DesktopManager_DisplaySettingsChanged;

            GPUSlider.Minimum = MainWindow.handheldDevice.GfxClock[0];
            GPUSlider.Maximum = MainWindow.handheldDevice.GfxClock[1];
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
            ComboBoxFrequency.SelectedItem = DesktopManager.GetScreenFrequency();
        }

        private void HotkeysManager_CommandExecuted(string listener)
        {
            Dispatcher.Invoke(() =>
            {
                switch (listener)
                {
                    case "increaseTDP":
                        {
                            if (!SettingsManager.GetBoolean("QuickToolsPerformanceTDPEnabled") || currentProfile.TDP_override)
                                return;

                            TDPSustainedSlider.Value++;
                            TDPBoostSlider.Value++;
                        }
                        break;
                    case "decreaseTDP":
                        {
                            if (!SettingsManager.GetBoolean("QuickToolsPerformanceTDPEnabled") || currentProfile.TDP_override)
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
            Dispatcher.Invoke(() =>
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

        private void ProfileManager_Updated(Profile profile, ProfileUpdateSource source, bool isCurrent)
        {
            if (!isCurrent)
                return;

            currentProfile = profile;

            UpdateControls();
        }

        private void ProfileManager_Discarded(Profile profile, bool isCurrent)
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
            Dispatcher.Invoke(() =>
            {
                if (currentProfile is not null)
                {
                    TDPToggle.IsEnabled = TDPSustainedSlider.IsEnabled = TDPBoostSlider.IsEnabled = CanChangeTDP && !currentProfile.TDP_override;
                    TDPWarning.Visibility = currentProfile.TDP_override ? Visibility.Visible : Visibility.Collapsed;
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

        private void TDPSustainedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!SettingsManager.GetBoolean("QuickToolsPerformanceTDPEnabled"))
                return;

            if (!TDPSustainedSlider.IsInitialized || !TDPBoostSlider.IsInitialized)
                return;

            MainWindow.performanceManager.RequestTDP(PowerType.Slow, TDPSustainedSlider.Value);
            MainWindow.performanceManager.RequestTDP(PowerType.Stapm, TDPSustainedSlider.Value);

            // Prevent sustained value being higher then boost
            if (TDPSustainedSlider.Value > TDPBoostSlider.Value)
            {
                TDPBoostSlider.Value = TDPSustainedSlider.Value;
            }

            if (!SettingsManager.IsInitialized)
                return;

            SettingsManager.SetProperty("QuickToolsPerformanceTDPSustainedValue", TDPSustainedSlider.Value);
        }

        private void TDPBoostSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!SettingsManager.GetBoolean("QuickToolsPerformanceTDPEnabled"))
                return;

            if (!TDPSustainedSlider.IsInitialized || !TDPBoostSlider.IsInitialized)
                return;

            MainWindow.performanceManager.RequestTDP(PowerType.Fast, TDPBoostSlider.Value);

            // Prevent boost value being lower then sustained
            if (TDPBoostSlider.Value < TDPSustainedSlider.Value)
            {
                TDPSustainedSlider.Value = TDPBoostSlider.Value;
            }

            if (!SettingsManager.IsInitialized)
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
                MainWindow.performanceManager.RequestTDP(MainWindow.handheldDevice.nTDP);

                MainWindow.performanceManager.StopTDPWatchdog();
            }

            if (!SettingsManager.IsInitialized)
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

            if (!SettingsManager.IsInitialized)
                return;

            SettingsManager.SetProperty("QuickToolsPerformanceGPUEnabled", GPUToggle.IsOn);
        }

        private void PowerModeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // update settings
            int value = (int)PowerModeSlider.Value;

            // update UI
            Dispatcher.Invoke(() =>
            {
                foreach (TextBlock tb in PowerModeGrid.Children)
                    tb.SetResourceReference(Control.ForegroundProperty, "SystemControlForegroundBaseMediumBrush");

                TextBlock TextBlock = (TextBlock)PowerModeGrid.Children[value];
                TextBlock.SetResourceReference(Control.ForegroundProperty, "AccentButtonBackground");
            });

            MainWindow.performanceManager.RequestPowerMode((int)PowerModeSlider.Value);

            if (!SettingsManager.IsInitialized)
                return;

            SettingsManager.SetProperty("QuickToolsPowerModeValue", value);
        }

        private void ComboBoxResolution_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ScreenResolution resolution = (ScreenResolution)ComboBoxResolution.SelectedItem;

            ComboBoxFrequency.Items.Clear();
            foreach (ScreenFrequency frequency in resolution.frequencies)
                ComboBoxFrequency.Items.Add(frequency);

            // pick current frequency, if available
            ComboBoxFrequency.SelectedItem = DesktopManager.GetScreenFrequency();

            SetResolution();
        }

        private void ComboBoxFrequency_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
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
            DesktopManager.SetResolution(resolution.width, resolution.height, frequency.frequency);
        }

        private void GPUSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!SettingsManager.GetBoolean("QuickToolsPerformanceGPUEnabled"))
                return;

            MainWindow.performanceManager.RequestGPUClock(GPUSlider.Value);

            if (!SettingsManager.IsInitialized)
                return;

            SettingsManager.SetProperty("QuickToolsPerformanceGPUValue", GPUSlider.Value);
        }
    }
}
