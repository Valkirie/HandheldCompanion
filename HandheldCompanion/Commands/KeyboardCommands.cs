using GregsStack.InputSimulatorStandard.Native;
using HandheldCompanion.Inputs;
using HandheldCompanion.Simulators;
using System;
using System.Linq;

namespace HandheldCompanion.Commands
{
    [Serializable]
    public class KeyboardCommands : ICommands
    {
        public InputsChord outputChord { get; set; } = new();

        public KeyboardCommands()
        {
            base.commandType = CommandType.Keyboard;

            base.Name = Properties.Resources.Hotkey_Keystrokes;
            base.Description = Properties.Resources.Hotkey_KeystrokesDesc;
            base.Glyph = "\uec31";
            // base.OnKeyUp = true;
            base.OnKeyDown = true;
        }

        public override void Execute(bool IsKeyDown, bool IsKeyUp)
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
                foreach (InputsKey key in outputChord.KeyState.OrderBy(key => key.Timestamp))
                {
                    if (key.IsKeyDown)
                        KeyboardSimulator.KeyDown((VirtualKeyCode)key.KeyValue);
                    else
                        KeyboardSimulator.KeyUp((VirtualKeyCode)key.KeyValue);
                }
            }

            base.Execute(IsKeyDown, IsKeyUp);
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

                // specific
                outputChord = this.outputChord.Clone() as InputsChord
            };

            return commands;
        }
    }
}
