using HandheldCompanion.Managers;
using HandheldCompanion.Platforms;
using System;
using System.Windows;
using System.Windows.Controls;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Views.QuickPages;

/// <summary>
///     Interaction logic for QuickOverlayPage.xaml
/// </summary>
public partial class QuickOverlayPage : Page
{
    public QuickOverlayPage(string Tag) : this()
    {
        this.Tag = Tag;
    }

    public QuickOverlayPage()
    {
        InitializeComponent();

        SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

        PlatformManager.RTSS.Updated += RTSS_Updated;
        PlatformManager.LibreHardwareMonitor.Updated += LibreHardwareMonitor_Updated;

        // force call
        // todo: make PlatformManager static
        RTSS_Updated(PlatformManager.RTSS.Status);
        LibreHardwareMonitor_Updated(PlatformManager.LibreHardwareMonitor.Status);
    }

    private void LibreHardwareMonitor_Updated(PlatformStatus status)
    {
        // UI thread (async)
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            switch (status)
            {
                case PlatformStatus.Ready:
                    OverlayDisplayLevelExtended.IsEnabled = true;
                    OverlayDisplayLevelFull.IsEnabled = true;
                    break;
                case PlatformStatus.Stalled:
                    // OverlayDisplayLevelExtended.IsEnabled = false;
                    // OverlayDisplayLevelFull.IsEnabled = false;
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
                    ComboBoxOverlayDisplayLevel.SelectedIndex = 0;
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
                case "OnScreenDisplayLevel":
                    var index = Convert.ToInt32(value);
                    ComboBoxOverlayDisplayLevel.SelectedIndex = index;
                    StackCustomSettings.Visibility = index == 4 ? Visibility.Visible : Visibility.Collapsed;
                    break;
                case "OnScreenDisplayTimeLevel":
                    ComboBoxOnScreenDisplayTimeLevel.SelectedIndex = Convert.ToInt32(value);
                    break;
                case "OnScreenDisplayFPSLevel":
                    ComboBoxOnScreenDisplayFPSLevel.SelectedIndex = Convert.ToInt32(value);
                    break;
                case "OnScreenDisplayCPULevel":
                    ComboBoxOnScreenDisplayCPULevel.SelectedIndex = Convert.ToInt32(value);
                    break;
                case "OnScreenDisplayGPULevel":
                    ComboBoxOnScreenDisplayGPULevel.SelectedIndex = Convert.ToInt32(value);
                    break;
                case "OnScreenDisplayRAMLevel":
                    ComboBoxOnScreenDisplayRAMLevel.SelectedIndex = Convert.ToInt32(value);
                    break;
                case "OnScreenDisplayVRAMLevel":
                    ComboBoxOnScreenDisplayVRAMLevel.SelectedIndex = Convert.ToInt32(value);
                    break;
                case "OnScreenDisplayBATTLevel":
                    ComboBoxOnScreenDisplayBATTLevel.SelectedIndex = Convert.ToInt32(value);
                    break;
            }
        });
    }

    private void ComboBoxOverlayDisplayLevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        SettingsManager.SetProperty("OnScreenDisplayLevel", ComboBoxOverlayDisplayLevel.SelectedIndex);
    }

    private void ComboBoxOnScreenDisplayTimeLevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        SettingsManager.SetProperty("OnScreenDisplayTimeLevel", ComboBoxOnScreenDisplayCPULevel.SelectedIndex);
    }

    private void ComboBoxOnScreenDisplayFPSLevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        SettingsManager.SetProperty("OnScreenDisplayFPSLevel", ComboBoxOnScreenDisplayCPULevel.SelectedIndex);
    }

    private void ComboBoxOnScreenDisplayCPULevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        SettingsManager.SetProperty("OnScreenDisplayCPULevel", ComboBoxOnScreenDisplayCPULevel.SelectedIndex);
    }

    private void ComboBoxOnScreenDisplayRAMLevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        SettingsManager.SetProperty("OnScreenDisplayRAMLevel", ComboBoxOnScreenDisplayRAMLevel.SelectedIndex);
    }

    private void ComboBoxOnScreenDisplayGPULevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        SettingsManager.SetProperty("OnScreenDisplayGPULevel", ComboBoxOnScreenDisplayGPULevel.SelectedIndex);
    }

    private void ComboBoxOnScreenDisplayVRAMLevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        SettingsManager.SetProperty("OnScreenDisplayVRAMLevel", ComboBoxOnScreenDisplayVRAMLevel.SelectedIndex);
    }

    private void ComboBoxOnScreenDisplayBATTLevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        SettingsManager.SetProperty("OnScreenDisplayBATTLevel", ComboBoxOnScreenDisplayBATTLevel.SelectedIndex);
    }
}