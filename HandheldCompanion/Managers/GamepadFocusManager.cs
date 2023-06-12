using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ControllerCommon.Controllers;
using ControllerCommon.Inputs;
using ControllerCommon.Utils;
using HandheldCompanion.Views;
using HandheldCompanion.Views.Classes;

namespace HandheldCompanion.Managers
{
    public static class GamepadFocusManager
    {
        // used to store current focus control based on window
        private static Dictionary<Window, Control> focusedElements = new();

        static GamepadFocusManager()
        {
        }

        public static void Focus(Control control)
        {
            if (control is null)
                return;

            WPFUtils.MakeFocusVisible(control);
            Keyboard.Focus(control);

            // get parent window from control
            Window parentWindow = Window.GetWindow(control);

            // set control border details to focused style
            focusedElements[parentWindow] = control;

            // bring to view
            control.BringIntoView();
        }

        public static Control FocusedElement(GamepadWindow window)
        {
            Control keyboardFocused = (Control)Keyboard.FocusedElement;
            if (keyboardFocused is not null && window.elements.Contains(keyboardFocused))
                return keyboardFocused;
            else if (focusedElements.TryGetValue(window, out Control control))
                return control;
            else
                return window.elements.FirstOrDefault();
        }

        private static bool HasFocusedElement(GamepadWindow window)
        {
            return focusedElements.ContainsKey(window);
        }

        public static void Start()
        {
        }

        private static AxisState prevAxisState = new();
        private static ButtonState prevButtonState = new();

        public static void UpdateReport(ControllerState controllerState)
        {
            if (MainWindow.overlayquickTools.Visibility == Visibility.Collapsed)
                return;

            if (controllerState.ButtonState.Equals(prevButtonState))
                return;

            prevButtonState = controllerState.ButtonState.Clone() as ButtonState;

            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                WPFUtils.Direction direction = WPFUtils.Direction.None;

                if (controllerState.ButtonState.Buttons.Contains(ButtonFlags.DPadUp))
                    direction = WPFUtils.Direction.Up;
                else if (controllerState.ButtonState.Buttons.Contains(ButtonFlags.DPadDown))
                    direction = WPFUtils.Direction.Down;
                else if (controllerState.ButtonState.Buttons.Contains(ButtonFlags.DPadLeft))
                    direction = WPFUtils.Direction.Left;
                else if (controllerState.ButtonState.Buttons.Contains(ButtonFlags.DPadRight))
                    direction = WPFUtils.Direction.Right;

                if (direction == WPFUtils.Direction.None)
                    return;

                // Keyboard
                var focusedElement = FocusedElement(MainWindow.overlayquickTools);
                if (focusedElement != null)
                {
                    var test = WPFUtils.GetClosestControl(focusedElement,
                        MainWindow.overlayquickTools.elements, direction);

                    Focus(test);
                }
            });
        }
    }
}
