using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using static PInvoke.Kernel32;
using BOOL = System.Int32;
using DWORD = System.Int32;
using HANDLE = System.IntPtr;
using LPVOID = System.IntPtr;

namespace ControllerCommon;

public static class WinAPI
{
    [Flags]
    public enum UFlags : uint
    {
        SWP_NOACTIVATE = 0x0010,
        SWP_NOMOVE = 0x0002,
        SWP_NOSIZE = 0x0001
    }

    [Flags]
    public enum HWND : int
    {
        SW_SHOWNOACTIVATE = 4,
        HWND_TOPMOST = -1
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

    [DllImport("user32.dll", EntryPoint = "SetWindowPos")]
    static extern bool SetWindowPos(
        int hWnd,             // Window handle
        int hWndInsertAfter,  // Placement-order handle
        int X,                // Horizontal position
        int Y,                // Vertical position
        int cx,               // Width
        int cy,               // Height
        uint uFlags);         // Window positioning flags

    [DllImport("user32.dll")]
    static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

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

    public static void ShowInactiveTopmost(Window window)
    {
        var hwnd = (HwndSource.FromVisual(window) as HwndSource).Handle;
        ShowWindow(hwnd, (int)HWND.SW_SHOWNOACTIVATE);

        SetWindowPos(hwnd.ToInt32(), (int)HWND.HWND_TOPMOST, 0, 0, 0, 0, (uint)UFlags.SWP_NOMOVE | (uint)UFlags.SWP_NOSIZE | (uint)UFlags.SWP_NOACTIVATE);
    }
}