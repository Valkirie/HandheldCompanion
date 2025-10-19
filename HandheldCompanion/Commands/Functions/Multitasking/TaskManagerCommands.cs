using System;
using System.Diagnostics;
using System.Linq;

namespace HandheldCompanion.Commands.Functions.Multitasking
{
    [Serializable]
    public class TaskManagerCommands : FunctionCommands
    {
        public TaskManagerCommands()
        {
            Name = Properties.Resources.Hotkey_TaskManager;
            Description = Properties.Resources.Hotkey_TaskManagerDesc;
            Glyph = "\uE9D9";
            OnKeyUp = true;
        }

        public override void Execute(bool IsKeyDown, bool IsKeyUp, bool IsBackground)
        {
            // Find an existing Task Manager window (if any)
            Process? existing = Process.GetProcessesByName("Taskmgr").FirstOrDefault(p => p.MainWindowHandle != IntPtr.Zero);
            if (existing != null)
            {
                try
                {
                    // Gracefully ask it to close
                    existing.CloseMainWindow();
                }
                catch { /* ignore */ }
            }
            else
            {
                // Summon Task Manager
                string exe = Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess
                    ? @"%windir%\sysnative\taskmgr.exe"   // 32-bit app launching 64-bit Task Manager
                    : @"%windir%\system32\taskmgr.exe";

                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = Environment.ExpandEnvironmentVariables(exe),
                        UseShellExecute = true
                    });
                }
                catch { /* ignore */ }
            }

            base.Execute(IsKeyDown, IsKeyUp, false);
        }

        public override object Clone()
        {
            TaskManagerCommands commands = new()
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
