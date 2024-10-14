using ColorPicker;
using ColorPicker.Models;
using HandheldCompanion.Managers;
using HandheldCompanion.Platforms;
using HandheldCompanion.Utils;
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
    public OverlayPage()
    {
        InitializeComponent();

        SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
        PlatformManager.RTSS.Updated += RTSS_Updated;

        // force call
        // todo: make PlatformManager static
        RTSS_Updated(PlatformManager.RTSS.Status);
    }

    public OverlayPage(string Tag) : this()
    {
        this.Tag = Tag;
    }

    private void RTSS_Updated(PlatformStatus status)
    {
        // UI thread (async)
        Application.Current.Dispatcher.Invoke(() =>
        {
            switch (status)
            {
                case PlatformStatus.Ready:
                    OnScreenDisplayLevel.IsEnabled = true;
                    break;
                case PlatformStatus.Stalled:
                    OnScreenDisplayLevel.IsEnabled = false;
                    OnScreenDisplayLevel.SelectedIndex = 0;
                    break;
            }
        });
    }

    private void SettingsManager_SettingValueChanged(string name, object value, bool temporary)
    {
        // UI thread
        Application.Current.Dispatcher.Invoke(() =>
        {
            switch (name)
            {
                case "OverlayModel":
                    OverlayModel.SelectedIndex = Convert.ToInt32(value);

                    // bug: SelectionChanged not triggered when control isn't loaded
                    if (!IsLoaded)
                        OverlayModel_SelectionChanged(this, null);
                    break;
                case "OverlayControllerAlignment":
                    UpdateUI_ControllerPosition(Convert.ToInt32(value));
                    break;
                case "OverlayControllerSize":
                    SliderControllerSize.Value = Convert.ToDouble(value);
                    break;
                case "OverlayRenderInterval":
                    Slider_Framerate.Value = Convert.ToDouble(value);
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

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
    }

    public void Page_Closed()
    {
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

        switch (controllerAlignment)
        {
            case 0:
            case 1:
            case 2:
                MainWindow.overlayModel.VerticalAlignment = VerticalAlignment.Top;
                break;
            case 3:
            case 4:
            case 5:
                MainWindow.overlayModel.VerticalAlignment = VerticalAlignment.Center;
                break;
            case 6:
            case 7:
            case 8:
                MainWindow.overlayModel.VerticalAlignment = VerticalAlignment.Bottom;
                break;
        }

        switch (controllerAlignment)
        {
            case 0:
            case 3:
            case 6:
                MainWindow.overlayModel.HorizontalAlignment = HorizontalAlignment.Left;
                break;
            case 1:
            case 4:
            case 7:
                MainWindow.overlayModel.HorizontalAlignment = HorizontalAlignment.Center;
                break;
            case 2:
            case 5:
            case 8:
                MainWindow.overlayModel.HorizontalAlignment = HorizontalAlignment.Right;
                break;
        }
    }

    private void SliderControllerSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        MainWindow.overlayModel.Width = SliderControllerSize.Value;
        MainWindow.overlayModel.Height = SliderControllerSize.Value;

        if (!IsLoaded)
            return;

        SettingsManager.SetProperty("OverlayControllerSize", SliderControllerSize.Value);
    }

    private void SliderTrackpadsSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded)
            return;

        SettingsManager.SetProperty("OverlayTrackpadsSize", SliderTrackpadsSize.Value);
    }

    private void OverlayModel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // update overlay
        MainWindow.overlayModel.UpdateOverlayMode((OverlayModelMode)OverlayModel.SelectedIndex);

        if (!IsLoaded)
            return;

        SettingsManager.SetProperty("OverlayModel", OverlayModel.SelectedIndex);
    }

    private void ControllerAlignment_Click(object sender, RoutedEventArgs e)
    {
        var Tag = int.Parse((string)((Button)sender).Tag);
        UpdateUI_ControllerPosition(Tag);

        if (!IsLoaded)
            return;

        SettingsManager.SetProperty("OverlayControllerAlignment", Tag);
    }

    private void TrackpadsAlignment_Click(object sender, RoutedEventArgs e)
    {
        var Tag = int.Parse((string)((Button)sender).Tag);
        UpdateUI_TrackpadsPosition(Tag);

        if (!IsLoaded)
            return;

        SettingsManager.SetProperty("OverlayTrackpadsAlignment", Tag);
    }

    private void SliderTrackpadsOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded)
            return;

        SettingsManager.SetProperty("OverlayTrackpadsOpacity", SliderTrackpadsOpacity.Value);
    }

    private void Expander_Expanded(object sender, RoutedEventArgs e)
    {
        ((Expander)sender).BringIntoView();
    }

    private void Toggle_MotionActivated_Toggled(object sender, RoutedEventArgs e)
    {
        MainWindow.overlayModel.MotionActivated = Toggle_MotionActivated.IsOn;
        //Toggle_FaceCamera.IsEnabled = Toggle_MotionActivated.IsOn ? true : false;

        if (!IsLoaded)
            return;

        SettingsManager.SetProperty("OverlayControllerMotion", Toggle_MotionActivated.IsOn);
    }

    private void Toggle_FaceCamera_Toggled(object sender, RoutedEventArgs e)
    {
        MainWindow.overlayModel.FaceCamera = Toggle_FaceCamera.IsOn;

        if (!IsLoaded)
            return;

        SettingsManager.SetProperty("OverlayFaceCamera", Toggle_FaceCamera.IsOn);
    }

    private void Slider_RestingPitch_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        MainWindow.overlayModel.RestingPitchAngleDeg = -1 * (float)Slider_RestingPitch.Value;

        if (!IsLoaded)
            return;

        SettingsManager.SetProperty("OverlayControllerRestingPitch", Slider_RestingPitch.Value);
    }

    private void Toggle_RenderAA_Toggled(object sender, RoutedEventArgs e)
    {
        MainWindow.overlayModel.ModelViewPort.SetValue(RenderOptions.EdgeModeProperty,
            Toggle_RenderAA.IsOn ? EdgeMode.Unspecified : EdgeMode.Aliased);

        if (!IsLoaded)
            return;

        SettingsManager.SetProperty("OverlayRenderAntialiasing", Toggle_RenderAA.IsOn);
    }

    private void Slider_Framerate_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        MainWindow.overlayModel.UpdateInterval(1000.0d / Slider_Framerate.Value);

        if (!IsLoaded)
            return;

        SettingsManager.SetProperty("OverlayRenderInterval", Slider_Framerate.Value);
    }

    private void SliderControllerOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        MainWindow.overlayModel.ModelViewPort.Opacity = SliderControllerOpacity.Value;

        if (!IsLoaded)
            return;

        SettingsManager.SetProperty("OverlayControllerOpacity", SliderControllerOpacity.Value);
    }

    private Color prevSelectedColor = new();
    private void StandardColorPicker_ColorChanged(object sender, RoutedEventArgs e)
    {
        // workaround: NotifyableColor is raising ColorChanged event infinitely
        ColorRoutedEventArgs colorArgs = (ColorRoutedEventArgs)e;
        if (prevSelectedColor == colorArgs.Color)
        {
            ColorPicker.Color = new NotifyableColor(new PickerControlBase());
            return;
        }
        prevSelectedColor = colorArgs.Color;

        MainWindow.overlayModel.Background = new SolidColorBrush(ColorPicker.SelectedColor);

        if (!IsLoaded)
            return;

        SettingsManager.SetProperty("OverlayControllerBackgroundColor", ColorPicker.SelectedColor);
    }

    private void Toggle_AlwaysOnTop_Toggled(object sender, RoutedEventArgs e)
    {
        MainWindow.overlayModel.Topmost = Toggle_AlwaysOnTop.IsOn;

        if (!IsLoaded)
            return;

        SettingsManager.SetProperty("OverlayControllerAlwaysOnTop", Toggle_AlwaysOnTop.IsOn);
    }

    private void SliderOnScreenUpdateRate_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded)
            return;

        SettingsManager.SetProperty("OnScreenDisplayRefreshRate", SliderOnScreenUpdateRate.Value);
    }

    private void OnScreenDisplayLevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        SettingsManager.SetProperty("OnScreenDisplayLevel", ((ComboBox)sender).SelectedIndex);
    }

    private void ComboBoxOnScreenDisplayTimeLevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        SettingsManager.SetProperty("OnScreenDisplayTimeLevel", ((ComboBox)sender).SelectedIndex);
    }


    private void ComboBoxOnScreenDisplayFPSLevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        SettingsManager.SetProperty("OnScreenDisplayFPSLevel", ((ComboBox)sender).SelectedIndex);
    }

    private void ComboBoxOnScreenDisplayCPULevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        SettingsManager.SetProperty("OnScreenDisplayCPULevel", ((ComboBox)sender).SelectedIndex);
    }

    private void ComboBoxOnScreenDisplayRAMLevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        SettingsManager.SetProperty("OnScreenDisplayRAMLevel", ((ComboBox)sender).SelectedIndex);
    }

    private void ComboBoxOnScreenDisplayGPULevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        SettingsManager.SetProperty("OnScreenDisplayGPULevel", ((ComboBox)sender).SelectedIndex);
    }

    private void ComboBoxOnScreenDisplayVRAMLevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        SettingsManager.SetProperty("OnScreenDisplayVRAMLevel", ((ComboBox)sender).SelectedIndex);
    }

    private void ComboBoxOnScreenDisplayBATTLevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
            return;

        SettingsManager.SetProperty("OnScreenDisplayBATTLevel", ((ComboBox)sender).SelectedIndex);
    }
}