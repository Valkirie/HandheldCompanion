using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ControllerCommon.Controllers;
using ControllerCommon.Inputs;
using ControllerCommon.Utils;
using HandheldCompanion.Views;
using HandheldCompanion.Views.Classes;
using Timer = System.Timers.Timer;

namespace HandheldCompanion.Managers
{
    public struct BorderDetails
    {
        public Brush BorderBrush;
        public Thickness BorderThickness;
    }

    public static class GamepadFocusManager
    {
        // used to store current focus control based on window
        private static Dictionary<Window, Control> focusedElements = new();

        private static Dictionary<Control, BorderDetails> controlBorderDetailsMap = new();

        private static BorderDetails focuseBorderDetails = new()
        {
            BorderBrush = (Brush)Application.Current.Resources["AccentButtonBackground"],
            BorderThickness = new Thickness(2),
        };

        static GamepadFocusManager()
        {
        }

        public static void Focus(Control control)
        {
            if (control is null)
                return;

            // get parent window from control
            Window parentWindow = Window.GetWindow(control);

            // get currently focused control, if any
            if (focusedElements.TryGetValue(parentWindow, out Control prevControl))
            {
                // get stored control border details
                if (controlBorderDetailsMap.TryGetValue(prevControl, out BorderDetails borderDetails))
                {
                    // restore stored control bordel details
                    prevControl.BorderBrush = borderDetails.BorderBrush;
                    prevControl.BorderThickness = borderDetails.BorderThickness;
                }
            }

            // store control border details
            controlBorderDetailsMap[control] = new()
            {
                BorderBrush = control.BorderBrush,
                BorderThickness = control.BorderThickness,
            };

            // set control border details to focused style
            focusedElements[parentWindow] = control;
            control.BorderBrush = focuseBorderDetails.BorderBrush;
            control.BorderThickness = focuseBorderDetails.BorderThickness;

            // bring to view
            control.BringIntoView();
        }

        public static Control FocusedElement(GamepadWindow window)
        {
            if (focusedElements.TryGetValue(window, out Control control))
                return control;

            return window.elements.FirstOrDefault();
        }

        private static bool HasFocusedElement(GamepadWindow window)
        {
            return focusedElements.ContainsKey(window);
        }

        public static void Start()
        {
            // PLACEHOLDER CODE
            Timer timer = new Timer(1000);
            timer.Elapsed += (s, e) => { UpdateReport(new ControllerState()); };
            timer.AutoReset = true;
            timer.Start();
        }

        private static AxisState prevAxisState = new();
        private static ButtonState prevButtonState = new();

        private static int tempIdx = 0;

        public static void UpdateReport(ControllerState controllerState)
        {
            if (MainWindow.overlayquickTools.Visibility == Visibility.Collapsed)
                return;

            if (controllerState.ButtonState.State == prevButtonState.State)
                return;

            // UI thread (async)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                controllerState.ButtonState = new ButtonState();
                controllerState.ButtonState.State.TryAdd(ButtonFlags.DPadDown, true);

                if (controllerState.ButtonState.State.ContainsKey(ButtonFlags.DPadDown))
                {
                    // Keyboard.
                    var focusedElement = FocusedElement(MainWindow.overlayquickTools);

                    var test = WPFUtils.GetClosestControl(focusedElement,
                        MainWindow.overlayquickTools.elements, tempIdx % 2 == 0 ? WPFUtils.Direction.Down : WPFUtils.Direction.Up);

                    tempIdx++;

                    Focus(test);
                }
            });
        }
    }
}
