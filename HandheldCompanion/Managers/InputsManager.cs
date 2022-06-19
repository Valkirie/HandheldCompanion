using ControllerCommon;
using Gma.System.MouseKeyHook;
using GregsStack.InputSimulatorStandard;
using GregsStack.InputSimulatorStandard.Native;
using HandheldCompanion.Views;
using SharpDX.XInput;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using WindowsInput;
using WindowsInput.Events;
using WindowsInput.Events.Sources;
using static ControllerCommon.TriggerInputs;
using Timer = System.Timers.Timer;

namespace HandheldCompanion.Managers
{
    public class InputsManager
    {
        // Gamepad vars
        private MultimediaTimer UpdateTimer;
        private MultimediaTimer ResetTimer;

        private ControllerEx controllerEx;
        private Gamepad Gamepad;
        private Gamepad prevGamepad;
        private State GamepadState;

        private int TriggerIdx;
        private bool TriggerLock;
        private string TriggerListener = string.Empty;
        private List<KeyEventArgsExt> TriggerBuffer = new();

        private Dictionary<string, bool> Triggered = new Dictionary<string, bool>();

        private Dictionary<string, TriggerInputs> Triggers = new()
        {
            { "overlayGamepad", new TriggerInputs((TriggerInputsType)Properties.Settings.Default.OverlayControllerTriggerType, Properties.Settings.Default.OverlayControllerTriggerValue) },
            { "overlayTrackpads", new TriggerInputs((TriggerInputsType)Properties.Settings.Default.OverlayTrackpadsTriggerType, Properties.Settings.Default.OverlayTrackpadsTriggerValue) },
            { "suspender", new TriggerInputs((TriggerInputsType)Properties.Settings.Default.SuspenderTriggerType, Properties.Settings.Default.SuspenderTriggerValue) },
        };

        // Keyboard vars
        private IKeyboardMouseEvents m_GlobalHook;
        private InputSimulator m_InputSimulator;

        public event UpdatedEventHandler Updated;
        public delegate void UpdatedEventHandler(Gamepad gamepad);

        public event TriggerRaisedEventHandler TriggerRaised;
        public delegate void TriggerRaisedEventHandler(string listener, TriggerInputs inputs);

        public event TriggerUpdatedEventHandler TriggerUpdated;
        public delegate void TriggerUpdatedEventHandler(string listener, TriggerInputs inputs);

        public InputsManager()
        {
            // initialize timers
            UpdateTimer = new MultimediaTimer(10);
            UpdateTimer.Tick += UpdateReport;

            ResetTimer = new MultimediaTimer(10);
            ResetTimer.Tick += (sender, e) => { ReleaseBuffer(); };

            m_GlobalHook = Hook.GlobalEvents();
            m_InputSimulator = new InputSimulator();
        }

        private void M_GlobalHook_KeyEvent(object? sender, KeyEventArgs e)
        {
            ResetTimer.Stop();

            if (TriggerLock)
                return;

            KeyEventArgsExt args = (KeyEventArgsExt)e;
            args.SuppressKeyPress = true;

            Debug.WriteLine("Key: {0}, IsKeyDown: {1}, IsKeyUp: {2}", args.KeyCode, args.IsKeyDown, args.IsKeyUp);

            TriggerBuffer.Add(args);

            // search for matching triggers
            foreach (var pair in Triggers)
            {
                string listener = pair.Key;
                TriggerInputs inputs = pair.Value;

                if (inputs.type != TriggerInputsType.Keyboard)
                    continue;

                // compare ordered enumerable
                var chord_keys = inputs.chord.Keys.OrderBy(key => key);
                var buffer_keys = GetBufferKeys().OrderBy(key => key);

                if (Enumerable.SequenceEqual(chord_keys, buffer_keys))
                {
                    TriggerBuffer.Clear();

                    // IsKeyDown, IsKeyUp
                    TriggerIdx++;
                    if (TriggerIdx % 2 == 0)
                    {
                        Triggered[listener] = true;
                        TriggerRaised?.Invoke(listener, Triggers[listener]);

                        Debug.WriteLine("Triggered: {0}:{1}", listener, Triggered[listener]);
                        TriggerIdx = 0;
                    }
                    return;
                }
            }

            ResetTimer.Start();
        }

