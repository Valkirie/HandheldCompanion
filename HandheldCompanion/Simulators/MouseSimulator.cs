using GregsStack.InputSimulatorStandard;
using HandheldCompanion.Actions;
using System;
using System.Drawing;

namespace HandheldCompanion.Simulators
{
    public static class MouseSimulator
    {
        private static InputSimulator InputSimulator;

        static MouseSimulator()
        {
            InputSimulator = new InputSimulator();
            InputSimulator.Mouse.MouseWheelClickSize = 6;
        }

        public static void MouseDown(MouseActionsType type, int scrollAmountInClicks = 0)
        {
            try
            {
                switch (type)
                {
                    case MouseActionsType.LeftButton:
                        InputSimulator.Mouse.LeftButtonDown();
                        break;
                    case MouseActionsType.RightButton:
                        InputSimulator.Mouse.RightButtonDown();
                        break;
                    case MouseActionsType.MiddleButton:
                        InputSimulator.Mouse.MiddleButtonDown();
                        break;
                    case MouseActionsType.ScrollUp:
                        InputSimulator.Mouse.VerticalScroll(scrollAmountInClicks);
                        break;
                    case MouseActionsType.ScrollDown:
                        InputSimulator.Mouse.VerticalScroll(-scrollAmountInClicks);
                        break;
                }
            }
            catch (Exception)
            {
                // Some simulated input commands were not sent successfully.
            }
        }

        public static void MouseUp(MouseActionsType type)
        {
            try
            {
                switch (type)
                {
                    case MouseActionsType.LeftButton:
                        InputSimulator.Mouse.LeftButtonUp();
                        break;
                    case MouseActionsType.RightButton:
                        InputSimulator.Mouse.RightButtonUp();
                        break;
                    case MouseActionsType.MiddleButton:
                        InputSimulator.Mouse.MiddleButtonUp();
                        break;
                }
            }
            catch (Exception)
            {
                // Some simulated input commands were not sent successfully.
            }
        }

        public static void MoveBy(int x, int y)
        {
            try
            {
                InputSimulator.Mouse.MoveMouseBy(x, y);
            }
            catch(Exception)
            {
                // Some simulated input commands were not sent successfully.
            }
        }

        public static void MoveTo(int x, int y)
        {
            try
            {
                InputSimulator.Mouse.MoveMouseTo(x, y);
            }
            catch (Exception)
            {
                // Some simulated input commands were not sent successfully.
            }
        }

        public static void HorizontalScroll(int x)
        {
            try
            {
                InputSimulator.Mouse.HorizontalScroll(x);
            }
            catch (Exception)
            {
                // Some simulated input commands were not sent successfully.
            }
        }

        public static void VerticalScroll(int y)
        {
            try
            {
                InputSimulator.Mouse.VerticalScroll(y);
            }
            catch (Exception)
            {
                // Some simulated input commands were not sent successfully.
            }
        }

        public static Point GetMousePosition()
        {
            return InputSimulator.Mouse.Position;
        }
    }
}
