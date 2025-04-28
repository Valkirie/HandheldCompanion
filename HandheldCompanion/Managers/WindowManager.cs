using HandheldCompanion.Misc;
using System.Linq;
using System.Windows.Forms;
using WpfScreenHelper.Enum;

namespace HandheldCompanion.Managers
{
    public static class WindowManager
    {
        public static ProcessWindowSettings GetWindowSettings(string path, string name, int Hwnd)
        {
            Profile profile = ManagerFactory.profileManager.GetProfileFromPath(path, true, true);
            if (!profile.Default)
            {
                // check if window already exist with a different name
                string hwndName = profile.WindowsSettings.FirstOrDefault(p => p.Value.Hwnd == Hwnd).Key;
                if (!string.IsNullOrEmpty(hwndName))
                    name = hwndName;

                if (profile.WindowsSettings.TryGetValue(name, out ProcessWindowSettings processWindowSettings))
                {
                    // user-created
                    processWindowSettings.IsGeneric = false;
                    processWindowSettings.Hwnd = Hwnd;
                    return processWindowSettings;
                }
            }

            return new();
        }

        public static void SetWindowSettings(ProcessWindow processWindow, Screen screen, bool borderless, WindowPositions windowPositions)
        {
            string name = processWindow.Name;

            // store settings to profile
            Profile profile = ManagerFactory.profileManager.GetProfileFromPath(processWindow.processEx.Path, true, true);
            if (!profile.Default)
            {
                // check if window already exist with a different name
                string hwndName = profile.WindowsSettings.FirstOrDefault(p => p.Value.Hwnd == processWindow.Hwnd).Key;
                if (!string.IsNullOrEmpty(hwndName))
                    name = hwndName;

                profile.WindowsSettings[name] = new(screen.DeviceName, borderless, windowPositions) { Hwnd = processWindow.Hwnd };
            }

            // update window settings
            processWindow.windowSettings = new(screen.DeviceName, borderless, windowPositions) { Hwnd = processWindow.Hwnd };

            ApplySettings(processWindow);
        }

        public static void SetTargetDisplay(ProcessWindow processWindow, Screen? screen)
        {
            string name = processWindow.Name;

            // store settings to profile
            Profile profile = ManagerFactory.profileManager.GetProfileFromPath(processWindow.processEx.Path, true, true);
            if (!profile.Default)
            {
                // check if window already exist with a different name
                string hwndName = profile.WindowsSettings.FirstOrDefault(p => p.Value.Hwnd == processWindow.Hwnd).Key;
                if (!string.IsNullOrEmpty(hwndName))
                    name = hwndName;

                if (!profile.WindowsSettings.ContainsKey(name))
                    profile.WindowsSettings[name] = new();

                profile.WindowsSettings[name].DeviceName = screen?.DeviceName ?? "\\\\.\\DISPLAY0";
            }

            // update window settings
            processWindow.windowSettings.DeviceName = screen?.DeviceName ?? "\\\\.\\DISPLAY0";

            ApplySettings(processWindow);
        }

        public static void SetTargetWindowPosition(ProcessWindow processWindow, WindowPositions windowPositions)
        {
            string name = processWindow.Name;

            // store settings to profile
            Profile profile = ManagerFactory.profileManager.GetProfileFromPath(processWindow.processEx.Path, true, true);
            if (!profile.Default)
            {
                // check if window already exist with a different name
                string hwndName = profile.WindowsSettings.FirstOrDefault(p => p.Value.Hwnd == processWindow.Hwnd).Key;
                if (!string.IsNullOrEmpty(hwndName))
                    name = hwndName;

                if (!profile.WindowsSettings.ContainsKey(name))
                    profile.WindowsSettings[name] = new();

                profile.WindowsSettings[name].WindowPositions = windowPositions;
            }

            // update window settings
            processWindow.windowSettings.WindowPositions = windowPositions;

            ApplySettings(processWindow);
        }

        public static void SetBorderless(ProcessWindow processWindow, bool borderless)
        {
            string name = processWindow.Name;

            // store settings to profile
            Profile profile = ManagerFactory.profileManager.GetProfileFromPath(processWindow.processEx.Path, true, true);
            if (!profile.Default)
            {
                // check if window already exist with a different name
                string hwndName = profile.WindowsSettings.FirstOrDefault(p => p.Value.Hwnd == processWindow.Hwnd).Key;
                if (!string.IsNullOrEmpty(hwndName))
                    name = hwndName;

                if (!profile.WindowsSettings.ContainsKey(name))
                    profile.WindowsSettings[name] = new();

                profile.WindowsSettings[name].Borderless = borderless;
            }

            // update window settings
            processWindow.windowSettings.Borderless = borderless;

            ApplySettings(processWindow);
        }

        public static void ApplySettings(ProcessWindow processWindow)
        {
            Screen screen = Screen.AllScreens.FirstOrDefault(screen => screen.DeviceName.Equals(processWindow.windowSettings.DeviceName));
            if (processWindow.windowSettings.IsGeneric)
                return;

            WinAPI.MakeBorderless(processWindow.Hwnd, processWindow.windowSettings.Borderless);
            WinAPI.MoveWindow(processWindow.Hwnd, screen, processWindow.windowSettings.WindowPositions);
            WinAPI.SetForegroundWindow(processWindow.Hwnd);
        }
    }
}
