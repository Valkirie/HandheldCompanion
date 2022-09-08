using Microsoft.WindowsAPICodePack.Shell;
using Microsoft.WindowsAPICodePack.Shell.PropertySystem;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.System.Diagnostics;
using static ControllerCommon.WinAPI;

namespace ControllerCommon.Utils
{
    public static class ProcessUtils
    {
        #region enums
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
        #endregion

        #region imports
        [DllImport("kernel32.dll")]
        public static extern bool GetBinaryType(string lpApplicationName, out BinaryType lpBinaryType);

        [DllImport("Kernel32.dll")]
        static extern uint QueryFullProcessImageName(IntPtr hProcess, uint flags, StringBuilder text, out uint size);

        [DllImport("user32.dll")]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        public delegate bool WindowEnumProc(IntPtr hwnd, IntPtr lparam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumChildWindows(IntPtr hwnd, WindowEnumProc callback, IntPtr lParam);
        #endregion

        public class FindHostedProcess
        {
            // Speical handling needs for UWP to get the child window process
            public const string UWPFrameHostApp = "ApplicationFrameHost.exe";

            public ProcessDiagnosticInfo Process { get; private set; }
            int attempt = 0;

            public FindHostedProcess(IntPtr foregroundProcessID)
            {
                try
                {
                    if (foregroundProcessID == IntPtr.Zero)
                        return;

                    Process = ProcessDiagnosticInfo.TryGetForProcessId((uint)WinAPI.GetWindowProcessId(foregroundProcessID));

                    if (Process == null)
                        return;

                    // Get real process
                    while (Process.ExecutableFileName.Equals(UWPFrameHostApp, StringComparison.InvariantCultureIgnoreCase) && attempt < 10)
                    {
                        EnumChildWindows(foregroundProcessID, ChildWindowCallback, IntPtr.Zero);
                        Task.Delay(500);
                    }
                }
                catch (Exception)
                {
                    Process = null;
                }
            }

            private bool ChildWindowCallback(IntPtr hwnd, IntPtr lparam)
            {
                var process = ProcessDiagnosticInfo.TryGetForProcessId((uint)WinAPI.GetWindowProcessId(hwnd));

                if (!Process.ExecutableFileName.Equals(UWPFrameHostApp, StringComparison.InvariantCultureIgnoreCase))
                    Process = process;

                attempt++;
                return true;
            }
        }

        public static string GetWindowTitle(IntPtr handle)
        {
            const int nChars = 256;
            StringBuilder Buff = new StringBuilder(nChars);

            if (GetWindowText(handle, Buff, nChars) > 0)
            {
                return Buff.ToString();
            }
            return null;
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

    public static class IconUtilities
    {
        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteObject(IntPtr hObject);

        public static ImageSource ToImageSource(this Icon icon)
        {
            Bitmap bitmap = icon.ToBitmap();
            IntPtr hBitmap = bitmap.GetHbitmap();

            ImageSource wpfBitmap = Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());

            if (!DeleteObject(hBitmap))
            {
                throw new Win32Exception();
            }

            return wpfBitmap;
        }
    }
}
