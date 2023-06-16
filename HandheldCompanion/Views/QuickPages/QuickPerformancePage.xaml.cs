using System;
using System.Windows;
using System.Windows.Controls;
using ControllerCommon.Devices;
using ControllerCommon.Platforms;
using HandheldCompanion.Managers;
using HandheldCompanion.Managers.Desktop;
using Inkore.UI.WPF.Modern.Controls;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Views.QuickPages;

/// <summary>
///     Interaction logic for QuickPerformancePage.xaml
/// </summary>
public partial class QuickPerformancePage : Page
{
    public QuickPerformancePage(string Tag) : this()
    {
        this.Tag = Tag;
    }

    public QuickPerformancePage()
    {
        InitializeComponent();

        MainWindow.performanceManager.PowerModeChanged += PerformanceManager_PowerModeChanged;
        MainWindow.performanceManager.PerfBoostModeChanged += PerformanceManager_PerfBoostModeChanged;

        SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

        SystemManager.PrimaryScreenChanged += DesktopManager_PrimaryScreenChanged;
        SystemManager.DisplaySettingsChanged += DesktopManager_DisplaySettingsChanged;

        PlatformManager.RTSS.Updated += RTSS_Updated;
        PlatformManager.HWiNFO.Updated += HWiNFO_Updated;

        // force call
        // todo: make PlatformManager static
        RTSS_Updated(PlatformManager.RTSS.Status);
        HWiNFO_Updated(PlatformManager.HWiNFO.Status);

        // todo: move me ?
        SettingsManager.SetProperty("QuietModeEnabled",
            MainWindow.CurrentDevice.Capacities.HasFlag(DeviceCapacities.FanControl));
    }

    private void HWiNFO_Updated(PlatformStatus status)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            switch (status)
            {
                case PlatformStatus.Ready:
                    OverlayDisplayLevelExtended.IsEnabled = true;
                    OverlayDisplayLevelFull.IsEnabled = true;
                    OverlayDisplayLevelAutoTDP.IsEnabled = true;
                    break;
                case PlatformStatus.Stalled:
                    OverlayDisplayLevelExtended.IsEnabled = false;
                    OverlayDisplayLevelFull.IsEnabled = false;
                    OverlayDisplayLevelAutoTDP.IsEnabled = false;
                    break;
            }
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
                    ComboBoxOverlayDisplayLevel.IsEnabled = true;
                    break;
                case PlatformStatus.Stalled:
                    ComboBoxOverlayDisplayLevel.IsEnabled = false;
                    break;
            }
        });
    }

    private void DesktopManager_PrimaryScreenChanged(DesktopScreen screen)
    {
        ComboBoxResolution.Items.Clear();
        foreach (var resolution in screen.resolutions)
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
        Application.Current.Dispatcher.BeginInvoke(() => { PowerModeSlider.Value = idx; });
    }

    private void PerformanceManager_PerfBoostModeChanged(bool value)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() => { CPUBoostToggle.IsOn = value; });
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
        var idx = (int)PowerModeSlider.Value;

        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            foreach (TextBlock tb in PowerModeGrid.Children)
                tb.SetResourceReference(Control.ForegroundProperty, "SystemControlForegroundBaseMediumBrush");

            var TextBlock = (TextBlock)PowerModeGrid.Children[idx];
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

        var resolution = (ScreenResolution)ComboBoxResolution.SelectedItem;

        ComboBoxFrequency.Items.Clear();
        foreach (var frequency in resolution.Frequencies.Values)
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

        var resolution = (ScreenResolution)ComboBoxResolution.SelectedItem;
        var frequency = (ScreenFrequency)ComboBoxFrequency.SelectedItem;

        // update current screen resolution
        SystemManager.SetResolution(resolution.Width, resolution.Height, (int)frequency.GetValue(Frequency.Full), resolution.BitsPerPel);
    }

    private async void QuietModeToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        var Disclosure = SettingsManager.GetBoolean("QuietModeDisclosure");
        if (QuietModeToggle.IsOn && !Disclosure)
        {
            // todo: localize me !
            var result = Dialog.ShowAsync(
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
        var value = QuietModeSlider.Value;
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

        var value = CPUBoostToggle.IsOn;
        MainWindow.performanceManager.RequestPerfBoostMode(value);
    }
}