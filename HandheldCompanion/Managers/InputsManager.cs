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
        // Gamepad vars
        private static MultimediaTimer UpdateTimer;
        private static MultimediaTimer ResetTimer;
        private static MultimediaTimer ListenerTimer;

        private static MultimediaTimer KeyDownTimer;
        private static MultimediaTimer KeyDownBufferTimer;

        private static bool KeyDownSequenced;
        private static InputsChord KeyDownChord = new();

        private static Dictionary<string, long> prevKeyDown = new();
        private static Dictionary<string, long> prevKeyUp = new();

        private static ControllerEx controllerEx;
        private static Gamepad Gamepad;
        private static Gamepad prevGamepad;
        private static State GamepadState;

        private const int TIME_BURST = 200;
        private const int TIME_LONG = 800;

        private static bool TriggerLock;
        private static bool TriggerIsCombo;
        private static InputsHotkey inputsHotkey = new();

        private static InputsChord prevChord;
        private static List<KeyEventArgsExt> TriggerBuffer = new();
        private static List<KeyEventArgsExt> Intercepted = new();

        private static Dictionary<string, bool> Triggered = new Dictionary<string, bool>();

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

        private const int m_fastInterval = 20;
        private const int m_slowInterval = 100;

        private static int KeyIndex;
        private static bool KeyUsed;

        public static bool IsInitialized;

        static InputsManager()
        {
            // initialize timers
            UpdateTimer = new MultimediaTimer(10);
            UpdateTimer.Tick += (sender, e) => UpdateReport();

            ResetTimer = new MultimediaTimer(m_fastInterval) { AutoReset = false };
            ResetTimer.Tick += (sender, e) => ReleaseBuffer();

            ListenerTimer = new MultimediaTimer(3000);
            ListenerTimer.Tick += (sender, e) => ListenerExpired();

            KeyDownTimer = new MultimediaTimer(TIME_LONG) { AutoReset = false };
            KeyDownTimer.Tick += (sender, e) => KeyDownTimer_Tick();

            KeyDownBufferTimer = new MultimediaTimer(m_slowInterval) { AutoReset = false };
            KeyDownBufferTimer.Tick += (sender, e) => { ExecuteSequence(); };

            m_GlobalHook = Hook.GlobalEvents();
            m_InputSimulator = new InputSimulator();

            HotkeysManager.HotkeyCreated += TriggerCreated;

            // make sure we don't hang the keyboard
            Thread.CurrentThread.Priority = ThreadPriority.Highest;
        }

        private static void KeyDownTimer_Tick()
        {
            // triggered when key is pressed for a long time
            KeyDownChord.type = InputsChordType.Hold;

            ExecuteSequence();
        }

        private static void ExecuteSequence()
        {
            KeyDownTimer.Stop();
            KeyDownBufferTimer.Stop();

            KeyDownSequenced = true;

            // what the fuck are we doing here ?
            if (string.IsNullOrEmpty(KeyDownChord.key) && KeyDownChord.buttons == GamepadButtonFlags.None)
                return;

            if (string.IsNullOrEmpty(inputsHotkey.Listener))
            {
                var trigger = GetTriggerFromChord(KeyDownChord);

                if (!string.IsNullOrEmpty(trigger))
                {
                    LogManager.LogDebug("Captured: KeyDown: {0}, ButtonFlags: {1}, Type: {2}", KeyDownChord.key, KeyDownChord.buttons, KeyDownChord.type);

                    TriggerBuffer.Clear();
                    TriggerRaised?.Invoke(trigger, KeyDownChord);
                }
                else
                {
                    LogManager.LogDebug("Released: KeyDown: {0}, ButtonFlags: {1}, Type: {2}", KeyDownChord.key, KeyDownChord.buttons, KeyDownChord.type);

                    TriggerBuffer.AddRange(Intercepted);
                    ResetTimer.Start();
                }

                Intercepted.Clear();
            }
            else
                StopListening(KeyDownChord);

            KeyDownChord = new();
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
            if (TriggerLock)
                return;

            ResetTimer.Stop();
            KeyUsed = false;

            KeyEventArgsExt args = (KeyEventArgsExt)e;
            KeyCode hookKey = (KeyCode)args.KeyValue;

            if (TriggerIsCombo)
            {
                args.SuppressKeyPress = true;
                if (args.IsKeyUp && args.IsExtendedKey)
                    Intercepted.AddRange(InjectModifiers(args));

                // add key to buffer
                Intercepted.Add(args);

                KeyDownBufferTimer.Restart();

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

                    // increase interval
                    ResetTimer.Interval = m_slowInterval;

                    break; // leave loop
                }
                else
                {
                    // restore default interval
                    ResetTimer.Interval = m_fastInterval;
                }
            }

            // if key is used or previous key was, we need to maintain key(s) order
            if (KeyUsed || KeyIndex > 0)
            {
                args.SuppressKeyPress = true;

                // add key to buffer
                TriggerBuffer.Add(args);

                if (args.IsKeyUp && args.IsExtendedKey)
                    TriggerBuffer.AddRange(InjectModifiers(args));

                // search for matching triggers
                foreach (DeviceChord pair in MainWindow.handheldDevice.listeners)
                {
                    // compare ordered enumerable
                    var chord_keys = pair.chord.OrderBy(key => key);
                    var buffer_keys = GetBufferKeys().OrderBy(key => key);

                    if (chord_keys.SequenceEqual(buffer_keys))
                    {
                        long time_last = TIME_BURST;
                        long time_duration = time_last;

                        if (args.IsKeyDown)
                        {
                            time_last = args.Timestamp - prevKeyDown[pair.name];
                            prevKeyDown[pair.name] = args.Timestamp;
                        }
                        else if (args.IsKeyUp)
                        {
                            time_last = args.Timestamp - prevKeyUp[pair.name];
                            prevKeyUp[pair.name] = args.Timestamp;
                        }

                        // check if inputs timestamp are too close from one to another
                        bool tooshort = time_last < TIME_BURST;

                        // only intercept inputs if not too close
                        if (!tooshort)
                            Intercepted.AddRange(TriggerBuffer);

                        // clear buffer
                        TriggerBuffer.Clear();

                        // leave if inputs are too close
                        if (tooshort)
                            break;

                        LogManager.LogDebug("KeyEvent: {0} at {1}, down: {2}, up: {3}", pair.name, args.Timestamp, args.IsKeyDown, args.IsKeyUp);

                        if (args.IsKeyDown)
                        {
                            KeyDownTimer.Start();
                            KeyDownSequenced = false;

                            // update vars
                            KeyDownChord.key = pair.name;
                            KeyDownChord.type = InputsChordType.Click;

                            return;
                        }

                        // Sequence was intercepted already
                        if (!KeyDownSequenced)
                            KeyDownBufferTimer.Start();

                        return; // prevent multiple shortcuts from being triggered
                    }
                }
            }

            ResetTimer.Start();
        }

        private static string GetTriggerFromName(string KeyDownListener)
        {
            foreach (var pair in Triggers)
                if (pair.Value.key == KeyDownListener)
                    return pair.Key;

            return string.Empty;
        }

        private static string GetTriggerFromChord(InputsChord chord)
        {
            foreach (var pair in Triggers)
                if (pair.Value.key == chord.key &&
                    pair.Value.type == chord.type &&
                    pair.Value.buttons == chord.buttons)
                    return pair.Key;

            return string.Empty;
        }

        public static void KeyPress(VirtualKeyCode key)
        {
            TriggerLock = true;

            m_InputSimulator.Keyboard.KeyPress(key);

            TriggerLock = false;
        }

        public static void KeyPress(VirtualKeyCode[] keys)
        {
            TriggerLock = true;

            foreach (VirtualKeyCode key in keys)
                m_InputSimulator.Keyboard.KeyDown(key);

            foreach (VirtualKeyCode key in keys)
                m_InputSimulator.Keyboard.KeyUp(key);

            TriggerLock = false;
        }

        public static void KeyStroke(VirtualKeyCode mod, VirtualKeyCode key)
        {
            m_InputSimulator.Keyboard.ModifiedKeyStroke(mod, key);
        }

        private static void ReleaseBuffer()
        {
            if (TriggerBuffer.Count == 0)
                return;

            // reset index
            KeyIndex = 0;

            try
            {
                TriggerLock = true;

                for (int i = 0; i < TriggerBuffer.Count; i++)
                {
                    KeyEventArgsExt args = TriggerBuffer[i];

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
                    int Timestamp = TriggerBuffer[i].Timestamp;
                    int n_Timestamp = Timestamp;

                    if (i + 1 < TriggerBuffer.Count)
                        n_Timestamp = TriggerBuffer[i + 1].Timestamp;

                    int d_Timestamp = n_Timestamp - Timestamp;
                    m_InputSimulator.Keyboard.Sleep(d_Timestamp);
                }
            }
            catch (Exception)
            {
            }

            // release lock
            TriggerLock = false;

            // clear buffer
            TriggerBuffer.Clear();
        }

        private static List<KeyCode> GetBufferKeys()
        {
            List<KeyCode> keys = new List<KeyCode>();

            foreach (KeyEventArgsExt e in TriggerBuffer)
                keys.Add((KeyCode)e.KeyValue);

            return keys;
        }

        public static void Start()
        {
            if (IsInitialized)
                return;

            foreach (DeviceChord pair in MainWindow.handheldDevice.listeners)
            {
                string listener = pair.name;
                List<KeyCode> chord = pair.chord;

                prevKeyUp[listener] = TIME_BURST;
                prevKeyDown[listener] = TIME_BURST;
            }

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
                    KeyDownChord.buttons = Gamepad.Buttons;
                    GamepadClearPending = false;
                }
                else
                    KeyDownChord.buttons |= Gamepad.Buttons;

                KeyDownTimer.Start();
                KeyDownSequenced = false;
            }
            // IsKeyUp
            else if (Gamepad.Buttons == 0 && KeyDownChord.buttons != GamepadButtonFlags.None)
            {
                GamepadClearPending = true;

                // Sequence was intercepted already
                if (!KeyDownSequenced)
                    KeyDownBufferTimer.Start();
            }

            Updated?.Invoke(Gamepad);
            prevGamepad = Gamepad;
        }

        public static void StartListening(InputsHotkey hotkey)
        {
            // already listening for input ?
            if (!string.IsNullOrEmpty(inputsHotkey.Listener))
                ListenerExpired();

            inputsHotkey = hotkey;

            TriggerBuffer = new();
            KeyDownChord = new();

            // store triggers
            prevChord = Triggers[inputsHotkey.Listener];
            Triggers[inputsHotkey.Listener] = KeyDownChord;

            ListenerTimer.Start();
        }

        private static void StopListening(InputsChord chord = null)
        {
            if (chord == null)
                chord = new InputsChord();

            Triggers[inputsHotkey.Listener] = new InputsChord(chord.buttons, chord.key, chord.combo, chord.type);
            TriggerUpdated?.Invoke(inputsHotkey.Listener, chord);

            LogManager.LogDebug("Trigger: {0} updated. key: {1}, buttons: {2}, type: {3}", inputsHotkey.Listener, chord.key, chord.buttons, chord.type);

            inputsHotkey = new();

            ListenerTimer.Stop();
            KeyDownTimer.Stop();
            KeyDownBufferTimer.Stop();
        }

        private static void ListenerExpired()
        {
            // restore previous chord
            StopListening(prevChord);
        }

        public static void ClearListening(InputsHotkey hotkey)
        {
            inputsHotkey = hotkey;
            StopListening();
        }

        public static void UpdateController(ControllerEx _controllerEx)
        {
            controllerEx = _controllerEx;
        }

        private static void TriggerCreated(Hotkey hotkey)
        {
            string listener = hotkey.inputsHotkey.Listener;

            Triggers.Add(listener, hotkey.inputsChord);
            Triggered[listener] = false;
        }
    }
}