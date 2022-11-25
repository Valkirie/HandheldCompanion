using ControllerCommon.Controllers;
using ControllerCommon.Devices;
using ControllerCommon.Managers;
using Gma.System.MouseKeyHook;
using GregsStack.InputSimulatorStandard;
using GregsStack.InputSimulatorStandard.Native;
using HandheldCompanion.Views;
using PrecisionTiming;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using WindowsInput.Events;

namespace HandheldCompanion.Managers
{
    public static class InputsManager
    {
        public enum ListenerType
        {
            Default,
            Output,
            UI,
        }

        // Gamepad variables
        private static PrecisionTimer KeyboardResetTimer;
        private static PrecisionTimer GamepadResetTimer;

        private static bool GamepadClearPending;
        private static ControllerButtonFlags prevButtons;

        // InputsChord variables
        private static InputsChord currentChord = new();
        private static InputsChord prevChord = new();
        private static string SpecialKey;

        private static PrecisionTimer InputsChordHoldTimer;
        private static PrecisionTimer InputsChordInputTimer;

        private static Dictionary<KeyValuePair<KeyCode, bool>, int> prevKeys = new();

        // Global variables
        private static PrecisionTimer ListenerTimer;

        private const short TIME_FLUSH = 5;             // default interval between buffer flush
        private const short TIME_SPAM = 50;             // default interval between two allowed inputs
        private const short TIME_FLUSH_EXTENDED = 150;  // extended buffer flush interval when expecting another chord key

        private const short TIME_NEXT = 500;            // default interval before submitting output keys used in combo
        private const short TIME_LONG = 600;            // default interval between two inputs from a chord
                                                        // default interval before considering a chord as hold

        private const short TIME_EXPIRED = 3000;        // default interval before considering a chord as expired if no input is detected

        private static ListenerType currentType;
        private static InputsHotkey currentHotkey = new();

        private static List<KeyEventArgsExt> BufferKeys = new();

        private static Dictionary<string, InputsChord> Triggers = new();

        // Keyboard vars
        private static IKeyboardMouseEvents m_GlobalHook;
        private static InputSimulator m_InputSimulator;

        public static event TriggerRaisedEventHandler TriggerRaised;
        public delegate void TriggerRaisedEventHandler(string listener, InputsChord inputs, bool IsKeyDown, bool IsKeyUp);

        public static event TriggerUpdatedEventHandler TriggerUpdated;
        public delegate void TriggerUpdatedEventHandler(string listener, InputsChord inputs, ListenerType type);

        public static event InitializedEventHandler Initialized;
        public delegate void InitializedEventHandler();

        private static short KeyIndex;
        private static bool KeyUsed;

        public static bool IsInitialized;

        /*
         * InputsManager v3
         * Note: I'd like to modify the InputSimulator library to extend its capacities and ModifiedKeyDown and ModifiedKeyUp
         *       https://github.com/GregsStack/InputSimulatorStandard
         */

        static InputsManager()
        {
            KeyboardResetTimer = new PrecisionTimer();
            KeyboardResetTimer.SetInterval(TIME_FLUSH);
            KeyboardResetTimer.SetAutoResetMode(false);
            KeyboardResetTimer.Tick += (sender, e) => ReleaseKeyboardBuffer();

            GamepadResetTimer = new PrecisionTimer();
            GamepadResetTimer.SetInterval(TIME_FLUSH);
            GamepadResetTimer.SetAutoResetMode(false);
            GamepadResetTimer.Tick += (sender, e) => ReleaseGamepadBuffer();

            ListenerTimer = new PrecisionTimer();
            ListenerTimer.SetInterval(TIME_EXPIRED);
            ListenerTimer.SetAutoResetMode(false);
            ListenerTimer.Tick += (sender, e) => ListenerExpired();

            InputsChordHoldTimer = new PrecisionTimer();
            InputsChordHoldTimer.SetInterval(TIME_LONG);
            InputsChordHoldTimer.SetAutoResetMode(false);
            InputsChordHoldTimer.Tick += (sender, e) => InputsChordHold_Elapsed();

            InputsChordInputTimer = new PrecisionTimer();
            InputsChordInputTimer.SetInterval(TIME_NEXT);
            InputsChordInputTimer.SetAutoResetMode(false);
            InputsChordInputTimer.Tick += (sender, e) => InputsChordInput_Elapsed();

            m_GlobalHook = Hook.GlobalEvents();
            m_InputSimulator = new InputSimulator();

            HotkeysManager.HotkeyCreated += TriggerCreated;

            Thread.CurrentThread.Priority = ThreadPriority.Highest;
        }

