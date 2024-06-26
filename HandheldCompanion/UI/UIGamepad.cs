using GregsStack.InputSimulatorStandard.Native;
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
using System.Windows.Input;
using System.Windows.Navigation;
using System.Xml.Linq;
using Frame = iNKORE.UI.WPF.Modern.Controls.Frame;
using Page = System.Windows.Controls.Page;
using Timer = System.Timers.Timer;

namespace HandheldCompanion.Managers
{
    public class UIGamepad
    {
        #region events
        public static event GotFocusEventHandler GotFocus;
        public delegate void GotFocusEventHandler(Control control);

        public static event LostFocusEventHandler LostFocus;
        public delegate void LostFocusEventHandler(Control control);
        #endregion

        private GamepadWindow _currentWindow;
        private ScrollViewer _currentScrollViewer;
        private Frame _gamepadFrame;
        private Page _gamepadPage;
        private Timer _gamepadTimer;

        private bool _goingBack;
        private bool _goingForward;

        private bool _rendered;
        private object _rendering = new();

        private ButtonState prevButtonState = new();

        // store the latest NavigationViewItem that had focus on this window
        private Control prevNavigation;
        // key: Page, store the latest control that had focus on this page
        private Dictionary<object, Control> prevControl = new();
        // key: Window, store which window has focus
        private static ConcurrentDictionary<Window, bool> _focused = new();

        public UIGamepad(GamepadWindow gamepadWindow, Frame contentFrame)
        {
            // set current window
            _currentWindow = gamepadWindow;
            _currentScrollViewer = _currentWindow.GetScrollViewer(_currentWindow);
            _currentWindow.GotFocus += _currentWindow_GotFocus;
            _currentWindow.GotKeyboardFocus += _currentWindow_GotFocus;
            _currentWindow.LostFocus += _currentWindow_LostFocus;

            //_currentWindow.IsVisibleChanged += _currentWindow_IsVisibleChanged;

            _currentWindow.GotGamepadWindowFocus += (sender) => _currentWindow_GotFocus(sender, new RoutedEventArgs());
            _currentWindow.LostGamepadWindowFocus += (sender) => _currentWindow_LostFocus(sender, new RoutedEventArgs());
            _currentWindow.ContentDialogOpened += _currentWindow_ContentDialogOpened;
            _currentWindow.ContentDialogClosed += _currentWindow_ContentDialogClosed;
            _currentWindow.Activated += (sender, e) => _currentWindow_GotFocus(sender, new RoutedEventArgs());
            _currentWindow.Deactivated += (sender, e) => _currentWindow_LostFocus(sender, new RoutedEventArgs());

            _gamepadFrame = contentFrame;
            _gamepadFrame.Navigated += ContentFrame_Navigated;

            _gamepadTimer = new Timer(25) { AutoReset = false };
            _gamepadTimer.Elapsed += _gamepadFrame_PageRendered;

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
            if (_focused.TryGetValue(_currentWindow, out bool isFocused) && isFocused)
                return;

            // set focus
            _focused[_currentWindow] = true;

            // raise event
            GotFocus?.Invoke(_currentWindow);

            foreach (GamepadWindow window in _focused.Keys)
            {
                if (window.Equals(_currentWindow))
                    continue;

                if (_focused.TryGetValue(window, out isFocused) && !isFocused)
                    continue;

                // remove focus
                _focused[window] = false;

                // raise event
                LostFocus?.Invoke(window);
            }
        }

        private void _currentWindow_LostFocus(object sender, RoutedEventArgs e)
        {
            // doesn't have focus
            if (_focused.TryGetValue(_currentWindow, out bool isFocused) && !isFocused)
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
            _focused[_currentWindow] = false;

            // halt timer
            _gamepadTimer.Stop();

            // raise event
            LostFocus?.Invoke(_currentWindow);

            foreach (GamepadWindow window in _focused.Keys)
            {
                if (window.Equals(_currentWindow))
                    continue;

                if (window.Visibility != Visibility.Visible)
                    continue;

                if (window.WindowState == WindowState.Minimized)
                    continue;

                if (_focused.TryGetValue(window, out isFocused) && isFocused)
                    continue;

                // set focus
                _focused[window] = true;

                // raise event
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

                // clear history on page swap
                if (_gamepadPage is not null)
                    prevControl.Remove(_gamepadPage.Tag, out _);

                // set rendering state
                _rendered = true;
            });
        }

        public void Focus(Control control)
        {
            if (control is null)
                return;

            // set tooltip on focus
            ToolTipService.SetShowsToolTipOnKeyboardFocus(control, true);

            // set tooltip initial delay
            string controlType = control.GetType().Name;
            switch (controlType)
            {
                case "Slider":
                    ToolTipService.SetInitialShowDelay(control, 0);
                    break;
                case "NavigationViewItem":
                    // update navigation
                    prevNavigation = (NavigationViewItem)control;
                    break;
                default:
                    ToolTipService.SetInitialShowDelay(control, 250);
                    break;
            }

            // set focus to control
            Keyboard.Focus(control);
            control.Focus();
            control.BringIntoView();
        }

