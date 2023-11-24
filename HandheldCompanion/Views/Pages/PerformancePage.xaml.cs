using HandheldCompanion.Devices;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Processors;
using HandheldCompanion.Utils;
using Inkore.UI.WPF.Modern.Controls;
using LiveCharts;
using LiveCharts.Definitions.Series;
using LiveCharts.Helpers;
using LiveCharts.Wpf;
using System;
using System.Collections.Specialized;
using System.Linq;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Views.Pages
{
    /// <summary>
    /// Interaction logic for PerformancePage.xaml
    /// </summary>
    public partial class PerformancePage : Page
    {
        private ChartPoint storedChartPoint;
        private PowerProfile selectedProfile;

        private LockObject updateLock = new();

        private const int UpdateInterval = 500;
        private static Timer UpdateTimer;

        public PerformancePage()
        {
            InitializeComponent();

            DataContext = new ViewModel();
            lvLineSeries.ActualValues.CollectionChanged += ActualValues_CollectionChanged;

            UpdateTimer = new Timer(UpdateInterval);
            UpdateTimer.AutoReset = false;
            UpdateTimer.Elapsed += (sender, e) => SubmitProfile();
        }

        public PerformancePage(string? Tag) : this()
        {
            this.Tag = Tag;

            SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
            PowerProfileManager.Updated += PowerProfileManager_Updated;
            PowerProfileManager.Deleted += PowerProfileManager_Deleted;

            MainWindow.performanceManager.ProcessorStatusChanged += PerformanceManager_StatusChanged;
            MainWindow.performanceManager.EPPChanged += PerformanceManager_EPPChanged;
            MainWindow.performanceManager.Initialized += PerformanceManager_Initialized;

            // device settings
            GPUSlider.Minimum = MainWindow.CurrentDevice.GfxClock[0];
            GPUSlider.Maximum = MainWindow.CurrentDevice.GfxClock[1];

            CPUSlider.Minimum = MotherboardInfo.ProcessorMaxClockSpeed / 4.0d;
            CPUSlider.Maximum = MotherboardInfo.ProcessorMaxClockSpeed;

            // motherboard settings
            CPUCoreSlider.Maximum = MotherboardInfo.NumberOfCores;

            FanModeSoftware.IsEnabled = MainWindow.CurrentDevice.Capabilities.HasFlag(DeviceCapabilities.FanControl);
        }

        private void Page_Loaded(object? sender, RoutedEventArgs? e)
        {
        }

        public void Page_Closed()
        {
        }

        private void PowerProfileManager_Deleted(PowerProfile profile)
        {
            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                var idx = -1;
                foreach (var item in ProfilesPicker.Items)
                {
                    if (item is not ComboBoxItem)
                        continue;

                    // get power profile
                    var parent = (ComboBoxItem)item;
                    if (parent.Content is not PowerProfile)
                        continue;

                    PowerProfile pr = (PowerProfile)parent.Content;

                    bool isCurrent = pr.Guid == profile.Guid;
                    if (isCurrent)
                    {
                        idx = ProfilesPicker.Items.IndexOf(parent);
                        break;
                    }
                }

                if (idx != -1)
                    ProfilesPicker.Items.RemoveAt(idx);
            });
        }

        private void PowerProfileManager_Updated(PowerProfile profile, UpdateSource source)
        {
            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                var idx = -1;
                foreach (var item in ProfilesPicker.Items)
                {
                    if (item is not ComboBoxItem)
                        continue;

                    // get power profile
                    var parent = (ComboBoxItem)item;
                    if (parent.Content is not PowerProfile)
                        continue;

                    PowerProfile pr = (PowerProfile)parent.Content;

                    bool isCurrent = pr.Guid == profile.Guid;
                    if (isCurrent)
                    {
                        idx = ProfilesPicker.Items.IndexOf(parent);
                        break;
                    }
                }

                ComboBoxItem comboBoxItem = new ComboBoxItem { Content = profile, Margin = new Thickness(15, 0, 15, 0) };

                if (idx != -1)
                {
                    // found it
                    ProfilesPicker.Items[idx] = comboBoxItem;
                }
                else
                {
                    // new entry
                    if (profile.IsDefault())
                        idx = ProfilesPicker.Items.IndexOf(OEMProfiles) + 1;
                    else
                        idx = ProfilesPicker.Items.IndexOf(UserProfiles) + 1;

                    ProfilesPicker.Items.Insert(idx, comboBoxItem);
                }

                ProfilesPicker.Items.Refresh();
                ProfilesPicker.SelectedItem = comboBoxItem;
            });
        }

        private void SettingsManager_SettingValueChanged(string? name, object value)
        {
            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                switch (name)
                {
                    case "ConfigurableTDPOverrideDown":
                        {
                            using (new ScopedLock(updateLock))
                            {
                                TDPSlider.Minimum = (double)value;
                            }
                        }
                        break;
                    case "ConfigurableTDPOverrideUp":
                        {
                            using (new ScopedLock(updateLock))
                            {
                                TDPSlider.Maximum = (double)value;
                            }
                        }
                        break;
                }
            });
        }

        public static void UpdateProfile()
        {
            if (UpdateTimer is not null)
            {
                UpdateTimer.Stop();
                UpdateTimer.Start();
            }
        }

        public void SubmitProfile(UpdateSource source = UpdateSource.ProfilesPage)
        {
            if (selectedProfile is null)
                return;

            PowerProfileManager.UpdateOrCreateProfile(selectedProfile, source);
        }

        private void PerformanceManager_StatusChanged(bool CanChangeTDP, bool CanChangeGPU)
        {
            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                StackProfileTDP.IsEnabled = CanChangeTDP;
                StackProfileAutoTDP.IsEnabled = CanChangeTDP && PlatformManager.RTSS.IsInstalled;

                StackProfileGPUClock.IsEnabled = CanChangeGPU;
            });
        }

        private void PerformanceManager_EPPChanged(uint EPP)
        {
            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                EPPSlider.Value = EPP;
            });
        }

        private void PerformanceManager_Initialized()
        {
            Processor processor = MainWindow.performanceManager.GetProcessor();
            if (processor is null)
                return;

            PerformanceManager_StatusChanged(processor.CanChangeTDP, processor.CanChangeGPU);
        }

        private void ChartOnDataClick(object sender, ChartPoint p)
        {
            if (p is null)
                return;

            // store current point
            storedChartPoint = p;
        }

        private void ChartOnUpdaterTick(object sender)
        {
            // do something
        }

        private void ActualValues_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // wait until lock is released
            if (updateLock)
                return;

            if (selectedProfile is null)
                return;

            for (int idx = 0; idx < lvLineSeries.ActualValues.Count; idx++)
                selectedProfile.FanProfile.fanSpeeds[idx] = (double)lvLineSeries.ActualValues[idx];

            UpdateProfile();
        }

        private void ChartMovePoint(Point point)
        {
            if (storedChartPoint is not null)
            {
                double pointY = Math.Max(0, Math.Min(100, point.Y));

                // update current poing value
                lvLineSeries.ActualValues[storedChartPoint.Key] = pointY;

                // prevent higher values from having lower fan speed
                for (int i = storedChartPoint.Key; i < lvLineSeries.ActualValues.Count; i++)
                {
                    if ((double)lvLineSeries.ActualValues[i] < pointY)
                        lvLineSeries.ActualValues[i] = pointY;
                }

                // prevent lower values from having higher fan speed
                for (int i = storedChartPoint.Key; i >= 0; i--)
                {
                    if ((double)lvLineSeries.ActualValues[i] > pointY)
                        lvLineSeries.ActualValues[i] = pointY;
                }
            }

            ISeriesView series = lvc.Series.FirstOrDefault();
            ChartPoint closestPoint = series.ClosestPointTo(point.X, AxisOrientation.X);

            ViewModel vm = (ViewModel)DataContext;
            vm.YPointer = closestPoint.Y;
            vm.XPointer = closestPoint.X;
        }

        private void ChartMouseMove(object sender, MouseEventArgs e)
        {
            Point point = lvc.ConvertToChartValues(e.GetPosition(lvc));
            ChartMovePoint(point);
        }

        private void ChatTouchMove(object sender, TouchEventArgs e)
        {
            Point point = lvc.ConvertToChartValues(e.GetTouchPoint(lvc).Position);
            ChartMovePoint(point);
            e.Handled = true;
        }

        private void ChartMouseUp(object sender, MouseButtonEventArgs e)
        {
            storedChartPoint = null;
        }

        private void CharMouseLeave(object sender, MouseEventArgs e)
        {
            storedChartPoint = null;
        }

        private void ButtonProfileCreate_Click(object sender, RoutedEventArgs e)
        {
            int idx = PowerProfileManager.profiles.Values.Where(p => !p.IsDefault()).Count() + 1;

            string Name = string.Format(Properties.Resources.PowerProfileManualName, idx);
            PowerProfile powerProfile = new PowerProfile(Name, Properties.Resources.PowerProfileManualDescription);

            PowerProfileManager.UpdateOrCreateProfile(powerProfile, UpdateSource.Creation);
        }

        private async void ButtonProfileDelete_Click(object sender, RoutedEventArgs e)
        {
            var result = Dialog.ShowAsync(
                $"{Properties.Resources.ProfilesPage_AreYouSureDelete1} \"{selectedProfile.Name}\"?",
                $"{Properties.Resources.ProfilesPage_AreYouSureDelete2}",
                ContentDialogButton.Primary,
                $"{Properties.Resources.ProfilesPage_Cancel}",
                $"{Properties.Resources.ProfilesPage_Delete}");
            await result; // sync call

            switch (result.Result)
            {
                case ContentDialogResult.Primary:
                    PowerProfileManager.DeleteProfile(selectedProfile);
                    ProfilesPicker.SelectedIndex = 1;
                    break;
            }
        }

        private void ButtonProfileEdit_Click(object sender, RoutedEventArgs e)
        {
            PowerProfileSettingsDialog.ShowAsync();
        }

        public void SelectionChanged(Guid guid)
        {
            var idx = -1;
            foreach (var item in ProfilesPicker.Items)
            {
                if (item is not ComboBoxItem)
                    continue;

                // get power profile
                var parent = (ComboBoxItem)item;
                if (parent.Content is not PowerProfile)
                    continue;

                PowerProfile pr = (PowerProfile)parent.Content;

                bool isCurrent = pr.Guid == guid;
                if (isCurrent)
                {
                    idx = ProfilesPicker.Items.IndexOf(parent);
                    break;
                }
            }

            if (idx != -1)
                ProfilesPicker.SelectedIndex = idx;
        }

        private void ProfilesPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProfilesPicker.SelectedItem is null)
                return;

            // if an update is pending, cut it short, it will disturb profile selection though
            if (UpdateTimer.Enabled)
            {
                UpdateTimer.Stop();
                SubmitProfile();
            }

            ComboBoxItem comboBoxItem = (ComboBoxItem)ProfilesPicker.SelectedItem;
            if (comboBoxItem.Content is not PowerProfile)
                return;

            selectedProfile = (PowerProfile)comboBoxItem.Content;

            UpdateUI();
        }

        private void UpdateUI()
        {
            if (selectedProfile is null)
                return;

            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                using (new ScopedLock(updateLock))
                {
                    // update PowerProfile settings
                    PowerProfileName.Text = selectedProfile.Name;
                    PowerProfileDescription.Text = selectedProfile.Description;

                    // we shouldn't allow users to modify some of default profile settings
                    FanMode.IsEnabled = !selectedProfile.Default;
                    ButtonProfileDelete.IsEnabled = !selectedProfile.Default;
                    ButtonProfileMore.IsEnabled = !selectedProfile.Default;

                    // Sustained TDP settings (slow, stapm, long)
                    TDPToggle.IsOn = selectedProfile.TDPOverrideEnabled;
                    var TDP = selectedProfile.TDPOverrideValues is not null
                        ? selectedProfile.TDPOverrideValues
                        : MainWindow.CurrentDevice.nTDP;
                    TDPSlider.Value = TDP[(int)PowerType.Slow];

                    // define slider(s) min and max values based on device specifications
                    TDPSlider.Minimum = SettingsManager.GetInt("ConfigurableTDPOverrideDown");
                    TDPSlider.Maximum = SettingsManager.GetInt("ConfigurableTDPOverrideUp");

                    // Automatic TDP
                    AutoTDPToggle.IsOn = selectedProfile.AutoTDPEnabled;
                    AutoTDPSlider.Value = (int)selectedProfile.AutoTDPRequestedFPS;

                    // EPP
                    EPPToggle.IsOn = selectedProfile.EPPOverrideEnabled;
                    EPPSlider.Value = selectedProfile.EPPOverrideValue;

                    // CPU Core Count
                    CPUCoreToggle.IsOn = selectedProfile.CPUCoreEnabled;
                    CPUCoreSlider.Value = selectedProfile.CPUCoreCount;

                    // CPU Clock control
                    CPUToggle.IsOn = selectedProfile.CPUOverrideEnabled;
                    CPUSlider.Value = selectedProfile.CPUOverrideValue != 0 ? selectedProfile.CPUOverrideValue : MotherboardInfo.ProcessorMaxClockSpeed;

                    // GPU Clock control
                    GPUToggle.IsOn = selectedProfile.GPUOverrideEnabled;
                    GPUSlider.Value = selectedProfile.GPUOverrideValue != 0 ? selectedProfile.GPUOverrideValue : 255 * 50;

                    // CPU Boost
                    CPUBoostToggle.IsOn = selectedProfile.CPUBoostEnabled;

                    // Power Mode
                    PowerMode.SelectedIndex = Array.IndexOf(PerformanceManager.PowerModes, selectedProfile.OSPowerMode);

                    // Fan control
                    FanMode.SelectedIndex = (int)selectedProfile.FanProfile.fanMode;

                    // update charts
                    for (int idx = 0; idx < lvLineSeries.ActualValues.Count; idx++)
                        lvLineSeries.ActualValues[idx] = selectedProfile.FanProfile.fanSpeeds[idx];
                }
            });
        }

        private void PowerProfileSettingsDialog_CloseButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (selectedProfile is null)
                return;

            // restore settings
            PowerProfileName.Text = selectedProfile.Name;
            PowerProfileDescription.Text = selectedProfile.Description;
        }

        private void PowerProfileSettingsDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (selectedProfile is null)
                return;

            // update power profile settings
            selectedProfile.Name = PowerProfileName.Text;
            selectedProfile.Description = PowerProfileDescription.Text;
            UpdateProfile();
        }

        private void TDPToggle_Toggled(object sender, RoutedEventArgs e)
        {
            // wait until lock is released
            if (updateLock)
                return;

            selectedProfile.TDPOverrideEnabled = TDPToggle.IsOn;
            selectedProfile.TDPOverrideValues = new double[3]
            {
                TDPSlider.Value,
                TDPSlider.Value,
                TDPSlider.Value
            };

            UpdateProfile();
        }

        private void TDPSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!TDPSlider.IsInitialized)
                return;

            // wait until lock is released
            if (updateLock)
                return;

            selectedProfile.TDPOverrideValues = new double[3]
            {
                TDPSlider.Value,
                TDPSlider.Value,
                TDPSlider.Value
            };

            UpdateProfile();
        }

        private void AutoTDPToggle_Toggled(object sender, RoutedEventArgs e)
        {
            // wait until lock is released
            if (updateLock)
                return;

            selectedProfile.AutoTDPEnabled = AutoTDPToggle.IsOn;
            UpdateProfile();
        }

        private void AutoTDPSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!AutoTDPSlider.IsInitialized)
                return;

            // wait until lock is released
            if (updateLock)
                return;

            selectedProfile.AutoTDPRequestedFPS = (int)AutoTDPSlider.Value;
            UpdateProfile();
        }

        private void CPUToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (selectedProfile is null)
                return;

            if (updateLock)
                return;

            selectedProfile.CPUOverrideEnabled = CPUToggle.IsOn;
            selectedProfile.CPUOverrideValue = (int)CPUSlider.Value;
            UpdateProfile();
        }

        private void CPUSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (selectedProfile is null)
                return;

            if (updateLock)
                return;

            selectedProfile.CPUOverrideValue = (int)CPUSlider.Value;
            UpdateProfile();
        }

        private void GPUToggle_Toggled(object sender, RoutedEventArgs e)
        {
            // wait until lock is released
            if (updateLock)
                return;

            selectedProfile.GPUOverrideEnabled = GPUToggle.IsOn;
            selectedProfile.GPUOverrideValue = (int)GPUSlider.Value;
            UpdateProfile();
        }

        private void GPUSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!GPUSlider.IsInitialized)
                return;

            if (selectedProfile is null)
                return;

            // wait until lock is released
            if (updateLock)
                return;

            selectedProfile.GPUOverrideValue = (int)GPUSlider.Value;
            UpdateProfile();
        }

        private void EPPToggle_Toggled(object sender, RoutedEventArgs e)
        {
            // wait until lock is released
            if (updateLock)
                return;

            selectedProfile.EPPOverrideEnabled = EPPToggle.IsOn;
            selectedProfile.EPPOverrideValue = (uint)EPPSlider.Value;
            UpdateProfile();
        }

        private void EPPSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!EPPSlider.IsInitialized)
                return;

            // wait until lock is released
            if (updateLock)
                return;

            selectedProfile.EPPOverrideValue = (uint)EPPSlider.Value;
            UpdateProfile();
        }

        private void CPUCoreToggle_Toggled(object sender, RoutedEventArgs e)
        {
            // wait until lock is released
            if (updateLock)
                return;

            selectedProfile.CPUCoreEnabled = CPUCoreToggle.IsOn;
            selectedProfile.CPUCoreCount = (int)CPUCoreSlider.Value;
            UpdateProfile();
        }

        private void CPUCoreSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!CPUCoreSlider.IsInitialized)
                return;

            // wait until lock is released
            if (updateLock)
                return;

            selectedProfile.CPUCoreCount = (int)CPUCoreSlider.Value;
            UpdateProfile();
        }

        private void CPUBoostToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (selectedProfile is null)
                return;

            if (updateLock)
                return;

            selectedProfile.CPUBoostEnabled = CPUBoostToggle.IsOn;
            UpdateProfile();
        }

        private void PowerMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PowerMode.SelectedIndex == -1)
                return;

            if (selectedProfile is null)
                return;

            // wait until lock is released
            if (updateLock)
                return;

            selectedProfile.OSPowerMode = PerformanceManager.PowerModes[PowerMode.SelectedIndex];
            UpdateProfile();
        }

        private void FanMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FanMode.SelectedIndex == -1)
                return;

            if (selectedProfile is null)
                return;

            // wait until lock is released
            if (updateLock)
                return;

            selectedProfile.FanProfile.fanMode = (FanMode)FanMode.SelectedIndex;
            UpdateProfile();
        }

        private void Expander_Expanded(object sender, RoutedEventArgs e)
        {
            ((Expander)sender).BringIntoView();
        }

        private void PowerProfilePresetSilent_Click(object sender, RoutedEventArgs e)
        {
            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                // update charts
                for (int idx = 0; idx < lvLineSeries.ActualValues.Count; idx++)
                    lvLineSeries.ActualValues[idx] = MainWindow.CurrentDevice.fanPresets[0][idx];
            });
        }

        private void PowerProfilePresetPerformance_Click(object sender, RoutedEventArgs e)
        {
            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                // update charts
                for (int idx = 0; idx < lvLineSeries.ActualValues.Count; idx++)
                    lvLineSeries.ActualValues[idx] = MainWindow.CurrentDevice.fanPresets[1][idx];
            });
        }

        private void PowerProfilePresetTurbo_Click(object sender, RoutedEventArgs e)
        {
            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                // update charts
                for (int idx = 0; idx < lvLineSeries.ActualValues.Count; idx++)
                    lvLineSeries.ActualValues[idx] = MainWindow.CurrentDevice.fanPresets[2][idx];
            });
        }
    }
}
