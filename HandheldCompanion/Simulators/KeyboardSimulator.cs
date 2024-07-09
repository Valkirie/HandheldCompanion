using Gma.System.MouseKeyHook;
using GregsStack.InputSimulatorStandard;
using GregsStack.InputSimulatorStandard.Native;
using HandheldCompanion.Inputs;
using System;
using System.Runtime.InteropServices;
using WindowsInput.Events;

namespace HandheldCompanion.Simulators;

public static class KeyboardSimulator
{
    private static readonly InputSimulator InputSimulator;

    static KeyboardSimulator()
    {
        InputSimulator = new InputSimulator();
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

    // A function that sends a key down event for a KeyEventArgs object using the keybd_event function
    public static void KeyDown(KeyEventArgsExt e)
    {
        // Get the virtual-key code and the scan code of the key
        byte vk = (byte)e.KeyValue;
        byte scan = (byte)e.ScanCode;

        // Check if the key is a modifier key (Ctrl, Alt, Shift)
        bool isModifier = ((e.Modifiers & System.Windows.Forms.Keys.Control) != 0) ||
                          ((e.Modifiers & System.Windows.Forms.Keys.Alt) != 0) ||
                          ((e.Modifiers & System.Windows.Forms.Keys.Shift) != 0);

        // If the key is a modifier key, send a key down event for it
        if (isModifier)
        {
            // Get the virtual-key code of the modifier key
            byte modifierVk = (byte)e.Modifiers;

            // Get the scan code of the modifier key
            byte modifierScan = (byte)MapVirtualKey(modifierVk, 0);

            // Send a key down event for the modifier key
            keybd_event(modifierVk, modifierScan, 0, UIntPtr.Zero);
        }

        // Send a key down event for the key
        keybd_event(vk, scan, 0, UIntPtr.Zero);
    }

    // A function that sends a key up event for a KeyEventArgs object using the keybd_event function
    public static void KeyUp(KeyEventArgsExt e)
    {
        // Get the virtual-key code and the scan code of the key
        byte vk = (byte)e.KeyValue;
        byte scan = (byte)e.ScanCode;

        // Check if the key is a modifier key (Ctrl, Alt, Shift)
        bool isModifier = ((e.Modifiers & System.Windows.Forms.Keys.Control) != 0) ||
                          ((e.Modifiers & System.Windows.Forms.Keys.Alt) != 0) ||
                          ((e.Modifiers & System.Windows.Forms.Keys.Shift) != 0);

        // Send a key up event for the key
        keybd_event(vk, scan, KEYEVENTF_KEYUP, UIntPtr.Zero);

        // If the key is a modifier key, send a key up event for it
        if (isModifier)
        {
            // Get the virtual-key code of the modifier key
            byte modifierVk = (byte)e.Modifiers;

            // Get the scan code of the modifier key
            byte modifierScan = (byte)MapVirtualKey(modifierVk, 0);

            // Send a key up event for the modifier key
            keybd_event(modifierVk, modifierScan, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }
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

    public static void KeyPress(VirtualKeyCode[] keys)
    {
        foreach (var key in keys)
            KeyDown(key);

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