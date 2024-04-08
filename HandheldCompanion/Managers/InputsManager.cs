using Gma.System.MouseKeyHook;
using GregsStack.InputSimulatorStandard.Native;
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
using static HandheldCompanion.Managers.InputsHotkey;
using Application = System.Windows.Application;
using ButtonState = HandheldCompanion.Inputs.ButtonState;
using Timer = System.Timers.Timer;

namespace HandheldCompanion.Managers;

public static class InputsManager
{
    public delegate void InitializedEventHandler();

    public delegate void TriggerRaisedEventHandler(string listener, InputsChord inputs, InputsHotkeyType type,
        bool IsKeyDown, bool IsKeyUp);

    public delegate void TriggerUpdatedEventHandler(string listener, InputsChord inputs, ListenerType type);

    public enum ListenerType
    {
        Default,
        Output,
        UI
    }

    private const short TIME_FLUSH = 5; // default interval between buffer flush
    private const short TIME_SPAM = 50; // default interval between two allowed inputs
    private const short TIME_FLUSH_EXTENDED = 150; // extended buffer flush interval when expecting another chord key

    private const short TIME_NEXT = 500; // default interval before submitting output keys used in combo

    private const short TIME_LONG = 600; // default interval between two inputs from a chord
    // default interval before considering a chord as hold

    private const short
        TIME_EXPIRED = 3000; // default interval before considering a chord as expired if no input is detected

    private const uint LLKHF_INJECTED = 0x00000010;
    private const uint LLKHF_LOWER_IL_INJECTED = 0x00000002;

    // Gamepad variables
    private static readonly PrecisionTimer KeyboardResetTimer;

    private static ButtonState prevState = new();

    // InputsChord variables
    private static InputsChord currentChord = new();
    private static InputsChord prevChord = new();
    private static InputsChord successChord = new();
    private static readonly InputsChord storedChord = new();
    private static string SpecialKey;

    private static readonly Timer InputsChordHoldTimer;
    private static readonly Timer InputsChordInputTimer;

    private static readonly Dictionary<KeyValuePair<KeyCode, bool>, int> prevKeys = new();

    // Global variables
    private static readonly Timer ListenerTimer;

    private static ListenerType currentType;
    private static InputsHotkey currentHotkey = new();

    private static readonly List<KeyEventArgsExt> BufferKeys = new();

    private static readonly Dictionary<string, InputsChord> Triggers = new();

    // Keyboard vars
    private static IKeyboardMouseEvents m_GlobalHook;

    private static short KeyIndex;
    private static bool KeyUsed;

    public static bool IsInitialized;

    private static bool IsKeyDown;
    private static bool IsKeyUp;

    /*
     * InputsManager v3
     * Note: I'd like to modify the InputSimulator library to extend its capacities and ModifiedKeyDown and ModifiedKeyUp
     *       https://github.com/GregsStack/InputSimulatorStandard
     */

    static InputsManager()
    {
        KeyboardResetTimer = new PrecisionTimer();
        KeyboardResetTimer.SetInterval(new Action(ReleaseKeyboardBuffer), TIME_FLUSH, false, 0, TimerMode.OneShot, true);

        ListenerTimer = new Timer(TIME_EXPIRED);
        ListenerTimer.AutoReset = false;
        ListenerTimer.Elapsed += (sender, e) => ListenerExpired();

        InputsChordHoldTimer = new Timer(TIME_LONG);
        InputsChordHoldTimer.AutoReset = false;
        InputsChordHoldTimer.Elapsed += (sender, e) => InputsChordHold_Elapsed();

        InputsChordInputTimer = new Timer(TIME_NEXT);
        InputsChordInputTimer.AutoReset = false;
        InputsChordInputTimer.Elapsed += (sender, e) => InputsChordInput_Elapsed();

        HotkeysManager.HotkeyCreated += TriggerCreated;
    }

    public static event TriggerRaisedEventHandler TriggerRaised;

    public static event TriggerUpdatedEventHandler TriggerUpdated;

    public static event InitializedEventHandler Initialized;

    private static void InputsChordHold_Elapsed()
    {
        // triggered when key is pressed for a long time
        currentChord.InputsType = InputsChordType.Long;
        if (CheckForSequence(true, false))
            successChord.InputsType = InputsChordType.Long;
    }

    private static void InputsChordInput_Elapsed()
    {
        // triggered after a key has been pressed (used by combo exclusively)
        CheckForSequence(false, true);
    }

