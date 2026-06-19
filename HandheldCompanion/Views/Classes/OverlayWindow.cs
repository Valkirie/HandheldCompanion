using HandheldCompanion.Helpers;
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using static HandheldCompanion.WinAPI;

namespace HandheldCompanion.Views.Classes;

public class OverlayWindow : Window
{
    public HorizontalAlignment _HorizontalAlignment;
    public VerticalAlignment _VerticalAlignment;

    public OverlayWindow()
    {
        // overlay specific settings
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;
        Focusable = false;
        ResizeMode = ResizeMode.NoResize;
        ShowActivated = false;
        FocusManager.SetIsFocusScope(this, false);

        SizeChanged += (o, e) => { UpdatePosition(); };

        Loaded += OverlayWindow_Loaded;
        IsVisibleChanged += OverlayWindow_IsVisibleChanged;
    }

    private void OverlayWindow_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        // TODO, IMPLEMENT ME
    }

    public new HorizontalAlignment HorizontalAlignment
    {
        get => _HorizontalAlignment;

        set
        {
            if (_HorizontalAlignment != value)
            {
                _HorizontalAlignment = value;
                UpdatePosition();
            }
        }
    }

    public new VerticalAlignment VerticalAlignment
    {
        get => _VerticalAlignment;

        set
        {
            if (_VerticalAlignment != value)
            {
                _VerticalAlignment = value;
                UpdatePosition();
            }
        }
    }

    private void OverlayWindow_Loaded(object sender, RoutedEventArgs e)
    {
        HwndSource source = (HwndSource)PresentationSource.FromVisual(this);
        source.AddHook(WndProc);

        //Set the window style to noactivate.
        var helper = new WindowInteropHelper(this);
        WinAPI.SetWindowLong(helper.Handle, GWL_EXSTYLE, WinAPI.GetWindowLong(helper.Handle, GWL_EXSTYLE) | WS_EX_NOACTIVATE);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_MOUSEACTIVATE)
        {
            handled = true;
            return new IntPtr(MA_NOACTIVATE);
        }
        else return IntPtr.Zero;
    }

    private void UpdatePosition()
    {
        var r = SystemParameters.WorkArea;

        switch (HorizontalAlignment)
        {
            case HorizontalAlignment.Left:
                Left = 0;
                break;

            default:
            case HorizontalAlignment.Center:
                Left = r.Width / 2 - Width / 2;
                break;

            case HorizontalAlignment.Right:
                Left = r.Right - Width;
                break;

            case HorizontalAlignment.Stretch:
                Left = 0;
                Width = SystemParameters.PrimaryScreenWidth;
                break;
        }

        switch (VerticalAlignment)
        {
            case VerticalAlignment.Top:
                Top = 0;
                break;

            default:
            case VerticalAlignment.Center:
                Top = r.Height / 2 - Height / 2;
                break;

            case VerticalAlignment.Bottom:
                Top = r.Height - Height;
                break;

            case VerticalAlignment.Stretch:
                Top = 0;
                Height = SystemParameters.PrimaryScreenHeight;
                break;
        }
    }

    public void SetVisibility(Visibility visibility)
    {
        // UI thread
        UIHelper.TryInvoke(() =>
        {
            this.Visibility = visibility;
        });
    }

    public virtual void ToggleVisibility()
    {
        // UI thread
        UIHelper.TryInvoke(() =>
        {
            switch (Visibility)
            {
                case Visibility.Visible:
                    Hide(true);
                    break;
                case Visibility.Collapsed:
                case Visibility.Hidden:
                    try { Show(); } catch { /* ItemsRepeater might have a NaN DesiredSize */ }
                    break;
            }
        }, DispatcherPriority.Normal);
    }

    public void Hide(bool collapse)
    {
        base.Hide();
        if (collapse)
            Visibility = Visibility.Collapsed;
    }
}