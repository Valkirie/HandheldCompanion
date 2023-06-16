using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using ControllerCommon.Controllers;
using ControllerCommon.Inputs;
using ControllerCommon.Utils;
using GregsStack.InputSimulatorStandard.Native;
using HandheldCompanion.Simulators;
using HandheldCompanion.Views;
using HandheldCompanion.Views.Classes;
using HandheldCompanion.Views.Windows;
using ModernWpf.Controls;

namespace HandheldCompanion.Managers
{
    public static class GamepadFocusManager
    {
        private static GamepadWindow _gamepadWindow;

        static GamepadFocusManager()
        {
            var mainWindow = MainWindow.GetCurrent();
            mainWindow.Activated += GamepadFocusManager_GotFocus;
            mainWindow.Deactivated += GamepadFocusManager_LostFocus;

            MainWindow.overlayquickTools.Activated += GamepadFocusManager_GotFocus;
            MainWindow.overlayquickTools.Deactivated += GamepadFocusManager_LostFocus;
        }

        private static void GamepadFocusManager_LostFocus(object? sender, System.EventArgs e)
        {
            if (_gamepadWindow == (GamepadWindow)sender)
                _gamepadWindow = null;
        }

        private static void GamepadFocusManager_GotFocus(object? sender, System.EventArgs e)
        {
            _gamepadWindow = (GamepadWindow)sender;
        }

        public static void Focus(Control control)
        {
            if (control is null)
                return;

            // force display keyboard focus rectangle
            WPFUtils.MakeFocusVisible(control);

            // set focus to control
            Keyboard.Focus(control);
            control.Focus();

            // bring to view
            control.BringIntoView();
        }

        public static Control FocusedElement(GamepadWindow window)
        {
            Control keyboardFocused = (Control)Keyboard.FocusedElement;

            if (keyboardFocused is not null)
            {
                // pick the last known control
                return keyboardFocused;
            }
            else if (window.GetType() == typeof(MainWindow))
            {
                // pick the top left navigantionviewitem
                return WPFUtils.GetTopLeftControl<NavigationViewItem>(window.elements);
            }
            else if (window.GetType() == typeof(OverlayQuickTools))
            {
                // pick the top left control
                return WPFUtils.GetTopLeftControl<Control>(window.elements);
            }

            return null;
        }

        public static void Start()
        {
        }

        private static AxisState prevAxisState = new();
        private static ButtonState prevButtonState = new();

        public static void UpdateReport(ControllerState controllerState)
        {
            if (_gamepadWindow is null)
                return;

            if (controllerState.ButtonState.Equals(prevButtonState))
                return;

            prevButtonState = controllerState.ButtonState.Clone() as ButtonState;

            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                // get current focused element
                Control focusedElement = FocusedElement(_gamepadWindow);
                if (focusedElement is null)
                    return;

                string elementType = focusedElement.GetType().Name;

                // set direction
                WPFUtils.Direction direction = WPFUtils.Direction.None;

                if (controllerState.ButtonState.Buttons.Contains(ButtonFlags.B1))
                {
                    // lazy
                    // todo: implement proper RoutedEvent call
                    switch (elementType)
                    {
                        case "Button":
                        case "ToggleSwitch":
                        case "ToggleButton":
                        case "CheckBox":
                            {
                                KeyboardSimulator.KeyPress(VirtualKeyCode.SPACE);
                            }
                            break;

                        case "NavigationViewItem":
                            {
                                // get the nearest non-navigation control
                                focusedElement = WPFUtils.GetClosestControl<Control>(focusedElement, _gamepadWindow.elements, WPFUtils.Direction.Right, new List<Type>() { typeof(NavigationViewItem) });
                                Focus(focusedElement);
                            }
                            break;

                        case "ComboBox":
                            {
                                KeyboardSimulator.KeyPress(new VirtualKeyCode[] { VirtualKeyCode.MENU, VirtualKeyCode.DOWN });
                            }
                            break;

                        case "ComboBoxItem":
                            {
                                KeyboardSimulator.KeyPress(VirtualKeyCode.RETURN);
                            }
                            break;

                        case "RadioButtons":
                            {
                            }
                            break;
                    }
                }
                else if (controllerState.ButtonState.Buttons.Contains(ButtonFlags.B2))
                {
                    // lazy
                    // todo: implement proper RoutedEvent call
                    switch (elementType)
                    {
                        default:
                            {
                                // get the nearest navigation control
                                focusedElement = WPFUtils.GetClosestControl<NavigationViewItem>(focusedElement, _gamepadWindow.elements, WPFUtils.Direction.Left);
                                Focus(focusedElement);
                            }
                            break;

                        case "ComboBoxItem":
                            {
                                KeyboardSimulator.KeyPress(VirtualKeyCode.ESCAPE);
                            }
                            break;

                        case "RadioButtons":
                            {
                            }
                            break;

                        case "NavigationViewItem":
                            {
                            }
                            break;
                    }
                }
                else if (controllerState.ButtonState.Buttons.Contains(ButtonFlags.DPadUp) || controllerState.ButtonState.Buttons.Contains(ButtonFlags.LeftThumbUp) || controllerState.ButtonState.Buttons.Contains(ButtonFlags.LeftPadClickUp))
                {
                    direction = WPFUtils.Direction.Up;
                }
                else if (controllerState.ButtonState.Buttons.Contains(ButtonFlags.DPadDown) || controllerState.ButtonState.Buttons.Contains(ButtonFlags.LeftThumbDown) || controllerState.ButtonState.Buttons.Contains(ButtonFlags.LeftPadClickDown))
                {
                    direction = WPFUtils.Direction.Down;
                }
                else if (controllerState.ButtonState.Buttons.Contains(ButtonFlags.DPadLeft) || controllerState.ButtonState.Buttons.Contains(ButtonFlags.LeftThumbLeft) || controllerState.ButtonState.Buttons.Contains(ButtonFlags.LeftPadClickLeft))
                {
                    direction = WPFUtils.Direction.Left;
                }
                else if (controllerState.ButtonState.Buttons.Contains(ButtonFlags.DPadRight) || controllerState.ButtonState.Buttons.Contains(ButtonFlags.LeftThumbRight) || controllerState.ButtonState.Buttons.Contains(ButtonFlags.LeftPadClickRight))
                {
                    direction = WPFUtils.Direction.Right;
                }

                // navigation
                if (direction != WPFUtils.Direction.None)
                {
                    switch(elementType)
                    {
                        default:
                            {
                                focusedElement = WPFUtils.GetClosestControl<Control>(focusedElement, _gamepadWindow.elements, direction, new List<Type>() { typeof(NavigationViewItem) });
                                Focus(focusedElement);
                            }
                            break;

                        case "NavigationViewItem":
                            {
                                focusedElement = WPFUtils.GetClosestControl<NavigationViewItem>(focusedElement, _gamepadWindow.elements, direction);
                                Focus(focusedElement);
                            }
                            break;

                        case "ComboBoxItem":
                            {
                                switch(direction)
                                {
                                    case WPFUtils.Direction.Up:
                                        KeyboardSimulator.KeyPress(VirtualKeyCode.UP);
                                        break;
                                    case WPFUtils.Direction.Down:
                                        KeyboardSimulator.KeyPress(VirtualKeyCode.DOWN);
                                        break;
                                }
                            }
                            break;
                    }
                }
            });
        }
    }
}
