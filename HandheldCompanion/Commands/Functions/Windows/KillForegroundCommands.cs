using HandheldCompanion.Controls;
using HandheldCompanion.Managers;
using System;

namespace HandheldCompanion.Commands.Functions.Windows
{
    [Serializable]
    public class KillForegroundCommands : FunctionCommands
    {
        public KillForegroundCommands()
        {
            Name = Properties.Resources.Hotkey_KillApp;
            Description = Properties.Resources.Hotkey_KillAppDesc;
            Glyph = "\uE8BB";
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
            KillForegroundCommands commands = new()
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
