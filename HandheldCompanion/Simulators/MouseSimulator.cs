using GregsStack.InputSimulatorStandard;
using System;
using System.Drawing;

namespace HandheldCompanion.Simulators
{
    public static class MouseSimulator
    {
        [Serializable]
        public enum MouseActionsType
        {
            LeftButton = 0,
            RightButton = 1,
            MiddleButton = 2,
            MoveByX = 3,
            MoveByY = 4
        }

        private static InputSimulator InputSimulator;

        static MouseSimulator()
        {
            InputSimulator = new InputSimulator();
        }

        public static void MouseDown(MouseActionsType type)
        {
            switch(type)
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

        public static Point GetMousePosition()
        {
            return InputSimulator.Mouse.Position;
        }
    }
}
