using System;
using System.Runtime.InteropServices;
using GregsStack.InputSimulatorStandard;
using GregsStack.InputSimulatorStandard.Native;
using HandheldCompanion.Managers;
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

    public static void KeyPress(OutputKey[] keys)
    {
        foreach (var key in keys)
            if (key.IsKeyDown)
                KeyDown((VirtualKeyCode)key.KeyValue);
            else
                KeyUp((VirtualKeyCode)key.KeyValue);
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