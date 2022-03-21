using Microsoft.WindowsAPICodePack.Shell;
using Microsoft.WindowsAPICodePack.Shell.PropertySystem;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Windows.System.Diagnostics;

namespace ControllerCommon.Utils
{
    public static class ProcessUtils
    {
        #region imports
        public enum BinaryType : uint
        {
            SCS_32BIT_BINARY = 0,   // A 32-bit Windows-based application
            SCS_64BIT_BINARY = 6,   // A 64-bit Windows-based application.
            SCS_DOS_BINARY = 1,     // An MS-DOS based application
            SCS_OS216_BINARY = 5,   // A 16-bit OS/2-based application
            SCS_PIF_BINARY = 3,     // A PIF file that executes an MS-DOS based application
            SCS_POSIX_BINARY = 4,   // A POSIX based application
            SCS_WOW_BINARY = 2      // A 16-bit Windows-based application
        }

        [DllImport("kernel32.dll")]
        public static extern bool GetBinaryType(string lpApplicationName, out BinaryType lpBinaryType);
        [DllImport("Kernel32.dll")]
        static extern uint QueryFullProcessImageName(IntPtr hProcess, uint flags, StringBuilder text, out uint size);
        #endregion

        public class WinAPIFunctions
        {
            //Used to get Handle for Foreground Window
            [DllImport("user32.dll", CharSet = CharSet.Auto)]
            private static extern IntPtr GetForegroundWindow();

            //Used to get ID of any Window
            [DllImport("user32.dll", CharSet = CharSet.Auto)]
            private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);
            public delegate bool WindowEnumProc(IntPtr hwnd, IntPtr lparam);

            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool EnumChildWindows(IntPtr hwnd, WindowEnumProc callback, IntPtr lParam);

            public static int GetWindowProcessId(IntPtr hwnd)
            {
                int pid;
                GetWindowThreadProcessId(hwnd, out pid);
                return pid;
            }

            public static IntPtr GetforegroundWindow()
            {
                return GetForegroundWindow();
            }
        }

        public class USBDeviceInfo
        {
            public USBDeviceInfo(string deviceId, string name, string description)
            {
                DeviceId = deviceId;
                Name = name;
                Description = description;
            }

            public string DeviceId { get; }
            public string Name { get; }
            public string Description { get; }

            public override string ToString()
            {
                return Name;
            }
        }

        public class FindHostedProcess
        {
            public ProcessDiagnosticInfo Process { get; private set; }

            public FindHostedProcess()
            {
                try
                {
                    var foregroundProcessID = WinAPIFunctions.GetforegroundWindow();

                    if (foregroundProcessID == IntPtr.Zero)
                        return;

                    Process = ProcessDiagnosticInfo.TryGetForProcessId((uint)WinAPIFunctions.GetWindowProcessId(foregroundProcessID));

                    // Get real process
                    if (Process.ExecutableFileName == "ApplicationFrameHost.exe")
                        WinAPIFunctions.EnumChildWindows(foregroundProcessID, ChildWindowCallback, IntPtr.Zero);
                }
                catch (Exception)
                {
                    Process = null;
                }
            }

            private bool ChildWindowCallback(IntPtr hwnd, IntPtr lparam)
            {
                var process = ProcessDiagnosticInfo.TryGetForProcessId((uint)WinAPIFunctions.GetWindowProcessId(hwnd));

                if (process.ExecutableFileName != "ApplicationFrameHost.exe")
                    Process = process;

                return true;
            }
        }

        public static List<USBDeviceInfo> GetUSBDevices()
        {
            var devices = new List<USBDeviceInfo>();

            using (var mos = new ManagementObjectSearcher(@"Select * From Win32_PnPEntity"))
            {
                using (ManagementObjectCollection collection = mos.Get())
                {
                    foreach (var device in collection)
                    {
                        try
                        {
                            var id = device.GetPropertyValue("DeviceId").ToString();
                            var name = device.GetPropertyValue("Name").ToString();
                            var description = device.GetPropertyValue("Description").ToString();
                            devices.Add(new USBDeviceInfo(id, name, description));
                        }
                        catch (Exception ex) { }
                    }
                }
            }

            return devices;
        }

        public static Dictionary<string, string> GetAppProperties(string filePath1)
        {
            Dictionary<string, string> AppProperties = new Dictionary<string, string>();

            ShellObject shellFile = ShellObject.FromParsingName(filePath1);
            foreach (var property in typeof(ShellProperties.PropertySystem).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                IShellProperty shellProperty = property.GetValue(shellFile.Properties.System, null) as IShellProperty;
                if (shellProperty?.ValueAsObject == null) continue;
                if (AppProperties.ContainsKey(property.Name)) continue;

                string[] shellPropertyValues = shellProperty.ValueAsObject as string[];

                if (shellPropertyValues != null && shellPropertyValues.Length > 0)
                {
                    foreach (string shellPropertyValue in shellPropertyValues)
                        AppProperties[property.Name] = shellPropertyValue.ToString();
                }
                else
                    AppProperties[property.Name] = shellProperty.ValueAsObject.ToString();
            }

            return AppProperties;
        }

        public static string GetPathToApp(Process process)
        {
            try
            {
                return process.MainModule.FileName;
            }
            catch
            {
                string query = "SELECT ExecutablePath, ProcessID FROM Win32_Process";
                ManagementObjectSearcher searcher = new(query);

                foreach (ManagementObject item in searcher.Get())
                {
                    object id = item["ProcessID"];
                    object path = item["ExecutablePath"];

                    if (path != null && id.ToString() == process.Id.ToString())
                    {
                        return path.ToString();
                    }
                }
            }

            return "";
        }
    }
}
