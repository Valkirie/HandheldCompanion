using HandheldCompanion.Devices;
using HandheldCompanion.Managers;
using HandheldCompanion.Platforms;
using Inkore.UI.WPF.Modern.Controls;
using System;
using System.Windows;
using System.Windows.Controls;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Views.QuickPages;

/// <summary>
///     Interaction logic for QuickDevicePage.xaml
/// </summary>
public partial class QuickDevicePage : Page
{
    public QuickDevicePage(string Tag) : this()
    {
        this.Tag = Tag;
    }

    public QuickDevicePage()
    {
        InitializeComponent();

        FullFanSpeedToggler.Visibility = MainWindow.CurrentDevice is LegionGo ? Visibility.Visible : Visibility.Collapsed;
        SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

        PlatformManager.RTSS.Updated += RTSS_Updated;
        PlatformManager.HWiNFO.Updated += HWiNFO_Updated;

        // force call
        // todo: make PlatformManager static
        RTSS_Updated(PlatformManager.RTSS.Status);
        HWiNFO_Updated(PlatformManager.HWiNFO.Status);
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
                    ComboBoxOverlayDisplayLevel.SelectedIndex = Convert.ToInt32(value);
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

    private void Toggle_cFFanSpeed_Toggled(object sender, RoutedEventArgs e)
    {
        if (MainWindow.CurrentDevice is LegionGo device)
        {
            ToggleSwitch toggleSwitch = (ToggleSwitch)sender;
            device.SetFanFullSpeed(toggleSwitch.IsOn);
        }
    }
}