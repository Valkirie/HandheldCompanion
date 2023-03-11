using GregsStack.InputSimulatorStandard;
using HandheldCompanion.Actions;
using System;
using System.ComponentModel;
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

        public static void MouseUp(MouseActionsType type)
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

        public static void MoveBy(int x, int y)
        {
            InputSimulator.Mouse.MoveMouseBy(x, y);
        }

        public static void MoveTo(int x, int y)
        {
            InputSimulator.Mouse.MoveMouseTo(x, y);
        }

        public static void HorizontalScroll(int x)
        {
            InputSimulator.Mouse.HorizontalScroll(x);
        }

        public static void VerticalScroll(int y)
        {
            InputSimulator.Mouse.VerticalScroll(y);
        }

        public static Point GetMousePosition()
        {
            return InputSimulator.Mouse.Position;
        }
    }
}
