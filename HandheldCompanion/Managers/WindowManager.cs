using HandheldCompanion.Misc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using WpfScreenHelper.Enum;

namespace HandheldCompanion.Managers
{
    public static class WindowManager
    {
        public static ProcessWindowSettings GetWindowSettings(string path, string name)
        {
            Profile profile = ManagerFactory.profileManager.GetProfileFromPath(path, true, true);
            if (!profile.Default && profile.WindowsSettings.TryGetValue(name, out ProcessWindowSettings processWindowSettings))
                return processWindowSettings;

            return null;
        }

        public static void SetWindowSettings(ProcessWindow processWindow, string screenName, bool borderless, WindowPositions windowPositions)
        {
            Screen screen = Screen.AllScreens.FirstOrDefault(screen => screen.DeviceName.Equals(screenName));
            SetWindowSettings(processWindow, screen, borderless, windowPositions);
        }

        public static void SetWindowSettings(ProcessWindow processWindow, Screen screen, bool borderless, WindowPositions windowPositions)
        {
            WinAPI.MakeBorderless(processWindow.Hwnd, borderless);
            WinAPI.MoveWindow(processWindow.Hwnd, screen, windowPositions);
            WinAPI.SetForegroundWindow(processWindow.Hwnd);

            // store settings to profile
            Profile profile = ManagerFactory.profileManager.GetProfileFromPath(processWindow.processEx.Path, true, true);
            if (!profile.Default)
                profile.WindowsSettings[processWindow.Name] = new(screen.DeviceName, borderless, windowPositions);
        }

        public static void ApplyWindowSettings()
        {

        }
    }
}
