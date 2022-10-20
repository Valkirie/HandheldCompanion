using System;
using System.Runtime.InteropServices;
using System.Security;
using static PInvoke.Kernel32;
using BOOL = System.Int32;
using DWORD = System.Int32;
using HANDLE = System.IntPtr;
using LPVOID = System.IntPtr;

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

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int GetWindowThreadProcessId(
            IntPtr hWnd,
            out int lpdwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern int SetPriorityClass(IntPtr hProcess, int dwPriorityClass);

        [Flags]
        public enum ProcessAccessFlags : uint
        {
            All = 0x001F0FFF,
            Terminate = 0x00000001,
            CreateThread = 0x00000002,
            VirtualMemoryOperation = 0x00000008,
            VirtualMemoryRead = 0x00000010,
            VirtualMemoryWrite = 0x00000020,
            DuplicateHandle = 0x00000040,
            CreateProcess = 0x000000080,
            SetQuota = 0x00000100,
            SetInformation = 0x00000200,
            QueryInformation = 0x00000400,
            QueryLimitedInformation = 0x00001000,
            Synchronize = 0x00100000
        }

        [Flags]
        public enum PriorityClass : uint
        {
            ABOVE_NORMAL_PRIORITY_CLASS = 0x8000,
            BELOW_NORMAL_PRIORITY_CLASS = 0x4000,
            HIGH_PRIORITY_CLASS = 0x80,
            IDLE_PRIORITY_CLASS = 0x40,
            NORMAL_PRIORITY_CLASS = 0x20,
            PROCESS_MODE_BACKGROUND_BEGIN = 0x100000,
            PROCESS_MODE_BACKGROUND_END = 0x200000,
            REALTIME_PRIORITY_CLASS = 0x100
        }

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
