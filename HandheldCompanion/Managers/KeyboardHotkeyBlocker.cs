using Gma.System.MouseKeyHook;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace HandheldCompanion.Managers;

[Flags]
public enum KeyboardHotkeyModifiers : byte
{
    None = 0,
    Control = 1,
    Alt = 2,
    Shift = 4,
    Windows = 8,
}

internal enum KeyboardHotkeyBlockResult
{
    None,
    Suppress,
    BypassApplication,
}

internal sealed class KeyboardHotkeyBlocker
{
    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint MAPVK_VK_TO_VSC = 0;
    private const ushort VK_DUMMY = 0x00FF;
    private const long INJECTED_EVENT_TIMEOUT_MS = 1000;

    private static readonly UIntPtr InjectedMarker = new(0x4843424Cu);

    private static readonly Keys[] ControlKeys = [Keys.LControlKey, Keys.RControlKey, Keys.ControlKey];
    private static readonly Keys[] AltKeys = [Keys.LMenu, Keys.RMenu, Keys.Menu];
    private static readonly Keys[] ShiftKeys = [Keys.LShiftKey, Keys.RShiftKey, Keys.ShiftKey];
    private static readonly Keys[] WindowsKeys = [Keys.LWin, Keys.RWin];

    private readonly object updateLock = new();
    private readonly Dictionary<string, BlockedHotkey> hotkeys = new(StringComparer.Ordinal);
    private readonly HashSet<Keys> physicalKeysDown = [];
    private readonly HashSet<Keys> releasedModifierKeys = [];
    private readonly Queue<InjectedKeyEvent> expectedInjectedEvents = [];

    private BlockedHotkey? activeHotkey;

    public void SetHotkey(string name, Keys actionKey, KeyboardHotkeyModifiers modifiers, bool enabled)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (actionKey == Keys.None)
            throw new ArgumentOutOfRangeException(nameof(actionKey));

