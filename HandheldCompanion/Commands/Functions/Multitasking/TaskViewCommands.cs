using System;
using System.Diagnostics;

namespace HandheldCompanion.Commands.Functions.Multitasking
{
    [Serializable]
    public class TaskViewCommands : FunctionCommands
    {
        public TaskViewCommands()
        {
            Name = Properties.Resources.Hotkey_Taskview;
            Description = Properties.Resources.Hotkey_TaskviewDesc;
            Glyph = "\ue7c4";
            OnKeyUp = true;
        }

        public override void Execute(bool IsKeyDown, bool IsKeyUp, bool IsBackground)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = "shell:::{3080F90E-D7AD-11D9-BD98-0000947B0257}",
                    UseShellExecute = true
                });
            }
            catch { /* ignore */ }

            base.Execute(IsKeyDown, IsKeyUp, false);
        }

        public override object Clone()
        {
            TaskViewCommands commands = new()
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
