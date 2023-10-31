using HandheldCompanion.Managers;
using HandheldCompanion.Platforms;
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

        SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

        PlatformManager.rTSS.Updated += RTSS_Updated;
        PlatformManager.hWiNFO.Updated += HWiNFO_Updated;

        // force call
        // todo: make PlatformManager static
        RTSS_Updated(PlatformManager.rTSS.Status);
        HWiNFO_Updated(PlatformManager.hWiNFO.Status);
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
}