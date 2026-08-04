using Gma.System.MouseKeyHook;
using HandheldCompanion.Shared;
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
    private const int KEY_PRESSED = 0x8000;

    private static readonly UIntPtr InjectedMarker = new(0x4843424Cu);

    private static readonly Keys[] ControlKeys = [Keys.LControlKey, Keys.RControlKey, Keys.ControlKey];
    private static readonly Keys[] AltKeys = [Keys.LMenu, Keys.RMenu, Keys.Menu];
    private static readonly Keys[] ShiftKeys = [Keys.LShiftKey, Keys.RShiftKey, Keys.ShiftKey];
    private static readonly Keys[] WindowsKeys = [Keys.LWin, Keys.RWin];

    private readonly object updateLock = new();
    private readonly Dictionary<string, BlockedHotkey> hotkeys = new(StringComparer.Ordinal);
    private readonly HashSet<Keys> physicalKeysDown = [];
    private readonly HashSet<Keys> releasedModifierKeys = [];
    private readonly LinkedList<InjectedKeyEvent> expectedInjectedEvents = [];
    private readonly Func<KeyboardInput[], InjectionResult> injectKeyboardInput;
    private readonly Func<Keys, bool>? keyStateOverride;
    private readonly bool logInjectionFailures;

    private BlockedHotkey? activeHotkey;

    public KeyboardHotkeyBlocker()
        : this(SendKeyboardInput, null, true)
    {
    }

    private KeyboardHotkeyBlocker(Func<KeyboardInput[], InjectionResult> injectKeyboardInput,
        Func<Keys, bool>? keyStateOverride, bool logInjectionFailures)
    {
        this.injectKeyboardInput = injectKeyboardInput;
        this.keyStateOverride = keyStateOverride;
        this.logInjectionFailures = logInjectionFailures;
    }

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
        PendingInjection? pending;
        LinkedListNode<InjectedKeyEvent>[]? expectedEvents = null;
        KeyboardHotkeyBlockResult result;

        lock (updateLock)
        {
            result = ProcessLocked(args, injected, out pending);

            // SendInput re-enters the low-level hook before it returns. Register the events first
            // so our synthetic keys cannot disturb Handheld Companion's chord state.
            if (pending is not null)
                expectedEvents = QueueExpectedInjectedEvents(pending.Value.Inputs);
        }

        // dispatch off the lock, SendInput can block and we're inside the hook callback
        if (pending is not null)
        {
            bool modifierReleased = DispatchPendingInjection(pending.Value, expectedEvents!);
            if (!modifierReleased)
            {
                lock (updateLock)
                {
                    if (activeHotkey == pending.Value.Hotkey)
                        activeHotkey = null;
                }

                // Nothing changed in Windows' modifier state, so preserve a balanced shortcut
                // by allowing the original action key through.
                return KeyboardHotkeyBlockResult.None;
            }
        }

        return result;
    }

    private KeyboardHotkeyBlockResult ProcessLocked(KeyEventArgsExt args, bool injected, out PendingInjection? pending)
    {
        pending = null;

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

        if (hotkeys.Count == 0 && activeHotkey is null)
            return KeyboardHotkeyBlockResult.None;

        PruneStaleKeys(args.KeyCode);

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
            pending = BuildModifierRelease(hotkey);
            return KeyboardHotkeyBlockResult.Suppress;
        }

        return KeyboardHotkeyBlockResult.None;
    }

    /// <summary>
    /// Drops keys the hardware no longer reports as held. Hooks receive nothing across the secure
    /// desktop, so a missed KeyUp would leave a modifier stuck down forever.
    /// </summary>
    private void PruneStaleKeys(Keys observedKey)
    {
        List<Keys>? stale = null;

        foreach (Keys key in physicalKeysDown)
        {
            if (key == observedKey || IsKeyPressed(key))
                continue;

            stale ??= [];
            stale.Add(key);
        }

        if (stale is null)
            return;

        foreach (Keys key in stale)
        {
            physicalKeysDown.Remove(key);
            releasedModifierKeys.Remove(key);
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

    private PendingInjection? BuildModifierRelease(BlockedHotkey hotkey)
    {
        List<Keys> modifiersToRelease = [];

        AddPressedModifiers(modifiersToRelease, hotkey.Modifiers, KeyboardHotkeyModifiers.Control, ControlKeys);
        AddPressedModifiers(modifiersToRelease, hotkey.Modifiers, KeyboardHotkeyModifiers.Alt, AltKeys);
        AddPressedModifiers(modifiersToRelease, hotkey.Modifiers, KeyboardHotkeyModifiers.Shift, ShiftKeys);
        AddPressedModifiers(modifiersToRelease, hotkey.Modifiers, KeyboardHotkeyModifiers.Windows, WindowsKeys);

        if (modifiersToRelease.Count == 0)
            return null;

        // Releasing Win or Alt without another key would open Start or activate the menu bar.
        // Send a harmless key first, then release the modifiers Windows already observed.
        bool needsDummy = hotkey.Modifiers.HasFlag(KeyboardHotkeyModifiers.Windows)
            || hotkey.Modifiers.HasFlag(KeyboardHotkeyModifiers.Alt);

        List<KeyboardInput> inputs = new(modifiersToRelease.Count + 2);

        if (needsDummy)
        {
            inputs.Add(CreateKeyInput(VK_DUMMY, false));
            inputs.Add(CreateKeyInput(VK_DUMMY, true));
        }

        for (int index = modifiersToRelease.Count - 1; index >= 0; index--)
            inputs.Add(CreateKeyInput((ushort)modifiersToRelease[index], true));

        return new(hotkey, [.. inputs], needsDummy ? 2 : 0);
    }

    private LinkedListNode<InjectedKeyEvent>[] QueueExpectedInjectedEvents(KeyboardInput[] inputs)
    {
        long expiresAt = Environment.TickCount64 + INJECTED_EVENT_TIMEOUT_MS;
        LinkedListNode<InjectedKeyEvent>[] expectedEvents = new LinkedListNode<InjectedKeyEvent>[inputs.Length];

        for (int index = 0; index < inputs.Length; index++)
        {
            KeybdInput keyboard = inputs[index].Data.Keyboard;
            expectedEvents[index] = expectedInjectedEvents.AddLast(
                new InjectedKeyEvent(keyboard.VirtualKey, (keyboard.Flags & KEYEVENTF_KEYUP) != 0, expiresAt));
        }

        return expectedEvents;
    }

    private bool DispatchPendingInjection(PendingInjection pending, LinkedListNode<InjectedKeyEvent>[] expectedEvents)
    {
        KeyboardInput[] inputs = pending.Inputs;
        InjectionResult injectionResult = injectKeyboardInput(inputs);
        int sent = (int)Math.Min(injectionResult.Sent, (uint)inputs.Length);

        lock (updateLock)
        {
            for (int index = sent; index < expectedEvents.Length; index++)
            {
                if (expectedEvents[index].List is not null)
                    expectedInjectedEvents.Remove(expectedEvents[index]);
            }

            for (int index = pending.ModifierOffset; index < sent; index++)
                releasedModifierKeys.Add((Keys)inputs[index].Data.Keyboard.VirtualKey);
        }

        if (sent == inputs.Length)
            return true;

        // unsent modifiers stay down for Windows, we let their physical KeyUp through
        if (logInjectionFailures)
        {
            LogManager.LogError("Failed to release modifiers for blocked shortcut {0}: sent {1} of {2} inputs, error {3}",
                pending.Hotkey.Name, sent, inputs.Length, injectionResult.Error);
        }

        return sent > pending.ModifierOffset;
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
        if (expectedInjectedEvents.First is not LinkedListNode<InjectedKeyEvent> expectedNode)
            return false;

        InjectedKeyEvent expected = expectedNode.Value;

        // KeyEventArgsExt normalizes VK_DUMMY (0xFF) to Keys.None in KeyCode, but preserves
        // the original virtual-key value in KeyValue.
        if (expected.KeyValue != args.KeyValue || expected.IsKeyUp != args.IsKeyUp)
            return false;

        expectedInjectedEvents.RemoveFirst();
        return true;
    }

    private void DiscardExpiredInjectedEvents()
    {
        long now = Environment.TickCount64;

        while (expectedInjectedEvents.First is LinkedListNode<InjectedKeyEvent> expectedNode && expectedNode.Value.ExpiresAt < now)
            expectedInjectedEvents.RemoveFirst();
    }

    private bool IsKeyPressed(Keys key)
    {
        return keyStateOverride?.Invoke(key) ?? (GetAsyncKeyState((int)key) & KEY_PRESSED) != 0;
    }

    private static InjectionResult SendKeyboardInput(KeyboardInput[] inputs)
    {
        uint sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<KeyboardInput>());
        int error = sent == inputs.Length ? 0 : Marshal.GetLastWin32Error();
        return new(sent, error);
    }

    internal static void RunSelfCheck()
    {
        KeyboardHotkeyBlocker reentrantBlocker = null!;
        bool injectedEventsBypassedApplication = true;

        reentrantBlocker = new(
            inputs =>
            {
                foreach (KeyboardInput input in inputs)
                {
                    KeybdInput keyboard = input.Data.Keyboard;
                    bool keyUp = (keyboard.Flags & KEYEVENTF_KEYUP) != 0;
                    KeyEventArgsExt args = CreateSelfCheckKeyEvent((Keys)keyboard.VirtualKey, keyUp);
                    injectedEventsBypassedApplication &= reentrantBlocker.Process(args, true) == KeyboardHotkeyBlockResult.BypassApplication;
                }

                return new((uint)inputs.Length, 0);
            },
            _ => true,
            false);

        reentrantBlocker.SetHotkey("Self-check Win+G", Keys.G, KeyboardHotkeyModifiers.Windows, true);
        EnsureSelfCheck(reentrantBlocker.Process(CreateSelfCheckKeyEvent(Keys.LWin, false), false) == KeyboardHotkeyBlockResult.None,
            "modifier KeyDown was unexpectedly suppressed");
        EnsureSelfCheck(reentrantBlocker.Process(CreateSelfCheckKeyEvent(Keys.G, false), false) == KeyboardHotkeyBlockResult.Suppress,
            "blocked shortcut was not suppressed");
        EnsureSelfCheck(injectedEventsBypassedApplication && reentrantBlocker.expectedInjectedEvents.Count == 0,
            "re-entrant injected events reached application chord handling");
        EnsureSelfCheck(reentrantBlocker.Process(CreateSelfCheckKeyEvent(Keys.G, true), false) == KeyboardHotkeyBlockResult.Suppress,
            "blocked action KeyUp was not suppressed");
        EnsureSelfCheck(reentrantBlocker.Process(CreateSelfCheckKeyEvent(Keys.LWin, true), false) == KeyboardHotkeyBlockResult.Suppress,
            "synthetically released modifier KeyUp was not suppressed");

        KeyboardHotkeyBlocker failedInjectionBlocker = new(_ => new(0, 5), _ => true, false);
        failedInjectionBlocker.SetHotkey("Self-check failed Win+G", Keys.G, KeyboardHotkeyModifiers.Windows, true);
        failedInjectionBlocker.Process(CreateSelfCheckKeyEvent(Keys.LWin, false), false);
        EnsureSelfCheck(failedInjectionBlocker.Process(CreateSelfCheckKeyEvent(Keys.G, false), false) == KeyboardHotkeyBlockResult.None,
            "failed injection did not pass the original shortcut through");
        EnsureSelfCheck(failedInjectionBlocker.expectedInjectedEvents.Count == 0,
            "failed injection left stale expected events");
    }

    private static KeyEventArgsExt CreateSelfCheckKeyEvent(Keys key, bool keyUp)
    {
        return new(key, 0, 0, !keyUp, keyUp, false, 0);
    }

    private static void EnsureSelfCheck(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException($"Keyboard hotkey blocker self-check failed: {message}");
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

    private readonly record struct InjectedKeyEvent(int KeyValue, bool IsKeyUp, long ExpiresAt);

    private readonly record struct PendingInjection(BlockedHotkey Hotkey, KeyboardInput[] Inputs, int ModifierOffset);

    private readonly record struct InjectionResult(uint Sent, int Error);

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

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int key);
}