        private static void InputsChordHold_Elapsed()
        {
            // triggered when key is pressed for a long time
            currentChord.InputsType = InputsChordType.Long;
            CheckForSequence(true, false);
        }

        private static void InputsChordInput_Elapsed()
        {
            // triggered after a key has been pressed (used by combo exclusively)
            CheckForSequence(false, true);
        }

        private static void CheckForSequence(bool IsKeyDown, bool IsKeyUp)
        {
            if (currentChord.GamepadButtons == ControllerButtonFlags.None &&
                currentChord.OutputKeys.Count == 0)
                return;

            // reset index
            KeyIndex = 0;

            // stop timers on KeyUp
            if (IsKeyUp)
            {
                InputsChordHoldTimer.Stop();
                InputsChordInputTimer.Stop();
            }

            if (string.IsNullOrEmpty(currentHotkey.Listener))
            {
                var keys = GetTriggersFromChord(currentChord);

                if (keys.Count != 0)
                {
                    LogManager.LogDebug("Captured: Buttons: {0}, Type: {1}, IsKeyDown: {2}", currentChord.GamepadButtons, currentChord.InputsType, IsKeyDown);

                    foreach (string key in keys)
                    {
                        InputsChord chord = Triggers[key];

                        switch (chord.InputsType)
                        {
                            case InputsChordType.Click:
                                {
                                    InputsHotkey hotkey = InputsHotkey.InputsHotkeys.Values.Where(item => item.Listener == key).FirstOrDefault();

                                    if (!hotkey.OnKeyDown && IsKeyDown)
                                        continue;

                                    if (!hotkey.OnKeyUp && IsKeyUp)
                                        continue;
                                }
                                break;

                            case InputsChordType.Long:
                                {
                                    // skip as we've already executed it
                                    if (IsKeyUp)
                                        continue;
                                }
                                break;

                            case InputsChordType.Hold:
                                {
                                    // skip as we've already executed it
                                    if (IsKeyDown && currentChord.InputsType == InputsChordType.Long)
                                        continue;
                                }
                                break;
                        }

                        TriggerRaised?.Invoke(key, chord, IsKeyDown, IsKeyUp);
                    }
                }
                else
                {
                    DeviceChord chord = MainWindow.handheldDevice.listeners.Where(a => a.button == currentChord.GamepadButtons).FirstOrDefault();
                    if (chord is null)
                        return;

                    List<KeyCode> chords = chord.chords[IsKeyDown];
                    LogManager.LogDebug("Released: KeyCodes: {0}, IsKeyDown: {1}", string.Join(',', chords), IsKeyDown);

                    if (IsKeyDown)
                        SendChordDown(chords);
                    else if (IsKeyUp)
                        SendChordUp(chords);
                }
            }
            else
            {
                if (IsKeyDown)
                {
                    switch (currentChord.InputsType)
                    {
                        case InputsChordType.Click:
                        case InputsChordType.Hold:
                            return;
                    }
                }

                InputsHotkey hotkey = InputsHotkey.InputsHotkeys.Values.Where(item => item.Listener == currentHotkey.Listener).FirstOrDefault();
                if (hotkey != null)
                {
                    switch (hotkey.OnKeyDown)
                    {
                        case true:
                            currentChord.InputsType = InputsChordType.Hold;
                            break;
                    }
                }

                StopListening(currentChord);
            }
        }

