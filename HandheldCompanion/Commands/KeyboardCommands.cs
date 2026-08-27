using GregsStack.InputSimulatorStandard.Native;
using HandheldCompanion.Inputs;
using HandheldCompanion.Simulators;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HandheldCompanion.Commands
{
    [Serializable]
    public class KeyboardCommands : ICommands
    {
        public const int MinimumKeyPressDelay = 0;
        public const int MaximumKeyPressDelay = 5000;

        public InputsChord outputChord { get; set; } = new();
        private int _keyPressDelay;
        public int KeyPressDelay
        {
            get => _keyPressDelay;
            set => _keyPressDelay = Math.Clamp(value, MinimumKeyPressDelay, MaximumKeyPressDelay);
        }

        public KeyboardCommands()
        {
            base.commandType = CommandType.Keyboard;

            base.Name = Properties.Resources.Hotkey_Keystrokes;
            base.Description = Properties.Resources.Hotkey_KeystrokesDesc;
            base.Glyph = "\uec31";
            // base.OnKeyUp = true;
            base.OnKeyDown = true;
        }

        public override void Execute(bool IsKeyDown, bool IsKeyUp, bool IsBackground)
        {
            if (OnKeyDown && OnKeyUp)
            {
                if (IsKeyDown)
                {
                    foreach (InputsKey key in outputChord.KeyState.Where(key => key.IsKeyDown))
                        KeyboardSimulator.KeyDown((VirtualKeyCode)key.KeyValue);
                }

                if (IsKeyUp)
                {
                    foreach (InputsKey key in outputChord.KeyState.Where(key => key.IsKeyUp))
                        KeyboardSimulator.KeyUp((VirtualKeyCode)key.KeyValue);
                }

                /*
                foreach (InputsKey key in outputChord.KeyState.Where(key => key.IsKeyDown == IsKeyDown).OrderBy(key => key.Timestamp))
                {
                    if (key.IsKeyDown)
                        KeyboardSimulator.KeyDown((VirtualKeyCode)key.KeyValue);
                    else
                        KeyboardSimulator.KeyUp((VirtualKeyCode)key.KeyValue);
                }
                */
            }
            else
            {
                Task.Run(async () =>
                {
                    foreach (InputsKey key in outputChord.KeyState.Where(key => key.IsKeyDown).OrderBy(key => key.Timestamp))
                        KeyboardSimulator.KeyDown((VirtualKeyCode)key.KeyValue);

                    await Task.Delay(KeyPressDelay).ConfigureAwait(false);

                    foreach (InputsKey key in outputChord.KeyState.Where(key => key.IsKeyUp).OrderBy(key => key.Timestamp))
                        KeyboardSimulator.KeyUp((VirtualKeyCode)key.KeyValue);
                });
            }

            base.Execute(IsKeyDown, IsKeyUp, false);
        }

        public override object Clone()
        {
            KeyboardCommands commands = new()
            {
                commandType = this.commandType,
                Name = this.Name,
                Description = this.Description,
                Glyph = this.Glyph,
                OnKeyUp = this.OnKeyUp,
                OnKeyDown = this.OnKeyDown,
                KeyPressDelay = this.KeyPressDelay,

                // specific
                outputChord = this.outputChord.Clone() as InputsChord ?? new InputsChord()
            };

            return commands;
        }
    }
}
