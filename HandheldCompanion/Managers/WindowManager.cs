using HandheldCompanion.Misc;
using HandheldCompanion.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using WpfScreenHelper.Enum;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace HandheldCompanion.Managers
{
    public static class WindowManager
    {
        public static ProcessWindowSettings GetWindowSettings(string path, string name)
        {
            Profile profile = ManagerFactory.profileManager.GetProfileFromPath(path, true, true);
            if (!profile.Default && profile.WindowsSettings.TryGetValue(name, out ProcessWindowSettings processWindowSettings))
                return processWindowSettings;

            return new();
        }

        public static void SetWindowSettings(ProcessWindow processWindow, Screen screen, bool borderless, WindowPositions windowPositions)
        {
            // store settings to profile
            Profile profile = ManagerFactory.profileManager.GetProfileFromPath(processWindow.processEx.Path, true, true);
            if (!profile.Default)
                profile.WindowsSettings[processWindow.Name] = new(screen.DeviceName, borderless, windowPositions);

            // update window settings
            processWindow.windowSettings = new(screen.DeviceName, borderless, windowPositions);

            ApplySettings(processWindow);
        }

        public static void SetTargetDisplay(ProcessWindow processWindow, Screen? screen)
        {
            // store settings to profile
            Profile profile = ManagerFactory.profileManager.GetProfileFromPath(processWindow.processEx.Path, true, true);
            if (!profile.Default)
            {
                if (!profile.WindowsSettings.ContainsKey(processWindow.Name))
                    profile.WindowsSettings[processWindow.Name] = new();

                profile.WindowsSettings[processWindow.Name].DeviceName = screen?.DeviceName ?? "\\\\.\\DISPLAY0";
            }

            // update window settings
            processWindow.windowSettings.DeviceName = screen?.DeviceName ?? "\\\\.\\DISPLAY0";

            ApplySettings(processWindow);
        }

        public static void SetTargetWindowPosition(ProcessWindow processWindow, WindowPositions windowPositions)
        {
            // store settings to profile
            Profile profile = ManagerFactory.profileManager.GetProfileFromPath(processWindow.processEx.Path, true, true);
            if (!profile.Default)
            {
                if (!profile.WindowsSettings.ContainsKey(processWindow.Name))
                    profile.WindowsSettings[processWindow.Name] = new();

                profile.WindowsSettings[processWindow.Name].WindowPositions = windowPositions;
            }

            // update window settings
            processWindow.windowSettings.WindowPositions = windowPositions;

            ApplySettings(processWindow);
        }

        public static void SetBorderless(ProcessWindow processWindow, bool borderless)
        {
            // store settings to profile
            Profile profile = ManagerFactory.profileManager.GetProfileFromPath(processWindow.processEx.Path, true, true);
            if (!profile.Default)
            {
                if (!profile.WindowsSettings.ContainsKey(processWindow.Name))
                    profile.WindowsSettings[processWindow.Name] = new();

                profile.WindowsSettings[processWindow.Name].Borderless = borderless;
            }

            // update window settings
            processWindow.windowSettings.Borderless = borderless;

            ApplySettings(processWindow);
        }

        public static void ApplySettings(ProcessWindow processWindow)
        {
            Screen screen = Screen.AllScreens.FirstOrDefault(screen => screen.DeviceName.Equals(processWindow.windowSettings.DeviceName));

            WinAPI.MakeBorderless(processWindow.Hwnd, processWindow.windowSettings.Borderless);
            WinAPI.MoveWindow(processWindow.Hwnd, screen, processWindow.windowSettings.WindowPositions);

            if (screen is not null)
                WinAPI.SetForegroundWindow(processWindow.Hwnd);
        }
    }
}
