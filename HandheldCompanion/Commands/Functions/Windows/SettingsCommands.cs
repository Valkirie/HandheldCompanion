using System;
using System.Diagnostics;
using System.Threading.Tasks;

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
