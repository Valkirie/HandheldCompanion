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
using Windows.System;

namespace HandheldCompanion.Commands.Functions.Windows
{
    [Serializable]
    public class SettingsCommands : FunctionCommands
    {
        public SettingsCommands()
        {
            Name = Properties.Resources.Hotkey_Settings;
            Description = Properties.Resources.Hotkey_SettingsDesc;
            Glyph = "\ue713";
            OnKeyUp = true;
        }

        public override void Execute(bool IsKeyDown, bool IsKeyUp)
        {
            Task.Run(() =>
            {
                Process.Start(new ProcessStartInfo("ms-settings://") { UseShellExecute = true });
            });

            base.Execute(IsKeyDown, IsKeyUp);
        }

        public override object Clone()
        {
            SettingsCommands commands = new();
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
