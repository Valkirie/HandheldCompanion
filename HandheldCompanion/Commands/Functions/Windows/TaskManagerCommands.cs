using GregsStack.InputSimulatorStandard.Native;
using HandheldCompanion.Simulators;
using System;

namespace HandheldCompanion.Commands.Functions.Windows
{
    [Serializable]
    public class TaskManagerCommands : FunctionCommands
    {
        public TaskManagerCommands()
        {
            Name = Properties.Resources.Hotkey_TaskManager;
            Description = Properties.Resources.Hotkey_TaskManagerDesc;
            Glyph = "\uE9D9";
            OnKeyUp = true;
        }

        public override void Execute(bool IsKeyDown, bool IsKeyUp, bool IsBackground)
        {
            KeyboardSimulator.KeyPress(new[] { VirtualKeyCode.LCONTROL, VirtualKeyCode.LSHIFT, VirtualKeyCode.ESCAPE });

            base.Execute(IsKeyDown, IsKeyUp, false);
        }

        public override object Clone()
        {
            TaskManagerCommands commands = new()
            {
                commandType = commandType,
                Name = Name,
                Description = Description,
                Glyph = Glyph,
                OnKeyUp = OnKeyUp,
                OnKeyDown = OnKeyDown
            };

            return commands;
        }
    }
}
