using System;
using System.Linq;
using System.Windows.Forms;
using WpfScreenHelper.Enum;

namespace HandheldCompanion.Commands.Functions.Multitasking
{
    [Serializable]
    public class SwapScreenCommands : FunctionCommands
    {
        private bool HasTwoScreen => Screen.AllScreens.Length > 1;

        public SwapScreenCommands()
        {
            Name = Properties.Resources.Hotkey_SwapScreen;
            Description = Properties.Resources.Hotkey_SwapScreenDesc;
            Glyph = "\ue8a7";
            OnKeyUp = true;
        }

        public override void Execute(bool IsKeyDown, bool IsKeyUp, bool IsBackground)
        {
            if (HasTwoScreen)
            {
                // get foreground window
                IntPtr hWnd = WinAPI.GetforegroundWindow();

                // get the other screen
                Screen currentScreen = Screen.FromHandle(hWnd);
                Screen nextScreen = Screen.AllScreens.Where(screen => screen.DeviceName != currentScreen.DeviceName).FirstOrDefault();
                if (nextScreen is not null)
                {
                    // move window
                    WinAPI.MoveWindow(hWnd, nextScreen, WindowPositions.Maximize);
                    WinAPI.SetForegroundWindow(hWnd);
                }
            }

            base.Execute(IsKeyDown, IsKeyUp, false);
        }

        public override object Clone()
        {
            SwapScreenCommands commands = new()
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
