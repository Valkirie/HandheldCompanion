using Gma.System.MouseKeyHook;
using GregsStack.InputSimulatorStandard.Native;
using HandheldCompanion.Commands;
using HandheldCompanion.Controllers;
using HandheldCompanion.Devices;
using HandheldCompanion.Inputs;
using HandheldCompanion.Simulators;
using PrecisionTiming;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using WindowsInput.Events;
using ButtonState = HandheldCompanion.Inputs.ButtonState;
using Timer = System.Timers.Timer;

namespace HandheldCompanion.Managers;

public static class InputsManager
{
    #region events
    public delegate void InitializedEventHandler();
    public static event InitializedEventHandler Initialized;

    public delegate void StartedListeningEventHandler(ButtonFlags buttonFlags, InputsChordTarget chordTarget);
    public static event StartedListeningEventHandler StartedListening;

    public delegate void StoppedListeningEventHandler(ButtonFlags buttonFlags, InputsChord storedChord);
    public static event StoppedListeningEventHandler StoppedListening;

    public delegate void CommandExecutedEventHandler(Hotkey hotkey, ICommands command);
    public static event CommandExecutedEventHandler CommandExecuted;
    #endregion

    private const short TIME_FLUSH = 5;             // default interval between buffer flush
    private const short TIME_FLUSH_EXTENDED = 150;  // extended buffer flush interval when expecting another chord key
    private const short TIME_FLUSH_HUMAN = 35;      // default interval between buffer flush when typing
    private const short TIME_FLUSH_HOTKEY = 500;    // extended buffer flush interval when expecting another chord key
    private const short TIME_NEXT = 500;            // default interval before submitting output keys used in combo
    private const short TIME_LONG = 600;            // default interval between two inputs from a chord
                                                    // default interval before considering a chord as hold
    private const short TIME_EXPIRED = 2000;        // default interval before considering a chord as expired if no input is detected

    private const uint LLKHF_INJECTED = 0x00000010;
    private const uint LLKHF_LOWER_IL_INJECTED = 0x00000002;

    // Gamepad variables
    private static readonly PrecisionTimer BufferFlushTimer;
    private static readonly Timer ListenerTimer;
    private static readonly Timer InputsChordHoldTimer;

    private static ButtonState prevState = new();

    // InputsChord variables
    private static InputsChord currentChord = new();
    private static InputsChord successChord = new();
    private static InputsChord bufferChord = new();

    // Hotkey variables
    public static bool IsListening = false;
    public static bool IsInput = false;

    // Keyboard vars
    private static IKeyboardMouseEvents m_GlobalHook;

    private static readonly Dictionary<bool, List<KeyEventArgsExt>> BufferKeys = new() { { true, new() }, { false, new() } };
    private static readonly List<KeyboardChord> successkeyChords = [];
    private static readonly Dictionary<bool, short> KeyIndexOEM = new() { { true, 0 }, { false, 0 } };
    private static readonly Dictionary<bool, short> KeyIndexHotkey = new() { { true, 0 }, { false, 0 } };
    private static readonly Dictionary<bool, bool> KeyUsed = new() { { true, false }, { false, false } };

    public static bool IsInitialized;

    private static bool IsKeyDown;
    private static bool IsKeyUp;

    /*
     * InputsManager v4
     * Note: Here be dragons. Thou art forewarned
     * Todo: Modify the InputSimulator library to extend its capacities with ModifiedKeyDown and ModifiedKeyUp
     *       https://github.com/GregsStack/InputSimulatorStandard
     */

    static InputsManager()
    {
        BufferFlushTimer = new PrecisionTimer();
        BufferFlushTimer.SetInterval(new Action(ReleaseKeyboardBuffer), TIME_FLUSH, false, 0, TimerMode.OneShot, true);

        InputsChordHoldTimer = new Timer(TIME_LONG)
        {
            AutoReset = false
        };
        InputsChordHoldTimer.Elapsed += (sender, e) => InputsChordHold_Elapsed();

        ListenerTimer = new Timer(TIME_EXPIRED)
        {
            AutoReset = false
        };
        ListenerTimer.Elapsed += (sender, e) => ExpiredListening();
    }

