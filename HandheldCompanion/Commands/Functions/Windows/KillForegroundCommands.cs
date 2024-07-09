using HandheldCompanion.Actions;
using HandheldCompanion.Controls;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
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

namespace HandheldCompanion.Commands.Functions.Windows
{
    [Serializable]
    public class KillForegroundCommands : FunctionCommands
    {
        public KillForegroundCommands()
        {
            Name = Properties.Resources.Hotkey_KillApp;
            Description = Properties.Resources.Hotkey_KillAppDesc;
            Glyph = "\ue71a";
            OnKeyUp = true;
        }

        public override void Execute(bool IsKeyDown, bool IsKeyUp)
        {
            // get current foreground process
            ProcessEx fProcess = ProcessManager.GetForegroundProcess();
            if (fProcess != null)
                fProcess.Process.Kill();

            base.Execute(IsKeyDown, IsKeyUp);
        }

        public override object Clone()
        {
            KillForegroundCommands commands = new();
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
