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
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Windows.Forms;
using WindowsInput.Events;
using static System.Net.Mime.MediaTypeNames;

namespace HandheldCompanion.Managers
{
    public static class InputsManager
    {
        // Gamepad vars
        private static MultimediaTimer UpdateTimer;
        private static MultimediaTimer ResetTimer;
        private static MultimediaTimer ListenerTimer;

        private static Dictionary<string, long> prevKeyDown = new();
        private static Dictionary<string, long> prevKeyUp = new();

        private static ControllerEx controllerEx;
        private static Gamepad Gamepad;
        private static Gamepad prevGamepad;
        private static State GamepadState;

        private const int TIME_BURST = 200;
        private const int TIME_LONG = 800;

        private static bool TriggerLock;
        private static string TriggerListener = string.Empty;
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
            UpdateTimer.Tick += UpdateReport;

            ResetTimer = new MultimediaTimer(m_fastInterval) { AutoReset = false };
            ResetTimer.Tick += ReleaseBuffer;

            ListenerTimer = new MultimediaTimer(3000);
            ListenerTimer.Tick += ListenerExpired;

            m_GlobalHook = Hook.GlobalEvents();
            m_InputSimulator = new InputSimulator();

            HotkeysManager.HotkeyCreated += TriggerCreated;

            // make sure we don't hang the keyboard
            Thread.CurrentThread.Priority = ThreadPriority.Highest;
        }

        private static void InjectModifiers(KeyEventArgsExt args)
        {
            if (args.Modifiers == Keys.None)
                return;

            foreach (Keys mode in ((Keys[])Enum.GetValues(typeof(Keys))).Where(a => a != Keys.None))
            {
                if (args.Modifiers.HasFlag(mode))
                {
                    KeyEventArgsExt mod = new KeyEventArgsExt(mode, args.ScanCode, args.Timestamp, args.IsKeyDown, args.IsKeyUp, true);
                    TriggerBuffer.Add(mod);
                }
            }
        }

        private static void M_GlobalHook_KeyEvent(object? sender, KeyEventArgs e)
        {
            ResetTimer.Stop();
            KeyUsed = false;

            if (TriggerLock)
                return;

            KeyEventArgsExt args = (KeyEventArgsExt)e;
            KeyCode hookKey = (KeyCode)args.KeyValue;

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
                    InjectModifiers(args);

                // search for matching triggers
                foreach (DeviceChord pair in MainWindow.handheldDevice.listeners)
                {
                    string listener = pair.name;
                    List<KeyCode> chord = pair.chord;

                    // compare ordered enumerable
                    var chord_keys = chord.OrderBy(key => key);
                    var buffer_keys = GetBufferKeys().OrderBy(key => key);

                    if (chord_keys.SequenceEqual(buffer_keys))
                    {
                        Intercepted.Clear();
                        Intercepted.AddRange(TriggerBuffer);

                        TriggerBuffer.Clear();

                        long time_last = TIME_BURST;
                        long time_duration = time_last;

                        if (args.IsKeyDown)
                        {
                            time_last = args.Timestamp - prevKeyDown[listener];
                            prevKeyDown[listener] = args.Timestamp;
                        }
                        else if (args.IsKeyUp)
                        {
                            time_last = args.Timestamp - prevKeyUp[listener];
                            prevKeyUp[listener] = args.Timestamp;
                        }

                        // skip call if too close
                        if (time_last < TIME_BURST)
                            break;

                        time_duration = args.Timestamp - prevKeyDown[listener];
                        if (time_duration > TIME_LONG)
                            listener += " (HOLD)";

                        LogManager.LogDebug("Triggered: {0} at {1}", listener, args.Timestamp);

                        if (string.IsNullOrEmpty(TriggerListener))
                        {
                            string trigger = GetTriggerFromName(listener);

                            // trigger isn't used
                            if (string.IsNullOrEmpty(trigger))
                            {
                                TriggerBuffer.AddRange(Intercepted);
                                break;
                            }
                            else if (args.IsKeyUp)
                                TriggerRaised?.Invoke(trigger, Triggers[trigger]);
                        }
                        else
                        {
                            InputsChord inputs = new InputsChord(InputsChordType.Keyboard, string.Join(",", chord), listener);
                            Triggers[TriggerListener] = inputs;

                            if (args.IsKeyUp)
                                StopListening(inputs);
                        }
                        return; // prevent multiple shortcuts from being triggered
                    }
                }
            }

