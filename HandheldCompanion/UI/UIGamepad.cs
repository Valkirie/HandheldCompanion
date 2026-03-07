using HandheldCompanion.Controllers;
using HandheldCompanion.Helpers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Shared;
using HandheldCompanion.UI;
using HandheldCompanion.Utils;
using HandheldCompanion.ViewModels;
using HandheldCompanion.Views;
using HandheldCompanion.Views.Classes;
using HandheldCompanion.Views.Windows;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Navigation;
using System.Windows.Threading;
using Frame = iNKORE.UI.WPF.Modern.Controls.Frame;
using ListView = System.Windows.Controls.ListView;
using ListViewItem = System.Windows.Controls.ListViewItem;
using Page = System.Windows.Controls.Page;
using Timer = System.Timers.Timer;

namespace HandheldCompanion.Managers
{
    public class UIGamepad
    {
        #region events
        public static event GotFocusEventHandler GotFocus;
        public delegate void GotFocusEventHandler(string Name);

        public static event LostFocusEventHandler LostFocus;
        public delegate void LostFocusEventHandler(string Name);
        #endregion

        private GamepadWindow gamepadWindow;
        private string windowName = string.Empty;

        private ScrollViewer scrollViewer;

        // NavigationViews
        // - windowNavigationView: the NavigationView hosted by the Window (e.g. MainWindow)
        // - pageNavigationView: an optional NavigationView hosted inside the current Page (e.g. LayoutPage)
        private NavigationView windowNavigationView;
        private NavigationView pageNavigationView;

        private Frame gamepadFrame;
        private Page gamepadPage;
        private Timer gamepadTimer;

        // tooltip
        private static Timer tooltipTimer;
        private static ToolTip tooltip = new ToolTip
        {
            Content = "This is a tooltip!",
            Placement = PlacementMode.Top,
            IsOpen = false // Start with tooltip hidden
        };

        private bool _goingBack;
        private bool _goingForward;
        private bool _navigating;

        private bool _rendered;
        private object _rendering = new();

        private ButtonState prevButtonState = new();

        // store the latest NavigationViewItem that had focus on this window (windowNavigationView)
        private Control prevNavigation;

        // store the latest NavigationViewItem that had focus on the current page (pageNavigationView)
        private Control prevPageNavigation;
        // key: Page, store the latest control that had focus on this page
        private Dictionary<object, Control> prevControl = [];
        // key: Window, store which window has focus
        private static ConcurrentDictionary<string, bool> _focused = new();

        private bool IsQuicktools => this.windowName.Equals("QuickTools");
        private bool IsMainWindow => !IsQuicktools;

        public static bool HasFocus()
        {
            return _focused.Any(w => w.Value);
        }

        private enum FocusSource
        {
            Visibility,
            Activate,
            Focus
        }

        public UIGamepad(GamepadWindow gamepadWindow, Frame contentFrame)
        {
            // set current window
            this.gamepadWindow = gamepadWindow;
            this.gamepadWindow.ContentDialogOpened += ContentDialogOpened;
            this.gamepadWindow.ContentDialogClosed += ContentDialogClosed;

            this.windowName = gamepadWindow.Tag.ToString();

            if (gamepadWindow is OverlayQuickTools quickTools)
            {
                quickTools.GotGamepadWindowFocus += (sender) => WindowGotFocus(null, null, FocusSource.Visibility);
                quickTools.LostGamepadWindowFocus += (sender) => WindowLostFocus(null, null, FocusSource.Visibility);
            }
            else if (gamepadWindow is MainWindow mainWindow)
            {
                mainWindow.GotFocus += (sender, e) => WindowGotFocus(sender, e, FocusSource.Focus);
                mainWindow.LostFocus += (sender, e) => WindowLostFocus(sender, e, FocusSource.Focus);
                mainWindow.Activated += (sender, e) => WindowGotFocus(sender, null, FocusSource.Activate);
                mainWindow.Deactivated += (sender, e) => WindowLostFocus(sender, null, FocusSource.Activate);
                mainWindow.StateChanged += (sender, e) =>
                {
                    switch (mainWindow.WindowState)
                    {
                        case WindowState.Normal:
                        case WindowState.Maximized:
                            WindowGotFocus(sender, null, FocusSource.Activate);
                            break;
                        case WindowState.Minimized:
                            WindowLostFocus(sender, null, FocusSource.Activate);
                            break;
                    }
                };
            }

            gamepadFrame = contentFrame;
            gamepadFrame.Navigated += ContentNavigated;

            gamepadTimer = new Timer(250) { AutoReset = false };
            gamepadTimer.Elapsed += ContentRendered;

            tooltipTimer = new Timer(2000) { AutoReset = false };
            tooltipTimer.Elapsed += TooltipTimer_Elapsed;

            ControllerManager.InputsUpdated += InputsUpdated;
        }

        public void Loaded()
        {
            this.scrollViewer = WPFUtils.FindVisualChild<ScrollViewer>(gamepadWindow);
            this.windowNavigationView = WPFUtils.FindVisualChild<NavigationView>(gamepadWindow);

            // will be resolved once the first Page is rendered
            this.pageNavigationView = null;
        }

        private void ContentDialogClosed(ContentDialog contentDialog)
        {
            if (gamepadPage is not null && prevControl.TryGetValue(gamepadPage.Tag, out Control control))
            {
                if (_focused[windowName])
                    Focus(control);
            }
        }

        private bool HasFlyoutOpen = false;
        private List<MenuItem> flyoutMenuItems = new();  // populated from MenuFlyout.Items when open
        private MenuItem focusedFlyoutItem = null;        // tracks which item is highlighted

        private void ContentDialogOpened(ContentDialog contentDialog)
        {
            // Defer: ContentDialog children are not in the visual tree yet when this
            // event fires (OnLayoutUpdated calls us synchronously mid-layout-pass).
            gamepadWindow.Dispatcher.BeginInvoke(() =>
            {
                Control control = WPFUtils.GetTopLeftControl<Control>(gamepadWindow.controlElements);
                Focus(control);
            }, DispatcherPriority.Loaded);
        }

