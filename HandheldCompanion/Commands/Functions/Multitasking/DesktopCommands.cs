using GregsStack.InputSimulatorStandard.Native;
using HandheldCompanion.Simulators;
using System;

namespace HandheldCompanion.Commands.Functions.Multitasking
{
    [Serializable]
    public class DesktopCommands : FunctionCommands
    {
        public DesktopCommands()
        {
            Name = Properties.Resources.Hotkey_Desktop;
            Description = Properties.Resources.Hotkey_DesktopDesc;
            Glyph = "\uE138";
            OnKeyUp = true;
        }

        public override void Execute(bool IsKeyDown, bool IsKeyUp, bool IsBackground)
        {
            KeyboardSimulator.KeyPress(new[] { VirtualKeyCode.LWIN, VirtualKeyCode.VK_D });

            base.Execute(IsKeyDown, IsKeyUp, false);
        }

        public override object Clone()
        {
            DesktopCommands commands = new()
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
