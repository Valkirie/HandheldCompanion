using Microsoft.WindowsAPICodePack.Shell;
using Microsoft.WindowsAPICodePack.Shell.PropertySystem;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.System.Diagnostics;
using Point = System.Windows.Point;

namespace HandheldCompanion.Utils;

public static class ProcessUtils
{
    #region enums

    public enum BinaryType : uint
    {
        SCS_32BIT_BINARY = 0, // A 32-bit Windows-based application
        SCS_64BIT_BINARY = 6, // A 64-bit Windows-based application.
        SCS_DOS_BINARY = 1, // An MS-DOS based application
        SCS_OS216_BINARY = 5, // A 16-bit OS/2-based application
        SCS_PIF_BINARY = 3, // A PIF file that executes an MS-DOS based application
        SCS_POSIX_BINARY = 4, // A POSIX based application
        SCS_WOW_BINARY = 2 // A 16-bit Windows-based application
    }

    #endregion

    public enum ShowWindowCommands
    {
        Hide = 0,
        Normal = 1,
        Minimized = 2,
        Maximized = 3,
        Restored = 9
    }

    public static string GetWindowTitle(IntPtr handle)
    {
        const int nChars = 256;
        var Buff = new StringBuilder(nChars);

        if (GetWindowText(handle, Buff, nChars) > 0) return Buff.ToString();
        return null;
    }

    public static Dictionary<string, string> GetAppProperties(string filePath1)
    {
        Dictionary<string, string> AppProperties = new();

        var shellFile = ShellObject.FromParsingName(filePath1);
        foreach (var property in typeof(ShellProperties.PropertySystem).GetProperties(BindingFlags.Public |
                     BindingFlags.Instance))
        {
            var shellProperty = property.GetValue(shellFile.Properties.System, null) as IShellProperty;
            if (shellProperty?.ValueAsObject is null) continue;
            if (AppProperties.ContainsKey(property.Name)) continue;

            if (shellProperty.ValueAsObject is string[] shellPropertyValues && shellPropertyValues.Length > 0)
                foreach (var shellPropertyValue in shellPropertyValues)
                    AppProperties[property.Name] = shellPropertyValue;
            else
                AppProperties[property.Name] = shellProperty.ValueAsObject.ToString();
        }

        return AppProperties;
    }

    public static string GetPathToApp(Process process, bool fast = true)
    {
        if (fast)
        {
            try
            {
                // fast but might trigger Win32Exception
                return process.MainModule.FileName;
            }
            catch { }
        }
        else
        {
            var query = $"SELECT ExecutablePath, ProcessID FROM Win32_Process WHERE ProcessID = {process.Id}";
            ManagementObjectSearcher searcher = new(query);

            foreach (ManagementObject item in searcher.Get())
                return Convert.ToString(item["ExecutablePath"]);
        }

        return string.Empty;
    }

    public static string GetPathToApp(int pid)
    {
        var size = 1024;
        var sb = new StringBuilder(size);
        var handle = OpenProcess(QueryLimitedInformation, false, pid);
        if (handle == IntPtr.Zero) return null;
        var success = QueryFullProcessImageName(handle, 0, sb, ref size);
        CloseHandle(handle);
        if (!success) return null;
        return sb.ToString();
    }

    // A function that takes an executable name as a parameter and returns an array of Process objects
    public static Process[] GetProcessesByExecutable(string executableName)
    {
        // Get all the processes running on the system
        Process[] allProcesses = Process.GetProcesses();

        // Create a list to store the matching processes
        List<Process> matchingProcesses = new List<Process>();

        // Loop through each process and check if its executable name matches the parameter
        foreach (Process process in allProcesses)
        {
            try
            {
                // Get the full path of the process executable
                string processPath = GetPathToApp(process.Id);
                if (string.IsNullOrEmpty(processPath))
                    continue;

                // Get the file name of the process executable
                string processFileName = Path.GetFileName(processPath);

                // Compare the file name with the parameter, ignoring case
                if (string.Equals(processFileName, executableName, StringComparison.OrdinalIgnoreCase))
                {
                    // Add the process to the list of matching processes
                    matchingProcesses.Add(process);
                }
            }
            catch (Exception e)
            {
                // Ignore any exceptions that may occur when accessing the process properties
                Console.WriteLine(e.Message);
            }
        }

        // Convert the list to an array and return it
        return matchingProcesses.ToArray();
    }

    public static List<Process> GetChildProcesses(Process process)
    {
        return new ManagementObjectSearcher($"select processid from win32_process Where parentprocessid=={process.Id}")
            .Get()
            .Cast<ManagementObject>()
            .Select(mo => Process.GetProcessById(Convert.ToInt32(mo["ProcessID"])))
            .ToList();
    }