    private static bool CheckForSequence(bool IsKeyDown, bool IsKeyUp)
    {
        if (currentChord.State.IsEmpty() &&
            currentChord.OutputKeys.Count == 0)
            return false;

        // stop timers on KeyUp
        if (IsKeyUp)
        {
            InputsChordHoldTimer.Stop();
            InputsChordInputTimer.Stop();
        }

        if (!IsListening)
        {
            List<string> keys = GetTriggersFromChord(currentChord);
            if (keys.Count != 0)
            {
                LogManager.LogDebug("Captured: Buttons: {0}, Type: {1}, IsKeyDown: {2}", string.Join(',', currentChord.State.Buttons),
                    currentChord.InputsType, IsKeyDown);

                foreach (string key in keys)
                {
                    InputsHotkey? hotkey = InputsHotkeys.Values.FirstOrDefault(item => item.Listener == key);
                    if (hotkey is null)
                        continue;

                    // special case HIDmode switch hotkey
                    // required here in order to (immediatly) disable hotkey while HIDmode is being changed to avoid duplicates
                    // this takes care of repeated keybinds actions
                    if (hotkey.Listener == "shortcutChangeHIDMode")
                    {
                        InputsChordType inputType = currentChord.InputsType;
                        if ((inputType == InputsChordType.Click && IsKeyUp) || (inputType == InputsChordType.Long && IsKeyDown))
                        {
                            List<Hotkey> hidHotkeys = HotkeysManager.Hotkeys.Values.Where(item => item.inputsHotkey.Listener.Equals("shortcutChangeHIDMode")).ToList();

                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                foreach (Hotkey hidHotkey in hidHotkeys)
                                {
                                    if (!hidHotkey.IsEnabled)
                                        continue;

                                    hidHotkey.IsEnabled = false;
                                }
                            });
                        }
                    }

                    InputsChord chord = Triggers[key];
                    switch (chord.InputsType)
                    {
                        case InputsChordType.Click:
                            {
                                if (!hotkey.OnKeyDown && IsKeyDown)
                                    continue;

                                if (!hotkey.OnKeyUp && IsKeyUp)
                                    continue;
                            }
                            break;

                        case InputsChordType.Long:
                            {
                                if (IsKeyUp)
                                {
                                    switch (hotkey.hotkeyType)
                                    {
                                        // we should always allow custom hotkeys (keyboard keys) to be released
                                        case InputsHotkeyType.Custom:
                                            break;
                                        default:
                                            continue;
                                    }
                                }
                            }
                            break;
                    }

                    TriggerRaised?.Invoke(key, chord, hotkey.hotkeyType, IsKeyDown, IsKeyUp);
                }

                return true;
            }

