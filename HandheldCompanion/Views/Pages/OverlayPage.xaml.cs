using ControllerCommon.Utils;
using HandheldCompanion.Managers;
using HandheldCompanion.Models;
using HandheldCompanion.Views.Windows;
using ModernWpf.Controls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GamepadButtonFlags = SharpDX.XInput.GamepadButtonFlags;
using Page = System.Windows.Controls.Page;

namespace HandheldCompanion.Views.Pages
{
    /// <summary>
    /// Interaction logic for OverlayPage.xaml
    /// </summary>
    public partial class OverlayPage : Page
    {
        private bool Initialized;

        public OverlayPage()
        {
            InitializeComponent();
            Initialized = true;
        }

        public OverlayPage(string Tag) : this()
        {
            this.Tag = Tag;

            MainWindow.inputsManager.TriggerUpdated += TriggerUpdated;

            // controller enabler
            ToyControllerRadio.IsEnabled = Properties.Settings.Default.OverlayControllerFisherPrice;

            // controller model
            OverlayModel.SelectedIndex = Properties.Settings.Default.OverlayModel;
            OverlayModel_SelectionChanged(this, null);

            // controller alignment
            var ControllerAlignment = Properties.Settings.Default.OverlayControllerAlignment;
            UpdateUI_ControllerPosition(ControllerAlignment);

            // controller size
            SliderControllerSize.Value = Properties.Settings.Default.OverlayControllerSize;
            SliderControllerSize_ValueChanged(this, null);

            // controller update interval
            Slider_Framerate.Value = Properties.Settings.Default.OverlayRenderInterval;
            Slider_Framerate_ValueChanged(this, null);

            Toggle_RenderAA.IsOn = Properties.Settings.Default.OverlayRenderAntialiasing;
            Toggle_RenderAA_Toggled(this, null);

            // trackpads size
            SliderTrackpadsSize.Value = Properties.Settings.Default.OverlayTrackpadsSize;
            SliderTrackpadsSize_ValueChanged(this, null);

            // controller trigger
            GamepadButtonFlags ControllerButton = (GamepadButtonFlags)Properties.Settings.Default.OverlayControllerTrigger;
            ControllerTriggerIcon.Glyph = InputUtils.GamepadButtonToGlyph((GamepadButtonFlagsExt)ControllerButton);
            ControllerTriggerText.Text = EnumUtils.GetDescriptionFromEnumValue(ControllerButton);

            // controller resting angles
            Slider_RestingPitch.Value = Properties.Settings.Default.OverlayControllerRestingPitch;
            Slider_RestingRoll.Value = Properties.Settings.Default.OverlayControllerRestingRoll;
            Slider_RestingYaw.Value = Properties.Settings.Default.OverlayControllerRestingYaw;

            // trackpads alignment
            var TrackpadsAlignment = Properties.Settings.Default.OverlayTrackpadsAlignment;
            UpdateUI_TrackpadsPosition(TrackpadsAlignment);

            // trackpads opacity
            SliderTrackpadsOpacity.Value = Properties.Settings.Default.OverlayTrackpadsOpacity;
            SliderTrackpadsOpacity_ValueChanged(this, null);

            // trackpads trigger
            GamepadButtonFlags TrackpadsButton = (GamepadButtonFlags)Properties.Settings.Default.OverlayTrackpadsTrigger;
            TrackpadsTriggerIcon.Glyph = InputUtils.GamepadButtonToGlyph((GamepadButtonFlagsExt)TrackpadsButton);
            TrackpadsTriggerText.Text = EnumUtils.GetDescriptionFromEnumValue(TrackpadsButton);
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
        }

        public void Page_Closed()
        {
        }

