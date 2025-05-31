using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using System;
using static HandheldCompanion.Misc.ProcessEx;

namespace HandheldCompanion.Commands.Functions.Multitasking
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

        public override void Execute(bool IsKeyDown, bool IsKeyUp, bool IsBackground)
        {
            try
            {
                // get current foreground process
                ProcessEx processEx = ProcessManager.GetCurrent();
                if (processEx is null)
                    return;

                ProcessFilter filter = ProcessManager.GetFilter(processEx.Executable, processEx.Path);

                switch (filter)
                {
                    case ProcessFilter.Allowed:
                        processEx?.Kill();
                        break;
                }
            }
            catch { }

            base.Execute(IsKeyDown, IsKeyUp, false);
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
