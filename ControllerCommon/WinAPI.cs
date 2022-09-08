using System.Runtime.InteropServices;

using BOOL = System.Int32;
using HANDLE = System.IntPtr;
using LPVOID = System.IntPtr;
using DWORD = System.Int32;
using ULONG = System.UInt32;
using System;
using System.Security;
using static ControllerCommon.Utils.ProcessUtils;
using static PInvoke.Kernel32;

namespace ControllerCommon
{
    public static class WinAPI
    {
        [DllImport("kernel32.dll")]
        public extern static BOOL GetProcessInformation(
            HANDLE hProcess,
            PROCESS_INFORMATION_CLASS ProcessInformationClass,
            LPVOID ProcessInformation,
            DWORD ProcessInformationSize);

        [DllImport("kernel32.dll")]
        public extern static BOOL SetProcessInformation(
            HANDLE hProcess,
            PROCESS_INFORMATION_CLASS ProcessInformationClass,
            LPVOID ProcessInformation,
            DWORD ProcessInformationSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(
            uint processAccess,
            bool bInheritHandle,
            uint processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [SuppressUnmanagedCodeSecurity]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseHandle(IntPtr hObject);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowThreadProcessId(
            IntPtr hWnd,
            out int lpdwProcessId);

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
}
