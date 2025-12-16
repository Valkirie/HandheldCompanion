using GregsStack.InputSimulatorStandard;
using HandheldCompanion.Actions;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace HandheldCompanion.Simulators;

public static class MouseSimulator
{
    private static readonly InputSimulator InputSimulator;

    // Shared state tracking for mouse buttons - allows multiple actions to check the same state
    private static readonly Dictionary<MouseActionsType, bool> ButtonStates = new();
    private static readonly object StateLock = new();

    static MouseSimulator()
    {
        InputSimulator = new InputSimulator();
        InputSimulator.Mouse.MouseWheelClickSize = 6;
    }

    /// <summary>
    /// Returns the shared state of a mouse button.
    /// Used for toggle desync detection when multiple actions target the same button.
    /// </summary>
    public static bool IsButtonDown(MouseActionsType type)
    {
        lock (StateLock)
        {
            return ButtonStates.TryGetValue(type, out var state) && state;
        }
    }

    public static void MouseDown(MouseActionsType type, int scrollAmountInClicks = 0)
    {
        try
        {
            switch (type)
            {
                case MouseActionsType.LeftButton:
                    InputSimulator.Mouse.LeftButtonDown();
                    lock (StateLock) { ButtonStates[type] = true; }
                    break;
                case MouseActionsType.RightButton:
                    InputSimulator.Mouse.RightButtonDown();
                    lock (StateLock) { ButtonStates[type] = true; }
                    break;
                case MouseActionsType.MiddleButton:
                    InputSimulator.Mouse.MiddleButtonDown();
                    lock (StateLock) { ButtonStates[type] = true; }
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
                    lock (StateLock) { ButtonStates[type] = false; }
                    break;
                case MouseActionsType.RightButton:
                    InputSimulator.Mouse.RightButtonUp();
                    lock (StateLock) { ButtonStates[type] = false; }
                    break;
                case MouseActionsType.MiddleButton:
                    InputSimulator.Mouse.MiddleButtonUp();
                    lock (StateLock) { ButtonStates[type] = false; }
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
        catch (Exception)
        {
            // Some simulated input commands were not sent successfully.
        }
    }

    // TODO: unused, remove?
    public static void MoveTo(double x, double y)
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