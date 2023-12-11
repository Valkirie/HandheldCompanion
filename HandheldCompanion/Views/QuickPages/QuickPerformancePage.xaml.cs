using HandheldCompanion.Devices;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Platforms;
using HandheldCompanion.Processors;
using HandheldCompanion.Utils;
using Inkore.UI.WPF.Modern.Controls;
using System;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Views.QuickPages;

/// <summary>
///     Interaction logic for QuickPerformancePage.xaml
/// </summary>
public partial class QuickPerformancePage : Page
{
    private const int UpdateInterval = 500;
    private readonly Timer UpdateTimer;
    private PowerProfile selectedProfile;

    private LockObject updateLock = new();

    public QuickPerformancePage(string Tag) : this()
    {
        this.Tag = Tag;
    }

    public QuickPerformancePage()
    {
        InitializeComponent();

        /*
        MainWindow.performanceManager.PowerModeChanged += PerformanceManager_PowerModeChanged;
        MainWindow.performanceManager.PerfBoostModeChanged += PerformanceManager_PerfBoostModeChanged;
        MainWindow.performanceManager.EPPChanged += PerformanceManager_EPPChanged;
        */

        SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

        PlatformManager.RTSS.Updated += RTSS_Updated;

        MainWindow.performanceManager.ProcessorStatusChanged += PerformanceManager_StatusChanged;
        MainWindow.performanceManager.EPPChanged += PerformanceManager_EPPChanged;
        MainWindow.performanceManager.Initialized += PerformanceManager_Initialized;

        HotkeysManager.CommandExecuted += HotkeysManager_CommandExecuted;

        PowerProfileManager.Updated += PowerProfileManager_Updated;
        PowerProfileManager.Deleted += PowerProfileManager_Deleted;

        // device settings
        GPUSlider.Minimum = MainWindow.CurrentDevice.GfxClock[0];
        GPUSlider.Maximum = MainWindow.CurrentDevice.GfxClock[1];

        CPUSlider.Minimum = MotherboardInfo.ProcessorMaxTurboSpeed / 4.0d;
        CPUSlider.Maximum = MotherboardInfo.ProcessorMaxTurboSpeed;

        // motherboard settings
        CPUCoreSlider.Maximum = MotherboardInfo.NumberOfCores;

        FanModeSoftware.IsEnabled = MainWindow.CurrentDevice.Capabilities.HasFlag(DeviceCapabilities.FanControl);

        UpdateTimer = new Timer(UpdateInterval);
        UpdateTimer.AutoReset = false;
        UpdateTimer.Elapsed += (sender, e) => SubmitProfile();

        // force call
        RTSS_Updated(PlatformManager.RTSS.Status);
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
                        if (selectedProfile is null || !selectedProfile.TDPOverrideEnabled)
                            return;

                        TDPSlider.Value++;
                    }
                    break;
                case "decreaseTDP":
                    {
                        if (selectedProfile is null || !selectedProfile.TDPOverrideEnabled)
                            return;

                        TDPSlider.Value--;
                    }
                    break;
            }
        });
    }

    private void PowerProfileManager_Deleted(PowerProfile profile)
    {
        // current power profile deleted, return to previous page
        bool isCurrent = selectedProfile?.Guid == profile.Guid;
        if (isCurrent)
            MainWindow.overlayquickTools.ContentFrame.GoBack();
    }

    private void PowerProfileManager_Updated(PowerProfile profile, UpdateSource source)
    {
        if (selectedProfile is null)
            return;

        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            // current power profile updated, update UI
            bool isCurrent = selectedProfile.Guid == profile.Guid;
            if (isCurrent)
                UpdateUI();
        });
    }

    private void RTSS_Updated(PlatformStatus status)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            switch (status)
            {
                case PlatformStatus.Ready:
                    var Processor = MainWindow.performanceManager.GetProcessor();
                    StackProfileAutoTDP.IsEnabled = true && Processor is not null ? Processor.CanChangeTDP : false;
                    break;
                case PlatformStatus.Stalled:
                    // StackProfileFramerate.IsEnabled = false;
                    // StackProfileAutoTDP.IsEnabled = false;
                    break;
            }
        });
    }

    public void UpdateProfile()
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

    /*
    private void PerformanceManager_PowerModeChanged(int idx)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() => { PowerModeSlider.Value = idx; });
    }

    private void PerformanceManager_PerfBoostModeChanged(bool value)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() => { CPUBoostToggle.IsOn = value; });
    }
    */

    private void SettingsManager_SettingValueChanged(string name, object value)
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

    private void CPUBoostToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (selectedProfile is null)
            return;

        if (updateLock)
            return;

        selectedProfile.CPUBoostEnabled = CPUBoostToggle.IsOn;
        UpdateProfile();
    }

    public void SelectionChanged(Guid guid)
    {
        // if an update is pending, cut it short, it will disturb profile selection though
        // keep me ?
        if (UpdateTimer.Enabled)
        {
            UpdateTimer.Stop();
            SubmitProfile();
        }

        selectedProfile = PowerProfileManager.GetProfile(guid);
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
                switch (selectedProfile.Default)
                {
                    case true:
                        // we shouldn't allow users to mess with default profile fan mode
                        FanMode.IsEnabled = false;
                        break;
                    case false:
                        FanMode.IsEnabled = true;
                        break;
                }

                // we shouldn't allow users to modify some of default profile settings
                Button_PowerSettings_Delete.IsEnabled = !selectedProfile.Default;

                // page name
                this.Title = selectedProfile.Name;

                // TDP
                TDPToggle.IsOn = selectedProfile.TDPOverrideEnabled;
                var TDP = selectedProfile.TDPOverrideValues is not null
                    ? selectedProfile.TDPOverrideValues
                    : MainWindow.CurrentDevice.nTDP;
                TDPSlider.Value = TDP[(int)PowerType.Slow];

                // CPU Clock control
                CPUToggle.IsOn = selectedProfile.CPUOverrideEnabled;
                CPUSlider.Value = selectedProfile.CPUOverrideValue != 0 ? selectedProfile.CPUOverrideValue : MotherboardInfo.ProcessorMaxTurboSpeed;

                // GPU Clock control
                GPUToggle.IsOn = selectedProfile.GPUOverrideEnabled;
                GPUSlider.Value = selectedProfile.GPUOverrideValue != 0 ? selectedProfile.GPUOverrideValue : 255 * 50;

                // AutoTDP
                AutoTDPToggle.IsOn = selectedProfile.AutoTDPEnabled;
                AutoTDPRequestedFPSSlider.Value = selectedProfile.AutoTDPRequestedFPS;

                // EPP
                EPPToggle.IsOn = selectedProfile.EPPOverrideEnabled;
                EPPSlider.Value = selectedProfile.EPPOverrideValue;

                // CPU Core Count
                CPUCoreToggle.IsOn = selectedProfile.CPUCoreEnabled;
                CPUCoreSlider.Value = selectedProfile.CPUCoreCount;

                // CPU Boost
                CPUBoostToggle.IsOn = selectedProfile.CPUBoostEnabled;

                // Power Mode
                PowerMode.SelectedIndex = Array.IndexOf(PerformanceManager.PowerModes, selectedProfile.OSPowerMode);

                // Fan control
                FanMode.SelectedIndex = (int)selectedProfile.FanProfile.fanMode;
            }
        });
    }

    private void TDPToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (selectedProfile is null)
            return;

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
        if (selectedProfile is null)
            return;

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
        if (selectedProfile is null)
            return;

        if (updateLock)
            return;

        selectedProfile.AutoTDPEnabled = AutoTDPToggle.IsOn;
        AutoTDPRequestedFPSSlider.Value = selectedProfile.AutoTDPRequestedFPS;

        UpdateProfile();
    }

    private void AutoTDPRequestedFPSSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (selectedProfile is null)
            return;

        if (updateLock)
            return;

        selectedProfile.AutoTDPRequestedFPS = (int)AutoTDPRequestedFPSSlider.Value;
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
        if (selectedProfile is null)
            return;

        if (updateLock)
            return;

        selectedProfile.GPUOverrideEnabled = GPUToggle.IsOn;
        selectedProfile.GPUOverrideValue = (int)GPUSlider.Value;
        UpdateProfile();
    }

    private void GPUSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (selectedProfile is null)
            return;

        if (updateLock)
            return;

        selectedProfile.GPUOverrideValue = (int)GPUSlider.Value;
        UpdateProfile();
    }

    private void EPPToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (selectedProfile is null)
            return;

        if (updateLock)
            return;

        selectedProfile.EPPOverrideEnabled = EPPToggle.IsOn;
        selectedProfile.EPPOverrideValue = (uint)EPPSlider.Value;
        UpdateProfile();
    }

    private void EPPSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (selectedProfile is null)
            return;

        if (updateLock)
            return;

        selectedProfile.EPPOverrideValue = (uint)EPPSlider.Value;
        UpdateProfile();
    }

    private void CPUCoreToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (selectedProfile is null)
            return;

        // wait until lock is released
        if (updateLock)
            return;

        selectedProfile.CPUCoreEnabled = CPUCoreToggle.IsOn;
        selectedProfile.CPUCoreCount = (int)CPUCoreSlider.Value;
        UpdateProfile();
    }

    private void CPUCoreSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (selectedProfile is null)
            return;

        if (!CPUCoreSlider.IsInitialized)
            return;

        // wait until lock is released
        if (updateLock)
            return;

        selectedProfile.CPUCoreCount = (int)CPUCoreSlider.Value;
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

    private async void Button_PowerSettings_Delete_Click(object sender, RoutedEventArgs e)
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
                break;
        }
    }
}