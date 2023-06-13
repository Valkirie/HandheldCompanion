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

            WPFUtils.MakeFocusVisible(control);
            Keyboard.Focus(control);

            // get parent window from control
            Window parentWindow = Window.GetWindow(control);

            // bring to view
            control.BringIntoView();
        }

        public static Control FocusedElement(GamepadWindow window)
        {
            Control keyboardFocused = (Control)Keyboard.FocusedElement;
            if (keyboardFocused is not null && window.elements.Contains(keyboardFocused))
                return keyboardFocused;
            else
                return WPFUtils.GetTopLeftControl(window.elements);
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

                WPFUtils.Direction direction = WPFUtils.Direction.None;

                if (controllerState.ButtonState.Buttons.Contains(ButtonFlags.B1))
                {
                    // lazy
                    // todo: implement proper RoutedEvent call
                    switch(focusedElement.GetType().Name)
                    {
                        case "Button":
                        case "ToggleSwitch":
                        case "ToggleButton":
                        case "CheckBox":
                            KeyboardSimulator.KeyPress(VirtualKeyCode.SPACE);
                            break;
                    }
                }
                else if (controllerState.ButtonState.Buttons.Contains(ButtonFlags.DPadUp))
                    direction = WPFUtils.Direction.Up;
                else if (controllerState.ButtonState.Buttons.Contains(ButtonFlags.DPadDown))
                    direction = WPFUtils.Direction.Down;
                else if (controllerState.ButtonState.Buttons.Contains(ButtonFlags.DPadLeft))
                    direction = WPFUtils.Direction.Left;
                else if (controllerState.ButtonState.Buttons.Contains(ButtonFlags.DPadRight))
                    direction = WPFUtils.Direction.Right;

                // navigation
                if (direction != WPFUtils.Direction.None)
                {
                    if (focusedElement != null)
                    {
                        var test = WPFUtils.GetClosestControl(focusedElement, _gamepadWindow.elements, direction);
                        if (test is not null)
                            Focus(test);
                        else
                            Focus(focusedElement);
                    }
                }
            });
        }
    }
}
