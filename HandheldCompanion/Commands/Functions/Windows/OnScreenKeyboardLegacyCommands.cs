using HandheldCompanion.Views.Windows;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using WpfScreenHelper.Enum;

namespace HandheldCompanion.Commands.Functions.Windows
{
    [Serializable]
    public class OnScreenKeyboardLegacyCommands : FunctionCommands
    {
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindow(string className, string windowTitle);

        public int KeyboardPosition = 0;

        public OnScreenKeyboardLegacyCommands()
        {
            Name = Properties.Resources.Hotkey_KeyboardLegacy;
            Description = Properties.Resources.Hotkey_KeyboardLegacyDesc;
            Glyph = "\uE765";
            OnKeyUp = true;
        }

        public override void Execute(bool IsKeyDown, bool IsKeyUp)
        {
            Task.Run(async () =>
            {
                // Check if there is any existing osk.exe process
                Process? existingOskProcess = Process.GetProcessesByName("osk").FirstOrDefault();
                if (existingOskProcess != null)
                {
                    // Kill the existing osk.exe process
                    existingOskProcess.Kill();
                }
                else
                {
                    // Start a new osk.exe process
                    Process OSK = Process.Start(new ProcessStartInfo("osk.exe") { UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden });
                    await Task.Delay(200);

                    // Find the OSK window. 
                    IntPtr hwndOSK = FindWindow("OSKMainClass", null);

                    Screen screen = Screen.FromHandle(OverlayQuickTools.GetCurrent().hwndSource.Handle);

                    switch (KeyboardPosition)
                    {
                        case 0:     // Bottom
                            WinAPI.MoveWindow(hwndOSK, screen, WindowPositions.Bottom);
                            break;
                        case 1:     // Maximize
                            WinAPI.MakeBorderless(hwndOSK, true);
                            WinAPI.MoveWindow(hwndOSK, screen, WindowPositions.Maximize);
                            break;
                    }
                }
            });

            base.Execute(IsKeyDown, IsKeyUp);
        }

        public override object Clone()
        {
            OnScreenKeyboardLegacyCommands commands = new()
            {
                commandType = this.commandType,
                Name = this.Name,
                Description = this.Description,
                Glyph = this.Glyph,
                OnKeyUp = this.OnKeyUp,
                OnKeyDown = this.OnKeyDown,
                KeyboardPosition = this.KeyboardPosition,
            };

            return commands;
        }
    }
}
