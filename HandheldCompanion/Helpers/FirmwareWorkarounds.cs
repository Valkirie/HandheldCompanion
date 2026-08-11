using Gma.System.MouseKeyHook;
using HandheldCompanion.Shared;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace HandheldCompanion.Helpers
{
    /// <summary>
    /// Groups firmware-specific input workarounds by device manufacturer.
    /// </summary>
    public static class FirmwareWorkarounds
    {
        /// <summary>
        /// Handles MSI firmware input quirks.
        /// </summary>
        public sealed class MSI
        {
            private const uint INPUT_KEYBOARD = 1;
            private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
            private const uint KEYEVENTF_KEYUP = 0x0002;

            // PowerToys uses the undocumented 0xFF key as a no-op between modifier events.
            private const ushort VK_DUMMY = 0x00FF;

            private static readonly UIntPtr InjectedMarker = new(0x4843424Cu);

            private bool enabled;
            private bool leftWinDown;
            private bool rightWinDown;
            private bool gDown;
            private bool shortcutActive;
            private bool releasedLeftWin;
            private bool releasedRightWin;
            private bool injecting;

            /// <summary>
            /// Gets or sets whether MSI firmware workarounds are active.
            /// </summary>
            public bool Enabled
            {
                get => enabled;
                set
                {
                    enabled = value;
                    if (!enabled)
                        Reset();
                }
            }

            /// <summary>
            /// Applies the MSI Claw Win+G workaround to a keyboard hook event.
            /// </summary>
            /// <param name="args">The keyboard hook event to inspect and, when required, suppress.</param>
            /// <param name="injected">Whether Windows marked the event as injected.</param>
            /// <returns>True when the event was injected by this workaround and should bypass InputsManager.</returns>
            public bool ProcessKeyboardEvent(KeyEventArgsExt args, bool injected)
            {
                if (injected && injecting)
                    return true;

                if (!injected && args.KeyCode is Keys.LWin or Keys.RWin)
                {
                    if (args.KeyCode == Keys.LWin)
                        leftWinDown = args.IsKeyDown;
                    else
                        rightWinDown = args.IsKeyDown;
                }

                if ((!enabled && !shortcutActive) || (!args.IsKeyDown && !args.IsKeyUp))
                    return false;

                if (args.KeyCode is Keys.LWin or Keys.RWin)
                {
                    bool released = args.KeyCode == Keys.LWin ? releasedLeftWin : releasedRightWin;
                    if (!args.IsKeyUp || !released)
                        return false;

                    if (args.KeyCode == Keys.LWin)
                        releasedLeftWin = false;
                    else
                        releasedRightWin = false;

                    shortcutActive = releasedLeftWin || releasedRightWin;
                    args.SuppressKeyPress = true;
                    return false;
                }

                if (args.KeyCode != Keys.G)
                    return false;

                bool windowsKeyDown = leftWinDown || rightWinDown;
                if (!windowsKeyDown && !gDown && !shortcutActive)
                    return false;

                if (args.IsKeyDown && windowsKeyDown && !shortcutActive)
                {
                    gDown = true;
                    shortcutActive = true;

                    List<KeyboardInput> inputs = new(4)
                    {
                        CreateKeyInput(VK_DUMMY, false),
                        CreateKeyInput(VK_DUMMY, true),
                    };

                    int leftWinIndex = -1;
                    int rightWinIndex = -1;

                    if (leftWinDown)
                    {
                        leftWinIndex = inputs.Count;
                        inputs.Add(CreateKeyInput((ushort)Keys.LWin, true));
                    }

                    if (rightWinDown)
                    {
                        rightWinIndex = inputs.Count;
                        inputs.Add(CreateKeyInput((ushort)Keys.RWin, true));
                    }

                    KeyboardInput[] inputArray = [.. inputs];
                    uint sent;
                    int error;
                    injecting = true;
                    try
                    {
                        sent = SendInput((uint)inputArray.Length, inputArray, Marshal.SizeOf<KeyboardInput>());
                        error = sent == inputArray.Length ? 0 : Marshal.GetLastWin32Error();
                        if (sent != inputArray.Length)
                        {
                            LogManager.LogError("Failed to apply MSI Claw Win+G firmware workaround: sent {0} of {1} inputs, error {2}",
                                sent, inputArray.Length, error);

                            // The dummy key-down is the only non-key-up record in this fixed sequence.
                            if (sent == 1)
                            {
                                KeyboardInput[] cleanupInput = [CreateKeyInput(VK_DUMMY, true)];
                                uint cleanupSent = SendInput(1, cleanupInput, Marshal.SizeOf<KeyboardInput>());
                                if (cleanupSent != 1)
                                {
                                    LogManager.LogError("Failed to release the MSI Claw firmware workaround dummy key, error {0}",
                                        Marshal.GetLastWin32Error());
                                }
                            }
                        }
                    }
                    finally
                    {
                        injecting = false;
                    }

                    releasedLeftWin = leftWinIndex >= 0 && sent > leftWinIndex;
                    releasedRightWin = rightWinIndex >= 0 && sent > rightWinIndex;

                    if (!releasedLeftWin && !releasedRightWin)
                    {
                        shortcutActive = false;
                        gDown = false;

                        // The ordinary InputsManager chord path still decides whether to suppress Win+G.
                        return false;
                    }
                }
                else if (args.IsKeyUp)
                {
                    gDown = false;
                }

                args.SuppressKeyPress = true;
                return false;

                static KeyboardInput CreateKeyInput(ushort key, bool keyUp)
                {
                    uint flags = keyUp ? KEYEVENTF_KEYUP : 0;
                    if (key is (ushort)Keys.LWin or (ushort)Keys.RWin)
                        flags |= KEYEVENTF_EXTENDEDKEY;

                    return new()
                    {
                        Type = INPUT_KEYBOARD,
                        Keyboard = new()
                        {
                            VirtualKey = key,
                            Flags = flags,
                            ExtraInfo = InjectedMarker,
                        }
                    };
                }
            }

            /// <summary>
            /// Clears tracked keyboard and shortcut state without changing whether the workaround is enabled.
            /// </summary>
            public void Reset()
            {
                leftWinDown = false;
                rightWinDown = false;
                gDown = false;
                shortcutActive = false;
                releasedLeftWin = false;
                releasedRightWin = false;
                injecting = false;
            }

            // Windows INPUT is 40 bytes on x64: type at offset 0 and its union at offset 8.
            [StructLayout(LayoutKind.Explicit, Size = 40)]
            private struct KeyboardInput
            {
                [FieldOffset(0)]
                public uint Type;

                [FieldOffset(8)]
                public KeyboardInputData Keyboard;
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct KeyboardInputData
            {
                public ushort VirtualKey;
                public ushort ScanCode;
                public uint Flags;
                public uint Time;
                public UIntPtr ExtraInfo;
            }

            /// <summary>
            /// Injects the keyboard sequence used to clear the MSI firmware shortcut state.
            /// </summary>
            [DllImport("user32.dll", SetLastError = true)]
            private static extern uint SendInput(uint count, KeyboardInput[] inputs, int inputSize);
        }
    }
}