    private static void InputsChordHold_Elapsed()
    {
        // triggered when key is pressed for a long time
        currentChord.chordType = InputsChordType.Long;

        if (CheckForSequence(true, false))
            successChord.chordType = InputsChordType.Long;
    }

    private static bool CheckForSequence(bool IsKeyDown, bool IsKeyUp)
    {
        // skip if empty
        if (currentChord.ButtonState.IsEmpty() && currentChord.KeyState.Count == 0)
            return false;

        if (IsListening)
        {
            // set flag
            bool existsCheck = false;

            List<KeyCode> chord = currentChord.KeyState.Select(key => (KeyCode)key.KeyValue).ToList();
            foreach (KeyboardChord OEMChord in IDevice.GetCurrent().OEMChords)
            {
                // compare ordered enumerable
                List<KeyCode> chord_keys = OEMChord.GetChord(IsKeyDown);
                if (chord_keys.Count == 0)
                    continue;

                existsCheck = chord_keys.All(x => chord.Any(y => x == y));

                if (existsCheck)
                    break;
            }

            if (existsCheck)
                currentChord.KeyState.Clear();
        }

        // stop timers on KeyUp
        if (IsKeyUp)
        {
            InputsChordHoldTimer.Stop();

            // clear buffer(s)
            BufferKeys[true].Clear();
        }

        if (!IsListening)
        {
            // set flag
            bool success = false;

            IEnumerable<Hotkey> hotkeys = HotkeysManager.GetHotkeys().Where(hk => ((hk.inputsChord.ButtonState.Equals(currentChord.ButtonState) && !hk.inputsChord.ButtonState.IsEmpty()) || currentChord.ButtonState[hk.ButtonFlags]) && hk.inputsChord.chordType == currentChord.chordType);
            foreach (Hotkey hotkey in hotkeys)
            {
                switch (currentChord.chordType)
                {
                    case InputsChordType.Click:
                        {
                            if (!hotkey.command.OnKeyDown && IsKeyDown)
                                continue;

                            if (!hotkey.command.OnKeyUp && IsKeyUp)
                                continue;
                        }
                        break;

                    case InputsChordType.Long:
                        {
                            if (IsKeyUp)
                            {
                                if (hotkey.command is KeyboardCommands)
                                    break;
                                else
                                    continue;
                            }
                        }
                        break;
                }

                // execute command
                hotkey.command.Execute(IsKeyDown, IsKeyUp);

                // raise event
                CommandExecuted?.Invoke(hotkey, hotkey.command);

                // set flag
                success = true;
            }

            return success;
        }
        else
        {
            switch(IsKeyDown)
            {
                case true:
                    {
                        if (currentChord.chordType == InputsChordType.Long)
                            StopListening();
                    }
                    break;
                case false:
                    {
                        StopListening();
                    }
                    break;
            }
        }

        return false;
    }

    private static void SetInterval(PrecisionTimer timer, short interval)
    {
        if (timer.GetPeriod() == interval)
            return;

        if (timer.IsRunning())
            timer.Stop();

        timer.SetPeriod(interval);
    }

    private static KeyEventArgsExt prevKeyEvent = new(Keys.None);

