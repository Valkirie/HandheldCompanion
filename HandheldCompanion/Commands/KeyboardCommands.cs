using GregsStack.InputSimulatorStandard.Native;
using HandheldCompanion.Actions;
using HandheldCompanion.Inputs;
using HandheldCompanion.Simulators;
using HandheldCompanion.Views;
using HandheldCompanion.Views.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            KeyboardCommands commands = new();
            commands.commandType = this.commandType;
            commands.Name = this.Name;
            commands.Description = this.Description;
            commands.Glyph = this.Glyph;
            commands.OnKeyUp = this.OnKeyUp;
            commands.OnKeyDown = this.OnKeyDown;

            // specific
            commands.outputChord = this.outputChord.Clone() as InputsChord;

            return commands;
        }
    }
}
