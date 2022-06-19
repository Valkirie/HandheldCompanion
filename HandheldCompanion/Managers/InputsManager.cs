using ControllerCommon;
using HandheldCompanion.Views;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
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
        private Timer ResetTimer;

        private ControllerEx controllerEx;
        private Gamepad Gamepad;
        private Gamepad prevGamepad;
        private State GamepadState;

        private string TriggerListener = string.Empty;
        private List<KeyCode> TriggerBuffer = new();

        private Dictionary<string, bool> Triggered = new Dictionary<string, bool>();

        private Dictionary<string, TriggerInputs> Triggers = new()
        {
            { "overlayGamepad", new TriggerInputs((TriggerInputsType)Properties.Settings.Default.OverlayControllerTriggerType, Properties.Settings.Default.OverlayControllerTriggerValue) },
            { "overlayTrackpads", new TriggerInputs((TriggerInputsType)Properties.Settings.Default.OverlayTrackpadsTriggerType, Properties.Settings.Default.OverlayTrackpadsTriggerValue) },
            { "suspender", new TriggerInputs((TriggerInputsType)Properties.Settings.Default.SuspenderTriggerType, Properties.Settings.Default.SuspenderTriggerValue) },
        };

        // Keyboard vars
        private IKeyboardEventSource m_GlobalHook;

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

            ResetTimer = new Timer(10);
            ResetTimer.AutoReset = false;
            ResetTimer.Elapsed += (sender, e) => { ReleaseBuffer(); };

            m_GlobalHook = Capture.Global.Keyboard();
        }

        private int TriggerIdx = 0;
        private void Keyboard_KeyDown(object? sender, EventSourceEventArgs<KeyDown> e)
        {
            KeyCode key = e.Data.Key;
            bool found = false;

            foreach (var pair in Triggers)
            {
                string listener = pair.Key;
                TriggerInputs inputs = pair.Value;

                if (inputs.type != TriggerInputsType.Keyboard)
                    continue;

                // is the first key the one just typed ?
                TriggerBuffer.Add(key);
                e.Next_Hook_Enabled = false;

                if (inputs.chord.Keys.ElementAt(TriggerIdx) == key)
                {
                    found = true;
                    TriggerIdx++;
                    ResetTimer.Start(); // release the key after a few ms
                }
            }

            if (!found)
                ReleaseBuffer();
            
            Debug.WriteLine("KeyDown: \t{0}", e.Data.Key);
        }

        private void ReleaseBuffer()
        {
            // release the key(s)
            if (TriggerBuffer.Count == 1)
                Simulate.Events().Click(TriggerBuffer).Invoke();
            else
                Simulate.Events().ClickChord(TriggerBuffer).Invoke();

            // clear buffer
            TriggerBuffer.Clear();
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

            m_GlobalHook.KeyDown += Keyboard_KeyDown;
            m_GlobalHook.Enabled = true;

            foreach (ChordClick chord in MainWindow.handheldDevice.listeners.Values)
            {
                var Listener = new KeyChordEventSource(m_GlobalHook, chord);
                Listener.Triggered += (x, y) => Listener_Triggered(m_GlobalHook, x, y);
                Listener.Enabled = true;
            }
        }

        public void Stop()
        {
            UpdateTimer.Stop();

            m_GlobalHook.KeyDown -= Keyboard_KeyDown;
            m_GlobalHook.Enabled = false;
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