    private static void M_GlobalHook_KeyEvent(object? sender, KeyEventArgs e)
    {
        KeyEventArgsExt args = (KeyEventArgsExt)e;

        bool Injected = (args.Flags & LLKHF_INJECTED) > 0;
        bool InjectedLL = (args.Flags & LLKHF_LOWER_IL_INJECTED) > 0;

        if ((Injected || InjectedLL))
            if (IsListening && currentChord.chordTarget != InputsChordTarget.Output)
                return;

        KeyCode hookKey = (KeyCode)args.KeyValue;

        // pause buffer flush timer
        BufferFlushTimer.Stop();

        // set flag
        KeyUsed[args.IsKeyDown] = false;

        if (args.IsKeyUp)
        {
            if (IsListening)
            {
                if (currentChord.chordTarget != InputsChordTarget.Output)
                    CheckForSequence(args.IsKeyDown, args.IsKeyUp);
            }

            foreach (KeyboardChord? chord in successkeyChords.ToList())
            {
                foreach (KeyCode keyCode in chord.chords[args.IsKeyUp])
                    BufferKeys[args.IsKeyDown].Add(new KeyEventArgsExt((Keys)keyCode, args.ScanCode, args.Timestamp, args.IsKeyDown, args.IsKeyUp, false, args.Flags));

                // calls current controller (if connected)
                IController controller = ControllerManager.GetTargetController();
                controller?.InjectState(chord.state, args.IsKeyDown, args.IsKeyUp);

                // remove chord
                successkeyChords.Remove(chord);
            }
        }
        else if (args.IsKeyDown)
        {
            // check if key is already stored, skip rest of the code
            if (BufferKeys[args.IsKeyDown].Any(key => key.KeyValue == args.KeyValue && key.IsKeyUp == args.IsKeyUp && key.IsKeyDown == args.IsKeyDown))
            {
                // check if key is used in a chord, suppress it
                if (successkeyChords.Any(chord => chord.chords[args.IsKeyDown].Contains((KeyCode)args.KeyCode)))
                    args.SuppressKeyPress = true;

                goto Done;
            }
        }

        // check if we're listening for keyboard inputs
        if (IsListening)
        {
            args.SuppressKeyPress = true;

            /*
            if (currentChord.HasKey(args))
                return;
            */

            if (prevKeyEvent.KeyCode == args.KeyCode && prevKeyEvent.IsKeyDown == args.IsKeyDown)
                return;

            // reset hold timer
            InputsChordHoldTimer.Stop();
            InputsChordHoldTimer.Start();

            // reset listener timer
            ListenerTimer.Stop();
            ListenerTimer.Start();

            // update previous key
            prevKeyEvent = args;

            // add key to chord
            currentChord.AddKey(args);
        }

        if (args.IsKeyDown)
        {
            // check if key is used by OEM chords
            foreach (KeyboardChord? pair in IDevice.GetCurrent().OEMChords.Where(a => !a.silenced))
            {
                List<KeyCode> chord = pair.chords[args.IsKeyDown];
                if (KeyIndexOEM[args.IsKeyDown] >= chord.Count)
                    continue;

                KeyCode chordKey = chord[KeyIndexOEM[args.IsKeyDown]];
                if (chordKey == hookKey)
                {
                    KeyUsed[args.IsKeyDown] = true;
                    KeyIndexOEM[args.IsKeyDown]++;

                    // increase interval as we're expecting a new chord key
                    SetInterval(BufferFlushTimer, TIME_FLUSH_EXTENDED);

                    break; // leave loop
                }

                // restore default interval
                SetInterval(BufferFlushTimer, TIME_FLUSH);
            }

            // check if key is used by hotkey chords
            foreach (Hotkey hotkey in HotkeysManager.GetHotkeys())
            {
                KeyboardChord pair = hotkey.keyChord;

                List<KeyCode> chord = pair.chords[args.IsKeyDown];
                if (KeyIndexHotkey[args.IsKeyDown] >= chord.Count)
                    continue;

                KeyCode chordKey = chord[KeyIndexHotkey[args.IsKeyDown]];
                if (chordKey == hookKey)
                {
                    KeyUsed[args.IsKeyDown] = true;
                    KeyIndexHotkey[args.IsKeyDown]++;

                    // increase interval as we're expecting a new chord key
                    SetInterval(BufferFlushTimer, TIME_FLUSH_HOTKEY);

                    break; // leave loop
                }

                // restore default human interval (31ms)
                SetInterval(BufferFlushTimer, TIME_FLUSH_HUMAN);
            }

            // if key is used or previous key was, we need to maintain key(s) order
            if (KeyUsed[args.IsKeyDown] || KeyIndexOEM[args.IsKeyDown] > 0 || KeyIndexHotkey[args.IsKeyDown] > 0)
            {
                args.SuppressKeyPress = true;

                // add key to buffer
                BufferKeys[args.IsKeyDown].Add(args);

                // prepare list
                List<KeyCode> buffer_keys = GetChord(BufferKeys[args.IsKeyDown]);

                // check if we have matching OEM chords
                if (KeyIndexOEM[args.IsKeyDown] != 0)
                {
                    foreach (KeyboardChord? chord in IDevice.GetCurrent().OEMChords.Where(a => a.chords[args.IsKeyDown].Count == buffer_keys.Count))
                    {
                        // compare ordered enumerable
                        List<KeyCode> chord_keys = chord.GetChord(args.IsKeyDown);
                        if (chord_keys.Count == 0)
                            continue;

                        bool existsCheck = chord_keys.All(x => buffer_keys.Any(y => x == y));
                        if (existsCheck)
                        {
                            // reset index
                            KeyIndexOEM[args.IsKeyDown] = 0;

                            // store successful hotkey
                            successkeyChords.Add(chord);

                            // clear buffer
                            BufferKeys[args.IsKeyDown].Clear();

                            // calls current controller (if connected)
                            IController controller = ControllerManager.GetTargetController();
                            controller?.InjectState(chord.state, args.IsKeyDown, args.IsKeyUp);

                            return;
                        }
                    }
                }

                // check if we have matching hotkey chords
                if (KeyIndexHotkey[args.IsKeyDown] != 0)
                {
                    if (args.IsKeyDown)
                    {
                        foreach (Hotkey hotkey in HotkeysManager.GetHotkeys().Where(h => h.keyChord.chords[args.IsKeyDown].Count == buffer_keys.Count))
                        {
                            KeyboardChord? chord = hotkey.keyChord;

                            // compare ordered enumerable
                            List<KeyCode> chord_keys = hotkey.keyChord.GetChord(args.IsKeyDown);
                            if (chord_keys.Count == 0)
                                continue;

                            bool existsCheck = chord_keys.All(x => buffer_keys.Any(y => x == y));
                            if (existsCheck)
                            {
                                // reset index
                                KeyIndexHotkey[args.IsKeyDown] = 0;

                                // store successful hotkey
                                successkeyChords.Add(chord);

                                IController controller = ControllerManager.GetTargetController();
                                controller?.InjectState(chord.state, args.IsKeyDown, args.IsKeyUp);

                                return;
                            }
                        }
                    }
                }
            }
            else
            {
                // manage AltGr
                if (args.IsKeyUp)
                {
                    switch (args.KeyValue)
                    {
                        case 165:
                            KeyboardSimulator.KeyUp((VirtualKeyCode)162);
                            break;
                    }
                }
            }
        }

    Done:
        BufferFlushTimer.Start();
    }

