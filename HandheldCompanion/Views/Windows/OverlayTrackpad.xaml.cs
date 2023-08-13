using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using HandheldCompanion.Managers;
using HandheldCompanion.Views.Classes;
using static HandheldCompanion.DS4Touch;
using Application = System.Windows.Application;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace HandheldCompanion.Views.Windows;

/// <summary>
///     Interaction logic for Overlay.xaml
/// </summary>
public partial class OverlayTrackpad : OverlayWindow
{
    private readonly double dpiInput;

    private readonly TouchInput leftInput;
    private readonly TouchInput rightInput;

    private double TrackpadOpacity = 0.25;
    private readonly double TrackpadOpacityTouched = 0.10; // extra opacity when touched

    public OverlayTrackpad()
    {
        InitializeComponent();
        this._hotkeyId = 2;

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
                        var size = Convert.ToInt32(value);
                        LeftTrackpad.Width = size;
                        RightTrackpad.Width = size;
                        Height = size;
                        HorizontalAlignment = HorizontalAlignment.Stretch;
                    }
                    break;
                case "OverlayTrackpadsAlignment":
                    {
                        var trackpadsAlignment = Convert.ToInt32(value);
                        switch (trackpadsAlignment)
                        {
                            case 0:
                                VerticalAlignment = VerticalAlignment.Top;
                                break;
                            case 1:
                                VerticalAlignment = VerticalAlignment.Center;
                                break;
                            case 2:
                                VerticalAlignment = VerticalAlignment.Bottom;
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

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        // do something
    }

    public double GetWindowsScaling()
    {
        return Screen.PrimaryScreen.Bounds.Width / SystemParameters.PrimaryScreenWidth;
    }

    private void Trackpad_TouchInput(TouchEventArgs e, CursorAction action, CursorButton button)
    {
        var args = e.TouchDevice;
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

        var normalizedX = point.Position.X / LeftTrackpad.ActualWidth * dpiInput / 2.0d;
        var normalizedY = point.Position.Y / LeftTrackpad.ActualWidth * dpiInput;

        normalizedX += button == CursorButton.TouchRight ? 0.5d : 0.0d;

        switch (action)
        {
            case CursorAction.CursorUp:
                DS4Touch.OnMouseUp(normalizedX, normalizedY, button, flags);
                break;
            case CursorAction.CursorDown:
                DS4Touch.OnMouseDown(normalizedX, normalizedY, button, flags);
                break;
            case CursorAction.CursorMove:
                DS4Touch.OnMouseMove(normalizedX, normalizedY, button, flags);
                break;
        }
    }

    private void Trackpad_PreviewTouchMove(object sender, TouchEventArgs e)
    {
        var name = ((FrameworkElement)sender).Name;

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
        var name = ((FrameworkElement)sender).Name;

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
                    ControllerManager.GetTargetController()?.Rumble(); // (1, 25, 0, 60);
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
                    ControllerManager.GetTargetController()?.Rumble(); // (1, 25, 0, 60);
                }
                break;
        }

        e.Handled = true;
    }

    private void Trackpad_PreviewTouchUp(object sender, TouchEventArgs e)
    {
        var name = ((FrameworkElement)sender).Name;

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

    private class TouchInput
    {
        public short Flags;
        public int Timestamp;
    }

    private void LeftTrackpadClick_PreviewTouchDown(object sender, TouchEventArgs e)
    {
        DS4Touch.OutputClickButton = true;
    }

    private void RightTrackpadClick_PreviewTouchDown(object sender, TouchEventArgs e)
    {
        DS4Touch.OutputClickButton = true;
    }
}