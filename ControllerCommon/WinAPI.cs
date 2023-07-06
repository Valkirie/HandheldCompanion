using System;
using System.Runtime.InteropServices;
using static PInvoke.Kernel32;
using BOOL = System.Int32;
using DWORD = System.Int32;
using HANDLE = System.IntPtr;
using LPVOID = System.IntPtr;

namespace ControllerCommon;

public static class WinAPI
{
    public const UInt32 SWP_NOSIZE = 0x0001;
    public const UInt32 SWP_NOMOVE = 0x0002;
    public const UInt32 SWP_NOACTIVATE = 0x0010;
    public const UInt32 SWP_NOZORDER = 0x0004;

    public const int WM_ACTIVATEAPP = 0x001C;
    public const int WM_ACTIVATE = 0x0006;
    public const int WM_SETFOCUS = 0x0007;
    public const int WM_WINDOWPOSCHANGING = 0x0046;
    public const int WM_SYSCOMMAND = 0x0112;

    public const int SC_MOVE = 0xF010;

    public static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
    public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_NOACTIVATE = 0x08000000;

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

    [DllImport("kernel32.dll")]
    public static extern int GetProcessInformation(
        HANDLE hProcess,
        PROCESS_INFORMATION_CLASS ProcessInformationClass,
        LPVOID ProcessInformation,
        int ProcessInformationSize);

    [DllImport("kernel32.dll")]
    public static extern int SetProcessInformation(
        HANDLE hProcess,
        PROCESS_INFORMATION_CLASS ProcessInformationClass,
        LPVOID ProcessInformation,
        int ProcessInformationSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern HANDLE OpenProcess(
        uint processAccess,
        bool bInheritHandle,
        uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern HANDLE GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern int GetWindowThreadProcessId(
        HANDLE hWnd,
        out int lpdwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern int SetPriorityClass(HANDLE hProcess, int dwPriorityClass);

    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    public static extern IntPtr DeferWindowPos(IntPtr hWinPosInfo, IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    public static extern IntPtr BeginDeferWindowPos(int nNumWindows);

    [DllImport("user32.dll")]
    public static extern bool EndDeferWindowPos(IntPtr hWinPosInfo);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowRect(IntPtr hWnd, ref RECT lpRect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern IntPtr GetActiveWindow();

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    public static int GetWindowProcessId(HANDLE hwnd)
    {
        int pid;
        GetWindowThreadProcessId(hwnd, out pid);
        return pid;
    }

    public static HANDLE GetforegroundWindow()
    {
        return GetForegroundWindow();
    }
}