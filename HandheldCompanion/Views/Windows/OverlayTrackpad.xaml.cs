using ControllerCommon;
using HandheldCompanion.Common;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;

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

        public OverlayTrackpad()
        {
            InitializeComponent();

            // touch vars
            dpiInput = GetWindowsScaling();
            leftInput = new TouchInput();
            rightInput = new TouchInput();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // do something
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

            MainWindow.pipeClient.SendMessage(new PipeClientCursor
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

                        LeftTrackpad.Opacity += 0.10;
                    }
                    break;
                case "RightTrackpad":
                    {
                        var elapsed = e.Timestamp - rightInput.Timestamp;
                        if (elapsed < 200)
                            rightInput.Flags = 30;

                        Trackpad_TouchInput(e, CursorAction.CursorDown, CursorButton.TouchRight);

                        RightTrackpad.Opacity += 0.10;
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
                        LeftTrackpad.Opacity -= 0.10;
                    }
                    break;
                case "RightTrackpad":
                    {
                        rightInput.Flags = 0;
                        Trackpad_TouchInput(e, CursorAction.CursorUp, CursorButton.TouchRight);
                        RightTrackpad.Opacity -= 0.10;
                    }
                    break;
            }

            e.Handled = true;
        }

        public void UpdateVisibility()
        {
            this.Dispatcher.Invoke(() =>
            {
                Visibility visibility = Visibility.Visible;
                switch (Visibility)
                {
                    case Visibility.Visible:
                        visibility = Visibility.Collapsed;
                        break;
                    case Visibility.Collapsed:
                    case Visibility.Hidden:
                        visibility = Visibility.Visible;
                        break;
                }
                Visibility = visibility;
            });
        }
    }
}
