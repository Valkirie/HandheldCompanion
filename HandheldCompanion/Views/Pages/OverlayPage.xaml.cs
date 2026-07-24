using HandheldCompanion.Helpers;
using HandheldCompanion.Managers;
using HandheldCompanion.Platforms;
using HandheldCompanion.ViewModels;
using iNKORE.UI.WPF.Controls;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Views.Pages;

/// <summary>
///     Interaction logic for OverlayPage.xaml
/// </summary>
public partial class OverlayPage : Page
{
    private readonly OverlayPageViewModel ViewModel;

    public OverlayPage()
    {
        ViewModel = new OverlayPageViewModel(false);
        DataContext = ViewModel;
        InitializeComponent();
        Loaded += OverlayPage_Loaded;
        Unloaded += OverlayPage_Unloaded;

        // manage events
        switch (ManagerFactory.settingsManager.Status)
        {
            default:
            case ManagerStatus.Initializing:
                ManagerFactory.settingsManager.Initialized += SettingsManager_Initialized;
                break;
            case ManagerStatus.Initialized:
                QuerySettings();
                break;
        }

        // raise events
        switch (ManagerFactory.platformManager.Status)
        {
            default:
            case ManagerStatus.Initializing:
                ManagerFactory.platformManager.Initialized += PlatformManager_Initialized;
                break;
            case ManagerStatus.Initialized:
                QueryPlatforms();
                break;
        }
    }

    private void SettingsManager_Initialized()
    {
        QuerySettings();
    }

    private void QuerySettings()
    {
        // manage events
        ManagerFactory.settingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

        // raise events
        SettingsManager_SettingValueChanged("OverlayModel", ManagerFactory.settingsManager.GetString("OverlayModel"), false, true);
        SettingsManager_SettingValueChanged("OverlayControllerAlignment", ManagerFactory.settingsManager.GetString("OverlayControllerAlignment"), false, true);
        SettingsManager_SettingValueChanged("OverlayControllerSize", ManagerFactory.settingsManager.GetString("OverlayControllerSize"), false, true);
        SettingsManager_SettingValueChanged("OverlayRenderAntialiasing", ManagerFactory.settingsManager.GetString("OverlayRenderAntialiasing"), false, true);
        SettingsManager_SettingValueChanged("OverlayTrackpadsSize", ManagerFactory.settingsManager.GetString("OverlayTrackpadsSize"), false, true);
        SettingsManager_SettingValueChanged("OverlayFaceCamera", ManagerFactory.settingsManager.GetString("OverlayFaceCamera"), false, true);
        SettingsManager_SettingValueChanged("OverlayControllerRestingPitch", ManagerFactory.settingsManager.GetString("OverlayControllerRestingPitch"), false, true);
        SettingsManager_SettingValueChanged("OverlayTrackpadsAlignment", ManagerFactory.settingsManager.GetString("OverlayTrackpadsAlignment"), false, true);
        SettingsManager_SettingValueChanged("OverlayTrackpadsOpacity", ManagerFactory.settingsManager.GetString("OverlayTrackpadsOpacity"), false, true);
        SettingsManager_SettingValueChanged("OverlayControllerOpacity", ManagerFactory.settingsManager.GetString("OverlayControllerOpacity"), false, true);
        SettingsManager_SettingValueChanged("OverlayControllerBackgroundColor", ManagerFactory.settingsManager.GetString("OverlayControllerBackgroundColor"), false, true);
        SettingsManager_SettingValueChanged("OverlayControllerAlwaysOnTop", ManagerFactory.settingsManager.GetString("OverlayControllerAlwaysOnTop"), false, true);
        SettingsManager_SettingValueChanged("OverlayControllerMotion", ManagerFactory.settingsManager.GetString("OverlayControllerMotion"), false, true);
        SettingsManager_SettingValueChanged("OnScreenDisplayRefreshRate", ManagerFactory.settingsManager.GetString("OnScreenDisplayRefreshRate"), false, true);
        SettingsManager_SettingValueChanged("OnScreenDisplayLevel", ManagerFactory.settingsManager.GetString("OnScreenDisplayLevel"), false, true);
        SettingsManager_SettingValueChanged("OnScreenDisplayTimeLevel", ManagerFactory.settingsManager.GetString("OnScreenDisplayTimeLevel"), false, true);
        SettingsManager_SettingValueChanged("OnScreenDisplayFPSLevel", ManagerFactory.settingsManager.GetString("OnScreenDisplayFPSLevel"), false, true);
        SettingsManager_SettingValueChanged("OnScreenDisplayCPULevel", ManagerFactory.settingsManager.GetString("OnScreenDisplayCPULevel"), false, true);
        SettingsManager_SettingValueChanged("OnScreenDisplayGPULevel", ManagerFactory.settingsManager.GetString("OnScreenDisplayGPULevel"), false, true);
        SettingsManager_SettingValueChanged("OnScreenDisplayRAMLevel", ManagerFactory.settingsManager.GetString("OnScreenDisplayRAMLevel"), false, true);
        SettingsManager_SettingValueChanged("OnScreenDisplayVRAMLevel", ManagerFactory.settingsManager.GetString("OnScreenDisplayVRAMLevel"), false, true);
        SettingsManager_SettingValueChanged("OnScreenDisplayBATTLevel", ManagerFactory.settingsManager.GetString("OnScreenDisplayBATTLevel"), false, true);
    }

