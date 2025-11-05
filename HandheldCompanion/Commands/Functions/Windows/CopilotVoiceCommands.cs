using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Automation;

namespace HandheldCompanion.Commands.Functions.Windows
{
    [Serializable]
    public class CopilotVoiceCommands : FunctionCommands
    {
        private bool IsLoading = false;
        public CopilotVoiceCommands()
        {
            Name = Properties.Resources.Hotkey_CopilotVoice;
            Description = Properties.Resources.Hotkey_CopilotVoiceDesc;
            Glyph = "\uE720";
            OnKeyUp = true;
        }

        public override void Execute(bool IsKeyDown, bool IsKeyUp, bool IsBackground)
        {
            Task.Run(async () =>
            {
                AutomationElement desktop = AutomationElement.RootElement;
                AutomationElement copilot = null;

                Task timeout = Task.Delay(TimeSpan.FromSeconds(5));
                while (!timeout.IsCompleted && copilot is null)
                {
                    copilot = desktop.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.ClassNameProperty, "WinUIDesktopWin32WindowClass")); // desktop.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.NameProperty, "Copilot"));

                    if (copilot is null)
                    {
                        // start copilot and/or show window
                        Process.Start(new ProcessStartInfo("ms-copilot://")
                        {
                            UseShellExecute = true
                        });

                        // set flag
                        IsLoading = true;
                    }
                    else if (!IsLoading)
                    {
                        // hide window
                        if (copilot.TryGetCurrentPattern(WindowPattern.Pattern, out var p) && p is WindowPattern wp)
                            try { wp.Close(); return; } catch { /* ignore */ }

                        // set flag
                        IsLoading = false;
                    }

                    await Task.Delay(200).ConfigureAwait(false);
                }

                if (copilot != null)
                {
                    // set flag
                    IsLoading = false;

                    ResizeAutomationElement(copilot, 0, 0, 465, 700);

                    AutomationElementCollection nodes = copilot.FindAll(TreeScope.Descendants, Condition.TrueCondition);
                    for (int i = 0; i < nodes.Count; i++)
                    {
                        AutomationElement b = nodes[i];

                        string name = b.Current.Name ?? "";
                        string accesskey = b.Current.AccessKey ?? "";

                        if (name.Equals("Talk to Copilot") || accesskey.Equals("Alt, T"))
                        {
                            if (b.TryGetCurrentPattern(InvokePattern.Pattern, out var p) && p is InvokePattern inv)
                                inv.Invoke();
                        }
                        /*
                        else if ((name.Equals("Open quick view") || accesskey.Equals("Alt, Q")) && copilot.Current.BoundingRectangle.Width > 660)
                        {
                            if (b.TryGetCurrentPattern(InvokePattern.Pattern, out var p) && p is InvokePattern inv)
                                inv.Invoke();
                        }
                        */
                    }
                }
            });

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

        public bool ResizeAutomationElement(AutomationElement window, int xPx, int yPx, int widthPx, int heightPx)
        {
            if (window == null) return false;
            var hwnd = (IntPtr)window.Current.NativeWindowHandle;
            if (hwnd == IntPtr.Zero) return false;

            const uint SWP_NOMOVE = 0x0002;
            const uint SWP_NOZORDER = 0x0004;
            const uint SWP_NOACTIVATE = 0x0010;

            return WinAPI.SetWindowPos(hwnd, IntPtr.Zero, xPx, yPx, widthPx, heightPx, (xPx == 0 && yPx == 0 ? SWP_NOMOVE : 0) | SWP_NOZORDER | SWP_NOACTIVATE);
        }
    }
}