    public static List<int> GetChildIds(Process process)
    {
        return new ManagementObjectSearcher($"select processid from win32_process Where parentprocessid={process.Id}")
            .Get()
            .Cast<ManagementObject>()
            .Select(mo => Convert.ToInt32(mo["ProcessID"]))
            .ToList();
    }

    public static WINDOWPLACEMENT GetPlacement(IntPtr hwnd)
    {
        var placement = new WINDOWPLACEMENT();
        placement.length = Marshal.SizeOf(placement);
        GetWindowPlacement(hwnd, ref placement);
        return placement;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct WINDOWPLACEMENT
    {
        public int length;
        public int flags;
        public ShowWindowCommands showCmd;
        public Point ptMinPosition;
        public Point ptMaxPosition;
        public Rectangle rcNormalPosition;
    }

    public class FindHostedProcess
    {
        // Speical handling needs for UWP to get the child window process
        private const string UWPFrameHostApp = "ApplicationFrameHost.exe";
        private readonly byte UWPattempt;

        public FindHostedProcess(IntPtr hWnd)
        {
            try
            {
                if (hWnd == IntPtr.Zero)
                    return;

                uint processId = (uint)WinAPI.GetWindowProcessId(hWnd);
                _realProcess = ProcessDiagnosticInfo.TryGetForProcessId(processId);
                if (_realProcess is null)
                    return;

                // Get real process
                while (_realProcess.ExecutableFileName == UWPFrameHostApp && UWPattempt < 10)
                {
                    EnumChildWindows(hWnd, ChildWindowCallback, IntPtr.Zero);
                    UWPattempt++;
                    Thread.Sleep(250);
                }
            }
            catch
            {
                _realProcess = null;
            }
        }

        public ProcessDiagnosticInfo _realProcess { get; private set; }

        private bool ChildWindowCallback(IntPtr hWnd, IntPtr lparam)
        {
            uint processId = (uint)WinAPI.GetWindowProcessId(hWnd);
            ProcessDiagnosticInfo childProcess = ProcessDiagnosticInfo.TryGetForProcessId(processId);

            if (childProcess.ExecutableFileName != UWPFrameHostApp)
                _realProcess = childProcess;

            return true;
        }
    }

    #region imports

    [DllImport("kernel32.dll")]
    public static extern bool GetBinaryType(string lpApplicationName, out BinaryType lpBinaryType);

    [DllImport("Kernel32.dll")]
    private static extern uint
        QueryFullProcessImageName(IntPtr hProcess, uint flags, StringBuilder text, out uint size);

    [DllImport("user32.dll")]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    public delegate bool WindowEnumProc(IntPtr hwnd, IntPtr lparam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumChildWindows(IntPtr hwnd, WindowEnumProc callback, IntPtr lParam);

    [DllImport("User32.dll")]
    public static extern bool SetForegroundWindow(IntPtr handle);

    [DllImport("User32.dll")]
    public static extern bool ShowWindow(IntPtr handle, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

    [DllImport("User32.dll")]
    public static extern bool IsIconic(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(
        IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(
        uint processAccess,
        bool bInheritHandle,
        int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool QueryFullProcessImageName(
        IntPtr hProcess,
        int dwFlags,
        StringBuilder lpExeName,
        ref int lpdwSize);

    private const int QueryLimitedInformation = 0x00001000;

    [ComImport]
    [Guid("4ce576fa-83dc-4F88-951c-9d0782b4e376")]
    public class UIHostNoLaunch
    {
    }

    [ComImport]
    [Guid("37c994e7-432b-4834-a2f7-dce1f13b834b")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface ITipInvocation
    {
        void Toggle(IntPtr hwnd);
    }

    [DllImport("user32.dll", SetLastError = false)]
    public static extern IntPtr GetDesktopWindow();

    [DllImport("ntdll.dll", EntryPoint = "NtSuspendProcess", SetLastError = true, ExactSpelling = false)]
    public static extern UIntPtr NtSuspendProcess(IntPtr processHandle);

    [DllImport("ntdll.dll", EntryPoint = "NtResumeProcess", SetLastError = true, ExactSpelling = false)]
    public static extern UIntPtr NtResumeProcess(IntPtr processHandle);

    #endregion
}

public static class IconUtilities
{
    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool DeleteObject(IntPtr hObject);

    public static ImageSource ToImageSource(this Icon icon)
    {
        var bitmap = icon.ToBitmap();
        var hBitmap = bitmap.GetHbitmap();

        ImageSource wpfBitmap = Imaging.CreateBitmapSourceFromHBitmap(
            hBitmap,
            IntPtr.Zero,
            Int32Rect.Empty,
            BitmapSizeOptions.FromEmptyOptions());

        if (!DeleteObject(hBitmap)) throw new Win32Exception();

        return wpfBitmap;
    }
}