    private static void ReleaseKeyboardBuffer()
    {
        // Checking if we're missing KeyUp(s)
        List<KeyEventArgsExt> pressedKeys = BufferKeys[true];
        List<KeyEventArgsExt> releasedKeys = BufferKeys[false];

        // Find keys that were pressed but not released
        List<KeyEventArgsExt> pressedButNotReleased = pressedKeys
            .Where(pressed => !releasedKeys.Any(released => released.KeyValue == pressed.KeyValue))
            .OrderBy(pressed => pressed.Timestamp)
            .ToList();

        // Add missing keys
        // This may have side effects, but they will certainly be much less harmful than a stuck key
        BufferKeys[false].AddRange(pressedButNotReleased);

        // Send all key inputs
        foreach (bool IsKeyDown in new[] { true, false })
        {
            if (BufferKeys[IsKeyDown].Count == 0)
                continue;

            // reset index
            KeyIndexOEM[IsKeyDown] = 0;
            KeyIndexHotkey[IsKeyDown] = 0;

            List<KeyEventArgsExt> keys = BufferKeys[IsKeyDown].OrderBy(a => a.Timestamp).ToList();
            for (int i = 0; i < keys.Count; i++)
            {
                KeyEventArgsExt args = keys[i];
                switch (IsKeyDown)
                {
                    case true:
                        KeyboardSimulator.KeyDown(args);
                        break;
                    case false:
                        KeyboardSimulator.KeyUp(args);
                        break;
                }
            }

            // clear buffer
            BufferKeys[IsKeyDown].Clear();
        }
    }

