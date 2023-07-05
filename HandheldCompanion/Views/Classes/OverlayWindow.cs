﻿using HandheldCompanion.Managers;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace HandheldCompanion.Views.Classes;

public class OverlayWindow : Window
{
    public HorizontalAlignment _HorizontalAlignment;
    public VerticalAlignment _VerticalAlignment;
    protected ushort _hotkeyId;

    private const int WM_MOUSEACTIVATE = 0x0021;
    private const int MA_NOACTIVATE = 0x0003;

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

        SourceInitialized += Overlay_SourceInitialized;
        IsVisibleChanged += OverlayWindow_IsVisibleChanged;
    }

    private void OverlayWindow_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_hotkeyId == 0)
            return;
        
        if (HotkeysManager.Hotkeys.TryGetValue(_hotkeyId, out Hotkey hotkey))
            hotkey.SetToggle(this.Visibility == Visibility.Visible ? true : false);
    }

    public new HorizontalAlignment HorizontalAlignment
    {
        get => _HorizontalAlignment;

        set
        {
            _HorizontalAlignment = value;
            UpdatePosition();
        }
    }

    public new VerticalAlignment VerticalAlignment
    {
        get => _VerticalAlignment;

        set
        {
            _VerticalAlignment = value;
            UpdatePosition();
        }
    }

    private void Overlay_SourceInitialized(object? sender, EventArgs e)
    {
        var source = PresentationSource.FromVisual(this) as HwndSource;
        source.AddHook(WndProc);

        //Set the window style to noactivate.
        var helper = new WindowInteropHelper(this);
        SetWindowLong(helper.Handle, GWL_EXSTYLE, GetWindowLong(helper.Handle, GWL_EXSTYLE) | WS_EX_NOACTIVATE);
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

    public virtual void ToggleVisibility()
    {
        // UI thread
        Application.Current.Dispatcher.Invoke(() =>
        {
            switch (Visibility)
            {
                case Visibility.Visible:
                    Hide();
                    break;
                case Visibility.Collapsed:
                case Visibility.Hidden:
                    Show();
                    break;
            }
        });
    }

    #region import

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    [DllImport("user32.dll")]
    public static extern IntPtr SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    #endregion
}