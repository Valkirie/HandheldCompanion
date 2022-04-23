using ControllerCommon.Utils;
using HandheldCompanion.Views.Windows;
using Microsoft.Extensions.Logging;
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
        private ILogger microsoftLogger;
        private Overlay overlay;

        public OverlayPage()
        {
            InitializeComponent();
        }

        public OverlayPage(string Tag, Overlay overlay, ILogger microsoftLogger) : this()
        {
            this.Tag = Tag;
            this.overlay = overlay;
            this.overlay.ControllerTriggerUpdated += Overlay_ControllerTriggerUpdated;
            this.overlay.TrackpadsTriggerUpdated += Overlay_TrackpadsTriggerUpdated;

            this.microsoftLogger = microsoftLogger;

            // overlay trigger
            OverlayTrigger.SelectedIndex = Properties.Settings.Default.OverlayTrigger;
            OverlayTrigger_SelectionChanged(this, null);

            // controller model
            OverlayModel.SelectedIndex = Properties.Settings.Default.OverlayModel;
            OverlayModel_SelectionChanged(this, null);

            // controller alignment
            var ControllerAlignment = Properties.Settings.Default.OverlayControllerAlignment;
            UpdateUI_ControllerPosition(ControllerAlignment);

            // controller size
            SliderControllerSize.Value = Properties.Settings.Default.OverlayControllerSize;
            SliderControllerSize_ValueChanged(this, null);

            // controller trigger
            GamepadButtonFlagsExt ControllerButton = (GamepadButtonFlagsExt)Properties.Settings.Default.OverlayControllerTrigger;
            ControllerTriggerIcon.Glyph = InputUtils.GamepadButtonToGlyph(ControllerButton);
            ControllerTriggerText.Text = EnumUtils.GetDescriptionFromEnumValue(ControllerButton);
            overlay.controllerTrigger = (GamepadButtonFlags)ControllerButton;

            // trackpads alignment
            var TrackpadsAlignment = Properties.Settings.Default.OverlayTrackpadsAlignment;
            UpdateUI_TrackpadsPosition(TrackpadsAlignment);

            // trackpads opacity
            SliderTrackpadsOpacity.Value = Properties.Settings.Default.OverlayTrackpadsOpacity;
            SliderTrackpadsOpacity_ValueChanged(this, null);

            // trackpads trigger
            GamepadButtonFlagsExt TrackpadsButton = (GamepadButtonFlagsExt)Properties.Settings.Default.OverlayTrackpadsTrigger;
            TrackpadsTriggerIcon.Glyph = InputUtils.GamepadButtonToGlyph(TrackpadsButton);
            TrackpadsTriggerText.Text = EnumUtils.GetDescriptionFromEnumValue(TrackpadsButton);
            overlay.trackpadTrigger = (GamepadButtonFlags)TrackpadsButton;
        }

        private void UpdateUI_TrackpadsPosition(int trackpadsAlignment)
        {
            foreach (Button button in OverlayTrackpadsAlignment.Children)
            {
                if (int.Parse((string)button.Tag) == trackpadsAlignment)
                    button.Background = (Brush)Application.Current.Resources["SystemControlForegroundAccentBrush"];
                else
                    button.Background = (Brush)Application.Current.Resources["SystemControlHighlightAltBaseLowBrush"];
            }

            switch (trackpadsAlignment)
            {
                case 0:
                    TrackpadsPositionUI.VerticalAlignment = VerticalAlignment.Top;
                    overlay.VirtualTrackpads.VerticalAlignment = VerticalAlignment.Top;
                    break;
                case 1:
                    TrackpadsPositionUI.VerticalAlignment = VerticalAlignment.Center;
                    overlay.VirtualTrackpads.VerticalAlignment = VerticalAlignment.Center;
                    break;
                case 2:
                    TrackpadsPositionUI.VerticalAlignment = VerticalAlignment.Bottom;
                    overlay.VirtualTrackpads.VerticalAlignment = VerticalAlignment.Bottom;
                    break;
            }
        }

        private void UpdateUI_ControllerPosition(int controllerAlignment)
        {
            foreach (SimpleStackPanel panel in OverlayControllerAlignment.Children)
                foreach (Button button in panel.Children)
                {
                    if (int.Parse((string)button.Tag) == controllerAlignment)
                        button.Background = (Brush)Application.Current.Resources["SystemControlForegroundAccentBrush"];
                    else
                        button.Background = (Brush)Application.Current.Resources["SystemControlHighlightAltBaseLowBrush"];
                }

            switch (controllerAlignment)
            {
                case 0:
                case 1:
                case 2:
                    ControllerPositionUI.VerticalAlignment = VerticalAlignment.Top;
                    overlay.VirtualController.VerticalAlignment = VerticalAlignment.Top;
                    break;
                case 3:
                case 4:
                case 5:
                    ControllerPositionUI.VerticalAlignment = VerticalAlignment.Center;
                    overlay.VirtualController.VerticalAlignment = VerticalAlignment.Center;
                    break;
                case 6:
                case 7:
                case 8:
                    ControllerPositionUI.VerticalAlignment = VerticalAlignment.Bottom;
                    overlay.VirtualController.VerticalAlignment = VerticalAlignment.Bottom;
                    break;
            }

            switch (controllerAlignment)
            {
                case 0:
                case 3:
                case 6:
                    ControllerPositionUI.HorizontalAlignment = HorizontalAlignment.Left;
                    overlay.VirtualController.HorizontalAlignment = HorizontalAlignment.Left;
                    break;
                case 1:
                case 4:
                case 7:
                    ControllerPositionUI.HorizontalAlignment = HorizontalAlignment.Center;
                    overlay.VirtualController.HorizontalAlignment = HorizontalAlignment.Center;
                    break;
                case 2:
                case 5:
                case 8:
                    ControllerPositionUI.HorizontalAlignment = HorizontalAlignment.Right;
                    overlay.VirtualController.HorizontalAlignment = HorizontalAlignment.Right;
                    break;
            }
        }

        private void Page_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            // do something
        }

        private void SliderControllerSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (overlay == null)
                return;

            overlay.VirtualController.Width = SliderControllerSize.Value;
            overlay.VirtualController.Height = SliderControllerSize.Value;// * 0.6d;

            // save settings
            Properties.Settings.Default.OverlayControllerSize = (int)SliderControllerSize.Value;
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

        private void OverlayTrigger_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            switch (OverlayTrigger.SelectedIndex)
            {
                case 0: // start
                    overlay.mainTrigger = GamepadButtonFlags.Start;
                    break;
                case 1: // back
                    overlay.mainTrigger = GamepadButtonFlags.Back;
                    break;
            }

            // save settings
            Properties.Settings.Default.OverlayTrigger = OverlayTrigger.SelectedIndex;
            Properties.Settings.Default.Save();
        }

        private void OverlayModel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            overlay.UpdateModelMode((OverlayModelMode)OverlayModel.SelectedIndex);

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
            if (overlay == null)
                return;

            overlay.LeftTrackpad.Opacity = SliderTrackpadsOpacity.Value;
            overlay.RightTrackpad.Opacity = SliderTrackpadsOpacity.Value;

            // save settings
            Properties.Settings.Default.OverlayTrackpadsOpacity = SliderTrackpadsOpacity.Value;
            Properties.Settings.Default.Save();
        }

        private void ControllerTriggerButton_Click(object sender, RoutedEventArgs e)
        {
            overlay.ControllerTriggerClicked();
            ControllerTriggerText.Text = "Listening..."; // todo: localization
        }

        private void Overlay_ControllerTriggerUpdated(GamepadButtonFlags button)
        {
            this.Dispatcher.Invoke(() =>
            {
                ControllerTriggerIcon.Glyph = InputUtils.GamepadButtonToGlyph((GamepadButtonFlagsExt)button);
                ControllerTriggerText.Text = EnumUtils.GetDescriptionFromEnumValue(button);
            });
            overlay.controllerTrigger = button;

            // save settings
            Properties.Settings.Default.OverlayControllerTrigger = (int)button;
            Properties.Settings.Default.Save();
        }

        private void TrackpadsTriggerButton_Click(object sender, RoutedEventArgs e)
        {
            overlay.TrackpadsTriggerClicked();
            TrackpadsTriggerText.Text = "Listening..."; // todo: localization
        }

        private void Overlay_TrackpadsTriggerUpdated(GamepadButtonFlags button)
        {
            this.Dispatcher.Invoke(() =>
            {
                TrackpadsTriggerIcon.Glyph = InputUtils.GamepadButtonToGlyph((GamepadButtonFlagsExt)button);
                TrackpadsTriggerText.Text = EnumUtils.GetDescriptionFromEnumValue(button);
            });
            overlay.trackpadTrigger = button;

            // save settings
            Properties.Settings.Default.OverlayTrackpadsTrigger = (int)button;
            Properties.Settings.Default.Save();
        }
    }
}
