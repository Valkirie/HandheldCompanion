using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows;

namespace HandheldCompanion
{
    public class WindowSinker
    {
        #region Properties

        const UInt32 SWP_NOSIZE = 0x0001;
        const UInt32 SWP_NOMOVE = 0x0002;
        const UInt32 SWP_NOACTIVATE = 0x0010;
        const UInt32 SWP_NOZORDER = 0x0004;
        const int WM_ACTIVATEAPP = 0x001C;
        const int WM_ACTIVATE = 0x0006;
        const int WM_SETFOCUS = 0x0007;
        const int WM_WINDOWPOSCHANGING = 0x0046;

        static readonly IntPtr HWND_BOTTOM = new IntPtr(1);

        static readonly int HWND_TOPMOST = -1;

        Window Window;

        #endregion

        #region WindowSinker

        public WindowSinker(Window Window)
        {
            this.Window = Window;
        }

        #endregion

        #region Methods

        [DllImport("user32.dll")]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        static extern IntPtr DeferWindowPos(IntPtr hWinPosInfo, IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        static extern IntPtr BeginDeferWindowPos(int nNumWindows);

        [DllImport("user32.dll")]
        static extern bool EndDeferWindowPos(IntPtr hWinPosInfo);

        void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            var Handle = (new WindowInteropHelper(Window)).Handle;

            var Source = HwndSource.FromHwnd(Handle);
            Source.RemoveHook(new HwndSourceHook(WndProc));
        }

        void OnLoaded(object sender, RoutedEventArgs e)
        {
            var Hwnd = new WindowInteropHelper(Window).Handle;
            SetWindowPos(Hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);

            var Handle = (new WindowInteropHelper(Window)).Handle;

            var Source = HwndSource.FromHwnd(Handle);
            Source.AddHook(new HwndSourceHook(WndProc));
        }

        IntPtr WndProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_SETFOCUS)
            {
                hWnd = new WindowInteropHelper(Window).Handle;
                SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);
                handled = true;
            }
            return IntPtr.Zero;
        }

        public void Sink()
        {
            Window.Loaded += OnLoaded;
            Window.Closing += OnClosing;
        }

        public void Unsink()
        {
            Window.Loaded -= OnLoaded;
            Window.Closing -= OnClosing;
        }

        #endregion
    }

    public static class WindowExtensions
    {
        #region Always On Bottom

        public static readonly DependencyProperty SinkerProperty = DependencyProperty.RegisterAttached("Sinker", typeof(WindowSinker), typeof(WindowExtensions), new UIPropertyMetadata(null));
        public static WindowSinker GetSinker(DependencyObject obj)
        {
            return (WindowSinker)obj.GetValue(SinkerProperty);
        }
        public static void SetSinker(DependencyObject obj, WindowSinker value)
        {
            obj.SetValue(SinkerProperty, value);
        }

        public static readonly DependencyProperty AlwaysOnBottomProperty = DependencyProperty.RegisterAttached("AlwaysOnBottom", typeof(bool), typeof(WindowExtensions), new UIPropertyMetadata(false, OnAlwaysOnBottomChanged));
        public static bool GetAlwaysOnBottom(DependencyObject obj)
        {
            return (bool)obj.GetValue(AlwaysOnBottomProperty);
        }
        public static void SetAlwaysOnBottom(DependencyObject obj, bool value)
        {
            obj.SetValue(AlwaysOnBottomProperty, value);
        }
        static void OnAlwaysOnBottomChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            var Window = sender as Window;
            if (Window != null)
            {
                if ((bool)e.NewValue)
                {
                    var Sinker = new WindowSinker(Window);
                    Sinker.Sink();
                    SetSinker(Window, Sinker);
                }
                else
                {
                    var Sinker = GetSinker(Window);
                    Sinker.Unsink();
                    SetSinker(Window, null);
                }
            }
        }

        #endregion
    }
}