        public Control FocusedElement(GamepadWindow window)
        {
            IInputElement keyboardFocused = null;

            if (Keyboard.FocusedElement is not null)
                if (Keyboard.FocusedElement.GetType().IsSubclassOf(typeof(Control)))
                    keyboardFocused = Keyboard.FocusedElement;

            if (keyboardFocused is null)
            {
                if (window is not null)
                    keyboardFocused = window;
                else
                    keyboardFocused = _currentWindow;
            }

            if (keyboardFocused.Focusable)
            {
                Control controlFocused = (Control)keyboardFocused;

                string keyboardType = controlFocused.GetType().Name;

                switch (keyboardType)
                {
                    case "MainWindow":
                    case "OverlayQuickTools":
                    case "TouchScrollViewer":
                        {
                            if (prevNavigation is not null)
                            {
                                // a new page opened
                                controlFocused = WPFUtils.GetTopLeftControl<Control>(window.controlElements);
                            }
                            else
                            {
                                // first start
                                prevNavigation = controlFocused = WPFUtils.GetTopLeftControl<NavigationViewItem>(window.controlElements);
                            }
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
                else if (window is MainWindow)
                {
                    // pick the top left NavigationViewItem
                    return WPFUtils.GetTopLeftControl<NavigationViewItem>(window.controlElements);
                }
                else if (window is OverlayQuickTools)
                {
                    // pick the top left Control
                    return WPFUtils.GetTopLeftControl<Control>(window.controlElements);
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
            if (!_focused.TryGetValue(_currentWindow, out bool isFocused) || !isFocused)
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

            // UI thread (async)
            Application.Current.Dispatcher.Invoke(() =>
            {
                // get current focused element
                Control focusedElement = FocusedElement(_currentWindow);
                if (focusedElement is null)
                    return;

                string elementType = focusedElement.GetType().Name;

                // set direction
                WPFUtils.Direction direction = WPFUtils.Direction.None;

                // force display keyboard focus rectangle
                WPFUtils.MakeFocusVisible(_currentWindow);

                if (controllerState.ButtonState.Buttons.Contains(ButtonFlags.B1))
                {
                    // lazy
                    // todo: implement proper RoutedEvent call
                    switch (elementType)
                    {
                        case "Button":
                        case "RepeatButton":
                            WPFUtils.SendKeyToControl(focusedElement, (int)VirtualKeyCode.RETURN);
                            break;
                        case "ToggleSwitch":
                            ((ToggleSwitch)focusedElement).IsOn = !((ToggleSwitch)focusedElement).IsOn;
                            break;
                        case "ToggleButton":
                            WPFUtils.SendKeyToControl(focusedElement, (int)VirtualKeyCode.RETURN);
                            break;
                        case "CheckBox":
                            ((CheckBox)focusedElement).IsChecked = !((CheckBox)focusedElement).IsChecked;
                            break;

                        case "NavigationViewItem":
                            {
                                // play sound
                                UISounds.PlayOggFile(UISounds.Expanded);

                                // set state
                                _goingForward = true;

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

                        case "ComboBox":
                            {
                                ComboBox comboBox = (ComboBox)focusedElement;
                                comboBox.IsDropDownOpen = true;
                            }
                            return;

                        case "ComboBoxItem":
                            WPFUtils.SendKeyToControl(focusedElement, (int)VirtualKeyCode.RETURN);
                            return;
                    }
                }
                else if (controllerState.ButtonState.Buttons.Contains(ButtonFlags.B2))
                {
                    // hide dialog, if any
                    if (_currentWindow.currentDialog is not null)
                        _currentWindow.currentDialog.Hide();

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
                                        break;
                                }
                            }
                            return;

                        case "ComboBoxItem":
                            {
                                ComboBox comboBox = ItemsControl.ItemsControlFromItemContainer(focusedElement) as ComboBox;
                                comboBox.IsDropDownOpen = false;
                            }
                            return;

                        case "NavigationViewItem":
                            {
                                if (_currentWindow is OverlayQuickTools)
                                    WPFUtils.SendKeyToControl(focusedElement, (int)VirtualKeyCode.ESCAPE);
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
                    if (prevNavigation is not null)
                    {
                        elementType = prevNavigation.GetType().Name;
                        focusedElement = prevNavigation;

                        direction = WPFUtils.Direction.Left;
                    }
                }
                else if (controllerState.ButtonState.Buttons.Contains(ButtonFlags.R1))
                {
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
                                if (comboBox.IsDropDownOpen)
                                {
                                    switch (direction)
                                    {
                                        case WPFUtils.Direction.Up:
                                            WPFUtils.SendKeyToControl(focusedElement, (int)VirtualKeyCode.UP);
                                            return;
                                        case WPFUtils.Direction.Down:
                                            WPFUtils.SendKeyToControl(focusedElement, (int)VirtualKeyCode.DOWN);
                                            return;
                                    }
                                }
                            }
                            break;

                        case "ComboBoxItem":
                            {
                                switch (direction)
                                {
                                    case WPFUtils.Direction.Up:
                                        WPFUtils.SendKeyToControl(focusedElement, (int)VirtualKeyCode.UP);
                                        return;
                                    case WPFUtils.Direction.Down:
                                        WPFUtils.SendKeyToControl(focusedElement, (int)VirtualKeyCode.DOWN);
                                        return;
                                }
                            }
                            break;

                        case "Slider":
                            {
                                switch (direction)
                                {
                                    case WPFUtils.Direction.Up:
                                    case WPFUtils.Direction.Down:
                                        focusedElement = WPFUtils.GetClosestControl<Control>(focusedElement, _currentWindow.controlElements, direction, new List<Type>() { typeof(NavigationViewItem) });
                                        Focus(focusedElement);
                                        return;

                                    case WPFUtils.Direction.Left:
                                        WPFUtils.SendKeyToControl(focusedElement, (int)VirtualKeyCode.LEFT);
                                        return;
                                    case WPFUtils.Direction.Right:
                                        WPFUtils.SendKeyToControl(focusedElement, (int)VirtualKeyCode.RIGHT);
                                        return;
                                }
                            }
                            break;
                    }

                    // default
                    focusedElement = WPFUtils.GetClosestControl<Control>(focusedElement, _currentWindow.controlElements, direction, new List<Type>() { typeof(NavigationViewItem) });
                    Focus(focusedElement);
                }
            });
        }
    }
}