        private void ReleaseBuffer()
        {
            if (TriggerBuffer.Count == 0)
                return;

            TriggerLock = true;

            for (int i = 0; i < TriggerBuffer.Count; i++)
            {
                KeyEventArgsExt args = TriggerBuffer[i];
                int Timestamp = TriggerBuffer[i].Timestamp;
                int n_Timestamp = Timestamp;
                
                if (i + 1 < TriggerBuffer.Count)
                    n_Timestamp = TriggerBuffer[i + 1].Timestamp;

                int d_Timestamp = n_Timestamp - Timestamp;

                switch (args.IsKeyDown)
                {
                    case true:
                        m_InputSimulator.Keyboard.KeyDown((VirtualKeyCode)args.KeyValue);
                        break;
                    case false:
                        m_InputSimulator.Keyboard.KeyUp((VirtualKeyCode)args.KeyValue);
                        break;
                }

                // send after initial delay
                Thread.Sleep(d_Timestamp);
            }

            TriggerLock = false;

            // clear buffer
            TriggerBuffer.Clear();
        }

        private List<KeyCode> GetBufferKeys()
        {
            List<KeyCode> keys = new List<KeyCode>();

            foreach(KeyEventArgsExt e in TriggerBuffer)
                keys.Add((KeyCode)e.KeyValue);

            return keys;
        }

        private void Listener_Triggered(IKeyboardEventSource Keyboard, object sender, KeyChordEventArgs e)
        {
            if (string.IsNullOrEmpty(TriggerListener))
            {
                // handle triggers
                foreach (var pair in Triggers)
                {
                    string listener = pair.Key;
                    TriggerInputs inputs = pair.Value;

                    if (inputs.type != TriggerInputsType.Keyboard)
                        continue;

                    ChordClick chord = inputs.chord;

                    if (chord.Keys.SequenceEqual(e.Chord.Keys))
                    {
                        Triggered[listener] = true;
                        TriggerRaised?.Invoke(listener, Triggers[listener]);

                        // clear buffer
                        TriggerBuffer.Clear();
                    }
                    else if (Triggered.ContainsKey(listener) && Triggered[listener])
                        Triggered[listener] = false;
                }
            }
            else
            {
                // handle listener
                foreach (ChordClick chord in MainWindow.handheldDevice.listeners.Values)
                {
                    if (e.Chord == chord)
                    {
                        TriggerInputs inputs = new TriggerInputs(TriggerInputsType.Keyboard, string.Join(",", e.Chord.Keys));
                        Triggers[TriggerListener] = inputs;

                        TriggerUpdated?.Invoke(TriggerListener, inputs);
                        TriggerListener = string.Empty;
                        return;
                    }
                }
            }

            Debug.WriteLine(string.Join(",", e.Chord.Keys));
        }

        public void Start()
        {
            foreach (var pair in Triggers)
                Triggered[pair.Key] = false;
            
            UpdateTimer.Start();

            m_GlobalHook.KeyDown += M_GlobalHook_KeyEvent;
            m_GlobalHook.KeyUp += M_GlobalHook_KeyEvent;
        }

        public void Stop()
        {
            UpdateTimer.Stop();

            //It is recommened to dispose it
            m_GlobalHook.KeyDown -= M_GlobalHook_KeyEvent;
            m_GlobalHook.KeyUp -= M_GlobalHook_KeyEvent;
            m_GlobalHook.Dispose();
        }

        private void UpdateReport(object? sender, EventArgs e)
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
                    TriggerInputs inputs = pair.Value;

                    if (inputs.type != TriggerInputsType.Gamepad)
                        continue;

                    GamepadButtonFlags buttons = inputs.buttons;

                    if (Gamepad.Buttons.HasFlag(buttons))
                    {
                        if (!Triggered[listener])
                        {
                            Triggered[listener] = true;
                            TriggerRaised?.Invoke(listener, Triggers[listener]);
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
                    Triggers[TriggerListener].type = TriggerInputsType.Gamepad;

                    TriggerUpdated?.Invoke(TriggerListener, Triggers[TriggerListener]);
                    TriggerListener = string.Empty;
                }
            }

            Updated?.Invoke(Gamepad);
            prevGamepad = Gamepad;
        }

        public void StartListening(string listener)
        {
            TriggerListener = listener;
            TriggerBuffer = new();

            Triggers[TriggerListener].buttons = 0;
            Triggers[TriggerListener].chord = new();
        }

        public void UpdateController(ControllerEx controllerEx)
        {
            this.controllerEx = controllerEx;
        }
    }
}