    private static List<KeyCode> GetChord(List<KeyEventArgsExt> args)
    {
        return args.Select(a => (KeyCode)a.KeyValue).OrderBy(key => key).ToList();
    }

    public static void Start()
    {
        InitGlobalHook();

        ControllerManager.InputsUpdated += UpdateInputs;

        IsInitialized = true;
        Initialized?.Invoke();

        LogManager.LogInformation("{0} has started", "InputsManager");
    }

    public static void Stop()
    {
        if (!IsInitialized)
            return;

        ControllerManager.InputsUpdated -= UpdateInputs;

        IsInitialized = false;

        DisposeGlobalHook();

        LogManager.LogInformation("{0} has stopped", "InputsManager");
    }

    private static void InitGlobalHook()
    {
        if (m_GlobalHook is not null)
            return;

        m_GlobalHook = Hook.GlobalEvents();
        m_GlobalHook.KeyDown += M_GlobalHook_KeyEvent;
        m_GlobalHook.KeyUp += M_GlobalHook_KeyEvent;
    }

    private static void DisposeGlobalHook()
    {
        if (m_GlobalHook is null)
            return;

        m_GlobalHook.KeyDown -= M_GlobalHook_KeyEvent;
        m_GlobalHook.KeyUp -= M_GlobalHook_KeyEvent;
        m_GlobalHook.Dispose();
        m_GlobalHook = null;
    }

    private static void UpdateInputs(ControllerState controllerState)
    {
        // prepare button state
        ButtonState buttonState = controllerState.ButtonState.Clone() as ButtonState;

        if (prevState.Equals(buttonState))
            return;

        // half-press should be removed if full-press is also present
        RemoveHalfPressIfFullPress(buttonState, ButtonFlags.L2Full,             ButtonFlags.L2Soft);
        RemoveHalfPressIfFullPress(buttonState, ButtonFlags.R2Full,             ButtonFlags.R2Soft);
        RemoveHalfPressIfFullPress(buttonState, ButtonFlags.LeftStickClick,     ButtonFlags.LeftStickTouch);
        RemoveHalfPressIfFullPress(buttonState, ButtonFlags.RightStickClick,    ButtonFlags.RightStickTouch);
        RemoveHalfPressIfFullPress(buttonState, ButtonFlags.LeftPadClick,       ButtonFlags.LeftPadTouch);
        RemoveHalfPressIfFullPress(buttonState, ButtonFlags.RightPadClick,      ButtonFlags.RightPadTouch);

        // reset hold timer
        InputsChordHoldTimer.Stop();
        InputsChordHoldTimer.Start();

        // reset listener timer
        if (IsListening)
        {
            ListenerTimer.Stop();
            ListenerTimer.Start();
        }

        // IsKeyDown
        if (!buttonState.IsEmpty())
        {
            if (!successChord.ButtonState.IsEmpty())
            {
                if (!buttonState.Contains(successChord.ButtonState))
                {
                    IsKeyDown = false;
                    IsKeyUp = true;
                    currentChord.chordType = successChord.chordType;

                    goto Done;
                }
            }

            bufferChord.ButtonState.AddRange(buttonState);

            currentChord.chordType = InputsChordType.Click;

            IsKeyDown = true;
            IsKeyUp = false;
        }
        // IsKeyUp
        else if (IsKeyDown && !currentChord.ButtonState.Equals(buttonState))
        {
            IsKeyDown = false;
            IsKeyUp = true;

            if (!successChord.ButtonState.IsEmpty())
            {
                currentChord.chordType = successChord.chordType;
            }
        }

    Done:
        currentChord.ButtonState = bufferChord.ButtonState.Clone() as ButtonState;

        if (CheckForSequence(IsKeyDown, IsKeyUp))
        {
            successChord = new()
            {
                ButtonState = currentChord.ButtonState.Clone() as ButtonState,
                chordType = currentChord.chordType
            };
        }

        if ((buttonState.IsEmpty() || !successChord.ButtonState.IsEmpty()) && IsKeyUp)
        {
            currentChord.ButtonState.Clear();
            bufferChord.ButtonState.Clear();
            successChord.ButtonState.Clear();
        }

        prevState = buttonState.Clone() as ButtonState;

        // GamepadResetTimer.Start();
    }

