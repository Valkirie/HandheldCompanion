using GregsStack.InputSimulatorStandard.Native;
using HandheldCompanion.Simulators;
using System;

namespace HandheldCompanion.Commands.Functions.Multitasking
{
    [Serializable]
    public class TaskViewCommands : FunctionCommands
    {
        public TaskViewCommands()
        {
            Name = Properties.Resources.Hotkey_Taskview;
            Description = Properties.Resources.Hotkey_TaskviewDesc;
            Glyph = "\ue7c4";
            OnKeyUp = true;
        }

        public override void Execute(bool IsKeyDown, bool IsKeyUp, bool IsBackground)
        {
            KeyboardSimulator.KeyPress(new[] { VirtualKeyCode.LWIN, VirtualKeyCode.TAB });

            base.Execute(IsKeyDown, IsKeyUp, false);
        }

        public override object Clone()
        {
            TaskViewCommands commands = new()
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