    private void QueryPlatforms()
    {
        // manage events
        PlatformManager.RTSS.Updated += RTSS_Updated;

        RTSS_Updated(PlatformManager.RTSS.Status);
    }

    private void PlatformManager_Initialized()
    {
        QueryPlatforms();
    }

    public OverlayPage(string Tag) : this()
    {
        this.Tag = Tag;
        this.Loaded += OverlayPage_Loaded;
        this.Unloaded += OverlayPage_Unloaded;
    }

    private void RTSS_Updated(PlatformStatus status)
    {
        // UI thread
        UIHelper.TryBeginInvoke(() =>
        {
            switch (status)
            {
                case PlatformStatus.Stalled:
                    OnScreenDisplayLevel.SelectedIndex = 0;
                    break;
            }
        });
    }

    private void OverlayPage_Loaded(object sender, RoutedEventArgs e) => ViewModel.OnPageLoaded();

    private void OverlayPage_Unloaded(object sender, RoutedEventArgs e) => ViewModel.OnPageUnloaded();

    private void SettingsManager_SettingValueChanged(string name, object? value, bool temporary, bool initializing)
    {
        if (temporary)
            return;

        // UI thread
        UIHelper.TryBeginInvoke(() =>
        {
            switch (name)
            {
                case "OverlayModel":
                    OverlayModel.SelectedIndex = Convert.ToInt32(value);
                    break;
                case "OverlayControllerAlignment":
                    UpdateUI_ControllerPosition(Convert.ToInt32(value));
                    break;
                case "OverlayControllerSize":
                    SliderControllerSize.Value = Convert.ToDouble(value);
                    break;
                case "OverlayRenderAntialiasing":
                    Toggle_RenderAA.IsOn = Convert.ToBoolean(value);
                    break;
                case "OverlayTrackpadsSize":
                    SliderTrackpadsSize.Value = Convert.ToDouble(value);
                    break;
                case "OverlayFaceCamera":
                    Toggle_FaceCamera.IsOn = Convert.ToBoolean(value);
                    break;
                case "OverlayControllerRestingPitch":
                    Slider_RestingPitch.Value = Convert.ToDouble(value);
                    break;
                case "OverlayTrackpadsAlignment":
                    UpdateUI_TrackpadsPosition(Convert.ToInt32(value));
                    break;
                case "OverlayTrackpadsOpacity":
                    SliderTrackpadsOpacity.Value = Convert.ToDouble(value);
                    break;
                case "OverlayControllerOpacity":
                    SliderControllerOpacity.Value = Convert.ToDouble(value);
                    break;
                case "OverlayControllerBackgroundColor":
                    ColorPicker.SelectedColor = (Color)ColorConverter.ConvertFromString(Convert.ToString(value));
                    break;
                case "OverlayControllerAlwaysOnTop":
                    Toggle_AlwaysOnTop.IsOn = Convert.ToBoolean(value);
                    break;
                case "OverlayControllerMotion":
                    Toggle_MotionActivated.IsOn = Convert.ToBoolean(value);
                    break;
                case "OnScreenDisplayRefreshRate":
                    SliderOnScreenUpdateRate.Value = Convert.ToDouble(value);
                    break;
                case "OnScreenDisplayLevel":
                    var index = Convert.ToInt32(value);
                    OnScreenDisplayLevel.SelectedIndex = index;
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

    public void Dispose()
    {
        ViewModel.OnPageUnloaded();

        // manage events
        ManagerFactory.settingsManager.SettingValueChanged -= SettingsManager_SettingValueChanged;
        PlatformManager.RTSS.Updated -= RTSS_Updated;
    }

    private void UpdateUI_TrackpadsPosition(int trackpadsAlignment)
    {
        foreach (Button button in OverlayTrackpadsAlignment.Children)
            if (int.Parse((string)button.Tag) == trackpadsAlignment)
                button.Style = Application.Current.FindResource("AccentButtonStyle") as Style;
            else
                button.Style = Application.Current.FindResource("DefaultButtonStyle") as Style;
    }

    private void UpdateUI_ControllerPosition(int controllerAlignment)
    {
        foreach (SimpleStackPanel panel in OverlayControllerAlignment.Children)
            foreach (Button button in panel.Children)
                if (int.Parse((string)button.Tag) == controllerAlignment)
                    button.Style = Application.Current.FindResource("AccentButtonStyle") as Style;
                else
                    button.Style = Application.Current.FindResource("DefaultButtonStyle") as Style;
    }

    private void SliderControllerSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("OverlayControllerSize", SliderControllerSize.Value);
    }

    private void SliderTrackpadsSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("OverlayTrackpadsSize", SliderTrackpadsSize.Value);
    }

    private void OverlayModel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("OverlayModel", OverlayModel.SelectedIndex);
    }

    private void ControllerAlignment_Click(object sender, RoutedEventArgs e)
    {
        var Tag = int.Parse((string)((Button)sender).Tag);
        UpdateUI_ControllerPosition(Tag);

        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("OverlayControllerAlignment", Tag);
    }

    private void TrackpadsAlignment_Click(object sender, RoutedEventArgs e)
    {
        var Tag = int.Parse((string)((Button)sender).Tag);
        UpdateUI_TrackpadsPosition(Tag);

        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("OverlayTrackpadsAlignment", Tag);
    }

    private void SliderTrackpadsOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("OverlayTrackpadsOpacity", SliderTrackpadsOpacity.Value);
    }

