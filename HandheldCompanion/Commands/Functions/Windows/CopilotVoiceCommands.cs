using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;

namespace HandheldCompanion.Commands.Functions.Windows
{
    [Serializable]
    public class CopilotVoiceCommands : FunctionCommands
    {
        public CopilotVoiceCommands()
        {
            Name = "Copilot Voice";
            Description = "Open Copilot and start voice (Alt+T)";
            Glyph = "\uE720";
            OnKeyUp = true;
        }

        public override void Execute(bool IsKeyDown, bool IsKeyUp, bool IsBackground)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "ms-copilot://",
                    UseShellExecute = true
                });
            }
            catch { /* ignore */ }

            AutomationElement desktop = AutomationElement.RootElement;
            AutomationElement copilot = null;

            Task timeout = Task.Delay(TimeSpan.FromSeconds(5));
            while (!timeout.IsCompleted && copilot is null)
            {
                copilot = desktop.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.NameProperty, "Copilot"));
                Thread.Sleep(200);
            }

            AutomationElement mic = null;
            if (copilot != null)
            {
                AutomationElementCollection nodes = copilot.FindAll(TreeScope.Descendants, Condition.TrueCondition);
                for (int i = 0; i < nodes.Count; i++)
                {
                    AutomationElement b = nodes[i];

                    string name = b.Current.Name ?? "";
                    string accesskey = b.Current.AccessKey ?? "";

                    if (name.Equals("Talk to Copilot") || accesskey.Equals("Alt, T"))
                    {
                        mic = b; break;
                    }
                }

                // Invoke it
                if (mic != null)
                {
                    if (mic.TryGetCurrentPattern(InvokePattern.Pattern, out var p) && p is InvokePattern inv)
                        inv.Invoke();
                }
            }

            base.Execute(IsKeyDown, IsKeyUp, false);
        }

        public override object Clone()
        {
            CopilotVoiceCommands commands = new()
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

        private static IntPtr FindCopilotWindow()
        {
            IntPtr result = IntPtr.Zero;

            EnumWindows((h, l) =>
            {
                if (!IsWindowVisible(h))
                    return true;

                var sb = new StringBuilder(256);
                GetWindowText(h, sb, sb.Capacity);
                string title = sb.ToString();

                if (!string.IsNullOrWhiteSpace(title) &&
                    title.IndexOf("copilot", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    result = h;
                    return false; // stop enum
                }

                return true;
            }, IntPtr.Zero);

            return result;
        }

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    }
}