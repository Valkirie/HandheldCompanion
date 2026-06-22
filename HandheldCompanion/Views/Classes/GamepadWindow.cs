using HandheldCompanion.Helpers;
using HandheldCompanion.Managers;
using HandheldCompanion.Utils;
using HandheldCompanion.Views.Windows;
using iNKORE.UI.WPF.Modern.Controls;
using iNKORE.UI.WPF.Modern.Controls.Primitives;
using System.Collections;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Navigation;
using System.Windows.Threading;
using WpfScreenHelper;
using static HandheldCompanion.WinAPI;
using Frame = System.Windows.Controls.Frame;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;

namespace HandheldCompanion.Views.Classes
{
    public class GamepadWindow : Window
    {
        public virtual string HomePageKey => string.Empty;

        // When a ContentDialog or a DropDownButton flyout is open, restrict navigation to
        // only the controls that live inside that popup / dialog.
        public DropDownButton? currentFlyoutButton;
        public FlyoutBase? currentFlyout;

        public List<Control> controlElements
        {
            get
            {
                if (currentDialog is not null || currentMessageBox is not null || currentFlyoutButton is not null)
                {
                    List<Control> popupElements = WPFUtils.GetElementsFromPopup<Control>(frameworkElements);
                    List<Control> adornerElements = WPFUtils.GetElementsFromAdornerLayer<Control>(frameworkElements);

                    return popupElements.Union(adornerElements).ToList<Control>();
                }
                else if (currentFlyout is not null)
                    return WPFUtils.GetElementsFromFlyout<Control>(currentFlyout);
                else
                    return frameworkElements.OfType<Control>().ToList();
            }
        }

        public List<FrameworkElement> frameworkElements
        {
            get
            {
                List<FrameworkElement> children = WPFUtils.FindChildren(this);
                foreach (FrameworkElement frameworkElement in children)
                    DisableKeyboardFocusVisuals(frameworkElement);

                return children;
            }
        }

        private static void DisableKeyboardFocusVisuals(FrameworkElement frameworkElement)
        {
            if (frameworkElement.FocusVisualStyle is not null)
                frameworkElement.FocusVisualStyle = null;

            PropertyInfo? useSystemFocusVisualsProperty = frameworkElement.GetType().GetProperty("UseSystemFocusVisuals", BindingFlags.Instance | BindingFlags.Public);
            if (useSystemFocusVisualsProperty?.PropertyType == typeof(bool) && useSystemFocusVisualsProperty.CanWrite)
                useSystemFocusVisualsProperty.SetValue(frameworkElement, false);
        }

        public ContentDialog? currentDialog;
        public MessageBox? currentMessageBox;

        protected UIGamepad gamepadFocusManager = null!;

        /// <summary>
        /// Suppresses the next button-state change on this window's focus manager.
        /// Call this on the destination window just before it gains focus mid-press,
        /// so that buttons still held during the transition are not replayed as new presses.
        /// </summary>
        public void SuppressNextGamepadInput() => gamepadFocusManager?.SuppressNextInput();

        public HwndSource hwndSource = null!;

        public bool HasForeground() => this is OverlayQuickTools || (WinAPI.GetForegroundWindow() == this.hwndSource.Handle);
        public bool IsPrimary => GetScreen().Primary;
        public bool IsIconic => ProcessUtils.IsIconic(this.hwndSource.Handle);

        private AdornerLayer? _adornerLayer;
        private HighlightAdorner? _highlightAdorner;
        private readonly List<ContentDialog> _contentDialogControls = [];
        private readonly List<MessageBox> _messageBoxControls = [];
        private readonly HashSet<FrameworkElement> _observedPages = new();
        private System.Windows.Controls.Frame? _contentFrame;
        private bool _contentFrameHooksInitialized;

        protected readonly DispatcherTimer _navDebounceTimer;
        protected string _pendingNavTag = string.Empty;

        public GamepadWindow()
        {
            Loaded += GamepadWindow_Loaded;
            StateChanged += Window_StateChanged;
            IsVisibleChanged += Window_VisibleChanged;

            _navDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _navDebounceTimer.Tick += NavDebounceTimer_Tick;
        }

        private void NavDebounceTimer_Tick(object? sender, EventArgs e)
        {
            _navDebounceTimer.Stop();
            ApplyPendingNavigation(_pendingNavTag);
        }

        protected virtual void ApplyPendingNavigation(string navItemTag) { }

        public virtual void NavigateToPage(string navItemTag) { }

        protected virtual void Window_VisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
        }

        protected virtual void Window_StateChanged(object? sender, EventArgs e)
        {
        }

        private void ContentDialog_Opened(object? sender, ContentDialogOpenedEventArgs e)
        {
            if (sender is ContentDialog contentDialog)
            {
                currentDialog = contentDialog;
                ContentDialogOpened?.Invoke(contentDialog);
            }
        }

