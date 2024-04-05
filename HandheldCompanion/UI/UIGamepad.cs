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
        private bool _focused;

        private ButtonState prevButtonState = new();

        // key: Windows, value: NavigationViewItem
        private Control prevNavigation;
        // key: Page
        private ConcurrentDictionary<object, Control> prevControl = new();

        public UIGamepad(GamepadWindow gamepadWindow, Frame contentFrame)
        {
            // set current window
            _currentWindow = gamepadWindow;
            _currentScrollViewer = _currentWindow.GetScrollViewer(_currentWindow);
            _currentWindow.GotFocus += _currentWindow_GotFocus;
            _currentWindow.GotKeyboardFocus += _currentWindow_GotFocus;
            _currentWindow.LostFocus += _currentWindow_LostFocus;

            //_currentWindow.IsVisibleChanged += _currentWindow_IsVisibleChanged;

            _currentWindow.GotGamepadWindowFocus += _currentWindow_GotGamepadWindowFocus;
            _currentWindow.LostGamepadWindowFocus += _currentWindow_LostGamepadWindowFocus;
            _currentWindow.ContentDialogOpened += _currentWindow_ContentDialogOpened;
            _currentWindow.ContentDialogClosed += _currentWindow_ContentDialogClosed;

            _currentWindow.Activated += (sender, e) => _currentWindow_GotFocus(sender, null);
            _currentWindow.Deactivated += (sender, e) => _currentWindow_LostFocus(sender, null);

            _gamepadFrame = contentFrame;
            _gamepadFrame.Navigated += ContentFrame_Navigated;

            // start listening to inputs
            switch (SettingsManager.GetBoolean("DesktopProfileOnStart"))
            {
                case true:
                    ControllerManager.InputsUpdated -= InputsUpdated;
                    break;
                case false:
                    ControllerManager.InputsUpdated += InputsUpdated;
                    break;
            }

            SettingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

            _gamepadTimer = new Timer(250) { AutoReset = false };
            _gamepadTimer.Elapsed += _gamepadFrame_PageRendered;
        }

        private void _currentWindow_ContentDialogClosed()
        {
            if (prevControl.TryGetValue(_gamepadPage.Tag, out Control control))
                Focus(control);
        }

        private void _currentWindow_ContentDialogOpened()
        {
            Control control = _currentWindow.controlElements.OfType<Button>().FirstOrDefault();
            Focus(control);
        }

        private void _currentWindow_GotGamepadWindowFocus()
        {
            _focused = true;
            GotFocus?.Invoke(_currentWindow);
        }

        private void _currentWindow_LostGamepadWindowFocus()
        {
            // halt timer
            _gamepadTimer.Stop();

            _focused = false;

            LostFocus?.Invoke(_currentWindow);
        }
        private void _currentWindow_GotFocus(object sender, RoutedEventArgs e)
        {
            // already has focus
            if (_focused)
                return;

            // set focus
            _focused = true;

            // raise event
            GotFocus?.Invoke(_currentWindow);
        }

        private void _currentWindow_LostFocus(object sender, RoutedEventArgs e)
        {
            // doesn't have focus
            if (!_focused)
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
            _focused = false;

            // halt timer
            _gamepadTimer.Stop();

            // raise event
            LostFocus?.Invoke(_currentWindow);
        }

        private void SettingsManager_SettingValueChanged(string name, object value)
        {
            switch (name)
            {
                case "DesktopLayoutEnabled":
                    {
                        bool enabled = SettingsManager.GetBoolean(name, true);
                        switch (enabled)
                        {
                            case true:
                                ControllerManager.InputsUpdated -= InputsUpdated;
                                break;
                            case false:
                                ControllerManager.InputsUpdated += InputsUpdated;
                                break;
                        }
                    }
                    break;
            }
        }

        private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
        {
            // set rendering state
            _rendered = false;

            // remove state
            _goingForward = false;

            // halt timer
            _gamepadTimer.Stop();

            // store current Frame
            _gamepadFrame = (Frame)sender;
            _gamepadFrame.ContentRendered += _gamepadFrame_ContentRendered;

            // store current Page
            _gamepadPage = (Page)_gamepadFrame.Content;
        }

        private void _gamepadFrame_ContentRendered(object? sender, EventArgs e)
        {
            _gamepadTimer.Stop();
            _gamepadTimer.Start();
        }

        private void _gamepadFrame_PageRendered(object? sender, System.Timers.ElapsedEventArgs e)
        {
            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                // specific-cases
                switch (_gamepadPage.Tag)
                {
                    case "layout":
                    case "SettingsMode0":
                    case "SettingsMode1":

                        // quicktools
                    case "quickhome":
                    case "quicksettings":
                    case "quickdevice":
                    case "quickperformance":
                    case "quickprofiles":
                    case "quicksuspender":
                    case "quickoverlay":
                        _goingForward = true;
                        break;
                }

                if (_goingBack && prevControl.TryGetValue(_gamepadPage.Tag, out Control control))
                {
                    Focus(control);

                    // remove state
                    _goingBack = false;
                }
                else if (_goingForward)
                {
                    if (prevControl.TryGetValue(_gamepadPage.Tag, out control))
                        Focus(control);
                    else
                    {
                        control = WPFUtils.GetTopLeftControl<Control>(_currentWindow.controlElements);
                        Focus(control);
                    }
                }
                /*
                else if (prevNavigation is null && _currentWindow.IsVisible && _currentWindow.WindowState != WindowState.Minimized)
                {
                    NavigationViewItem currentNavigationViewItem = (NavigationViewItem)WPFUtils.GetTopLeftControl<NavigationViewItem>(_currentWindow.elements);
                    prevNavigation = currentNavigationViewItem;
                    Focus(currentNavigationViewItem);
                }
                */

                // clear history
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
                        {
                            switch (controlFocused.Name)
                            {
                                case "b_ServiceStart":
                                case "b_ServiceStop":
                                case "b_ServiceInstall":
                                case "b_ServiceDelete":
                                    break;
                                default:
                                    {
                                        // update navigation
                                        prevNavigation = (NavigationViewItem)controlFocused;
                                    }
                                    break;
                            }
                        }
                        break;

                    default:
                        {
                            // store current control
                            if (_gamepadPage is not null)
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
            if (!_rendered || !_focused)
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
            Application.Current.Dispatcher.BeginInvoke(() =>
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

                                if (prevControl.TryGetValue(_gamepadPage.Tag, out Control control))
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
                                            //TODO: this sometimes happens. we need to handle this.
                                            if (prevNavigation is null)
                                                LogManager.LogInformation("prevNav is null");

                                            // restore previous NavigationViewItem
                                            Focus(prevNavigation);
                                        }
                                        return;

                                    // todo: shouldn't be hardcoded
                                    case "layout":
                                    case "SettingsMode0":
                                    case "SettingsMode1":
                                    case "quickhome":
                                    case "quicksettings":
                                    case "quickdevice":
                                    case "quickperformance":
                                    case "quickprofiles":
                                    case "quicksuspender":
                                    case "quickoverlay":
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
                                            // restore previous NavigationViewItem
                                            if (prevNavigation is not null)
                                                Focus(prevNavigation);
                                            else
                                            {
                                                switch (_gamepadPage.Tag)
                                                {
                                                    // todo: shouldn't be hardcoded
                                                    case "quickhome":
                                                    case "quicksettings":
                                                    case "quickdevice":
                                                    case "quickperformance":
                                                    case "quickprofiles":
                                                    case "quicksuspender":
                                                    case "quickoverlay":
                                                        {
                                                            // set state
                                                            _goingBack = true;

                                                            // go back to previous page
                                                            if (_gamepadFrame.CanGoBack)
                                                                _gamepadFrame.GoBack();
                                                        }
                                                        break;
                                                }
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
                        direction = WPFUtils.Direction.Left;
                    }
                }
                else if (controllerState.ButtonState.Buttons.Contains(ButtonFlags.R1))
                {
                    if (prevNavigation is not null)
                    {
                        elementType = prevNavigation.GetType().Name;
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