        public void UnlockToyController()
        {
            this.Dispatcher.Invoke(() =>
            {
                ToyControllerRadio.IsEnabled = true;
            });
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
                    MainWindow.overlay.VirtualTrackpads.VerticalAlignment = VerticalAlignment.Top;
                    break;
                case 1:
                    TrackpadsPositionUI.VerticalAlignment = VerticalAlignment.Center;
                    MainWindow.overlay.VirtualTrackpads.VerticalAlignment = VerticalAlignment.Center;
                    break;
                case 2:
                    TrackpadsPositionUI.VerticalAlignment = VerticalAlignment.Bottom;
                    MainWindow.overlay.VirtualTrackpads.VerticalAlignment = VerticalAlignment.Bottom;
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
                    MainWindow.overlay.VirtualController.VerticalAlignment = VerticalAlignment.Top;
                    break;
                case 3:
                case 4:
                case 5:
                    ControllerPositionUI.VerticalAlignment = VerticalAlignment.Center;
                    MainWindow.overlay.VirtualController.VerticalAlignment = VerticalAlignment.Center;
                    break;
                case 6:
                case 7:
                case 8:
                    ControllerPositionUI.VerticalAlignment = VerticalAlignment.Bottom;
                    MainWindow.overlay.VirtualController.VerticalAlignment = VerticalAlignment.Bottom;
                    break;
            }

            switch (controllerAlignment)
            {
                case 0:
                case 3:
                case 6:
                    ControllerPositionUI.HorizontalAlignment = HorizontalAlignment.Left;
                    MainWindow.overlay.VirtualController.HorizontalAlignment = HorizontalAlignment.Left;
                    break;
                case 1:
                case 4:
                case 7:
                    ControllerPositionUI.HorizontalAlignment = HorizontalAlignment.Center;
                    MainWindow.overlay.VirtualController.HorizontalAlignment = HorizontalAlignment.Center;
                    break;
                case 2:
                case 5:
                case 8:
                    ControllerPositionUI.HorizontalAlignment = HorizontalAlignment.Right;
                    MainWindow.overlay.VirtualController.HorizontalAlignment = HorizontalAlignment.Right;
                    break;
            }
        }

