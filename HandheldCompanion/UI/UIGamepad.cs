using HandheldCompanion.Controllers;
using HandheldCompanion.Inputs;
using HandheldCompanion.UI;
using HandheldCompanion.Utils;
using HandheldCompanion.Views;
using HandheldCompanion.Views.Classes;
using HandheldCompanion.Views.Windows;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Navigation;
using Frame = iNKORE.UI.WPF.Modern.Controls.Frame;
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

        private GamepadWindow _currentWindow;
        private string _currentName = string.Empty;

        private ScrollViewer _currentScrollViewer;
        private Frame _gamepadFrame;
        private Page _gamepadPage;

        private Timer _gamepadTimer;

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

        private bool _rendered;
        private object _rendering = new();

        private ButtonState prevButtonState = new();

        // store the latest NavigationViewItem that had focus on this window
        private Control prevNavigation;
        // key: Page, store the latest control that had focus on this page
        private Dictionary<object, Control> prevControl = [];
        // key: Window, store which window has focus
        private static ConcurrentDictionary<string, bool> _focused = new();

        public UIGamepad(GamepadWindow gamepadWindow, Frame contentFrame)
        {
            // set current window
            _currentWindow = gamepadWindow;
            _currentName = gamepadWindow.Tag.ToString();

            _currentScrollViewer = _currentWindow.GetScrollViewer(_currentWindow);

            _currentWindow.GotFocus += _currentWindow_GotFocus;
            _currentWindow.GotKeyboardFocus += _currentWindow_GotFocus;
            _currentWindow.LostFocus += _currentWindow_LostFocus;

            if (_currentWindow is OverlayQuickTools quickTools)
            {
                quickTools.GotGamepadWindowFocus += (sender) => _currentWindow_GotFocus(sender, new RoutedEventArgs());
                quickTools.LostGamepadWindowFocus += (sender) => _currentWindow_LostFocus(sender, new RoutedEventArgs());
            }

            _currentWindow.ContentDialogOpened += _currentWindow_ContentDialogOpened;
            _currentWindow.ContentDialogClosed += _currentWindow_ContentDialogClosed;
            _currentWindow.Activated += (sender, e) => _currentWindow_GotFocus(sender, new RoutedEventArgs());
            _currentWindow.Deactivated += (sender, e) => _currentWindow_LostFocus(sender, new RoutedEventArgs());

            _gamepadFrame = contentFrame;
            _gamepadFrame.Navigated += ContentFrame_Navigated;

            _gamepadTimer = new Timer(25) { AutoReset = false };
            _gamepadTimer.Elapsed += _gamepadFrame_PageRendered;

            tooltipTimer = new Timer(2000) { AutoReset = false };
            tooltipTimer.Elapsed += TooltipTimer_Elapsed;

            ControllerManager.InputsUpdated += InputsUpdated;
        }

        private void _currentWindow_ContentDialogClosed(ContentDialog contentDialog)
        {
            // set flag
            HasDialogOpen = false;

            if (prevControl.TryGetValue(_gamepadPage.Tag, out Control control))
                Focus(control);
        }

        private bool HasDialogOpen = false;

        private void _currentWindow_ContentDialogOpened(ContentDialog contentDialog)
        {
            // set flag
            HasDialogOpen = true;

            Control control = _currentWindow.controlElements.OfType<Button>().FirstOrDefault();
            Focus(control);
        }

        private void _currentWindow_GotFocus(object sender, RoutedEventArgs e)
        {
            // already has focus
            if (_focused.TryGetValue(_currentName, out bool isFocused) && isFocused)
                return;

            // set focus
            _focused[_currentName] = _currentWindow.IsPrimary();

            // raise event
            if (_focused[_currentName])
            {
                GotFocus?.Invoke(_currentName);

                foreach (string window in _focused.Keys)
                {
                    if (window.Equals(_currentName))
                        continue;

                    if (_focused.TryGetValue(window, out isFocused) && !isFocused)
                        continue;

                    // remove focus
                    _focused[window] = false;

                    // raise event
                    LostFocus?.Invoke(window);
                }
            }
        }

        private void _currentWindow_LostFocus(object sender, RoutedEventArgs e)
        {
            // doesn't have focus
            if (_focused.TryGetValue(_currentName, out bool isFocused) && !isFocused)
                return;

            // check if sender is part of current window
            if (e is not null && e.OriginalSource is not null)
            {
                Window yourParentWindow = Window.GetWindow((DependencyObject)e.OriginalSource);

                // sender is part of parent window, return
                if (yourParentWindow == _currentWindow)
                    return;
            }

            // unset focus
            _focused[_currentName] = false;

            // halt timer
            _gamepadTimer.Stop();

            // raise event
            LostFocus?.Invoke(_currentName);

            foreach (string window in _focused.Keys)
            {
                if (window.Equals(_currentName))
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

                if (_focused.TryGetValue(window, out isFocused) && isFocused)
                    continue;

                // set focus
                _focused[window] = gamepadWindow.IsPrimary();

                // raise event
                if (_focused[window])
                    GotFocus?.Invoke(window);
            }
        }

        private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
        {
            lock (_rendering)
            {
                // halt timer
                _gamepadTimer.Stop();

                // set rendering state
                _rendered = false;

                // remove state
                _goingForward = false;

                // store current Frame and listen to render events
                if (_gamepadPage != (Page)_gamepadFrame.Content)
                {
                    _gamepadFrame = (Frame)sender;
                    _gamepadFrame.ContentRendered += _gamepadFrame_ContentRendered;

                    // store current Page
                    _gamepadPage = (Page)_gamepadFrame.Content;
                }
            }
        }

        private void _gamepadFrame_ContentRendered(object? sender, EventArgs e)
        {
            _gamepadTimer.Stop();
            _gamepadTimer.Start();
        }

        private void _gamepadFrame_PageRendered(object? sender, System.Timers.ElapsedEventArgs e)
        {
            // stop listening for render events
            _gamepadFrame.ContentRendered -= _gamepadFrame_ContentRendered;

            // UI thread (async)
            Application.Current.Dispatcher.Invoke(() =>
            {
                // store top left navigation view item
                if (prevNavigation is null)
                    prevNavigation = WPFUtils.GetTopLeftControl<NavigationViewItem>(_currentWindow.controlElements);

                // specific-cases
                switch (_gamepadPage.Tag)
                {
                    case "layout":
                    case "SettingsMode0":
                    case "SettingsMode1":
                    case "quickperformance":
                        _goingForward = true;
                        break;
                }

                if (prevControl.TryGetValue(_gamepadPage.Tag, out Control control))
                {
                    if (_goingBack)
                    {
                        Focus(control);

                        // remove state
                        _goingBack = false;
                    }
                    else if (_goingForward && control is not null)
                    {
                        Focus(control);
                    }
                    else if (_goingForward && control is null)
                    {
                        control = WPFUtils.GetTopLeftControl<Control>(_currentWindow.controlElements);
                        Focus(control);
                    }
                }
                else if (_goingForward)
                {
                    control = WPFUtils.GetTopLeftControl<Control>(_currentWindow.controlElements);
                    Focus(control);
                }

                // clear history on page swap
                if (_gamepadPage is not null && _currentWindow is OverlayQuickTools)
                    prevControl.Remove(_gamepadPage.Tag, out _);

                // set rendering state
                _rendered = true;
            });
        }

        private Control forcedFocus;
        private Control parentFocus;

        private void TooltipTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            // UI thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                tooltip.IsOpen = false;
            });
        }

        public void Focus(Control control, Control parent = null, bool force = false)
        {
            if (control is null)
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
                case "NavigationViewItem":
                    // update navigation
                    prevNavigation = (NavigationViewItem)control;
                    break;
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

            FocusManager.SetFocusedElement(_currentWindow, control);
            _currentWindow.SetFocusedElement(control);
        }

        public Control GetFocusedElement()
        {
            IInputElement FocusedElement = forcedFocus is not null ? forcedFocus : FocusManager.GetFocusedElement(_currentWindow);

            DependencyObject commonAncestor = VisualTreeHelperExtensions.FindCommonAncestor((DependencyObject)FocusedElement, _currentWindow);
            if (commonAncestor is null && forcedFocus is null)
            {
                FocusManager.SetFocusedElement(_currentWindow, WPFUtils.GetTopLeftControl<Control>(_currentWindow.controlElements));
                FocusedElement = FocusManager.GetFocusedElement(_currentWindow);
            }

            if (FocusedElement is null)
                FocusedElement = _currentWindow;

            if (FocusedElement.Focusable && FocusedElement is Control)
            {
                Control controlFocused = (Control)FocusedElement;

                string keyboardType = controlFocused.GetType().Name;

                switch (keyboardType)
                {
                    case "MainWindow":
                    case "OverlayQuickTools":
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
                            // store current control if not part of a dialog
                            if (_gamepadPage is not null && !HasDialogOpen)
                                prevControl[_gamepadPage.Tag] = controlFocused;
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
                    return WPFUtils.GetTopLeftControl<NavigationViewItem>(_currentWindow.controlElements);
                }
            }

            return null;
        }

        // declare a DateTime variable to store the last time the function was called
        private DateTime lastCallTime;

        // declare a DateTime variable to store the last time the button state changed
        private DateTime lastChangeTime;

        private void InputsUpdated(ControllerState controllerState)
        {
            // skip if page hasn't yet rendered
            if (!_rendered)
                return;

            // skip if page doesn't have focus
            if (!_focused.TryGetValue(_currentName, out bool isFocused) || !isFocused)
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
                        // check if the function has been called within the last 100ms
                        if ((currentTime - lastCallTime).TotalMilliseconds >= 100)
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
                prevButtonState = controllerState.ButtonState.Clone() as ButtonState;
            }

            // UI thread
            Application.Current.Dispatcher.Invoke(() =>
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
                    if (focusedElement is Button button)
                    {
                        // raise event
                        button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

                        // execute command
                        button.Command?.Execute(button.CommandParameter);
                        Focus(button);
                    }
                    else if (focusedElement is RepeatButton repeatButton)
                    {
                        // raise event
                        repeatButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

                        // execute command
                        repeatButton.Command?.Execute(repeatButton.CommandParameter);
                        Focus(repeatButton);
                    }
                    else if (focusedElement is ToggleButton toggleButton)
                    {
                        // raise event
                        toggleButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

                        // execute command
                        toggleButton.Command?.Execute(toggleButton.CommandParameter);
                        Focus(toggleButton);

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
                        else if (toggleButton.Command is not null)
                        {
                            // toggle state is managed by the command
                        }
                        else
                        {
                            toggleButton.IsChecked = !toggleButton.IsChecked;
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

                        if (radioButton.Command is not null)
                            radioButton.Command.Execute(radioButton.CommandParameter);
                    }
                    else if (focusedElement is CheckBox checkBox)
                    {
                        // set state
                        checkBox.IsChecked = !checkBox.IsChecked;

                        if (checkBox.Command is not null)
                            checkBox.Command.Execute(checkBox.CommandParameter);
                    }
                    else if (focusedElement is NavigationViewItem navigationViewItem)
                    {
                        // play sound
                        UISounds.PlayOggFile(UISounds.Expanded);

                        // set state
                        _goingForward = true;

                        if (prevControl.TryGetValue(_gamepadPage.Tag, out Control control) && control is not NavigationViewItem)
                        {
                            Focus(control);
                            return;
                        }
                        else
                        {
                            // get the nearest non-navigation control
                            focusedElement = WPFUtils.GetTopLeftControl<Control>(_currentWindow.controlElements);
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
                            focusedElement = (ComboBoxItem)comboBox.ItemContainerGenerator.ContainerFromIndex(idx);

                        Focus(focusedElement, comboBox, true);
                        return;
                    }
                    else if (focusedElement is ComboBoxItem comboBoxItem)
                    {
                        // get the associated ComboBox
                        comboBox = ItemsControl.ItemsControlFromItemContainer(focusedElement) as ComboBox;

                        if (comboBox.IsDropDownOpen)
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
                }
                else if (controllerState.ButtonState.Buttons.Contains(ButtonFlags.B2))
                {
                    // hide dialog, if any
                    if (_currentWindow.currentDialog is not null)
                    {
                        _currentWindow.currentDialog.Hide();
                        return;
                    }

                    // lazy
                    // todo: implement proper RoutedEvent call
                    switch (elementType)
                    {
                        default:
                            {
                                switch (_gamepadPage.Tag)
                                {
                                    default:
                                        {
                                            if (HasDialogOpen && prevControl.TryGetValue(_gamepadPage, out Control control))
                                                Focus(control);
                                            else
                                                Focus(prevNavigation);
                                        }
                                        return;

                                    // todo: shouldn't be hardcoded
                                    case "layout":
                                    case "SettingsMode0":
                                    case "SettingsMode1":
                                    case "quickperformance":
                                        {
                                            // set state
                                            _goingBack = true;

                                            // play sound
                                            UISounds.PlayOggFile(UISounds.Collapse);

                                            // go back to previous page
                                            if (_gamepadFrame.CanGoBack)
                                                _gamepadFrame.GoBack();
                                        }
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
                                        comboBox.IsDropDownOpen = false;
                                        break;
                                    case false:
                                        {
                                            switch (_gamepadPage.Tag)
                                            {
                                                // todo: shouldn't be hardcoded
                                                case "quickperformance":
                                                    {
                                                        // set state
                                                        _goingBack = true;

                                                        // go back to previous page
                                                        if (_gamepadFrame.CanGoBack)
                                                            _gamepadFrame.GoBack();
                                                    }
                                                    break;
                                                default:
                                                    // restore previous NavigationViewItem
                                                    if (prevNavigation is not null)
                                                        Focus(prevNavigation);
                                                    break;
                                            }
                                        }
                                        return;
                                }
                            }
                            break;

                        case "ComboBoxItem":
                            {
                                ComboBox comboBox = ItemsControl.ItemsControlFromItemContainer(focusedElement) as ComboBox;
                                comboBox.IsDropDownOpen = false;
                            }
                            return;

                        case "NavigationViewItem":
                            {
                                switch (_gamepadPage.Tag)
                                {
                                    // todo: shouldn't be hardcoded
                                    case "layout":
                                    case "SettingsMode0":
                                    case "SettingsMode1":
                                    case "quickperformance":
                                        {
                                            // set state
                                            _goingBack = true;

                                            // play sound
                                            UISounds.PlayOggFile(UISounds.Collapse);

                                            // go back to previous page
                                            if (_gamepadFrame.CanGoBack)
                                                _gamepadFrame.GoBack();
                                        }
                                        return;
                                }

                                if (_currentWindow is OverlayQuickTools overlayQuickTools)
                                    overlayQuickTools.ToggleVisibility();
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
                                // To get the first RadioButton in the list, if any
                                RadioButton firstRadioButton = WPFUtils.FindChildren(focusedElement).FirstOrDefault(c => c is RadioButton) as RadioButton;
                                if (firstRadioButton is not null)
                                    firstRadioButton.IsChecked = true;
                            }
                            break;
                    }
                }
                else if (controllerState.ButtonState.Buttons.Contains(ButtonFlags.L1))
                {
                    if (_currentWindow.currentDialog is not null)
                        return;

                    if (prevNavigation is not null)
                    {
                        elementType = prevNavigation.GetType().Name;
                        focusedElement = prevNavigation;

                        direction = WPFUtils.Direction.Left;
                    }
                }
                else if (controllerState.ButtonState.Buttons.Contains(ButtonFlags.R1))
                {
                    if (_currentWindow.currentDialog is not null)
                        return;

                    if (prevNavigation is not null)
                    {
                        elementType = prevNavigation.GetType().Name;
                        focusedElement = prevNavigation;

                        direction = WPFUtils.Direction.Right;
                    }
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
                else if (controllerState.ButtonState.Buttons.Contains(ButtonFlags.RightStickUp) || controllerState.ButtonState.Buttons.Contains(ButtonFlags.RightPadClickUp))
                {
                    _currentScrollViewer?.ScrollToVerticalOffset(_currentScrollViewer.VerticalOffset - 50);
                }
                else if (controllerState.ButtonState.Buttons.Contains(ButtonFlags.RightStickDown) || controllerState.ButtonState.Buttons.Contains(ButtonFlags.RightPadClickDown))
                {
                    _currentScrollViewer?.ScrollToVerticalOffset(_currentScrollViewer.VerticalOffset + 50);
                }
                else if (controllerState.ButtonState.Buttons.Contains(ButtonFlags.Start))
                {
                    if (_currentWindow is MainWindow mainWindow)
                    {
                        switch (mainWindow.navView.IsPaneOpen)
                        {
                            case false:
                                if (prevNavigation is not null)
                                    Focus(prevNavigation);
                                break;
                            case true:
                                {
                                    if (prevControl.TryGetValue(_gamepadPage.Tag, out Control control) && control is not NavigationViewItem)
                                        Focus(control);
                                    else
                                    {
                                        // get the nearest non-navigation control
                                        focusedElement = WPFUtils.GetTopLeftControl<Control>(_currentWindow.controlElements);
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
                    switch (elementType)
                    {
                        case "NavigationViewItem":
                            {
                                focusedElement = WPFUtils.GetClosestControl<NavigationViewItem>(focusedElement, _currentWindow.controlElements, direction);
                                Focus(focusedElement);
                            }
                            return;

                        case "ComboBox":
                            {
                                ComboBox comboBox = (ComboBox)focusedElement;
                                int idx = comboBox.SelectedIndex;

                                if (comboBox.IsDropDownOpen && idx != -1)
                                {
                                    focusedElement = (ComboBoxItem)comboBox.ItemContainerGenerator.ContainerFromIndex(idx);
                                    Focus(focusedElement, comboBox, true);
                                    return;
                                }
                            }
                            break;

                        case "ComboBoxItem":
                            {
                                if (focusedElement is ComboBoxItem comboBoxItem)
                                {
                                    ComboBox comboBox = ItemsControl.ItemsControlFromItemContainer(focusedElement) as ComboBox;
                                    if (comboBox.IsDropDownOpen)
                                    {
                                        int idx = comboBox.Items.IndexOf(comboBoxItem);
                                        if (idx == -1)
                                            idx = comboBox.Items.IndexOf(comboBoxItem.Content);

                                        switch (direction)
                                        {
                                            case WPFUtils.Direction.Up:
                                                idx--;
                                                break;

                                            case WPFUtils.Direction.Down:
                                                idx++;
                                                break;
                                        }

                                        // Get the ComboBoxItem
                                        idx = Math.Max(0, Math.Min(comboBox.Items.Count - 1, idx));

                                        focusedElement = (ComboBoxItem)comboBox.ItemContainerGenerator.ContainerFromIndex(idx);
                                        Focus(focusedElement, comboBox, true);
                                    }
                                    return;
                                }
                            }
                            break;

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
                    focusedElement = WPFUtils.GetClosestControl<Control>(focusedElement, _currentWindow.controlElements, direction, [typeof(NavigationViewItem)]);
                    Focus(focusedElement);
                }
            });
        }
    }
}