        private void WindowGotFocus(object sender, RoutedEventArgs e, FocusSource focusSource)
        {
            // already has focus
            if (_focused.TryGetValue(windowName, out bool isFocused) && isFocused)
                return;

            // check focus based on our scenarios
            bool gamepadFocused = false;

            WindowState windowState = gamepadWindow.WindowState;
            if (windowState != WindowState.Minimized)
            {
                switch (focusSource)
                {
                    case FocusSource.Visibility:
                        gamepadFocused = gamepadWindow.IsHitTestVisible && gamepadWindow.IsVisible;

                        // only send gamepad inputs to quicktools if it's on main screen
                        // this is important for dual screen devices
                        if (gamepadWindow is OverlayQuickTools)
                            gamepadFocused &= gamepadWindow.IsPrimary;
                        break;
                    case FocusSource.Activate:
                        gamepadFocused = gamepadWindow.IsActive;
                        break;
                    case FocusSource.Focus:
                        gamepadFocused = gamepadWindow.IsFocused;
                        break;
                }
            }

            // set focus
            _focused[windowName] = gamepadFocused;

            // raise event
            if (_focused[windowName])
            {
                LogManager.LogDebug("GotFocus: {0}", windowName);
                GotFocus?.Invoke(windowName);

                foreach (string window in _focused.Keys)
                {
                    if (window.Equals(windowName))
                        continue;

                    if (_focused.TryGetValue(window, out isFocused) && !isFocused)
                        continue;

                    // remove focus
                    _focused[window] = false;

                    // raise event
                    LostFocus?.Invoke(window);
                }
            }

            if (gamepadPage is not null && gamepadPage.IsLoaded)
                ContentRendered(null, null);
        }

        private void WindowLostFocus(object sender, RoutedEventArgs e, FocusSource focusSource)
        {
            // doesn't have focus
            if (_focused.TryGetValue(windowName, out bool isFocused) && !isFocused)
                return;

            // check if sender is part of current window
            if (e is not null && e.OriginalSource is not null)
            {
                Window yourParentWindow = Window.GetWindow((DependencyObject)e.OriginalSource);

                // sender is part of parent window, return
                if (yourParentWindow == gamepadWindow)
                    return;
            }

            // unset focus
            _focused[windowName] = false;

            // halt timer
            gamepadTimer.Stop();

            // raise event
            LogManager.LogDebug("LostFocus: {0}", windowName);
            LostFocus?.Invoke(windowName);

            foreach (string window in _focused.Keys)
            {
                if (window.Equals(windowName))
                    continue;

                GamepadWindow gamepadWindow;
                switch (window)
                {
                    default:
                    case "Main":
                        gamepadWindow = MainWindow.GetCurrent();
                        break;
                    case "QuickTools":
                        gamepadWindow = OverlayQuickTools.GetCurrent();
                        break;
                }

                if (gamepadWindow.Visibility != Visibility.Visible)
                    continue;

                if (gamepadWindow.WindowState == WindowState.Minimized)
                    continue;

                if (!gamepadWindow.IsActive && gamepadWindow is MainWindow)
                    continue;

                if (!gamepadWindow.IsPrimary)
                    continue;

                if (_focused.TryGetValue(window, out isFocused) && isFocused)
                    continue;

                // set focus
                _focused[window] = true;

                // raise event
                if (_focused[window])
                    GotFocus?.Invoke(window);
            }

            // hide tooltip
            tooltip.PlacementTarget = null;
            tooltip.IsOpen = false;
        }

        private void ContentNavigated(object sender, NavigationEventArgs e)
        {
            lock (_rendering)
            {
                // halt timer
                gamepadTimer.Stop();

                // set state(s)
                _rendered = false;

                // remove state(s)
                _navigating = false;

                // store current Frame and listen to render events
                if (gamepadPage != (Page)gamepadFrame.Content)
                {
                    gamepadFrame = (Frame)sender;
                    gamepadFrame.ContentRendered += ContentRendering;

                    // store current Page
                    gamepadPage = (Page)gamepadFrame.Content;

                    // reset page-scoped navigation view state
                    pageNavigationView = null;
                    prevPageNavigation = null;
                }
                else
                {
                    // page already rendered
                    ContentRendered(null, null);
                }
            }
        }

        private void ContentRendering(object? sender, EventArgs e)
        {
            gamepadTimer.Stop();
            gamepadTimer.Start();
        }

        private void ContentRendered(object? sender, System.Timers.ElapsedEventArgs e)
        {
            // stop listening for render events
            gamepadFrame.ContentRendered -= ContentRendering;

            // UI thread
            UIHelper.TryInvoke(() =>
            {
                // refresh page-scoped NavigationView if any
                // (e.g. LayoutPage has its own NavigationView hosted inside the Page)
                pageNavigationView = WPFUtils.FindVisualChild<NavigationView>(gamepadPage);

                // store selected navigation items (window and page)
                if (windowNavigationView?.SelectedItem is NavigationViewItem windowNvi)
                    prevNavigation = windowNvi;

                if (pageNavigationView?.SelectedItem is NavigationViewItem pageNvi)
                    prevPageNavigation = pageNvi;

                // update status
                _navigating = _goingForward;

                List<Type> IgnoreList = new() { typeof(NavigationViewItem) };

                if (IsQuicktools)
                {
                    IgnoreList.Add(typeof(AppBarButton));
                }

                Control control;
                if (prevControl.TryGetValue(gamepadPage.Tag, out control))
                {
                    if (_goingBack)
                    {
                        Focus(control);

                        // remove state
                        _goingBack = false;
                    }
                    else if (_navigating && control is not null)
                    {
                        Focus(control);
                    }
                    else if (_navigating && control is null)
                    {
                        // get the nearest non-navigation control
                        control = WPFUtils.GetTopLeftControl<Control>(gamepadWindow.controlElements, IgnoreList);
                        Focus(control);
                    }
                }
                else if (_navigating)
                {
                    // get the nearest non-navigation control
                    control = WPFUtils.GetTopLeftControl<Control>(gamepadWindow.controlElements, IgnoreList);
                    Focus(control);
                }

                // set rendering state
                _rendered = true;
            });
        }

        private Control forcedFocus;
        private Control parentFocus;

        private void TooltipTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            // UI thread
            UIHelper.TryInvoke(() =>
            {
                tooltip.PlacementTarget = null;
                tooltip.IsOpen = false;
            });
        }

