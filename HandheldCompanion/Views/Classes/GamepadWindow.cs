using HandheldCompanion.Helpers;
using HandheldCompanion.Managers;
using HandheldCompanion.Utils;
using HandheldCompanion.Views.Windows;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
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

        public GamepadWindow()
        {
            LayoutUpdated += OnLayoutUpdated;
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
            return IntPtr.Zero;
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
