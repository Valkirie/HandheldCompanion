using ControllerCommon;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HandheldCompanion.Managers
{
    public class CheatManager
    {
        // Gamepad vars
        private MultimediaTimer UpdateTimer;
        private ControllerEx controllerEx;
        private Gamepad Gamepad;
        private Gamepad prevGamepad;
        private State GamepadState;

        public event CheatedEventHandler Cheated;
        public delegate void CheatedEventHandler(string cheat);

        public CheatManager()
        {
            // initialize timers
            UpdateTimer = new MultimediaTimer(10);
        }

        public void Start()
        {
            UpdateTimer.Tick += UpdateReport;
            UpdateTimer.Start();
        }

        public void Stop()
        {
            UpdateTimer.Tick -= UpdateReport;
            UpdateTimer.Stop();
        }

        public void UpdateController(ControllerEx controllerEx)
        {
            this.controllerEx = controllerEx;
        }

        private List<GamepadButtonFlags> inputs = new() { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        private int inputs_idx = 0;

        private static List<GamepadButtonFlags> listened = new()
        {
            GamepadButtonFlags.DPadLeft,
            GamepadButtonFlags.DPadUp,
            GamepadButtonFlags.DPadDown,
            GamepadButtonFlags.DPadRight,
            GamepadButtonFlags.A,
            GamepadButtonFlags.B,
            GamepadButtonFlags.X,
            GamepadButtonFlags.Y
        };

        private static Dictionary<List<GamepadButtonFlags>, string> cheats = new Dictionary<List<GamepadButtonFlags>, string>()
        {
            // Konami code
            { new List<GamepadButtonFlags>() {
                GamepadButtonFlags.DPadUp, GamepadButtonFlags.DPadUp,
                GamepadButtonFlags.DPadDown, GamepadButtonFlags.DPadDown,
                GamepadButtonFlags.DPadLeft, GamepadButtonFlags.DPadLeft,
                GamepadButtonFlags.DPadRight, GamepadButtonFlags.DPadRight,
                GamepadButtonFlags.B, GamepadButtonFlags.A
            }, "OverlayControllerFisherPrice" }
        };

        private void UpdateReport(object? sender, EventArgs e)
        {
            // get current gamepad state
            if (controllerEx != null && controllerEx.IsConnected())
            {
                GamepadState = controllerEx.GetState();
                Gamepad = GamepadState.Gamepad;
            }

            if (prevGamepad.Equals(Gamepad))
                return;
            else
                prevGamepad = Gamepad;

            foreach (var button in listened)
            {
                if (Gamepad.Buttons.HasFlag(button))
                {
                    inputs[inputs_idx] = button;

                    if (inputs_idx < inputs.Count - 1)
                        inputs_idx++;
                    else
                        inputs_idx = 0;
                }
            }

            foreach (var cheat in cheats)
            {
                if (cheat.Key.OrderBy(a => a).SequenceEqual(inputs.OrderBy(b => b)))
                {
                    Properties.Settings.Default[cheat.Value] = true;
                    inputs = new() { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }; // reset

                    controllerEx?.Identify();
                    Cheated?.Invoke(cheat.Value);
                }
            }
        }
    }
}