        private void SliderControllerSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!Initialized)
                return;

            MainWindow.overlay.VirtualController.Width = SliderControllerSize.Value;
            MainWindow.overlay.VirtualController.Height = SliderControllerSize.Value;

            // save settings
            Properties.Settings.Default.OverlayControllerSize = (int)SliderControllerSize.Value;
            Properties.Settings.Default.Save();
        }

        private void SliderTrackpadsSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!Initialized)
                return;

            MainWindow.overlay.LeftTrackpad.Height = MainWindow.overlay.LeftTrackpad.Width = SliderTrackpadsSize.Value;
            MainWindow.overlay.RightTrackpad.Height = MainWindow.overlay.RightTrackpad.Width = SliderTrackpadsSize.Value;

            // save settings
            Properties.Settings.Default.OverlayTrackpadsSize = (int)SliderTrackpadsSize.Value;
            Properties.Settings.Default.Save();
        }

        private void Scrolllock_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            MainWindow.scrollLock = true;
        }

        private void Scrolllock_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            MainWindow.scrollLock = false;
        }

        private void OverlayModel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!Initialized)
                return;

            // update overlay
            MainWindow.overlay.UpdateOverlayMode((OverlayModelMode)OverlayModel.SelectedIndex);

            // save settings
            Properties.Settings.Default.OverlayModel = OverlayModel.SelectedIndex;
            Properties.Settings.Default.Save();
        }

        private void ControllerAlignment_Click(object sender, RoutedEventArgs e)
        {
            int Tag = int.Parse((string)((Button)sender).Tag);
            UpdateUI_ControllerPosition(Tag);

            // save settings
            Properties.Settings.Default.OverlayControllerAlignment = Tag;
            Properties.Settings.Default.Save();
        }

        private void TrackpadsAlignment_Click(object sender, RoutedEventArgs e)
        {
            int Tag = int.Parse((string)((Button)sender).Tag);
            UpdateUI_TrackpadsPosition(Tag);

            // save settings
            Properties.Settings.Default.OverlayTrackpadsAlignment = Tag;
            Properties.Settings.Default.Save();
        }

        private void SliderTrackpadsOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!Initialized)
                return;

            MainWindow.overlay.LeftTrackpad.Opacity = SliderTrackpadsOpacity.Value;
            MainWindow.overlay.RightTrackpad.Opacity = SliderTrackpadsOpacity.Value;

            // save settings
            Properties.Settings.Default.OverlayTrackpadsOpacity = SliderTrackpadsOpacity.Value;
            Properties.Settings.Default.Save();
        }

        private void ControllerTriggerButton_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.inputsManager.StartListening("overlayGamepad");
            ControllerTriggerText.Text = Properties.Resources.OverlayPage_Listening;
        }

        private void TrackpadsTriggerButton_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.inputsManager.StartListening("overlayTrackpads");
            TrackpadsTriggerText.Text = Properties.Resources.OverlayPage_Listening;
        }

        private void TriggerUpdated(string listener, GamepadButtonFlags button)
        {
            this.Dispatcher.Invoke(() =>
            {
                switch (listener)
                {
                    case "overlayGamepad":
                        ControllerTriggerIcon.Glyph = InputUtils.GamepadButtonToGlyph((GamepadButtonFlagsExt)button);
                        ControllerTriggerText.Text = EnumUtils.GetDescriptionFromEnumValue(button);
                        Properties.Settings.Default.OverlayControllerTrigger = (int)button;
                        break;
                    case "overlayTrackpads":
                        TrackpadsTriggerIcon.Glyph = InputUtils.GamepadButtonToGlyph((GamepadButtonFlagsExt)button);
                        TrackpadsTriggerText.Text = EnumUtils.GetDescriptionFromEnumValue(button);
                        Properties.Settings.Default.OverlayTrackpadsTrigger = (int)button;
                        break;
                }
            });
            
            Properties.Settings.Default.Save();
        }

        private void Expander_Expanded(object sender, RoutedEventArgs e)
        {
            ((Expander)sender).BringIntoView();
        }

        private void Slider_RestingPitch_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!Initialized)
                return;

            MainWindow.overlay.DesiredAngleDeg.X = Slider_RestingPitch.Value;

            // save settings
            Properties.Settings.Default.OverlayControllerRestingPitch = Slider_RestingPitch.Value;
            Properties.Settings.Default.Save();
        }

        private void Slider_RestingYaw_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!Initialized)
                return;

            MainWindow.overlay.DesiredAngleDeg.Z = Slider_RestingYaw.Value;

            // save settings
            Properties.Settings.Default.OverlayControllerRestingYaw = Slider_RestingYaw.Value;
            Properties.Settings.Default.Save();
        }

        private void Slider_RestingRoll_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!Initialized)
                return;

            MainWindow.overlay.DesiredAngleDeg.Y = Slider_RestingRoll.Value;

            // save settings
            Properties.Settings.Default.OverlayControllerRestingRoll = Slider_RestingRoll.Value;
            Properties.Settings.Default.Save();
        }

        private void Toggle_RenderAA_Toggled(object sender, RoutedEventArgs e)
        {
            if (!Initialized)
                return;

            MainWindow.overlay.ModelViewPort.SetValue(RenderOptions.EdgeModeProperty, Toggle_RenderAA.IsOn ? EdgeMode.Unspecified : EdgeMode.Aliased);

            // save settings
            Properties.Settings.Default.OverlayRenderAntialiasing = Toggle_RenderAA.IsOn;
            Properties.Settings.Default.Save();
        }

        private void Slider_Framerate_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!Initialized)
                return;

            MainWindow.overlay.UpdateInterval(1000.0d / Slider_Framerate.Value);

            // save settings
            Properties.Settings.Default.OverlayRenderInterval = Slider_Framerate.Value;
            Properties.Settings.Default.Save();
        }
    }
}
