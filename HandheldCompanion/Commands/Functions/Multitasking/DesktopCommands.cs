using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace HandheldCompanion.Commands.Functions.Multitasking
{
    [Serializable]
    public class DesktopCommands : FunctionCommands
    {
        private Guid ShellGuid = new("3080F90D-D7AD-11D9-BD98-0000947B0257");

        public DesktopCommands()
        {
            Name = Properties.Resources.Hotkey_Desktop;
            Description = Properties.Resources.Hotkey_DesktopDesc;
            Glyph = "\uE138";
            OnKeyUp = true;
        }

        public override void Execute(bool IsKeyDown, bool IsKeyUp, bool IsBackground)
        {
            Task.Run(() =>
            {
                Process.Start(new ProcessStartInfo("explorer.exe")
                {
                    UseShellExecute = true,
                    Arguments = $"shell:::{ShellGuid:B}"
                });
            });

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
