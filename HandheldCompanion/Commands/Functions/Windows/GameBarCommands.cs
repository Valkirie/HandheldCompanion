using System;
using System.Diagnostics;

namespace HandheldCompanion.Commands.Functions.Windows
{
    [Serializable]
    public class GameBarCommands : FunctionCommands
    {
        private const string aumid = "Microsoft.XboxGamingOverlay_8wekyb3d8bbwe!App";

        public GameBarCommands()
        {
            Name = Properties.Resources.Hotkey_GameBar;
            Description = Properties.Resources.Hotkey_GameBarDesc;
            Glyph = "\uE713";
            OnKeyUp = true;
        }

        public override void Execute(bool IsKeyDown, bool IsKeyUp, bool IsBackground)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"shell:AppsFolder\\{aumid}",
                UseShellExecute = true
            });

            base.Execute(IsKeyDown, IsKeyUp, false);
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
