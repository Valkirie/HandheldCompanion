using HandheldCompanion.Misc;
using System;
using System.Linq;
using System.Windows.Forms;
using WpfScreenHelper.Enum;

namespace HandheldCompanion.Managers
{
    public static class WindowManager
    {
        private static string ClearString(string input)
        {
            // todo: improve me
            // make me executable specific to improve the window's name cleaning logic ?
            if (input == null)
                return null;

            // If there's any '|' at all, split on '|', keep first and last
            if (input.Contains("|"))
            {
                var parts = input.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    var first = parts[0].Trim();
                    var last = parts[parts.Length - 1].Trim();
                    return $"{first} | {last}";
                }
                // if somehow only one non-empty segment, just trim and return
                return input.Trim();
            }

            // Otherwise if there's a '-', truncate at the first one
            var dashIndex = input.IndexOf('-');
            if (dashIndex >= 0)
            {
                return input.Substring(0, dashIndex).Trim();
            }

            // No '|' or '-', just return trimmed original
            return input.Trim();
        }

        public static ProcessWindowSettings GetWindowSettings(string path, string name, int Hwnd)
        {
            Profile profile = ManagerFactory.profileManager.GetProfileFromPath(path, true, true);
            if (!profile.Default)
            {
                // check if window already exist with a different name
                string hwndName = profile.WindowsSettings.FirstOrDefault(p => p.Value.Hwnd == Hwnd).Key;
                if (!string.IsNullOrEmpty(hwndName))
                    name = hwndName;

                // clear name
                name = ClearString(name);

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

                // clear name
                name = ClearString(name);

                profile.WindowsSettings[name] = new(screen.DeviceName, borderless, windowPositions) { Hwnd = processWindow.Hwnd, IsGeneric = false };
            }

            // update window settings
            processWindow.windowSettings = new(screen.DeviceName, borderless, windowPositions) { Hwnd = processWindow.Hwnd, IsGeneric = false };

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

                // clear name
                name = ClearString(name);

                if (!profile.WindowsSettings.ContainsKey(name))
                    profile.WindowsSettings[name] = new();

                profile.WindowsSettings[name].DeviceName = screen?.DeviceName ?? "\\\\.\\DISPLAY0";
            }

            // update window settings
            processWindow.windowSettings.DeviceName = screen?.DeviceName ?? "\\\\.\\DISPLAY0";
            processWindow.windowSettings.IsGeneric = false;

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

                // clear name
                name = ClearString(name);

                if (!profile.WindowsSettings.ContainsKey(name))
                    profile.WindowsSettings[name] = new();

                profile.WindowsSettings[name].WindowPositions = windowPositions;
            }

            // update window settings
            processWindow.windowSettings.WindowPositions = windowPositions;
            processWindow.windowSettings.IsGeneric = false;

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

                // clear name
                name = ClearString(name);

                if (!profile.WindowsSettings.ContainsKey(name))
                    profile.WindowsSettings[name] = new();

                profile.WindowsSettings[name].Borderless = borderless;
            }

            // update window settings
            processWindow.windowSettings.Borderless = borderless;
            processWindow.windowSettings.IsGeneric = false;

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