    private void Toggle_MotionActivated_Toggled(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("OverlayControllerMotion", Toggle_MotionActivated.IsOn);
    }

    private void Toggle_FaceCamera_Toggled(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("OverlayFaceCamera", Toggle_FaceCamera.IsOn);
    }

    private void Slider_RestingPitch_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("OverlayControllerRestingPitch", Slider_RestingPitch.Value);
    }

    private void Toggle_RenderAA_Toggled(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("OverlayRenderAntialiasing", Toggle_RenderAA.IsOn);
    }

    private void Slider_Framerate_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("OverlayRenderInterval", Slider_Framerate.Value);
    }

    private void SliderControllerOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("OverlayControllerOpacity", SliderControllerOpacity.Value);
    }

    private void StandardColorPicker_ColorChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("OverlayControllerBackgroundColor", ColorPicker.SelectedColor);
    }

    private void Toggle_AlwaysOnTop_Toggled(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("OverlayControllerAlwaysOnTop", Toggle_AlwaysOnTop.IsOn);
    }

    private void SliderOnScreenUpdateRate_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("OnScreenDisplayRefreshRate", SliderOnScreenUpdateRate.Value);
    }

    private void OnScreenDisplayLevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("OnScreenDisplayLevel", ((ComboBox)sender).SelectedIndex);
    }

    private void ComboBoxOnScreenDisplayTimeLevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("OnScreenDisplayTimeLevel", ((ComboBox)sender).SelectedIndex);
    }


    private void ComboBoxOnScreenDisplayFPSLevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("OnScreenDisplayFPSLevel", ((ComboBox)sender).SelectedIndex);
    }

    private void ComboBoxOnScreenDisplayCPULevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("OnScreenDisplayCPULevel", ((ComboBox)sender).SelectedIndex);
    }

    private void ComboBoxOnScreenDisplayRAMLevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("OnScreenDisplayRAMLevel", ((ComboBox)sender).SelectedIndex);
    }

    private void ComboBoxOnScreenDisplayGPULevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("OnScreenDisplayGPULevel", ((ComboBox)sender).SelectedIndex);
    }

    private void ComboBoxOnScreenDisplayVRAMLevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("OnScreenDisplayVRAMLevel", ((ComboBox)sender).SelectedIndex);
    }

    private void ComboBoxOnScreenDisplayBATTLevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        ManagerFactory.settingsManager.SetProperty("OnScreenDisplayBATTLevel", ((ComboBox)sender).SelectedIndex);
    }
}