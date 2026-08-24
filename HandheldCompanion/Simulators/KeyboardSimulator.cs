using Gma.System.MouseKeyHook;
using GregsStack.InputSimulatorStandard;
using GregsStack.InputSimulatorStandard.Native;
using HandheldCompanion.Inputs;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using WindowsInput.Events;

namespace HandheldCompanion.Simulators;

public static class KeyboardSimulator
{
    private static readonly InputSimulator InputSimulator;

    // Shared toggle state per key - all bindings targeting the same key share this state
    private static readonly Dictionary<VirtualKeyCode, bool> ToggleStates = new();
    private static readonly object ToggleLock = new();

    static KeyboardSimulator()
    {
        InputSimulator = new InputSimulator();
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    /// <summary>
    /// Flip the toggle state for a key. Returns the new toggle state.
    /// Call this on button press (rising edge) when HasToggle is enabled.
    /// </summary>
    public static bool FlipToggle(VirtualKeyCode key)
    {
        lock (ToggleLock)
        {
            bool wasToggled = ToggleStates.TryGetValue(key, out var state) && state;
            bool newState = !wasToggled;
            ToggleStates[key] = newState;
            return newState;
        }
    }

    /// <summary>
    /// Get the current toggle state for a key.
    /// Also checks if key was released externally and resets toggle if so.
    /// </summary>
    public static bool GetToggleState(VirtualKeyCode key)
    {
        lock (ToggleLock)
        {
            if (!ToggleStates.TryGetValue(key, out var state) || !state)
                return false;

            // Check if key is actually pressed using Windows API (detect external release)
            short keyState = GetAsyncKeyState((int)key);
            bool isActuallyPressed = (keyState & 0x8000) != 0;

            if (!isActuallyPressed)
            {
                // External release detected (user pressed physical key, or another app released it)
                ToggleStates[key] = false;
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Reset toggle state for a key without sending KeyUp.
    /// </summary>
    public static void ResetToggle(VirtualKeyCode key)
    {
        lock (ToggleLock)
        {
            ToggleStates[key] = false;
        }
    }

    [DllImport("user32.dll")]
    private static extern int MapVirtualKey(int uCode, uint uMapType);

    [DllImport("user32.dll")]
    static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    // Define some constants for dwFlags values
    public const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    public const uint KEYEVENTF_KEYUP = 0x0002;
    public const uint KEYEVENTF_SCANCODE = 0x0008;
    public const uint KEYEVENTF_UNICODE = 0x0004;
    private const uint MAPVK_VK_TO_VSC_EX = 4;

    private static bool IsExtendedKey(VirtualKeyCode key)
    {
        return (MapVirtualKey((int)key, MAPVK_VK_TO_VSC_EX) & 0xFF00) == 0xE000;
    }

    // A function that sends a key down event for a KeyEventArgs object using the keybd_event function
    public static void KeyDown(KeyEventArgsExt e)
    {
        KeyDown(e, UIntPtr.Zero);
    }

    public static void KeyDown(KeyEventArgsExt e, UIntPtr extraInfo)
    {
        byte vk = (byte)e.KeyValue;
        byte scan = (byte)e.ScanCode;
        uint flags = IsExtendedKey((VirtualKeyCode)e.KeyValue) ? KEYEVENTF_EXTENDEDKEY : 0;

        keybd_event(vk, scan, flags, extraInfo);
    }

    // A function that sends a key up event for a KeyEventArgs object using the keybd_event function
    public static void KeyUp(KeyEventArgsExt e)
    {
        KeyUp(e, UIntPtr.Zero);
    }

    public static void KeyUp(KeyEventArgsExt e, UIntPtr extraInfo)
    {
        byte vk = (byte)e.KeyValue;
        byte scan = (byte)e.ScanCode;
        uint flags = (IsExtendedKey((VirtualKeyCode)e.KeyValue) ? KEYEVENTF_EXTENDEDKEY : 0) | KEYEVENTF_KEYUP;

        keybd_event(vk, scan, flags, extraInfo);
    }

    public static void KeyDown(VirtualKeyCode key)
    {
        try
        {
            InputSimulator.Keyboard.KeyDown(key);
        }
        catch (Exception)
        {
            // Some simulated input commands were not sent successfully.
        }
    }

    public static void KeyUp(VirtualKeyCode key, UIntPtr extraInfo)
    {
        byte vk = (byte)key;
        byte scan = (byte)MapVirtualKey((int)key, MAPVK_VK_TO_VSC_EX);
        uint flags = (IsExtendedKey(key) ? KEYEVENTF_EXTENDEDKEY : 0) | KEYEVENTF_KEYUP;

        keybd_event(vk, scan, flags, extraInfo);
    }

    public static void KeyDown(KeyCode[] keys)
    {
        foreach (var key in keys)
            KeyDown((VirtualKeyCode)key);
    }

    public static void KeyUp(VirtualKeyCode key)
    {
        try
        {
            InputSimulator.Keyboard.KeyUp(key);
        }
        catch (Exception)
        {
            // Some simulated input commands were not sent successfully.
        }
    }

    public static void KeyUp(KeyCode[] keys)
    {
        foreach (var key in keys)
            KeyUp((VirtualKeyCode)key);
    }

    public static void KeyPress(VirtualKeyCode key)
    {
        try
        {
            InputSimulator.Keyboard.KeyPress(key);
        }
        catch (Exception)
        {
            // Some simulated input commands were not sent successfully.
        }
    }

    public static async void KeyPress(VirtualKeyCode[] keys, int delay = 250)
    {
        foreach (var key in keys)
            KeyDown(key);

        await Task.Delay(delay);

        foreach (var key in keys)
            KeyUp(key);
    }

    public static void KeyPress(KeyCode[] keys)
    {
        foreach (var key in keys)
            KeyPress((VirtualKeyCode)key);
    }

    public static void KeyPress(InputsKey[] keys)
    {
        foreach (InputsKey key in keys)
        {
            VirtualKeyCode virtualKeyCode = (VirtualKeyCode)key.KeyValue;
            if (key.IsKeyDown)
                KeyDown(virtualKeyCode);
            else
                KeyUp(virtualKeyCode);
        }
    }

    public static void KeyStroke(VirtualKeyCode mod, VirtualKeyCode key)
    {
        try
        {
            InputSimulator.Keyboard.ModifiedKeyStroke(mod, key);
        }
        catch (Exception)
        {
            // Some simulated input commands were not sent successfully.
        }
    }

    public static string GetVirtualKey(VirtualKeyCode key)
    {
        var c = (char)MapVirtualKey((int)key, 2);
        if (char.IsControl(c))
            return key.ToString();

        return c.ToString();
    }
}
