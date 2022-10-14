using ControllerCommon;
using ControllerCommon.Devices;
using ControllerCommon.Managers;
using Gma.System.MouseKeyHook;
using GregsStack.InputSimulatorStandard;
using GregsStack.InputSimulatorStandard.Native;
using HandheldCompanion.Managers.Classes;
using HandheldCompanion.Views;
using SharpDX.XInput;
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
        // Gamepad variables
        private static ControllerEx controllerEx;
        private static Gamepad Gamepad;
        private static Gamepad prevGamepad;
        private static State GamepadState;
        private static MultimediaTimer UpdateTimer;
        private static MultimediaTimer ResetTimer;

        // InputsChord variables
        private static InputsChord inputsChord = new();
        private static InputsChord prevChord = new();

        private static MultimediaTimer InputsChordHoldTimer;
        private static MultimediaTimer InputsChordInputTimer;

        private static List<KeyCode> prevKeyDown = new();

        // Global variables
        private static MultimediaTimer ListenerTimer;

        private const short TIME_RELEASE = 10;      // default interval between gamepad updates
        private const short TIME_FLUSH = 20;        // default interval between buffer flush
        private const short TIME_FLUSH_EXT = 100;   // extended buffer flush interval when expecting another chord key

        private const short TIME_NEXT = 500;        // default interval before submitting output keys used in combo
        private const short TIME_LONG = 800;        // default interval between two inputs from a chord
                                                    // default interval before considering a chord as hold

        private const short TIME_EXPIRED = 3000;    // default interval before considering a chord as expired if no input is detected

        private static bool IsLocked;
        private static bool IsCombo;
        private static InputsHotkey inputsHotkey = new();

        private static List<KeyEventArgsExt> ReleasedKeys = new();
        private static List<KeyEventArgsExt> CapturedKeys = new();

        private static Dictionary<string, InputsChord> Triggers = new();

        // Keyboard vars
        private static IKeyboardMouseEvents m_GlobalHook;
        private static InputSimulator m_InputSimulator;

        public static event UpdatedEventHandler Updated;
        public delegate void UpdatedEventHandler(Gamepad gamepad);

        public static event TriggerRaisedEventHandler TriggerRaised;
        public delegate void TriggerRaisedEventHandler(string listener, InputsChord inputs);

        public static event TriggerUpdatedEventHandler TriggerUpdated;
        public delegate void TriggerUpdatedEventHandler(string listener, InputsChord inputs);

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
            // initialize timers
            UpdateTimer = new MultimediaTimer(TIME_RELEASE);
            UpdateTimer.Tick += (sender, e) => UpdateReport();

            ResetTimer = new MultimediaTimer(TIME_FLUSH) { AutoReset = false };
            ResetTimer.Tick += (sender, e) => ReleaseBuffer();

            ListenerTimer = new MultimediaTimer(TIME_EXPIRED);
            ListenerTimer.Tick += (sender, e) => ListenerExpired();

            InputsChordHoldTimer = new MultimediaTimer(TIME_LONG) { AutoReset = false };
            InputsChordHoldTimer.Tick += (sender, e) => InputsChordHold_Elapsed();

            InputsChordInputTimer = new MultimediaTimer(TIME_NEXT) { AutoReset = false };
            InputsChordInputTimer.Tick += (sender, e) => { ExecuteSequence(); };

            m_GlobalHook = Hook.GlobalEvents();
            m_InputSimulator = new InputSimulator();

            HotkeysManager.HotkeyCreated += TriggerCreated;

            Thread.CurrentThread.Priority = ThreadPriority.Highest;
        }

        public static void UpdateController(ControllerEx _controllerEx)
        {
            controllerEx = _controllerEx;
        }

        private static void InputsChordHold_Elapsed()
        {
            // triggered when key is pressed for a long time
            inputsChord.InputsType = InputsChordType.Hold;

            ExecuteSequence();
        }

        private static void ExecuteSequence()
        {
            InputsChordHoldTimer.Stop();
            InputsChordInputTimer.Stop();

            if (string.IsNullOrEmpty(inputsChord.SpecialKey) &&
                inputsChord.GamepadButtons == GamepadButtonFlags.None &&
                inputsChord.OutputKeys.Count == 0)
                return;

            if (string.IsNullOrEmpty(inputsHotkey.Listener))
            {
                string key = GetTriggerFromChord(inputsChord);

                if (!string.IsNullOrEmpty(key))
                {
                    LogManager.LogDebug("Captured: KeyDown: {0}, ButtonFlags: {1}, Type: {2}", inputsChord.SpecialKey, inputsChord.GamepadButtons, inputsChord.InputsType);

                    ReleasedKeys.Clear();

                    InputsChord chord = Triggers[key];
                    TriggerRaised?.Invoke(key, chord);
                }
                else
                {
                    LogManager.LogDebug("Released: KeyDown: {0}, ButtonFlags: {1}, Type: {2}", inputsChord.SpecialKey, inputsChord.GamepadButtons, inputsChord.InputsType);

                    ReleasedKeys.AddRange(CapturedKeys);
                    ResetTimer.Start();
                }

                CapturedKeys.Clear();
            }
            else
                StopListening(inputsChord);

            // reset chord
            inputsChord = new();
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
                    KeyEventArgsExt mod = new KeyEventArgsExt(mode, args.ScanCode, args.Timestamp, args.IsKeyDown, args.IsKeyUp, true);
                    mods.Add(mod);
                }
            }

            return mods;
        }

        private static void M_GlobalHook_KeyEvent(object? sender, KeyEventArgs e)
        {
            if (IsLocked)
                return;

            ResetTimer.Stop();
            KeyUsed = false;

            KeyEventArgsExt args = (KeyEventArgsExt)e;
            KeyCode hookKey = (KeyCode)args.KeyValue;

            // are we listening for keyboards inputs as part of a custom hotkey ?
            if (IsCombo)
            {
                args.SuppressKeyPress = true;
                if (args.IsKeyUp && args.IsExtendedKey)
                    CapturedKeys.AddRange(InjectModifiers(args));

                // add key to InputsChord
                inputsChord.AddKey(args);

                InputsChordInputTimer.Restart();

                return;
            }

            foreach (DeviceChord pair in MainWindow.handheldDevice.listeners)
            {
                List<KeyCode> chord = pair.chord;
                if (KeyIndex >= chord.Count)
                    continue;

                KeyCode chordKey = chord[KeyIndex];
                if (chordKey == hookKey)
                {
                    KeyUsed = true;
                    KeyIndex++;

                    // increase interval as we're expecting a new chord key
                    ResetTimer.Interval = TIME_FLUSH_EXT;

                    break; // leave loop
                }
                else
                {
                    // restore default interval
                    ResetTimer.Interval = TIME_FLUSH;
                }
            }

            // if key is used or previous key was, we need to maintain key(s) order
            if (KeyUsed || KeyIndex > 0)
            {
                args.SuppressKeyPress = true;

                // add key to buffer
                ReleasedKeys.Add(args);

                if (args.IsKeyUp && args.IsExtendedKey)
                    ReleasedKeys.AddRange(InjectModifiers(args));

                // search for matching triggers
                List<KeyCode> buffer_keys = GetBufferKeys().OrderBy(key => key).ToList();

                foreach (DeviceChord pair in MainWindow.handheldDevice.listeners)
                {
                    // compare ordered enumerable
                    List<KeyCode> chord_keys = pair.chord.OrderBy(key => key).ToList();

                    if (chord_keys.SequenceEqual(buffer_keys))
                    {
                        // check if inputs timestamp are too close from one to another
                        bool unexpected = false;

                        if (args.IsKeyDown)
                        {
                            prevKeyDown = chord_keys;
                        }
                        else if (args.IsKeyUp)
                        {
                            if (!chord_keys.SequenceEqual(prevKeyDown))
                                unexpected = true;
                            else
                                prevKeyDown.Clear();
                        }

                        // only intercept inputs if not too close
                        if (!unexpected)
                            CapturedKeys.AddRange(ReleasedKeys);

                        // clear buffer
                        ReleasedKeys.Clear();

                        // leave if inputs are too close
                        if (unexpected)
                            break;

                        LogManager.LogDebug("KeyEvent: {0} at {1}, down: {2}, up: {3}", pair.name, args.Timestamp, args.IsKeyDown, args.IsKeyUp);

                        if (args.IsKeyDown)
                        {
                            InputsChordHoldTimer.Restart();

                            // update vars
                            inputsChord.SpecialKey = pair.name;
                            inputsChord.InputsType = InputsChordType.Click;

                            return;
                        }

                        // Sequence was intercepted already
                        if (InputsChordHoldTimer.Enabled)
                            ExecuteSequence();

                        return; // prevent multiple shortcuts from being triggered
                    }
                }
            }

            ResetTimer.Start();
        }

        private static string GetTriggerFromName(string KeyDownListener)
        {
            foreach (var pair in Triggers)
                if (pair.Value.SpecialKey == KeyDownListener)
                    return pair.Key;

            return string.Empty;
        }

        private static string GetTriggerFromChord(InputsChord chord)
        {
            foreach (var pair in Triggers)
                if (pair.Value.SpecialKey == chord.SpecialKey &&
                    pair.Value.InputsType == chord.InputsType &&
                    pair.Value.GamepadButtons == chord.GamepadButtons)
                    return pair.Key;

            return string.Empty;
        }

        public static void KeyPress(VirtualKeyCode key)
        {
            IsLocked = true;

            m_InputSimulator.Keyboard.KeyPress(key);

            IsLocked = false;
        }

        public static void KeyPress(VirtualKeyCode[] keys)
        {
            IsLocked = true;

            foreach (VirtualKeyCode key in keys)
                m_InputSimulator.Keyboard.KeyDown(key);

            foreach (VirtualKeyCode key in keys)
                m_InputSimulator.Keyboard.KeyUp(key);

            IsLocked = false;
        }

        public static void KeyPress(List<OutputKey> keys)
        {
            IsLocked = true;

            foreach (OutputKey key in keys)
            {
                if (key.IsKeyDown)
                    m_InputSimulator.Keyboard.KeyDown((VirtualKeyCode)key.KeyValue);
                else
                    m_InputSimulator.Keyboard.KeyUp((VirtualKeyCode)key.KeyValue);
            }

            IsLocked = false;
        }

        public static void KeyStroke(VirtualKeyCode mod, VirtualKeyCode key)
        {
            m_InputSimulator.Keyboard.ModifiedKeyStroke(mod, key);
        }

        private static void ReleaseBuffer()
        {
            if (ReleasedKeys.Count == 0)
                return;

            // reset index
            KeyIndex = 0;

            try
            {
                IsLocked = true;

                for (int i = 0; i < ReleasedKeys.Count; i++)
                {
                    KeyEventArgsExt args = ReleasedKeys[i];

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

                    // send after initial delay
                    int Timestamp = ReleasedKeys[i].Timestamp;
                    int n_Timestamp = Timestamp;

                    if (i + 1 < ReleasedKeys.Count)
                        n_Timestamp = ReleasedKeys[i + 1].Timestamp;

                    int d_Timestamp = n_Timestamp - Timestamp;
                    m_InputSimulator.Keyboard.Sleep(d_Timestamp);
                }
            }
            catch (Exception)
            {
            }

            // release lock
            IsLocked = false;

            // clear buffer
            ReleasedKeys.Clear();
        }

        private static List<KeyCode> GetBufferKeys()
        {
            List<KeyCode> keys = new List<KeyCode>();

            foreach (KeyEventArgsExt e in ReleasedKeys)
                keys.Add((KeyCode)e.KeyValue);

            return keys;
        }

        public static void Start()
        {
            if (IsInitialized)
                return;

            UpdateTimer.Start();

            m_GlobalHook.KeyDown += M_GlobalHook_KeyEvent;
            m_GlobalHook.KeyUp += M_GlobalHook_KeyEvent;

            IsInitialized = true;
        }

        public static void Stop()
        {
            if (!IsInitialized)
                return;

            UpdateTimer.Stop();

            //It is recommened to dispose it
            m_GlobalHook.KeyDown -= M_GlobalHook_KeyEvent;
            m_GlobalHook.KeyUp -= M_GlobalHook_KeyEvent;

            IsInitialized = false;
        }

        private static bool GamepadClearPending;
        private static void UpdateReport()
        {
            // get current gamepad state
            if (controllerEx != null && controllerEx.IsConnected())
            {
                GamepadState = controllerEx.GetState();
                Gamepad = GamepadState.Gamepad;
            }

            if (prevGamepad.GetHashCode() == Gamepad.GetHashCode())
                return;

            // IsKeyDown
            if (Gamepad.Buttons != 0)
            {
                if (GamepadClearPending)
                {
                    inputsChord.GamepadButtons = Gamepad.Buttons;
                    GamepadClearPending = false;
                }
                else
                    inputsChord.GamepadButtons |= Gamepad.Buttons;

                InputsChordHoldTimer.Restart();
            }
            // IsKeyUp
            else if (Gamepad.Buttons == 0 && inputsChord.GamepadButtons != GamepadButtonFlags.None)
            {
                GamepadClearPending = true;

                // Sequence was intercepted already
                if (InputsChordHoldTimer.Enabled)
                    ExecuteSequence();
            }

            Updated?.Invoke(Gamepad);
            prevGamepad = Gamepad;
        }

        public static void StartListening(Hotkey hotkey)
        {
            // force expiration on previous listener, if any
            if (!string.IsNullOrEmpty(inputsHotkey.Listener))
                ListenerExpired();

            inputsHotkey = hotkey.inputsHotkey;
            inputsChord = hotkey.inputsChord;
            prevChord = new InputsChord(inputsChord.GamepadButtons, inputsChord.SpecialKey, inputsChord.OutputKeys, inputsChord.InputsType);

            switch (hotkey.IsCombo)
            {
                case true:
                    inputsChord.OutputKeys.Clear();
                    break;
                default:
                case false:
                    inputsChord.GamepadButtons = GamepadButtonFlags.None;
                    inputsChord.SpecialKey = string.Empty;
                    break;
            }

            IsCombo = hotkey.IsCombo;

            ReleasedKeys = new();

            ListenerTimer.Start();
        }

        private static void StopListening(InputsChord inputsChord = null)
        {
            if (inputsChord == null)
                inputsChord = new InputsChord();

            Triggers[inputsHotkey.Listener] = new InputsChord(inputsChord.GamepadButtons, inputsChord.SpecialKey, inputsChord.OutputKeys, inputsChord.InputsType);
            TriggerUpdated?.Invoke(inputsHotkey.Listener, inputsChord);

            LogManager.LogDebug("Trigger: {0} updated. key: {1}, buttons: {2}, type: {3}", inputsHotkey.Listener, inputsChord.SpecialKey, inputsChord.GamepadButtons, inputsChord.InputsType);

            inputsHotkey = new();
            InputsManager.inputsChord = new();
            IsCombo = false;

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
            inputsHotkey = hotkey.inputsHotkey;
            StopListening();
        }

        private static void TriggerCreated(Hotkey hotkey)
        {
            string listener = hotkey.inputsHotkey.Listener;

            Triggers.Add(listener, hotkey.inputsChord);
        }
    }
}