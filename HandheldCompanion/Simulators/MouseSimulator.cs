using GregsStack.InputSimulatorStandard;
using HandheldCompanion.Actions;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;

namespace HandheldCompanion.Simulators;

public static class MouseSimulator
{
    private static readonly InputSimulator InputSimulator;

    // Shared toggle state per mouse button - all bindings targeting the same button share this state
    private static readonly Dictionary<MouseActionsType, bool> ToggleStates = new();
    private static readonly object ToggleLock = new();

    // Virtual key codes for mouse buttons (for GetAsyncKeyState)
    private const int VK_LBUTTON = 0x01;
    private const int VK_RBUTTON = 0x02;
    private const int VK_MBUTTON = 0x04;

    static MouseSimulator()
    {
        InputSimulator = new InputSimulator();
        InputSimulator.Mouse.MouseWheelClickSize = 6;
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    /// <summary>
    /// Flip the toggle state for a mouse button. Returns the new toggle state.
    /// Call this on button press (rising edge) when HasToggle is enabled.
    /// </summary>
    public static bool FlipToggle(MouseActionsType type)
    {
        lock (ToggleLock)
        {
            bool wasToggled = ToggleStates.TryGetValue(type, out var state) && state;
            bool newState = !wasToggled;
            ToggleStates[type] = newState;
            return newState;
        }
    }

    /// <summary>
    /// Get the current toggle state for a mouse button.
    /// Also checks if button was released externally and resets toggle if so.
    /// </summary>
    public static bool GetToggleState(MouseActionsType type)
    {
        lock (ToggleLock)
        {
            if (!ToggleStates.TryGetValue(type, out var state) || !state)
                return false;

            // Check if button is actually pressed using Windows API
            int vk = type switch
            {
                MouseActionsType.LeftButton => VK_LBUTTON,
                MouseActionsType.RightButton => VK_RBUTTON,
                MouseActionsType.MiddleButton => VK_MBUTTON,
                _ => 0
            };

            if (vk != 0)
            {
                short keyState = GetAsyncKeyState(vk);
                bool isActuallyPressed = (keyState & 0x8000) != 0;

                if (!isActuallyPressed)
                {
                    // External release detected
                    ToggleStates[type] = false;
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Reset toggle state for a mouse button without sending MouseUp.
    /// </summary>
    public static void ResetToggle(MouseActionsType type)
    {
        lock (ToggleLock)
        {
            ToggleStates[type] = false;
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

    public static int MouseX => InputSimulator.Mouse.Position.X;
    public static int MouseY => InputSimulator.Mouse.Position.Y;

    public static void Sleep(int timeout)
    {
        try
        {
            InputSimulator.Mouse.Sleep(timeout);
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