            // get the associated keys
            foreach (DeviceChord? chord in IDevice.GetCurrent().OEMChords.Where(a => currentChord.State.Contains(a.state)))
            {
                // it could be the currentChord isn't mapped but a InputsChordType.Long is
                currentChord.InputsType = InputsChordType.Long;
                keys = GetTriggersFromChord(currentChord);
                if (keys.Count != 0)
                    return false;

                var layout = LayoutManager.GetCurrent();
                if (layout is not null)
                    foreach (var button in chord.state.Buttons)
                        if (layout.ButtonLayout.ContainsKey(button))
                            return false;

                var chords = chord.chords[IsKeyDown];
                LogManager.LogDebug("Released: KeyCodes: {0}, IsKeyDown: {1}", string.Join(',', chords), IsKeyDown);

                if (IsKeyDown)
                {
                    KeyboardSimulator.KeyDown(chords.ToArray());

                    // stop hold timer
                    InputsChordHoldTimer.Stop();
                }

                // else if (IsKeyUp)
                KeyboardSimulator.KeyUp(chords.ToArray());

                return true;
            }
        }
        else
        {
            if (IsKeyDown)
                switch (currentChord.InputsType)
                {
                    case InputsChordType.Click:
                        return false;
                }

            StopListening(currentChord);
        }

        return false;
    }

    private static List<KeyEventArgsExt> InjectModifiers(KeyEventArgsExt args)
    {
        List<KeyEventArgsExt> mods = new();

        if (args.Modifiers == Keys.None)
            return mods;

        foreach (var mode in ((Keys[])Enum.GetValues(typeof(Keys))).Where(a => a != Keys.None))
            if (args.Modifiers.HasFlag(mode))
            {
                var mod = new KeyEventArgsExt(mode, args.ScanCode, args.Timestamp, args.IsKeyDown, args.IsKeyUp, true,
                    args.Flags);
                mods.Add(mod);
            }

        return mods;
    }

    private static void SetInterval(PrecisionTimer timer, short interval)
    {
        if (timer.GetPeriod() == interval)
            return;

        if (timer.IsRunning())
            timer.Stop();

        timer.SetPeriod(interval);
    }

    private static void M_GlobalHook_KeyEvent(object? sender, KeyEventArgs e)
    {
        KeyEventArgsExt args = (KeyEventArgsExt)e;

        bool Injected = (args.Flags & LLKHF_INJECTED) > 0;
        bool InjectedLL = (args.Flags & LLKHF_LOWER_IL_INJECTED) > 0;

        if ((Injected || InjectedLL) && currentType != ListenerType.Output)
            return;

        KeyCode hookKey = (KeyCode)args.KeyValue;

        KeyboardResetTimer.Stop();
        KeyUsed = false;

        // are we listening for keyboards inputs as part of a custom hotkey ?
        if (currentType == ListenerType.Output)
        {
            args.SuppressKeyPress = true;

            // add key to InputsChord
            currentChord.AddKey(args);

            InputsChordInputTimer.Stop();
            InputsChordInputTimer.Start();

            return;
        }

        foreach (DeviceChord? pair in IDevice.GetCurrent().OEMChords.Where(a => !a.silenced))
        {
            List<KeyCode> chord = pair.chords[args.IsKeyDown];
            if (KeyIndex >= chord.Count)
                continue;

            // simplified process for single key chords
            if (chord.Count == 1)
            {
                if (chord[0] == hookKey)
                {
                    // calls current controller (if connected)
                    IController controller = ControllerManager.GetTargetController();
                    controller?.InjectState(pair.state, args.IsKeyDown, args.IsKeyUp);
                    return;
                }
            }

            KeyCode chordKey = chord[KeyIndex];
            if (chordKey == hookKey)
            {
                KeyUsed = true;
                KeyIndex++;

                // increase interval as we're expecting a new chord key
                SetInterval(KeyboardResetTimer, TIME_FLUSH_EXTENDED);

                break; // leave loop
            }

            // restore default interval
            SetInterval(KeyboardResetTimer, TIME_FLUSH);
        }

        // if key is used or previous key was, we need to maintain key(s) order
        if (KeyUsed || KeyIndex > 0)
        {
            args.SuppressKeyPress = true;

            // add key to buffer
            BufferKeys.Add(args);

            // search for matching triggers
            List<KeyCode> buffer_keys = GetChord(BufferKeys);

            foreach (DeviceChord? chord in IDevice.GetCurrent().OEMChords.Where(a =>
                         a.chords[args.IsKeyDown].Count == BufferKeys.Count))
            {
                // compare ordered enumerable
                List<KeyCode> chord_keys = chord.GetChord(args.IsKeyDown);

                bool existsCheck = chord_keys.All(x => buffer_keys.Any(y => x == y));
                if (existsCheck)
                {
                    // reset index
                    KeyIndex = 0;

                    // check if inputs timestamp are too close from one to another
                    bool IsKeyUnexpected = args.IsKeyUp && string.IsNullOrEmpty(SpecialKey);

                    // do not bother checking timing if key is already unexpected
                    if (!IsKeyUnexpected)
                    {
                        KeyValuePair<KeyCode, bool> pair = new KeyValuePair<KeyCode, bool>(hookKey, args.IsKeyDown);
                        int prevTimestamp = prevKeys.TryGetValue(pair, out var key) ? key : TIME_SPAM;
                        prevKeys[pair] = args.Timestamp;

                        // spamming
                        if (args.Timestamp - prevTimestamp < TIME_SPAM)
                            IsKeyUnexpected = true;
                    }

                    // clear buffer
                    BufferKeys.Clear();

                    // leave if inputs are too close
                    if (IsKeyUnexpected)
                        return;

                    // calls current controller (if connected)
                    IController controller = ControllerManager.GetTargetController();
                    controller?.InjectState(chord.state, args.IsKeyDown, args.IsKeyUp);

                    if (args.IsKeyDown)
                        SpecialKey = chord.name;
                    else if (args.IsKeyUp)
                        SpecialKey = string.Empty;

                    return;
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

        KeyboardResetTimer.Start();
    }

    private static List<string> GetTriggersFromChord(InputsChord lookup)
    {
        List<string> keys = new();

        foreach (var pair in Triggers)
        {
            var key = pair.Key;
            var chord = pair.Value;

            var InputsType = chord.InputsType;
            var State = chord.State;

            if (InputsType.HasFlag(lookup.InputsType) && State.Buttons.Count() != 0 && lookup.State.Equals(State))
                keys.Add(key);
        }

        return keys;
    }

    private static void ReleaseKeyboardBuffer()
    {
        if (BufferKeys.Count == 0)
            return;

        // reset index
        KeyIndex = 0;

        var keys = BufferKeys.OrderBy(a => a.Timestamp).ToList();
        for (var i = 0; i < keys.Count; i++)
        {
            var args = keys[i];

            switch (args.IsKeyDown)
            {
                case true:
                    KeyboardSimulator.KeyDown(args);
                    break;
                case false:
                    KeyboardSimulator.KeyUp(args);
                    break;
            }

            // clear buffer
            BufferKeys.Remove(args);
        }

        // clear buffer
        BufferKeys.Clear();
    }

    private static List<KeyCode> GetChord(List<KeyEventArgsExt> args)
    {
        return args.Select(a => (KeyCode)a.KeyValue).OrderBy(key => key).ToList();
    }

    public static void Start()
    {
        if (IDevice.GetCurrent().HasKey())
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

        // half-press should be removed if full-press is also present
        if (currentChord.State[ButtonFlags.L2Full])
        {
            currentChord.State[ButtonFlags.L2Soft] = false;
            storedChord.State[ButtonFlags.L2Soft] = false;
            buttonState[ButtonFlags.L2Soft] = false;
        }

        if (currentChord.State[ButtonFlags.R2Full])
        {
            currentChord.State[ButtonFlags.R2Soft] = false;
            storedChord.State[ButtonFlags.R2Soft] = false;
            buttonState[ButtonFlags.R2Soft] = false;
        }

        if (currentChord.State[ButtonFlags.LeftStickClick])
        {
            currentChord.State[ButtonFlags.LeftStickTouch] = false;
            storedChord.State[ButtonFlags.LeftStickTouch] = false;
            buttonState[ButtonFlags.LeftStickTouch] = false;
        }

        if (currentChord.State[ButtonFlags.RightStickClick])
        {
            currentChord.State[ButtonFlags.RightStickTouch] = false;
            storedChord.State[ButtonFlags.RightStickTouch] = false;
            buttonState[ButtonFlags.RightStickTouch] = false;
        }

        if (currentChord.State[ButtonFlags.LeftPadClick])
        {
            currentChord.State[ButtonFlags.LeftPadTouch] = false;
            storedChord.State[ButtonFlags.LeftPadTouch] = false;
            buttonState[ButtonFlags.LeftPadTouch] = false;
        }

        if (currentChord.State[ButtonFlags.RightPadClick])
        {
            currentChord.State[ButtonFlags.RightPadTouch] = false;
            storedChord.State[ButtonFlags.RightPadTouch] = false;
            buttonState[ButtonFlags.RightPadTouch] = false;
        }

        if (prevState.Equals(buttonState))
            return;

        // reset hold timer
        InputsChordHoldTimer.Stop();
        InputsChordHoldTimer.Start();

        // IsKeyDown
        if (!buttonState.IsEmpty())
        {
            if (!successChord.State.IsEmpty())
            {
                if (!buttonState.Contains(successChord.State))
                {
                    IsKeyDown = false;
                    IsKeyUp = true;
                    currentChord.InputsType = successChord.InputsType;

                    goto Done;
                }
            }

            storedChord.State.AddRange(buttonState);

            currentChord.InputsType = InputsChordType.Click;

            IsKeyDown = true;
            IsKeyUp = false;
        }
        // IsKeyUp
        else if (IsKeyDown && !currentChord.State.Equals(buttonState))
        {
            IsKeyDown = false;
            IsKeyUp = true;

            if (!successChord.State.IsEmpty())
                currentChord.InputsType = successChord.InputsType;
        }

    Done:
        currentChord.State = storedChord.State.Clone() as ButtonState;

        if (CheckForSequence(IsKeyDown, IsKeyUp))
        {
            successChord = new();
            successChord.State = currentChord.State.Clone() as ButtonState;
            successChord.InputsType = currentChord.InputsType;
        }

        if ((buttonState.IsEmpty() || !successChord.State.IsEmpty()) && IsKeyUp)
        {
            currentChord.State.Clear();
            storedChord.State.Clear();
            successChord.State.Clear();
        }

        prevState = buttonState.Clone() as ButtonState;

        // GamepadResetTimer.Start();
    }

    public static bool IsListening => !string.IsNullOrEmpty(currentHotkey.Listener);

    public static void StartListening(Hotkey hotkey, ListenerType type)
    {
        if (!IDevice.GetCurrent().HasKey())
            InitGlobalHook();

        // force expiration on previous listener, if any
        if (IsListening)
            ListenerExpired();

        // store current hotkey values
        prevChord = new InputsChord(hotkey.inputsChord.State, hotkey.inputsChord.OutputKeys,
            hotkey.inputsChord.InputsType);

        currentHotkey = hotkey.inputsHotkey;
        currentChord = hotkey.inputsChord;
        currentType = type;

        switch (type)
        {
            case ListenerType.Output:
                currentChord.OutputKeys.Clear();
                break;
            default:
            case ListenerType.UI:
            case ListenerType.Default:
                currentChord.State.Clear();
                break;
        }

        BufferKeys.Clear();

        ListenerTimer.Start();
    }

    private static void StopListening(InputsChord inputsChord = null)
    {
        if (!IDevice.GetCurrent().HasKey())
            DisposeGlobalHook();

        if (inputsChord is null)
            inputsChord = new InputsChord();

        // the below logic is here to make sure every KeyDown has an equivalent KeyUp
        List<OutputKey> missingOutputs = new List<OutputKey>();
        foreach (OutputKey key in inputsChord.OutputKeys.Where(k => k.IsKeyDown))
        {
            bool hasUp = inputsChord.OutputKeys.Any(k => k.KeyValue == key.KeyValue && k.IsKeyUp);
            if (!hasUp)
            {
                missingOutputs.Add(new()
                {
                    KeyValue = key.KeyValue,
                    ScanCode = key.ScanCode,
                    IsExtendedKey = key.IsExtendedKey,
                    IsKeyDown = false,
                    IsKeyUp = true,
                    Timestamp = key.Timestamp,
                });
            }
        }

        // invert order to make sure key are released in right order
        missingOutputs.Reverse();
        foreach (OutputKey key in missingOutputs)
            inputsChord.OutputKeys.Add(key);

        switch (currentType)
        {
            case ListenerType.Default:
            case ListenerType.Output:
            case ListenerType.UI:
                Triggers[currentHotkey.Listener] =
                    new InputsChord(inputsChord.State, inputsChord.OutputKeys, inputsChord.InputsType);
                break;
        }

        TriggerUpdated?.Invoke(currentHotkey.Listener, inputsChord, currentType);

        LogManager.LogDebug("Trigger: {0} updated. buttons: {1}, type: {2}", currentHotkey.Listener,
            string.Join(",", inputsChord.State.Buttons), inputsChord.InputsType);

        currentHotkey = new InputsHotkey();
        currentChord = new InputsChord();
        currentType = ListenerType.Default;

        ListenerTimer.Stop();
        InputsChordHoldTimer.Stop();
        InputsChordInputTimer.Stop();
    }

    private static void ListenerExpired()
    {
        // restore previous chord
        StopListening(prevChord);
    }

    public static void ClearListening(Hotkey hotkey)
    {
        currentHotkey = hotkey.inputsHotkey;
        StopListening();
    }

    private static void TriggerCreated(Hotkey hotkey)
    {
        var listener = hotkey.inputsHotkey.Listener;

        Triggers.TryAdd(listener, hotkey.inputsChord);
    }

    internal static void InvokeTrigger(Hotkey hotkey, bool IsKeyDown, bool IsKeyUp)
    {
        if (IsKeyDown && hotkey.inputsHotkey.OnKeyDown)
            TriggerRaised?.Invoke(hotkey.inputsHotkey.Listener, hotkey.inputsChord, hotkey.inputsHotkey.hotkeyType,
                true, false);

        if (IsKeyUp && hotkey.inputsHotkey.OnKeyUp)
            TriggerRaised?.Invoke(hotkey.inputsHotkey.Listener, hotkey.inputsChord, hotkey.inputsHotkey.hotkeyType,
                false, true);
    }
}