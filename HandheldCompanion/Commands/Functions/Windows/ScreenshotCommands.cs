using GregsStack.InputSimulatorStandard.Native;
using HandheldCompanion.Simulators;
using System;

namespace HandheldCompanion.Commands.Functions.Windows
{
    [Serializable]
    public class ScreenshotCommands : FunctionCommands
    {
        public ScreenshotCommands()
        {
            Name = Properties.Resources.Hotkey_PrintScreen;
            Description = Properties.Resources.Hotkey_PrintScreenDesc;
            Glyph = "\uF7ED";
            OnKeyUp = true;
        }

        public override void Execute(bool IsKeyDown, bool IsKeyUp)
        {
            KeyboardSimulator.KeyPress(new[] { VirtualKeyCode.LWIN, VirtualKeyCode.LSHIFT, VirtualKeyCode.VK_S });

            base.Execute(IsKeyDown, IsKeyUp);
        }

        public override object Clone()
        {
            ScreenshotCommands commands = new()
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