        private static List<KeyEventArgsExt> InjectModifiers(KeyEventArgsExt args)
        {
            List<KeyEventArgsExt> mods = new();

            if (args.Modifiers == Keys.None)
                return mods;

            foreach (Keys mode in ((Keys[])Enum.GetValues(typeof(Keys))).Where(a => a != Keys.None))
            {
                if (args.Modifiers.HasFlag(mode))
                {
                    KeyEventArgsExt mod = new KeyEventArgsExt(mode, args.ScanCode, args.Timestamp, args.IsKeyDown, args.IsKeyUp, true, args.Flags);
                    mods.Add(mod);
                }
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

        const uint LLKHF_INJECTED = 0x00000010;
        const uint LLKHF_LOWER_IL_INJECTED = 0x00000002;

        private static void M_GlobalHook_KeyEvent(object? sender, KeyEventArgs e)
        {
            KeyEventArgsExt args = (KeyEventArgsExt)e;

            var Injected = (args.Flags & LLKHF_INJECTED) > 0;
            var InjectedLL = (args.Flags & LLKHF_LOWER_IL_INJECTED) > 0;

            if (Injected || InjectedLL)
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

            foreach (DeviceChord pair in MainWindow.handheldDevice.listeners.Where(a => !a.silenced))
            {
                List<KeyCode> chord = pair.chords[args.IsKeyDown];
                if (KeyIndex >= chord.Count)
                    continue;

                KeyCode chordKey = chord[KeyIndex];
                if (chordKey == hookKey)
                {
                    KeyUsed = true;
                    KeyIndex++;

                    // increase interval as we're expecting a new chord key
                    SetInterval(KeyboardResetTimer, TIME_FLUSH_EXTENDED);

                    break; // leave loop
                }
                else
                {
                    // restore default interval
                    SetInterval(KeyboardResetTimer, TIME_FLUSH);
                }
            }

            // if key is used or previous key was, we need to maintain key(s) order
            if (KeyUsed || KeyIndex > 0)
            {
                args.SuppressKeyPress = true;

                // add key to buffer
                BufferKeys.Add(args);

                // search for matching triggers
                string buffer_keys = GetChord(BufferKeys);

                foreach (DeviceChord chord in MainWindow.handheldDevice.listeners.Where(a => a.chords[args.IsKeyDown].Count == BufferKeys.Count))
                {
                    // compare ordered enumerable
                    string chord_keys = chord.GetChord(args.IsKeyDown);

                    if (chord_keys.Equals(buffer_keys))
                    {
                        // reset index
                        KeyIndex = 0;

                        // check if inputs timestamp are too close from one to another
                        bool IsKeyUnexpected = args.IsKeyUp && string.IsNullOrEmpty(SpecialKey);

                        // do not bother checking timing if key is already unexpected
                        if (!IsKeyUnexpected)
                        {
                            var pair = new KeyValuePair<KeyCode, bool>(hookKey, args.IsKeyDown);
                            var prevTimestamp = prevKeys.ContainsKey(pair) ? prevKeys[pair] : TIME_SPAM;
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
                        var controller = ControllerManager.GetTargetController();
                        controller?.InjectButton(chord.button, args.IsKeyDown, args.IsKeyUp);

                        if (args.IsKeyDown)
                            SpecialKey = chord.name;
                        else if (args.IsKeyUp)
                            SpecialKey = string.Empty;

                        return;
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
                string key = pair.Key;
                InputsChord chord = pair.Value;

                InputsChordType InputsType = chord.InputsType;
                ControllerButtonFlags GamepadButtons = chord.GamepadButtons;

                if (InputsType.HasFlag(lookup.InputsType) &&
                    GamepadButtons == lookup.GamepadButtons)
                    keys.Add(key);
            }

            return keys;
        }

        public static void KeyPress(VirtualKeyCode key)
        {
            m_InputSimulator.Keyboard.KeyPress(key);
        }

        public static void KeyPress(VirtualKeyCode[] keys)
        {
            foreach (VirtualKeyCode key in keys)
                m_InputSimulator.Keyboard.KeyDown(key);

            foreach (VirtualKeyCode key in keys)
                m_InputSimulator.Keyboard.KeyUp(key);
        }

        public static void SendChordDown(List<KeyCode> keys)
        {
            foreach (KeyCode key in keys)
                m_InputSimulator.Keyboard.KeyDown((VirtualKeyCode)key);
        }

        public static void SendChordUp(List<KeyCode> keys)
        {
            foreach (KeyCode key in keys)
                m_InputSimulator.Keyboard.KeyUp((VirtualKeyCode)key);
        }

        public static void KeyPress(List<OutputKey> keys)
        {
            foreach (OutputKey key in keys)
            {
                if (key.IsKeyDown)
                    m_InputSimulator.Keyboard.KeyDown((VirtualKeyCode)key.KeyValue);
                else
                    m_InputSimulator.Keyboard.KeyUp((VirtualKeyCode)key.KeyValue);
            }
        }

        public static void KeyStroke(VirtualKeyCode mod, VirtualKeyCode key)
        {
            m_InputSimulator.Keyboard.ModifiedKeyStroke(mod, key);
        }

        private static void ReleaseGamepadBuffer()
        {
            // do something
        }

        private static void ReleaseKeyboardBuffer()
        {
            if (BufferKeys.Count == 0)
                return;

            // reset index
            KeyIndex = 0;

            List<KeyEventArgsExt> keys = BufferKeys.OrderBy(a => a.Timestamp).ToList();
            for (int i = 0; i < keys.Count; i++)
            {
                KeyEventArgsExt args = keys[i];

                // improve me
                VirtualKeyCode key = (VirtualKeyCode)args.KeyValue;
                if (args.KeyValue == 0)
                {
                    if (args.Control)
                        key = VirtualKeyCode.LCONTROL;
                    else if (args.Alt)
                        key = VirtualKeyCode.RMENU;
                    else if (args.Shift)
                        key = VirtualKeyCode.RSHIFT;
                }

                switch (args.IsKeyDown)
                {
                    case true:
                        m_InputSimulator.Keyboard.KeyDown(key);
                        break;
                    case false:
                        m_InputSimulator.Keyboard.KeyUp(key);
                        break;
                }
            }

            // clear buffer
            BufferKeys.Clear();
        }

        private static string GetChord(List<KeyEventArgsExt> args)
        {
            return string.Join(" | ", args.Select(a => (KeyCode)a.KeyValue).OrderBy(key => key).ToList());
        }

        public static void Start()
        {
            m_GlobalHook.KeyDown += M_GlobalHook_KeyEvent;
            m_GlobalHook.KeyUp += M_GlobalHook_KeyEvent;

            IsInitialized = true;
            Initialized?.Invoke();
        }

        public static void Stop()
        {
            if (!IsInitialized)
                return;

            IsInitialized = false;

            //It is recommened to dispose it
            m_GlobalHook.KeyDown -= M_GlobalHook_KeyEvent;
            m_GlobalHook.KeyUp -= M_GlobalHook_KeyEvent;
        }

        public static void UpdateReport(ControllerButtonFlags Buttons)
        {
            GamepadResetTimer.Stop();

            bool IsKeyDown = false;
            bool IsKeyUp = false;

            if (prevButtons == Buttons)
                return;

            // IsKeyDown (filter on "fake" keys)
            if (Buttons != ControllerButtonFlags.None)
            {
                // reset hold timer
                InputsChordHoldTimer.Stop();
                InputsChordHoldTimer.Start();

                if (GamepadClearPending)
                {
                    currentChord.GamepadButtons = Buttons;
                    GamepadClearPending = false;
                }
                else
                    currentChord.GamepadButtons |= Buttons;

                currentChord.InputsType = InputsChordType.Click;

                IsKeyDown = true;
            }
            // IsKeyUp
            else if (Buttons == ControllerButtonFlags.None && currentChord.GamepadButtons != ControllerButtonFlags.None)
            {
                GamepadClearPending = true;

                IsKeyUp = true;
            }

            if (currentChord.GamepadButtons != ControllerButtonFlags.None)
                CheckForSequence(IsKeyDown, IsKeyUp);

            if (IsKeyUp)
            {
                currentChord.GamepadButtons = ControllerButtonFlags.None;
            }

            prevButtons = Buttons;

            GamepadResetTimer.Start();
        }

        public static void StartListening(Hotkey hotkey, ListenerType type)
        {
            // force expiration on previous listener, if any
            if (!string.IsNullOrEmpty(currentHotkey.Listener))
                ListenerExpired();

            // store current hotkey values
            prevChord = new InputsChord(hotkey.inputsChord.GamepadButtons, hotkey.inputsChord.OutputKeys, hotkey.inputsChord.InputsType);

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
                    currentChord.GamepadButtons = ControllerButtonFlags.None;
                    break;
            }

            BufferKeys.Clear();

            ListenerTimer.Start();
        }

        private static void StopListening(InputsChord inputsChord = null)
        {
            if (inputsChord == null)
                inputsChord = new InputsChord();

            switch (currentType)
            {
                case ListenerType.Default:
                case ListenerType.Output:
                case ListenerType.UI:
                    Triggers[currentHotkey.Listener] = new InputsChord(inputsChord.GamepadButtons, inputsChord.OutputKeys, inputsChord.InputsType);
                    break;
            }

            TriggerUpdated?.Invoke(currentHotkey.Listener, inputsChord, currentType);

            LogManager.LogDebug("Trigger: {0} updated. buttons: {1}, type: {2}", currentHotkey.Listener, inputsChord.GamepadButtons, inputsChord.InputsType);

            currentHotkey = new();
            currentChord = new();
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
            string listener = hotkey.inputsHotkey.Listener;

            if (!Triggers.ContainsKey(listener))
                Triggers.Add(listener, hotkey.inputsChord);
        }

        internal static void InvokeTrigger(Hotkey hotkey, bool IsKeyDown, bool IsKeyUp)
        {
            if (IsKeyDown && hotkey.inputsHotkey.OnKeyDown)
                TriggerRaised?.Invoke(hotkey.inputsHotkey.Listener, hotkey.inputsChord, true, false);

            if (IsKeyUp && hotkey.inputsHotkey.OnKeyUp)
                TriggerRaised?.Invoke(hotkey.inputsHotkey.Listener, hotkey.inputsChord, false, true);
        }
    }
}