    private static void RemoveHalfPressIfFullPress(ButtonState buttonState, ButtonFlags fullPress, ButtonFlags halfPress)
    {
        if (currentChord.ButtonState[fullPress])
        {
            currentChord.ButtonState[halfPress] = false;
            bufferChord.ButtonState[halfPress] = false;
            buttonState[halfPress] = false;
        }
    }

    private static ButtonFlags currentButtonFlags = ButtonFlags.None;
    public static void StartListening(ButtonFlags buttonFlags, InputsChordTarget chordTarget)
    {
        // force expiration on previous listener, if any
        if (IsListening)
            StopListening();

        // set flag
        IsListening = true;

        // set variable
        currentButtonFlags = buttonFlags;
        prevKeyEvent = new(Keys.None);

        // reset chords
        currentChord = new()
        {
            chordType = InputsChordType.Click,
            chordTarget = chordTarget,
        };
        successChord = new();
        bufferChord = new();

        // raise event
        StartedListening?.Invoke(buttonFlags, chordTarget);

        ListenerTimer.Start();
    }

    public static void StopListening()
    {
        // set flag
        IsListening = false;

        ListenerTimer.Stop();

        // the below logic is here to make sure every KeyDown has an equivalent KeyUp
        List<InputsKey> missingOutputs = [];

        foreach (InputsKey inputsKey in currentChord.KeyState.Where(k => k.IsKeyDown))
        {
            bool hasUp = currentChord.KeyState.Any(k => k.KeyValue == inputsKey.KeyValue && k.IsKeyUp);
            if (!hasUp)
            {
                missingOutputs.Add(new()
                {
                    KeyValue = inputsKey.KeyValue,
                    ScanCode = inputsKey.ScanCode,
                    IsExtendedKey = inputsKey.IsExtendedKey,
                    IsKeyDown = false,
                    IsKeyUp = true,
                    Timestamp = inputsKey.Timestamp,
                });
            }
        }

        // invert order to make sure key are released in right order
        missingOutputs.Reverse();
        foreach (InputsKey inputsKey in missingOutputs)
            currentChord.KeyState.Add(inputsKey);

        // check if we contain OEM buttons
        foreach (ButtonFlags buttonFlags in IDevice.GetCurrent().OEMButtons)
            if (currentChord.ButtonState.Buttons.Contains(buttonFlags))
                currentChord.KeyState.Clear();

        // remove hotkey chord
        currentChord.ButtonState[currentButtonFlags] = false;

        // raise event
        StoppedListening?.Invoke(currentButtonFlags, currentChord);

        // reset variable
        currentButtonFlags = ButtonFlags.None;
        prevKeyEvent = new(Keys.None);

        // reset chords
        currentChord = new();
        successChord = new();
        bufferChord = new();
    }

    private static void ExpiredListening()
    {
        StopListening();
    }
}