        private void ContentDialog_Closed(object? sender, ContentDialogClosedEventArgs e)
        {
            if (sender is ContentDialog contentDialog)
            {
                currentDialog = null;
                ContentDialogClosed?.Invoke(contentDialog);
            }
        }

        private void MessageBox_Opened(object? sender, MessageBoxOpenedEventArgs e)
        {
            if (sender is MessageBox messageBox)
            {
                currentMessageBox = messageBox;
                MessageBoxOpened?.Invoke(messageBox);
            }
        }

        private void MessageBox_Closed(object? sender, MessageBoxClosedEventArgs e)
        {
            if (sender is MessageBox messageBox)
            {
                currentMessageBox = null;
                MessageBoxClosed?.Invoke(messageBox);
            }
        }

        private void GamepadWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_contentFrameHooksInitialized)
            {
                _contentFrame = FindName("ContentFrame") as Frame ?? WPFUtils.FindChildren(this).OfType<Frame>().FirstOrDefault();

                if (_contentFrame is not null)
                    _contentFrame.LoadCompleted += ContentFrame_Navigated;

                _contentFrameHooksInitialized = true;
            }

            RegisterModalControls(this);

            if (_contentFrame?.Content is not null)
                RegisterModalControls(_contentFrame.Content);
        }

        private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
        {
            if (e.Content is not null)
                RegisterModalControls(e.Content);
        }

        private void RegisterModalControls(object root)
        {
            foreach (FieldInfo field in root.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                RegisterModalControl(field.GetValue(root));
            }

            if (root is FrameworkElement frameworkElement)
            {
                foreach (object? value in frameworkElement.Resources.Values)
                    RegisterModalControl(value);
            }
        }

        private void RegisterModalControl(object? value)
        {
            switch (value)
            {
                case ContentDialog contentDialog:
                    if (_contentDialogControls.Contains(contentDialog))
                        return;

                    contentDialog.Opened += ContentDialog_Opened;
                    contentDialog.Closed += ContentDialog_Closed;
                    _contentDialogControls.Add(contentDialog);

                    if (ContentDialog.GetOpenDialog(this) == contentDialog)
                        ContentDialog_Opened(contentDialog, null!);
                    return;

                case MessageBox messageBox:
                    if (_messageBoxControls.Contains(messageBox))
                        return;

                    messageBox.Opened += MessageBox_Opened;
                    messageBox.Closed += MessageBox_Closed;
                    _messageBoxControls.Add(messageBox);
                    return;
            }
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
                    _adornerLayer?.Remove(_highlightAdorner);
                    _highlightAdorner = null;
                }

                // skip navigation view items, they have their own focus visual logic
                if (focusedControl is NavigationViewItem)
                    return;

                _adornerLayer = AdornerLayer.GetAdornerLayer(focusedControl);
                if (_adornerLayer != null)
                {
                    _highlightAdorner = new HighlightAdorner(focusedControl);
                    _adornerLayer.Add(_highlightAdorner);
                }
            });
        }

        private Control focusedControl = null!;
        public Control GetFocusedElement()
        {
            return focusedControl;
        }

        public Screen GetScreen()
        {
            return Screen.FromHandle(hwndSource.Handle);
        }

        public void ClearMouseHover()
        {
            if (hwndSource?.Handle is IntPtr hwnd && hwnd != IntPtr.Zero)
                WinAPI.PostMessage(hwnd, WM_MOUSELEAVE, IntPtr.Zero, IntPtr.Zero);
        }

        protected void InvokeGotGamepadWindowFocus()
        {
            GotGamepadWindowFocus?.Invoke(this);
        }

        protected void InvokeLostGamepadWindowFocus()
        {
            LostGamepadWindowFocus?.Invoke(this);
        }

        public void Hide(bool collapse)
        {
            base.Hide();
            if (collapse)
                Visibility = Visibility.Collapsed;
        }

        #region events
        public event GotGamepadWindowFocusEventHandler? GotGamepadWindowFocus;
        public delegate void GotGamepadWindowFocusEventHandler(object sender);

        public event LostGamepadWindowFocusEventHandler? LostGamepadWindowFocus;
        public delegate void LostGamepadWindowFocusEventHandler(object sender);

        public event ContentDialogOpenedEventHandler? ContentDialogOpened;
        public delegate void ContentDialogOpenedEventHandler(ContentDialog contentDialog);

        public event ContentDialogClosedEventHandler? ContentDialogClosed;
        public delegate void ContentDialogClosedEventHandler(ContentDialog contentDialog);

        public event MessageBoxOpenedEventHandler? MessageBoxOpened;
        public delegate void MessageBoxOpenedEventHandler(MessageBox messageBox);

        public event MessageBoxClosedEventHandler? MessageBoxClosed;
        public delegate void MessageBoxClosedEventHandler(MessageBox messageBox);
        #endregion
    }
}