        lock (updateLock)
        {
            if (enabled)
            {
                hotkeys[name] = new(name, actionKey, modifiers);
                return;
            }

            hotkeys.Remove(name);
        }
    }

    public KeyboardHotkeyBlockResult Process(KeyEventArgsExt args, bool injected)
    {
        lock (updateLock)
        {
            DiscardExpiredInjectedEvents();

            if (injected)
            {
                if (TryConsumeExpectedInjectedEvent(args))
                    return KeyboardHotkeyBlockResult.BypassApplication;

                return KeyboardHotkeyBlockResult.None;
            }

            if (args.IsKeyDown)
                physicalKeysDown.Add(args.KeyCode);
            else if (args.IsKeyUp)
                physicalKeysDown.Remove(args.KeyCode);
            else
                return KeyboardHotkeyBlockResult.None;

            if (activeHotkey is not null)
            {
                bool suppress = args.KeyCode == activeHotkey.ActionKey || releasedModifierKeys.Contains(args.KeyCode);

                if (args.IsKeyUp)
                    releasedModifierKeys.Remove(args.KeyCode);

                if (!physicalKeysDown.Contains(activeHotkey.ActionKey) && !AreModifiersDown(activeHotkey.Modifiers))
                    activeHotkey = null;

                if (suppress)
                    return KeyboardHotkeyBlockResult.Suppress;
            }

            if (!args.IsKeyDown)
                return KeyboardHotkeyBlockResult.None;

            foreach (BlockedHotkey hotkey in hotkeys.Values)
            {
                if (hotkey.ActionKey != args.KeyCode || !AreModifiersDown(hotkey.Modifiers))
                    continue;

                activeHotkey = hotkey;
                ReleasePressedModifiers(hotkey.Modifiers);
                return KeyboardHotkeyBlockResult.Suppress;
            }

            return KeyboardHotkeyBlockResult.None;
        }
    }

    public void ResetState()
    {
        lock (updateLock)
        {
            activeHotkey = null;
            physicalKeysDown.Clear();
            releasedModifierKeys.Clear();
            expectedInjectedEvents.Clear();
        }
    }

    public void Clear()
    {
        lock (updateLock)
        {
            hotkeys.Clear();
            activeHotkey = null;
            physicalKeysDown.Clear();
            releasedModifierKeys.Clear();
            expectedInjectedEvents.Clear();
        }
    }

    private bool AreModifiersDown(KeyboardHotkeyModifiers modifiers)
    {
        return (!modifiers.HasFlag(KeyboardHotkeyModifiers.Control) || IsAnyKeyDown(ControlKeys))
            && (!modifiers.HasFlag(KeyboardHotkeyModifiers.Alt) || IsAnyKeyDown(AltKeys))
            && (!modifiers.HasFlag(KeyboardHotkeyModifiers.Shift) || IsAnyKeyDown(ShiftKeys))
            && (!modifiers.HasFlag(KeyboardHotkeyModifiers.Windows) || IsAnyKeyDown(WindowsKeys));
    }

    private bool IsAnyKeyDown(Keys[] keys)
    {
        foreach (Keys key in keys)
        {
            if (physicalKeysDown.Contains(key))
                return true;
        }

        return false;
    }

    private void ReleasePressedModifiers(KeyboardHotkeyModifiers modifiers)
    {
        List<Keys> modifiersToRelease = [];

        AddPressedModifiers(modifiersToRelease, modifiers, KeyboardHotkeyModifiers.Control, ControlKeys);
        AddPressedModifiers(modifiersToRelease, modifiers, KeyboardHotkeyModifiers.Alt, AltKeys);
        AddPressedModifiers(modifiersToRelease, modifiers, KeyboardHotkeyModifiers.Shift, ShiftKeys);
        AddPressedModifiers(modifiersToRelease, modifiers, KeyboardHotkeyModifiers.Windows, WindowsKeys);

        if (modifiersToRelease.Count == 0)
            return;

        // Releasing Win or Alt without another key would open Start or activate the menu bar.
        // Send a harmless key first, then release the modifiers Windows already observed.
        List<KeyboardInput> inputs = new(modifiersToRelease.Count + 2)
        {
            CreateKeyInput(VK_DUMMY, false),
            CreateKeyInput(VK_DUMMY, true),
        };

        for (int index = modifiersToRelease.Count - 1; index >= 0; index--)
            inputs.Add(CreateKeyInput((ushort)modifiersToRelease[index], true));

        uint sent = SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<KeyboardInput>());
        long expiresAt = Environment.TickCount64 + INJECTED_EVENT_TIMEOUT_MS;

        for (int index = 0; index < sent; index++)
        {
            KeyboardInput input = inputs[index];
            expectedInjectedEvents.Enqueue(new((Keys)input.Data.Keyboard.VirtualKey, (input.Data.Keyboard.Flags & KEYEVENTF_KEYUP) != 0, expiresAt));

            if (index >= 2)
                releasedModifierKeys.Add((Keys)input.Data.Keyboard.VirtualKey);
        }
    }

    private void AddPressedModifiers(List<Keys> destination, KeyboardHotkeyModifiers modifiers, KeyboardHotkeyModifiers modifier, Keys[] keys)
    {
        if (!modifiers.HasFlag(modifier))
            return;

        foreach (Keys key in keys)
        {
            if (physicalKeysDown.Contains(key))
                destination.Add(key);
        }
    }

    private bool TryConsumeExpectedInjectedEvent(KeyEventArgsExt args)
    {
        if (!expectedInjectedEvents.TryPeek(out InjectedKeyEvent expected))
            return false;

        if (expected.Key != args.KeyCode || expected.IsKeyUp != args.IsKeyUp)
            return false;

        expectedInjectedEvents.Dequeue();
        return true;
    }

    private void DiscardExpiredInjectedEvents()
    {
        long now = Environment.TickCount64;

        while (expectedInjectedEvents.TryPeek(out InjectedKeyEvent expected) && expected.ExpiresAt < now)
            expectedInjectedEvents.Dequeue();
    }

    private static KeyboardInput CreateKeyInput(ushort key, bool keyUp)
    {
        uint flags = keyUp ? KEYEVENTF_KEYUP : 0;
        if (key is (ushort)Keys.RControlKey or (ushort)Keys.RMenu or (ushort)Keys.LWin or (ushort)Keys.RWin)
            flags |= KEYEVENTF_EXTENDEDKEY;

        return new()
        {
            Type = INPUT_KEYBOARD,
            Data = new()
            {
                Keyboard = new()
                {
                    VirtualKey = key,
                    ScanCode = (ushort)MapVirtualKey(key, MAPVK_VK_TO_VSC),
                    Flags = flags,
                    ExtraInfo = InjectedMarker,
                }
            }
        };
    }

    private sealed record BlockedHotkey(string Name, Keys ActionKey, KeyboardHotkeyModifiers Modifiers);

    private readonly record struct InjectedKeyEvent(Keys Key, bool IsKeyUp, long ExpiresAt);

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public uint Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MouseInput Mouse;

        [FieldOffset(0)]
        public KeybdInput Keyboard;

        [FieldOffset(0)]
        public HardwareInput Hardware;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeybdInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HardwareInput
    {
        public uint Message;
        public ushort ParameterLow;
        public ushort ParameterHigh;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, KeyboardInput[] inputs, int inputSize);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint code, uint mapType);
}