        public void Focus(Control control, Control parent = null, bool force = false)
        {
            if (control is null || !HasFocus())
                return;

            // prevent keyboard focus from overlapping with our own tooltip logic
            ToolTipService.SetShowsToolTipOnKeyboardFocus(control, false);

            // manage tooltip
            if (tooltip.PlacementTarget != control)
            {
                // hide tooltip
                tooltip.IsOpen = false;

                // change target
                tooltip.PlacementTarget = control;

                // (re)start timer
                tooltipTimer.Stop();
                tooltipTimer.Start();
            }

            if (control.ToolTip is not null)
            {
                tooltip.Content = control.ToolTip.ToString();
                tooltip.IsOpen = true;
            }

            // set tooltip initial delay
            string controlType = control.GetType().Name;
            switch (controlType)
            {
                case "ContentDialog":
                    return;
            }

            if (force)
            {
                forcedFocus = control;
                parentFocus = parent;
            }
            else
            {
                forcedFocus = null;
                parentFocus = null;
            }

            // set focus to control
            control.Focus();
            control.BringIntoView();
            Keyboard.Focus(control);
            FocusManager.SetFocusedElement(gamepadWindow, control);
            gamepadWindow.SetFocusedElement(control);
        }

        public Control GetFocusedElement()
        {
            // When a flyout is open, return the tracked popup item directly.
            // Popup elements have no common visual ancestor with gamepadWindow,
            // so the FindCommonAncestor check below would incorrectly reset focus.
            if (HasFlyoutOpen && focusedFlyoutItem is not null)
                return focusedFlyoutItem;

            IInputElement FocusedElement = forcedFocus is not null ? forcedFocus : gamepadWindow.GetFocusedElement();

            DependencyObject commonAncestor = VisualTreeHelperExtensions.FindCommonAncestor((DependencyObject)FocusedElement, gamepadWindow);
            if (commonAncestor is null && forcedFocus is null)
            {
                FocusManager.SetFocusedElement(gamepadWindow, WPFUtils.GetTopLeftControl<Control>(gamepadWindow.controlElements));
                FocusedElement = FocusManager.GetFocusedElement(gamepadWindow);
            }

            if (FocusedElement is null)
                FocusedElement = gamepadWindow;

            if (FocusedElement.Focusable && FocusedElement is Control)
            {
                Control controlFocused = (Control)FocusedElement;

                string keyboardType = controlFocused.GetType().Name;

                switch (keyboardType)
                {
                    case "MainWindow":
                    case "OverlayQuickTools":
                    case "ScrollViewer":
                    case "TouchScrollViewer":
                        {
                            // a new page opened
                            if (prevNavigation is not null)
                                controlFocused = prevNavigation;
                        }
                        break;

                    case "NavigationViewItem":
                        break;

                    default:
                        {
                            // store current control if not part of a dialog or open flyout
                            if (gamepadPage is not null && gamepadWindow.currentDialog is null && !HasFlyoutOpen)
                                prevControl[gamepadPage.Tag] = controlFocused;
                        }
                        break;
                }

                if (controlFocused is not null)
                {
                    // pick the last known Control
                    return controlFocused;
                }
                else
                {
                    // pick nearest navigation element
                    return WPFUtils.GetTopLeftControl<NavigationViewItem>(gamepadWindow.controlElements);
                }
            }
            else
            {
                // pick nearest navigation element
                return WPFUtils.GetTopLeftControl<Control>(gamepadWindow.controlElements);
            }

            return null;
        }

        private static List<Control> GetNavigationItems(NavigationView navView)
        {
            if (navView is null)
                return [];

            // Prefer logical order from MenuItems/FooterMenuItems (stable and matches UI order).
            // Fallback to visual tree enumeration if containers are not NavigationViewItem instances.
            var ordered = new List<Control>();

            try
            {
                if (navView.MenuItems is not null)
                {
                    foreach (var mi in navView.MenuItems)
                    {
                        if (mi is NavigationViewItem nvi && nvi.IsEnabled && nvi.IsVisible)
                            ordered.Add(nvi);
                    }
                }

                if (navView.FooterMenuItems is not null)
                {
                    foreach (var mi in navView.FooterMenuItems)
                    {
                        if (mi is NavigationViewItem nvi && nvi.IsEnabled && nvi.IsVisible)
                            ordered.Add(nvi);
                    }
                }
            }
            catch
            {
                // Some NavigationView implementations may throw or not expose these collections.
                // We'll fall back to visual enumeration below.
                ordered.Clear();
            }

            if (ordered.Count > 0)
                return ordered;

            // Fallback: collect all NavigationViewItem instances within this NavigationView.
            // This ensures navigation does not "jump" across nested NavigationViews.
            return WPFUtils.FindVisualChildren<NavigationViewItem>(navView)
                           .Where(i => i is Control c && c.IsEnabled && c.IsVisible)
                           .Cast<Control>()
                           .ToList();
        }

        private static bool IsTopPaneNavigationView(NavigationView navView)
        {
            // For our purposes, only a fully left-displayed pane should be treated as a navigation "sidebar".
            return navView is not null && navView.PaneDisplayMode == NavigationViewPaneDisplayMode.Top;
        }

        private static WPFUtils.Direction GetDirectionTowardsPane(NavigationView navView)
        {
            // Used when we want to move focus "back" to the pane items from content.
            // Left-pane -> Left, Top-pane -> Up.
            return navView.PaneDisplayMode == NavigationViewPaneDisplayMode.Top ? WPFUtils.Direction.Up : WPFUtils.Direction.Left;
        }

        private static NavigationViewItem? GetSelectedNavigationViewItem(NavigationView navView)
        {
            if (navView is null)
                return null;

            // Best effort: SelectedItem can be a container or a data item.
            if (navView.SelectedItem is NavigationViewItem selected)
                return selected;

            // Fallback: find the visual container that is marked selected.
            return WPFUtils.FindVisualChildren<NavigationViewItem>(navView).FirstOrDefault(i => i.IsSelected);
        }

        // declare a DateTime variable to store the last time the function was called
        private DateTime lastCallTime;

        // declare a DateTime variable to store the last time the button state changed
        private DateTime lastChangeTime;

