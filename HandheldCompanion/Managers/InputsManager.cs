using ControllerCommon;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandheldCompanion.Managers
{
    public class InputsManager
    {
        // Gamepad vars
        private MultimediaTimer UpdateTimer;
        private ControllerEx controllerEx;
        private Gamepad Gamepad;
        private Gamepad prevGamepad;

        private State GamepadState;

        private string TriggerListener = string.Empty;
        private Dictionary<string, bool> Triggered = new Dictionary<string, bool>();
        private Dictionary<string, GamepadButtonFlags> TriggerButtons = new Dictionary<string, GamepadButtonFlags>()
        {
            { "overlayGamepad", (GamepadButtonFlags)Properties.Settings.Default.OverlayControllerTrigger },
            { "overlayTrackpads", (GamepadButtonFlags)Properties.Settings.Default.OverlayTrackpadsTrigger },
            { "suspender", (GamepadButtonFlags)Properties.Settings.Default.SuspenderTrigger },
        };

        public event UpdatedEventHandler Updated;
        public delegate void UpdatedEventHandler(Gamepad gamepad);

        public event TriggerRaisedEventHandler TriggerRaised;
        public delegate void TriggerRaisedEventHandler(string listener, GamepadButtonFlags button);

        public event TriggerUpdatedEventHandler TriggerUpdated;
        public delegate void TriggerUpdatedEventHandler(string listener, GamepadButtonFlags button);

        public InputsManager()
        {
            // initialize timers
            UpdateTimer = new MultimediaTimer(10);
            UpdateTimer.Tick += UpdateReport;
        }

        public void Start()
        {
            foreach (var pair in TriggerButtons)
                Triggered[pair.Key] = false;
            
            UpdateTimer.Start();
        }

        public void Stop()
        {
            UpdateTimer.Stop();
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
                foreach (var pair in TriggerButtons)
                {
                    string listener = pair.Key;
                    GamepadButtonFlags buttons = pair.Value;

                    if (Gamepad.Buttons.HasFlag(buttons))
                    {
                        if (!Triggered[listener])
                        {
                            Triggered[listener] = true;
                            TriggerRaised?.Invoke(listener, TriggerButtons[listener]);
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
                    TriggerButtons[TriggerListener] |= Gamepad.Buttons;
                else if (Gamepad.Buttons == 0 && TriggerButtons[TriggerListener] != 0)
                {
                    TriggerUpdated?.Invoke(TriggerListener, TriggerButtons[TriggerListener]);
                    TriggerListener = string.Empty;
                }
            }

            Updated?.Invoke(Gamepad);
            prevGamepad = Gamepad;
        }

        public void StartListening(string listener)
        {
            TriggerListener = listener;
            TriggerButtons[TriggerListener] = 0;
        }

        public void UpdateController(ControllerEx controllerEx)
        {
            this.controllerEx = controllerEx;
        }
    }
}
