using ControllerCommon.Pipes;
using HandheldCompanion.Managers;
using HandheldCompanion.Views.Classes;
using System;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using Application = System.Windows.Application;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace HandheldCompanion.Views.Windows
{
    /// <summary>
    /// Interaction logic for Overlay.xaml
    /// </summary>
    public partial class OverlayTrackpad : OverlayWindow
    {
        private class TouchInput
        {
            public int Timestamp;
            public short Flags;
        }

        private TouchInput leftInput;
        private TouchInput rightInput;
        private double dpiInput;

        private double TrackpadOpacity = 0.25;
        private double TrackpadOpacityTouched = 0.10; // extra opacity when touched

        public OverlayTrackpad()
        {
            InitializeComponent();

            SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

            // touch vars
            dpiInput = GetWindowsScaling();
            leftInput = new TouchInput();
            rightInput = new TouchInput();
        }

        private void SettingsManager_SettingValueChanged(string name, object value)
        {
            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                switch (name)
                {
                    case "OverlayTrackpadsSize":
                        {
                            int size = Convert.ToInt32(value);
                            this.LeftTrackpad.Width = size;
                            this.RightTrackpad.Width = size;
                            this.Height = size;
                            this.HorizontalAlignment = HorizontalAlignment.Stretch;
                        }
                        break;
                    case "OverlayTrackpadsAlignment":
                        {
                            int trackpadsAlignment = Convert.ToInt32(value);
                            switch (trackpadsAlignment)
                            {
                                case 0:
                                    this.VerticalAlignment = VerticalAlignment.Top;
                                    break;
                                case 1:
                                    this.VerticalAlignment = VerticalAlignment.Center;
                                    break;
                                case 2:
                                    this.VerticalAlignment = VerticalAlignment.Bottom;
                                    break;
                            }
                        }
                        break;
                    case "OverlayTrackpadsOpacity":
                        {
                            TrackpadOpacity = Convert.ToDouble(value);
                            LeftTrackpad.Opacity = TrackpadOpacity;
                            RightTrackpad.Opacity = TrackpadOpacity;
                        }
                        break;
                }
            });
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // do something
        }

        private void UpdateUI_TrackpadsPosition(int trackpadsAlignment)
        {
        }

        public double GetWindowsScaling()
        {
            return Screen.PrimaryScreen.Bounds.Width / SystemParameters.PrimaryScreenWidth;
        }

        private void Trackpad_TouchInput(TouchEventArgs e, CursorAction action, CursorButton button)
        {
            TouchDevice args = e.TouchDevice;
            TouchPoint point;
            int flags;

            switch (button)
            {
                default:
                case CursorButton.TouchLeft:
                    {
                        point = args.GetTouchPoint(LeftTrackpad);
                        flags = leftInput.Flags;

                        leftInput.Timestamp = e.Timestamp;
                    }
                    break;
                case CursorButton.TouchRight:
                    {
                        point = args.GetTouchPoint(RightTrackpad);
                        flags = rightInput.Flags;

                        rightInput.Timestamp = e.Timestamp;
                    }
                    break;
            }

            var normalizedX = (point.Position.X / (LeftTrackpad.ActualWidth) * dpiInput) / 2.0d;
            var normalizedY = (point.Position.Y / (LeftTrackpad.ActualWidth) * dpiInput);

            normalizedX += button == CursorButton.TouchRight ? 0.5d : 0.0d;

            PipeClient.SendMessage(new PipeClientCursor
            {
                action = action,
                x = normalizedX,
                y = normalizedY,
                button = button,
                flags = flags
            });
        }

        private void Trackpad_PreviewTouchMove(object sender, TouchEventArgs e)
        {
            string name = ((FrameworkElement)sender).Name;

            switch (name)
            {
                case "LeftTrackpad":
                    {
                        Trackpad_TouchInput(e, CursorAction.CursorMove, CursorButton.TouchLeft);
                    }
                    break;
                case "RightTrackpad":
                    {
                        Trackpad_TouchInput(e, CursorAction.CursorMove, CursorButton.TouchRight);
                    }
                    break;
            }

            e.Handled = true;
        }

        private void Trackpad_PreviewTouchDown(object sender, TouchEventArgs e)
        {
            string name = ((FrameworkElement)sender).Name;

            switch (name)
            {
                case "LeftTrackpad":
                    {
                        var elapsed = e.Timestamp - leftInput.Timestamp;
                        if (elapsed < 200)
                            leftInput.Flags = 30;

                        Trackpad_TouchInput(e, CursorAction.CursorDown, CursorButton.TouchLeft);

                        LeftTrackpad.Opacity = TrackpadOpacity + TrackpadOpacityTouched;

                        // send vibration (todo: make it a setting)
                        ControllerManager.GetTargetController()?.Rumble(1, 125, 0);
                    }
                    break;
                case "RightTrackpad":
                    {
                        var elapsed = e.Timestamp - rightInput.Timestamp;
                        if (elapsed < 200)
                            rightInput.Flags = 30;

                        Trackpad_TouchInput(e, CursorAction.CursorDown, CursorButton.TouchRight);

                        RightTrackpad.Opacity = TrackpadOpacity + TrackpadOpacityTouched;

                        // send vibration (todo: make it a setting)
                        ControllerManager.GetTargetController()?.Rumble(1, 0, 125);
                    }
                    break;
            }

            e.Handled = true;
        }

        private void Trackpad_PreviewTouchUp(object sender, TouchEventArgs e)
        {
            string name = ((FrameworkElement)sender).Name;

            switch (name)
            {
                case "LeftTrackpad":
                    {
                        leftInput.Flags = 0;
                        Trackpad_TouchInput(e, CursorAction.CursorUp, CursorButton.TouchLeft);
                        LeftTrackpad.Opacity = TrackpadOpacity - TrackpadOpacityTouched;
                    }
                    break;
                case "RightTrackpad":
                    {
                        rightInput.Flags = 0;
                        Trackpad_TouchInput(e, CursorAction.CursorUp, CursorButton.TouchRight);
                        RightTrackpad.Opacity = TrackpadOpacity - TrackpadOpacityTouched;
                    }
                    break;
            }

            e.Handled = true;
        }
    }
}