        private void InputsUpdated(ControllerState controllerState, bool IsMapped)
        {
            // skip if page hasn't yet rendered
            if (!_rendered)
                return;

            // skip if inputs were not remapped
            if (!IsMapped)
                return;

            // skip if page doesn't have focus
            if (!_focused.TryGetValue(windowName, out bool isFocused) || !isFocused)
                return;

            // stop gamepad navigation when InputsManager is listening
            if (InputsManager.IsListening)
                return;

            // get the current time
            DateTime currentTime = DateTime.Now;

            // check if the button state is equal to the previous button state
            if (controllerState.ButtonState.Equals(prevButtonState))
            {
                if (controllerState.ButtonState.Buttons.Any())
                {
                    // check if the button state has been the same for at least 600ms
                    if ((currentTime - lastChangeTime).TotalMilliseconds >= 600)
                    {
                        // check if the function has been called within the last 25ms
                        if ((currentTime - lastCallTime).TotalMilliseconds >= 25)
                        {
                            // update the last call time
                            lastCallTime = currentTime;
                        }
                        else
                        {
                            return;
                        }
                    }
                    else
                    {
                        return;
                    }
                }
                else
                {
                    return;
                }
            }
            else
            {
                // update the last change time and the last call time
                lastChangeTime = currentTime;
                lastCallTime = currentTime;
                ButtonState.Overwrite(controllerState.ButtonState, prevButtonState);
            }

            // UI thread
            UIHelper.TryInvoke(() =>
            {
                try
                {
                    // get current focused element
                    Control focusedElement = GetFocusedElement();
                    if (focusedElement is null)
                        return;

                    string elementType = focusedElement.GetType().Name;

                    // set direction
                    WPFUtils.Direction direction = WPFUtils.Direction.None;

                    if (controllerState.ButtonState.Buttons.Contains(ButtonFlags.B1))
                    {
                        if (focusedElement is Button button && focusedElement is not DropDownButton)
                        {
                            Focus(button);

                            if (focusedElement.Tag is ProfileViewModel profileViewModel)
                            {
                                // set state
                                _goingForward = true;
                            }
                            else
                            {
                                switch (focusedElement.Tag)
                                {
                                    case "Navigation":
                                        // set state
                                        _goingForward = true;
                                        break;
                                    case "GoBack":
                                        if (gamepadFrame.CanGoBack)
                                        {
                                            // set state
                                            _goingBack = true;
                                            _goingForward = false;
                                            gamepadFrame.GoBack();
                                        }
                                        break;
                                }
                            }

                            if (button.IsEnabled)
                            {
                                // execute command
                                button.Command?.Execute(button.CommandParameter);

                                // raise event
                                button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                            }
                        }
                        else if (focusedElement is RepeatButton repeatButton)
                        {
                            Focus(repeatButton);

                            if (repeatButton.IsEnabled)
                            {
                                // execute command
                                repeatButton.Command?.Execute(repeatButton.CommandParameter);

                                // raise event
                                repeatButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                            }
                        }
                        else if (focusedElement is ToggleButton toggleButton)
                        {
                            Focus(toggleButton);

                            if (toggleButton.IsEnabled)
                            {
                                // execute command
                                toggleButton.Command?.Execute(toggleButton.CommandParameter);

                                // specific scenario
                                if (toggleButton.Name.Equals("ExpanderHeader"))
                                {
                                    Expander Expander = WPFUtils.FindParent<Expander>(toggleButton);
                                    if (Expander is not null)
                                    {
                                        // set state
                                        Expander.IsExpanded = !Expander.IsExpanded;
                                    }
                                }
                                else if (toggleButton is RadioButton radioButton)
                                {
                                    toggleButton.IsChecked = !toggleButton.IsChecked;
                                }
                                else
                                {
                                    switch (toggleButton.Tag)
                                    {
                                        default:
                                            toggleButton.IsChecked = !toggleButton.IsChecked;
                                            break;
                                        case "Hotkey":
                                            // Hotkey IsChecked status is managed by a ViewModel
                                            break;
                                    }
                                }

                                // raise event
                                toggleButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                            }
                        }
                        else if (focusedElement is SettingsCard settingsCard)
                        {
                            if (settingsCard.IsClickEnabled)
                            {
                                Focus(settingsCard);

                                // execute command
                                settingsCard.Command?.Execute(settingsCard.CommandParameter);

                                // raise event
                                settingsCard.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

                                switch (focusedElement.Tag)
                                {
                                    case "Navigation":
                                        // set state
                                        _goingForward = true;
                                        break;
                                    case "GoBack":
                                        if (gamepadFrame.CanGoBack)
                                        {
                                            // set state
                                            _goingBack = true;
                                            _goingForward = false;
                                            gamepadFrame.GoBack();
                                        }
                                        break;
                                }
                            }
                        }
                        else if (focusedElement is ToggleSwitch toggleSwitch)
                        {
                            // set state
                            toggleSwitch.IsOn = !toggleSwitch.IsOn;
                        }
                        else if (focusedElement is RadioButton radioButton)
                        {
                            // set state
                            radioButton.IsChecked = !radioButton.IsChecked;

                            radioButton.Command?.Execute(radioButton.CommandParameter);
                        }
                        else if (focusedElement is HyperlinkButton hyperlinkButton)
                        {
                            if (hyperlinkButton.NavigateUri is not null)
                                Process.Start(new ProcessStartInfo(hyperlinkButton.NavigateUri.AbsoluteUri) { UseShellExecute = true });

                            if (hyperlinkButton.Command is not null)
                                hyperlinkButton.Command?.Execute(hyperlinkButton.CommandParameter);
                        }
                        else if (focusedElement is CheckBox checkBox)
                        {
                            // set state
                            checkBox.IsChecked = !checkBox.IsChecked;

                            checkBox.Command?.Execute(checkBox.CommandParameter);
                        }
                        else if (focusedElement is NavigationViewItem navigationViewItem)
                        {
                            // play sound
                            UISounds.PlayOggFile(UISounds.Expanded);

                            if (prevControl.TryGetValue(gamepadPage.Tag, out Control control) && control is not NavigationViewItem)
                            {
                                Focus(control);
                                return;
                            }
                            else
                            {
                                List<Type> IgnoreList = new();
                                if (IsQuicktools)
                                    IgnoreList.Add(typeof(AppBarButton));

                                // get the nearest non-navigation control
                                focusedElement = WPFUtils.GetTopLeftControl<Control>(gamepadWindow.controlElements, IgnoreList);
                                Focus(focusedElement);
                                return;
                            }
                        }
                        else if (focusedElement is ComboBox comboBox)
                        {
                            comboBox.DropDownClosed += (sender, e) =>
                            {
                                Focus(comboBox, null, true);
                            };

                            // set state
                            comboBox.IsDropDownOpen = !comboBox.IsDropDownOpen;

                            // get currently selected control
                            int idx = comboBox.SelectedIndex;
                            if (idx != -1)
                            {
                                focusedElement = (ComboBoxItem)comboBox.ItemContainerGenerator.ContainerFromIndex(idx);
                            }
                            else if (comboBox.IsDropDownOpen)
                            {
                                // Nothing selected — focus the first available item so Up/Down works immediately
                                for (int i = 0; i < comboBox.Items.Count; i++)
                                {
                                    if (comboBox.ItemContainerGenerator.ContainerFromIndex(i) is ComboBoxItem ci && ci.IsEnabled && ci.IsVisible)
                                    {
                                        focusedElement = ci;
                                        break;
                                    }
                                }
                            }

                            Focus(focusedElement, comboBox, true);
                            return;
                        }
                        else if (focusedElement is ComboBoxItem comboBoxItem)
                        {
                            // get the associated ComboBox
                            comboBox = ItemsControl.ItemsControlFromItemContainer(focusedElement) as ComboBox;
                            if (comboBox is not null && comboBox.IsDropDownOpen && comboBoxItem.IsEnabled)
                            {
                                int idx = comboBox.Items.IndexOf(comboBoxItem);
                                if (idx == -1)
                                    idx = comboBox.Items.IndexOf(comboBoxItem.Content);

                                comboBox.SelectedIndex = idx;
                                comboBox.IsDropDownOpen = false;

                                Focus(comboBox);
                            }
                            return;
                        }
                        else if (focusedElement is ListBoxItem listBoxItem)
                        {
                            // get the associated ComboBox
                            ListBox listBox = ItemsControl.ItemsControlFromItemContainer(focusedElement) as ListBox;
                            if (listBox is not null && listBox.IsEnabled)
                            {
                                // leave ListBox and get below control
                                // todo: we could look for the neareast control with a specific tag, like in HTML with Submit button from a form ?
                                focusedElement = WPFUtils.GetClosestControl<Control>(listBox, gamepadWindow.controlElements, WPFUtils.Direction.Down);
                                Focus(focusedElement);
                            }
                            return;
                        }
                        else if (focusedElement is DropDownButton dropDownButton)
                        {
                            var flyout = dropDownButton.Flyout;
                            if (flyout is not null)
                            {
                                // Self-removing closed handler: clears flyout state and restores focus to the button
                                EventHandler<object> closedHandler = null;
                                closedHandler = (s, e) =>
                                {
                                    flyout.Closed -= closedHandler;
                                    HasFlyoutOpen = false;
                                    gamepadWindow.currentFlyoutButton = null;
                                    flyoutMenuItems.Clear();
                                    focusedFlyoutItem = null;

                                    UIHelper.TryInvoke(() =>
                                    {
                                        // Don't steal focus from a ContentDialog that caused this flyout to close.
                                        if (_focused[windowName] && gamepadWindow.currentDialog is null)
                                            Focus(dropDownButton);
                                    });
                                };
                                flyout.Closed += closedHandler;

                                // Self-removing opened handler: switches focus into the flyout
                                EventHandler<object> openedHandler = null;
                                openedHandler = (s, e) =>
                                {
                                    flyout.Opened -= openedHandler;
                                    HasFlyoutOpen = true;
                                    gamepadWindow.currentFlyoutButton = dropDownButton;

                                    UIHelper.TryInvoke(() =>
                                    {
                                        // MenuItems live in a separate popup visual tree — obtain directly
                                        flyoutMenuItems.Clear();
                                        if (dropDownButton.Flyout is MenuFlyout menuFlyout)
                                        {
                                            foreach (var item in menuFlyout.Items)
                                                if (item is MenuItem mi)
                                                    flyoutMenuItems.Add(mi);
                                        }

                                        // Highlight first enabled item; avoid Focus() which calls
                                        // FocusManager.SetFocusedElement(gamepadWindow) and can close the popup
                                        var firstItem = flyoutMenuItems.FirstOrDefault(m => m.IsEnabled);
                                        if (firstItem is not null)
                                        {
                                            focusedFlyoutItem = firstItem;
                                            Keyboard.Focus(firstItem);
                                            gamepadWindow.SetFocusedElement(firstItem);
                                        }
                                    });
                                };
                                flyout.Opened += openedHandler;

                                flyout.ShowAt(dropDownButton);
                            }
                            return;
                        }
                        else if (focusedElement is MenuItem menuItem && HasFlyoutOpen)
                        {
                            // Execute the menu item command
                            if (menuItem.IsEnabled &&
                                menuItem.Command?.CanExecute(menuItem.CommandParameter) == true)
                                menuItem.Command.Execute(menuItem.CommandParameter);

                            // MenuFlyout auto-closes after a MenuItem is invoked; force it if not
                            if (HasFlyoutOpen)
                                gamepadWindow.currentFlyoutButton?.Flyout?.Hide();
                            return;
                        }
                    }
                    else if (controllerState.ButtonState.Buttons.Contains(ButtonFlags.B2))
                    {
                        // close flyout, if any
                        if (HasFlyoutOpen && gamepadWindow.currentFlyoutButton?.Flyout is { } openFlyout)
                        {
                            openFlyout.Hide();
                            return;
                        }

                        // hide dialog, if any
                        if (gamepadWindow.currentDialog is not null)
                        {
                            gamepadWindow.currentDialog.Hide();
                            return;
                        }

                        // If we're currently within a page-scoped (nested) NavigationView, pressing B should:
                        //  1) first bring focus back to the page NavigationViewItem
                        //  2) only on a subsequent press, leave the page
                        // This prevents accidental "page exit" when the user intended to go back to the page navigation pane.
                        if (focusedElement is Control focusedControl)
                        {
                            NavigationView? focusedNavView = WPFUtils.FindParent<NavigationView>(focusedControl);
                            if (focusedNavView is not null && focusedNavView != windowNavigationView)
                            {
                                // First press: move focus to the (closest/selected) NavigationViewItem
                                if (focusedElement is not NavigationViewItem)
                                {
                                    List<Control> pageNavItems = GetNavigationItems(focusedNavView);
                                    Control? navItem = null;

                                    if (prevPageNavigation is not null && WPFUtils.FindParent<NavigationView>(prevPageNavigation) == focusedNavView)
                                        navItem = prevPageNavigation;

                                    navItem ??= pageNavItems.OfType<NavigationViewItem>().FirstOrDefault(i => i.IsSelected) as Control;
                                    navItem ??= focusedNavView.SelectedItem as NavigationViewItem;

                                    if (navItem is null && pageNavItems.Count > 0)
                                    {
                                        // Find the closest NavigationViewItem towards the pane (Left or Top)
                                        Control? closest = WPFUtils.GetClosestControl<NavigationViewItem>(focusedControl, pageNavItems, GetDirectionTowardsPane(focusedNavView));

                                        // GetClosestControl returns the source if none found; guard against that.
                                        if (closest is NavigationViewItem)
                                            navItem = closest;
                                        else
                                            navItem = pageNavItems.FirstOrDefault();
                                    }

                                    if (navItem is not null)
                                    {
                                        prevPageNavigation = navItem;
                                        Focus(navItem);
                                        return;
                                    }
                                }
                                else
                                {
                                    // Second press (already on a page NavigationViewItem): leave the page if possible.
                                    if (!IsQuicktools && gamepadFrame.CanGoBack)
                                    {
                                        _goingBack = true;
                                        _goingForward = false;
                                        gamepadFrame.GoBack();
                                        return;
                                    }
                                }
                            }
                        }

                        // lazy
                        // todo: implement proper RoutedEvent call
                        switch (elementType)
                        {
                            default:
                                {
                                    if (gamepadWindow.currentDialog is not null && prevControl.TryGetValue(gamepadPage, out Control control))
                                    {
                                        Focus(control);
                                        return;
                                    }
                                }
                                break;

                            case "ComboBox":
                                {
                                    ComboBox comboBox = (ComboBox)focusedElement;
                                    switch (comboBox.IsDropDownOpen)
                                    {
                                        case true:
                                            {
                                                comboBox.IsDropDownOpen = false;
                                                return;
                                            }
                                            break;
                                    }
                                }
                                break;

                            case "ComboBoxItem":
                                {
                                    if (ItemsControl.ItemsControlFromItemContainer(focusedElement) is ComboBox comboBox)
                                    {
                                        comboBox.IsDropDownOpen = false;
                                        return;
                                    }
                                }
                                break;

                            case "NavigationViewItem":
                                {
                                    if (gamepadWindow is OverlayQuickTools overlayQuickTools)
                                    {
                                        overlayQuickTools.ToggleVisibility();
                                        return;
                                    }
                                }
                                break;
                        }

                        // go back to previous page
                        if (_goingForward && !IsQuicktools)
                        {
                            if (gamepadFrame.CanGoBack)
                            {
                                // set state
                                _goingBack = true;
                                _goingForward = false;
                                gamepadFrame.GoBack();
                            }
                        }
                        // Special handling for LayoutItemPage: always navigate back to LayoutPage when B is pressed
                        else if (gamepadPage is not null && gamepadPage.GetType().Name == "LayoutItemPage" && !IsQuicktools)
                        {
                            if (MainWindow.layoutPage is not null)
                            {
                                MainWindow.NavView_Navigate(MainWindow.layoutPage);
                                return;
                            }
                        }
                        else if (prevNavigation is not null)
                            Focus(prevNavigation);
                    }
                    else if (controllerState.ButtonState.Buttons.Contains(ButtonFlags.B3))
                    {
                        switch (elementType)
                        {
                            case "Button":
                                {
                                    if (focusedElement.Tag is ProfileViewModel profileViewModel)
                                    {
                                        profileViewModel.OpenLayout.Execute(null);
                                    }
                                }
                                break;
                        }
                    }
                    else if (controllerState.ButtonState.Buttons.Contains(ButtonFlags.B4))
                    {
                        switch (elementType)
                        {
                            case "Button":
                                {
                                    if (focusedElement.Tag is ProfileViewModel profileViewModel)
                                    {
                                        profileViewModel.ToggleProcessCommand.Execute(null);
                                    }
                                    else
                                    {
                                        // To get the first RadioButton in the list, if any
                                        RadioButton firstRadioButton = WPFUtils.FindChildren(focusedElement).FirstOrDefault(c => c is RadioButton) as RadioButton;
                                        if (firstRadioButton is not null)
                                            firstRadioButton.IsChecked = true;
                                    }
                                }
                                break;
                        }
                    }
                    else if (controllerState.ButtonState.Buttons.Contains(ButtonFlags.L1) || controllerState.ButtonState.Buttons.Contains(ButtonFlags.R1)
                          || controllerState.ButtonState.Buttons.Contains(ButtonFlags.L2Full) || controllerState.ButtonState.Buttons.Contains(ButtonFlags.R2Full))
                    {
                        if (gamepadWindow.currentDialog is not null)
                            return;

                        bool isWindowScope = controllerState.ButtonState.Buttons.Contains(ButtonFlags.L1) || controllerState.ButtonState.Buttons.Contains(ButtonFlags.R1);
                        bool isLeft = controllerState.ButtonState.Buttons.Contains(ButtonFlags.L1) || controllerState.ButtonState.Buttons.Contains(ButtonFlags.L2Soft);

                        NavigationView? targetNavView = null;
                        Control? startItem = null;

                        if (isWindowScope)
                        {
                            targetNavView = windowNavigationView;
                            startItem = prevNavigation;
                        }
                        else
                        {
                            targetNavView ??= pageNavigationView;
                            startItem = prevPageNavigation;
                        }

                        if (targetNavView is null || startItem is null)
                            return;

                        // Gather items once (stable order).
                        List<Control> navItems = GetNavigationItems(targetNavView);
                        if (navItems.Count == 0)
                            return;

                        // Only "target" (focus) NavigationViewItem when the pane is fully Left.
                        // For Top/other modes, we switch selection directly without stealing focus.
                        if (!IsTopPaneNavigationView(targetNavView))
                            return;

                        elementType = startItem.GetType().Name;
                        focusedElement = startItem;

                        // set state(s)
                        _goingForward = true;
                        _navigating = true;

                        direction = isLeft ? WPFUtils.Direction.Left : WPFUtils.Direction.Right;
                    }
                    else if (controllerState.ButtonState.Buttons.Contains(ButtonFlags.DPadUp) || controllerState.ButtonState.Buttons.Contains(ButtonFlags.LeftStickUp) || controllerState.ButtonState.Buttons.Contains(ButtonFlags.LeftPadClickUp))
                    {
                        direction = WPFUtils.Direction.Up;
                    }
                    else if (controllerState.ButtonState.Buttons.Contains(ButtonFlags.DPadDown) || controllerState.ButtonState.Buttons.Contains(ButtonFlags.LeftStickDown) || controllerState.ButtonState.Buttons.Contains(ButtonFlags.LeftPadClickDown))
                    {
                        direction = WPFUtils.Direction.Down;
                    }
                    else if (controllerState.ButtonState.Buttons.Contains(ButtonFlags.DPadLeft) || controllerState.ButtonState.Buttons.Contains(ButtonFlags.LeftStickLeft) || controllerState.ButtonState.Buttons.Contains(ButtonFlags.LeftPadClickLeft))
                    {
                        direction = WPFUtils.Direction.Left;
                    }
                    else if (controllerState.ButtonState.Buttons.Contains(ButtonFlags.DPadRight) || controllerState.ButtonState.Buttons.Contains(ButtonFlags.LeftStickRight) || controllerState.ButtonState.Buttons.Contains(ButtonFlags.LeftPadClickRight))
                    {
                        direction = WPFUtils.Direction.Right;
                    }
                    else if (/*controllerState.ButtonState.Buttons.Contains(ButtonFlags.RightStickUp) ||*/ controllerState.ButtonState.Buttons.Contains(ButtonFlags.RightPadClickUp))
                    {
                        scrollViewer?.ScrollToVerticalOffset(scrollViewer.VerticalOffset - 50);
                    }
                    else if (/*controllerState.ButtonState.Buttons.Contains(ButtonFlags.RightStickDown) ||*/ controllerState.ButtonState.Buttons.Contains(ButtonFlags.RightPadClickDown))
                    {
                        scrollViewer?.ScrollToVerticalOffset(scrollViewer.VerticalOffset + 50);
                    }
                    else if (controllerState.ButtonState.Buttons.Contains(ButtonFlags.Start))
                    {
                        if (gamepadWindow is MainWindow mainWindow)
                        {
                            switch (mainWindow.navView.IsPaneOpen)
                            {
                                case false:
                                    if (prevNavigation is not null)
                                        Focus(prevNavigation);
                                    break;
                                case true:
                                    {
                                        if (prevControl.TryGetValue(gamepadPage.Tag, out Control control) && control is not NavigationViewItem)
                                            Focus(control);
                                        else
                                        {
                                            // get the nearest non-navigation control
                                            focusedElement = WPFUtils.GetTopLeftControl<Control>(gamepadWindow.controlElements);
                                            Focus(focusedElement);
                                        }
                                    }
                                    break;
                            }

                            mainWindow.navView.IsPaneOpen = !mainWindow.navView.IsPaneOpen;
                            return;
                        }
                    }

                    // navigation
                    if (direction != WPFUtils.Direction.None)
                    {
                        if (!_navigating)
                            _goingForward = false;

                        switch (elementType)
                        {
                            case "NavigationViewItem":
                                {
                                    if (focusedElement is not null)
                                    {
                                        NavigationView? scope = WPFUtils.FindParent<NavigationView>(focusedElement) ?? windowNavigationView;
                                        List<Control> scopeItems = scope is not null ? GetNavigationItems(scope) : gamepadWindow.controlElements;

                                        Control target;

                                        // For NavigationView, treat Left/Right as Previous/Next in visual order.
                                        // This is important for Left-pane NavigationViews (vertical lists) where "left" and "right"
                                        // do not map cleanly to spatial navigation.
                                        if (direction == WPFUtils.Direction.Left || direction == WPFUtils.Direction.Right)
                                        {
                                            int idx = scopeItems.IndexOf(focusedElement);
                                            if (idx < 0)
                                                idx = 0;

                                            int nextIdx = direction == WPFUtils.Direction.Left ? idx - 1 : idx + 1;
                                            if (scopeItems.Count > 0)
                                            {
                                                if (nextIdx < 0)
                                                    nextIdx = scopeItems.Count - 1;
                                                else if (nextIdx >= scopeItems.Count)
                                                    nextIdx = 0;
                                                target = scopeItems[nextIdx];
                                            }
                                            else
                                            {
                                                target = focusedElement;
                                            }
                                        }
                                        else
                                        {
                                            // Fallback to spatial navigation for Up/Down.
                                            target = WPFUtils.GetClosestControl<NavigationViewItem>(focusedElement, scopeItems, direction);
                                        }

                                        Focus(target);

                                        // update navigation caches
                                        if (target is NavigationViewItem)
                                        {
                                            if (scope is not null && scope == windowNavigationView)
                                                prevNavigation = target;
                                            else
                                                prevPageNavigation = target;
                                        }
                                    }
                                }
                                return;

                            case "ListView":
                                {
                                    ListView listView = (ListView)focusedElement;
                                    int idx = listView.SelectedIndex;

                                    if (idx != -1)
                                    {
                                        focusedElement = (ListViewItem)listView.ItemContainerGenerator.ContainerFromIndex(idx);
                                        Focus(focusedElement, listView, true);
                                        return;
                                    }
                                }
                                break;

                            case "ListViewItem":
                                {
                                    if (focusedElement is ListViewItem listViewItem)
                                    {
                                        if (ItemsControl.ItemsControlFromItemContainer(focusedElement) is ListView listView)
                                        {
                                            int idx = listView.Items.IndexOf(listViewItem);
                                            if (idx == -1)
                                                idx = listView.Items.IndexOf(listViewItem.Content);

                                            while (true) // Loop to skip disabled items
                                            {
                                                switch (direction)
                                                {
                                                    case WPFUtils.Direction.Up:
                                                        idx--;
                                                        break;

                                                    case WPFUtils.Direction.Down:
                                                        idx++;
                                                        break;
                                                }

                                                // Ensure index is within bounds
                                                if (idx < 0 || idx >= listView.Items.Count)
                                                {
                                                    focusedElement = WPFUtils.GetClosestControl<Control>(listView, gamepadWindow.controlElements, direction, [typeof(Control)]);
                                                    Focus(focusedElement);
                                                    return;
                                                }

                                                // Get the ListViewItem at the new index
                                                focusedElement = (ListViewItem)listView.ItemContainerGenerator.ContainerFromIndex(idx);

                                                // Check if the focused element is enabled
                                                if (focusedElement != null && focusedElement.IsEnabled)
                                                {
                                                    // If the element is enabled, focus it and break out of the loop
                                                    Focus(focusedElement, listView, true);
                                                    break;
                                                }

                                                // If the element is not enabled, continue to the next item in the loop
                                            }
                                        }
                                        return;
                                    }
                                }
                                break;

                            case "ComboBox":
                                {
                                    ComboBox comboBox = (ComboBox)focusedElement;
                                    int idx = comboBox.SelectedIndex;

                                    if (comboBox.IsDropDownOpen)
                                    {
                                        if (idx != -1)
                                        {
                                            focusedElement = (ComboBoxItem)comboBox.ItemContainerGenerator.ContainerFromIndex(idx);
                                            Focus(focusedElement, comboBox, true);
                                        }
                                        else
                                        {
                                            // No item selected yet — jump to the first enabled item
                                            // so that subsequent Up/Down navigation works correctly.
                                            for (int i = 0; i < comboBox.Items.Count; i++)
                                            {
                                                if (comboBox.ItemContainerGenerator.ContainerFromIndex(i) is ComboBoxItem ci && ci.IsEnabled && ci.IsVisible)
                                                {
                                                    Focus(ci, comboBox, true);
                                                    break;
                                                }
                                            }
                                        }
                                        return;
                                    }
                                }
                                break;

                            case "ComboBoxItem":
                                {
                                    if (focusedElement is ComboBoxItem comboBoxItem)
                                    {
                                        if (ItemsControl.ItemsControlFromItemContainer(focusedElement) is ComboBox comboBox)
                                        {
                                            if (comboBox.IsDropDownOpen)
                                            {
                                                int idx = comboBox.Items.IndexOf(comboBoxItem);
                                                if (idx == -1)
                                                    idx = comboBox.Items.IndexOf(comboBoxItem.Content);

                                                while (true) // Loop to skip disabled items
                                                {
                                                    switch (direction)
                                                    {
                                                        case WPFUtils.Direction.Up:
                                                            idx--;
                                                            break;

                                                        case WPFUtils.Direction.Down:
                                                            idx++;
                                                            break;
                                                    }

                                                    // Ensure index is within bounds
                                                    if (idx < 0 || idx >= comboBox.Items.Count)
                                                    {
                                                        // We've reached the top or bottom, so stop the loop
                                                        break;
                                                    }

                                                    // Get the ComboBoxItem at the new index
                                                    focusedElement = (ComboBoxItem)comboBox.ItemContainerGenerator.ContainerFromIndex(idx);

                                                    // Check if the focused element is enabled
                                                    if (focusedElement != null && focusedElement.IsEnabled && focusedElement.IsVisible)
                                                    {
                                                        // If the element is enabled, focus it and break out of the loop
                                                        Focus(focusedElement, comboBox, true);
                                                        break;
                                                    }

                                                    // If the element is not enabled, continue to the next item in the loop
                                                }
                                            }
                                        }
                                        return;
                                    }
                                }
                                break;

                            case "MenuItem":
                                {
                                    if (HasFlyoutOpen && flyoutMenuItems.Count > 0 && focusedElement is MenuItem currentMenuItem)
                                    {
                                        int idx = flyoutMenuItems.IndexOf(currentMenuItem);
                                        if (idx < 0) idx = 0;

                                        while (true)
                                        {
                                            int nextIdx = idx;
                                            switch (direction)
                                            {
                                                case WPFUtils.Direction.Up:   nextIdx--; break;
                                                case WPFUtils.Direction.Down: nextIdx++; break;
                                                default:
                                                    // Left/Right: stay on current item
                                                    return;
                                            }

                                            if (nextIdx < 0 || nextIdx >= flyoutMenuItems.Count)
                                            {
                                                // Reached top or bottom edge — stay on current item
                                                return;
                                            }

                                            idx = nextIdx;
                                            var candidate = flyoutMenuItems[idx];
                                            if (candidate.IsEnabled)
                                            {
                                                focusedFlyoutItem = candidate;
                                                Keyboard.Focus(candidate);
                                                gamepadWindow.SetFocusedElement(candidate);
                                                return;
                                            }
                                            // disabled item — keep looping
                                        }
                                    }
                                    return;
                                }

                            case "Slider":
                                {
                                    switch (direction)
                                    {
                                        case WPFUtils.Direction.Left:
                                            ((Slider)focusedElement).Value -= ((Slider)focusedElement).TickFrequency;
                                            Focus(focusedElement);
                                            return;
                                        case WPFUtils.Direction.Right:
                                            ((Slider)focusedElement).Value += ((Slider)focusedElement).TickFrequency;
                                            Focus(focusedElement);
                                            return;
                                    }
                                }
                                break;
                        }

                        // default
                        if (focusedElement is not null)
                        {
                            focusedElement = WPFUtils.GetClosestControl<Control>(focusedElement, gamepadWindow.controlElements, direction, [typeof(NavigationViewItem)]);

                            if (focusedElement is ListView listView)
                            {
                                int idx = listView.SelectedIndex;
                                if (idx == -1 && listView.Items.Count != 0) idx = 0;

                                if (idx != -1)
                                    focusedElement = (ListViewItem)listView.ItemContainerGenerator.ContainerFromIndex(idx);
                            }

                            Focus(focusedElement);
                        }
                    }
                }
                catch { }
            }, DispatcherPriority.Normal);
        }
    }
}
