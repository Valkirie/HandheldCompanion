using HandheldCompanion.Helpers;
using HandheldCompanion.Managers;
using HandheldCompanion.Shared;
using HandheldCompanion.Utils;
using HandheldCompanion.Views.Windows;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using WpfScreenHelper;

namespace HandheldCompanion.Views.Classes
{
    public class GamepadWindow : Window
    {
        public List<Control> controlElements => currentDialog is not null ? WPFUtils.GetElementsFromPopup<Control>(frameworkElements) : frameworkElements.OfType<Control>().ToList();
        public List<FrameworkElement> frameworkElements
        {
            get
            {
                List<FrameworkElement> children = WPFUtils.FindChildren(this);
                foreach (FrameworkElement frameworkElement in children)
                    frameworkElement.FocusVisualStyle = null;

                return children;
            }
        }

        public ContentDialog currentDialog;
        private ContentDialog contentDialog => ContentDialog.GetOpenDialog(this);

        protected UIGamepad gamepadFocusManager;

        public HwndSource hwndSource;

        public bool HasForeground() => this is OverlayQuickTools || (WinAPI.GetForegroundWindow() == this.hwndSource.Handle);
        public bool IsPrimary => GetScreen().Primary;
        public bool IsIconic => ProcessUtils.IsIconic(this.hwndSource.Handle);

        private AdornerLayer _adornerLayer;
        private HighlightAdorner _highlightAdorner;

        protected const int WM_DISPLAYCHANGE = 0x007E;
        protected const int WM_DPICHANGED = 0x02E0;
        protected const int WM_POWERBROADCAST = 0x0218;
        protected const int WM_PAINT = 0x000F;

        // hack variables
        private Timer WMPaintTimer = new(100) { AutoReset = false };
        private bool WMPaintPending = false;
        private DateTime prevDraw = DateTime.MinValue;

        [DllImport("dwmapi.dll")]
        private static extern int DwmFlush();

        public GamepadWindow()
        {
            LayoutUpdated += OnLayoutUpdated;
            StateChanged += Window_StateChanged;
            IsVisibleChanged += Window_VisibleChanged;

            WMPaintTimer.Elapsed += WMPaintTimer_Elapsed;
        }

        protected virtual void Window_VisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsLoaded)
                WMPaint_Trigger();
        }

        protected virtual void Window_StateChanged(object? sender, EventArgs e)
        {
            if (IsLoaded)
                WMPaint_Trigger();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            hwndSource = HwndSource.FromHwnd(hwnd);
            hwndSource.AddHook(WndProc);

            base.OnSourceInitialized(e);
        }

        protected virtual IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case WM_PAINT:
                    DateTime drawTime = DateTime.Now;

                    double drawDiff = Math.Abs((prevDraw - drawTime).TotalMilliseconds);
                    if (drawDiff < 200)
                        WMPaint_Trigger();

                    // update previous drawing time
                    prevDraw = drawTime;
                    break;
            }

            return IntPtr.Zero;
        }

        public void WMPaint_Trigger()
        {
            if (!WMPaintPending)
            {
                // disable GPU acceleration
                RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;

                // set flag
                WMPaintPending = true;

                LogManager.LogWarning("ProcessRenderMode set to {0}", RenderOptions.ProcessRenderMode);
            }

            if (WMPaintPending)
            {
                WMPaintTimer.Stop();
                WMPaintTimer.Start();
            }
        }

        private void WMPaintTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            if (WMPaintPending)
            {
                // enable GPU acceleration
                RenderOptions.ProcessRenderMode = RenderMode.Default;

                // reset flag
                WMPaintPending = false;

                LogManager.LogWarning("ProcessRenderMode set to {0}", RenderOptions.ProcessRenderMode);
            }
        }

        public void SetFocusedElement(Control focusedControl)
        {
            // store current focused control
            this.focusedControl = focusedControl;

            // UI thread
            UIHelper.TryInvoke(() =>
            {
                if (_highlightAdorner != null)
                {
                    _adornerLayer.Remove(_highlightAdorner);
                    _highlightAdorner = null;
                }

                _adornerLayer = AdornerLayer.GetAdornerLayer(focusedControl);
                if (_adornerLayer != null)
                {
                    _highlightAdorner = new HighlightAdorner(focusedControl);
                    _adornerLayer.Add(_highlightAdorner);
                }
            });
        }

        private Control focusedControl;
        public Control GetFocusedElement()
        {
            return focusedControl;
        }

        public Screen GetScreen()
        {
            return Screen.FromHandle(hwndSource.Handle);
        }

        private void OnLayoutUpdated(object? sender, EventArgs e)
        {
            if (this.Visibility != Visibility.Visible || this.WindowState == WindowState.Minimized)
                return;

            // check if a content dialog is open
            if (contentDialog is not null)
            {
                // a content dialog just opened
                if (currentDialog is null)
                {
                    // store content dialog
                    currentDialog = contentDialog;

                    // raise event
                    ContentDialogOpened?.Invoke(currentDialog);
                }
            }
            else if (contentDialog is null)
            {
                // a content dialog just closed
                if (currentDialog is not null)
                {
                    // raise event
                    ContentDialogClosed?.Invoke(currentDialog);

                    // clear content dialog
                    currentDialog = null;
                }
            }
        }

        protected void InvokeGotGamepadWindowFocus()
        {
            GotGamepadWindowFocus?.Invoke(this);
        }

        protected void InvokeLostGamepadWindowFocus()
        {
            LostGamepadWindowFocus?.Invoke(this);
        }

        #region events
        public event GotGamepadWindowFocusEventHandler GotGamepadWindowFocus;
        public delegate void GotGamepadWindowFocusEventHandler(object sender);

        public event LostGamepadWindowFocusEventHandler LostGamepadWindowFocus;
        public delegate void LostGamepadWindowFocusEventHandler(object sender);

        public event ContentDialogOpenedEventHandler ContentDialogOpened;
        public delegate void ContentDialogOpenedEventHandler(ContentDialog contentDialog);

        public event ContentDialogClosedEventHandler ContentDialogClosed;
        public delegate void ContentDialogClosedEventHandler(ContentDialog contentDialog);
        #endregion
    }
}
