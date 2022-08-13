﻿using ControllerCommon.Utils;
using ModernWpf.Controls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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

            // controller enabler
            ToyControllerRadio.IsEnabled = Properties.Settings.Default.OverlayControllerFisherPrice;
            OEMControllerRadio.IsEnabled = MainWindow.handheldDevice.ProductSupported;

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

            // controller face camera and resting angle
            Toggle_FaceCamera.IsOn = Properties.Settings.Default.OverlayFaceCamera;
            Slider_RestingPitch.Value = Properties.Settings.Default.OverlayControllerRestingPitch;

            // trackpads alignment
            var TrackpadsAlignment = Properties.Settings.Default.OverlayTrackpadsAlignment;
            UpdateUI_TrackpadsPosition(TrackpadsAlignment);

            // trackpads opacity
            SliderTrackpadsOpacity.Value = Properties.Settings.Default.OverlayTrackpadsOpacity;
            SliderTrackpadsOpacity_ValueChanged(this, null);

            // controller opacity
            SliderControllerOpacity.Value = Properties.Settings.Default.OverlayControllerOpacity;
            SliderControllerOpacity_ValueChanged(this, null);
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

        private void Expander_Expanded(object sender, RoutedEventArgs e)
        {
            ((Expander)sender).BringIntoView();
        }

        private void Toggle_FaceCamera_Toggled(object sender, RoutedEventArgs e)
        {
            if (!Initialized)
                return;

            MainWindow.overlay.FaceCamera = Toggle_FaceCamera.IsOn;
            Slider_RestingPitch.IsEnabled = Toggle_FaceCamera.IsOn == true ? true : false;

            // save settings
            Properties.Settings.Default.OverlayFaceCamera = Toggle_FaceCamera.IsOn;
            Properties.Settings.Default.Save();
        }
        private void Slider_RestingPitch_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!Initialized)
                return;

            MainWindow.overlay.DesiredAngleDeg.X = -1 * Slider_RestingPitch.Value;

            // save settings
            Properties.Settings.Default.OverlayControllerRestingPitch = Slider_RestingPitch.Value;
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

        private void SliderControllerOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!Initialized)
                return;

            MainWindow.overlay.ModelViewPort.Opacity = SliderControllerOpacity.Value;

            // save settings
            Properties.Settings.Default.OverlayControllerOpacity = SliderControllerOpacity.Value;
            Properties.Settings.Default.Save();
        }
    }
}
