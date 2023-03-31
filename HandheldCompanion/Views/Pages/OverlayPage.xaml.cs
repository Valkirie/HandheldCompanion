using ControllerCommon.Utils;
using HandheldCompanion.Managers;
using ModernWpf.Controls;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Views.Pages
{
    /// <summary>
    /// Interaction logic for OverlayPage.xaml
    /// </summary>
    public partial class OverlayPage : Page
    {
        public OverlayPage()
        {
            InitializeComponent();

            SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;
        }

        private void SettingsManager_SettingValueChanged(string name, object value)
        {
            // UI thread
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                switch (name)
                {
                    case "OverlayModel":
                        OverlayModel.SelectedIndex = Convert.ToInt32(value);
                        OverlayModel_SelectionChanged(this, null); // bug: SelectionChanged not triggered when control isn't loaded
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
                }
            });
        }

        public OverlayPage(string Tag) : this()
        {
            this.Tag = Tag;
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
            {
                if (int.Parse((string)button.Tag) == trackpadsAlignment)
                    button.Style = Application.Current.FindResource("AccentButtonStyle") as Style;
                else
                    button.Style = Application.Current.FindResource("DefaultButtonStyle") as Style;
            }

            switch (trackpadsAlignment)
            {
                case 0:
                    TrackpadsPositionUI.VerticalAlignment = VerticalAlignment.Top;
                    MainWindow.overlayTrackpad.VerticalAlignment = VerticalAlignment.Top;
                    break;
                case 1:
                    TrackpadsPositionUI.VerticalAlignment = VerticalAlignment.Center;
                    MainWindow.overlayTrackpad.VerticalAlignment = VerticalAlignment.Center;
                    break;
                case 2:
                    TrackpadsPositionUI.VerticalAlignment = VerticalAlignment.Bottom;
                    MainWindow.overlayTrackpad.VerticalAlignment = VerticalAlignment.Bottom;
                    break;
            }
        }

        private void UpdateUI_ControllerPosition(int controllerAlignment)
        {
            foreach (SimpleStackPanel panel in OverlayControllerAlignment.Children)
                foreach (Button button in panel.Children)
                {
                    if (int.Parse((string)button.Tag) == controllerAlignment)
                        button.Style = Application.Current.FindResource("AccentButtonStyle") as Style;
                    else
                        button.Style = Application.Current.FindResource("DefaultButtonStyle") as Style;
                }

            switch (controllerAlignment)
            {
                case 0:
                case 1:
                case 2:
                    ControllerPositionUI.VerticalAlignment = VerticalAlignment.Top;
                    MainWindow.overlayModel.VerticalAlignment = VerticalAlignment.Top;
                    break;
                case 3:
                case 4:
                case 5:
                    ControllerPositionUI.VerticalAlignment = VerticalAlignment.Center;
                    MainWindow.overlayModel.VerticalAlignment = VerticalAlignment.Center;
                    break;
                case 6:
                case 7:
                case 8:
                    ControllerPositionUI.VerticalAlignment = VerticalAlignment.Bottom;
                    MainWindow.overlayModel.VerticalAlignment = VerticalAlignment.Bottom;
                    break;
            }

            switch (controllerAlignment)
            {
                case 0:
                case 3:
                case 6:
                    ControllerPositionUI.HorizontalAlignment = HorizontalAlignment.Left;
                    MainWindow.overlayModel.HorizontalAlignment = HorizontalAlignment.Left;
                    break;
                case 1:
                case 4:
                case 7:
                    ControllerPositionUI.HorizontalAlignment = HorizontalAlignment.Center;
                    MainWindow.overlayModel.HorizontalAlignment = HorizontalAlignment.Center;
                    break;
                case 2:
                case 5:
                case 8:
                    ControllerPositionUI.HorizontalAlignment = HorizontalAlignment.Right;
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
            MainWindow.overlayTrackpad.LeftTrackpad.Width = SliderTrackpadsSize.Value;
            MainWindow.overlayTrackpad.RightTrackpad.Width = SliderTrackpadsSize.Value;
            MainWindow.overlayTrackpad.Height = SliderTrackpadsSize.Value;
            MainWindow.overlayTrackpad.HorizontalAlignment = HorizontalAlignment.Stretch;

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
            int Tag = int.Parse((string)((Button)sender).Tag);
            UpdateUI_ControllerPosition(Tag);

            if (!IsLoaded)
                return;

            SettingsManager.SetProperty("OverlayControllerAlignment", Tag);
        }

        private void TrackpadsAlignment_Click(object sender, RoutedEventArgs e)
        {
            int Tag = int.Parse((string)((Button)sender).Tag);
            UpdateUI_TrackpadsPosition(Tag);

            if (!IsLoaded)
                return;

            SettingsManager.SetProperty("OverlayTrackpadsAlignment", Tag);
        }

        private void SliderTrackpadsOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            MainWindow.overlayTrackpad.LeftTrackpad.Opacity = SliderTrackpadsOpacity.Value;
            MainWindow.overlayTrackpad.RightTrackpad.Opacity = SliderTrackpadsOpacity.Value;

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

            if (!IsLoaded)
                return;

            SettingsManager.SetProperty("OverlayControllerMotion", Toggle_MotionActivated.IsOn);
        }
        private void Toggle_FaceCamera_Toggled(object sender, RoutedEventArgs e)
        {
            MainWindow.overlayModel.FaceCamera = Toggle_FaceCamera.IsOn;
            Slider_RestingPitch.IsEnabled = Toggle_FaceCamera.IsOn == true ? true : false;

            if (!IsLoaded)
                return;

            SettingsManager.SetProperty("OverlayFaceCamera", Toggle_FaceCamera.IsOn);
        }
        private void Slider_RestingPitch_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            MainWindow.overlayModel.DesiredAngleDeg.X = -1 * Slider_RestingPitch.Value;

            if (!IsLoaded)
                return;

            SettingsManager.SetProperty("OverlayControllerRestingPitch", Slider_RestingPitch.Value);
        }

        private void Toggle_RenderAA_Toggled(object sender, RoutedEventArgs e)
        {
            MainWindow.overlayModel.ModelViewPort.SetValue(RenderOptions.EdgeModeProperty, Toggle_RenderAA.IsOn ? EdgeMode.Unspecified : EdgeMode.Aliased);

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

        private void StandardColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
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
    }
}
