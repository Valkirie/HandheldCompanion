using GregsStack.InputSimulatorStandard.Native;
using HandheldCompanion.Simulators;
using System;

namespace HandheldCompanion.Commands.Functions.Windows
{
    [Serializable]
    public class GameBarCommands : FunctionCommands
    {
        public GameBarCommands()
        {
            Name = Properties.Resources.Hotkey_GameBar;
            Description = Properties.Resources.Hotkey_GameBarDesc;
            Glyph = "\uE713";
            OnKeyUp = true;
        }

        public override void Execute(bool IsKeyDown, bool IsKeyUp)
        {
            KeyboardSimulator.KeyPress(new[] { VirtualKeyCode.LWIN, VirtualKeyCode.VK_G });

            base.Execute(IsKeyDown, IsKeyUp);
        }

        public override object Clone()
        {
            GameBarCommands commands = new()
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