            ResetTimer.Start();
        }

        private static string GetTriggerFromName(string name)
        {
            foreach (var pair in Triggers)
            {
                string p_trigger = pair.Key;
                string p_name = pair.Value.name;

                if (p_name == name)
                    return p_trigger;
            }

            return "";
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

        private static void ReleaseBuffer(object? sender, EventArgs e)
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

                TriggerLock = false;

                // clear buffer
                TriggerBuffer.Clear();
            }
            catch (Exception)
            {
            }
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
        }

        public static void Stop()
        {
            if (!IsInitialized)
                return;

            UpdateTimer.Stop();

            //It is recommened to dispose it
            m_GlobalHook.KeyDown -= M_GlobalHook_KeyEvent;
            m_GlobalHook.KeyUp -= M_GlobalHook_KeyEvent;
        }

        private static void UpdateReport(object? sender, EventArgs e)
        {
            // get current gamepad state
            if (controllerEx != null && controllerEx.IsConnected())
            {
                GamepadState = controllerEx.GetState();
                Gamepad = GamepadState.Gamepad;
            }

            if (prevGamepad.GetHashCode() == Gamepad.GetHashCode())
                return;

            if (string.IsNullOrEmpty(TriggerListener))
            {
                // handle triggers
                foreach (var pair in Triggers)
                {
                    string listener = pair.Key;
                    InputsChord inputs = pair.Value;

                    if (inputs.type != InputsChordType.Gamepad)
                        continue;

                    GamepadButtonFlags buttons = inputs.buttons;

                    if (Gamepad.Buttons.HasFlag(buttons))
                    {
                        if (!Triggered[listener])
                        {
                            Triggered[listener] = true;
                            TriggerRaised?.Invoke(listener, Triggers[listener]);
                            return; // prevent multiple shortcuts from being triggered
                        }
                    }
                    else if (Triggered.ContainsKey(listener) && Triggered[listener])
                        Triggered[listener] = false;
                }
            }
            else
            {
                // handle listener
                if (Gamepad.Buttons != 0)
                    Triggers[TriggerListener].buttons |= Gamepad.Buttons;

                else if (Gamepad.Buttons == 0 && Triggers[TriggerListener].buttons != GamepadButtonFlags.None)
                {
                    Triggers[TriggerListener].type = InputsChordType.Gamepad;

                    StopListening(Triggers[TriggerListener]);
                }
            }

            Updated?.Invoke(Gamepad);
            prevGamepad = Gamepad;
        }

        private static InputsChord chordBuffer;

        public static void StartListening(string listener)
        {
            // already listening for input ?
            if (!String.IsNullOrEmpty(TriggerListener))
                ListenerExpired(null, null);

            TriggerListener = listener;
            TriggerBuffer = new();

            // store triggers
            chordBuffer = Triggers[TriggerListener];
            Triggers[TriggerListener] = new();

            ListenerTimer.Start();
        }

        private static void StopListening(InputsChord chord)
        {
            TriggerUpdated?.Invoke(TriggerListener, chord);
            TriggerListener = string.Empty;

            ListenerTimer.Stop();
        }

        private static void ListenerExpired(object? sender, EventArgs e)
        {
            // no input pressed
            Triggers[TriggerListener] = chordBuffer;
            StopListening(chordBuffer);
        }

        public static void ClearListening(string listener)
        {
            TriggerUpdated?.Invoke(listener, new InputsChord());
            Triggers[listener] = new();
        }

        public static void UpdateController(ControllerEx _controllerEx)
        {
            controllerEx = _controllerEx;
        }

        private static void TriggerCreated(Hotkey hotkey)
        {
            string listener = hotkey.inputsHotkey.GetListener();

            Triggers.Add(listener, hotkey.inputsChord);
            Triggered[listener] = false;
        }
    }
}