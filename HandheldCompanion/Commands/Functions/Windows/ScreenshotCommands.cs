using GregsStack.InputSimulatorStandard.Native;
using HandheldCompanion.Actions;
using HandheldCompanion.Controls;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Simulators;
using HandheldCompanion.Utils;
using HandheldCompanion.Views;
using HandheldCompanion.Views.Windows;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.System;

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
            ScreenshotCommands commands = new();
            commands.commandType = commandType;
            commands.Name = Name;
            commands.Description = Description;
            commands.Glyph = Glyph;
            commands.OnKeyUp = OnKeyUp;
            commands.OnKeyDown = OnKeyDown;

            return commands;
        }
    }
}
