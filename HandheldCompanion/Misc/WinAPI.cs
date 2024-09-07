using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using WpfScreenHelper.Enum;
using static PInvoke.Kernel32;
using HANDLE = System.IntPtr;
using LPVOID = System.IntPtr;

namespace HandheldCompanion;

public static class WinAPI
{
    public const UInt32 SWP_NOSIZE = 0x0001;
    public const UInt32 SWP_NOMOVE = 0x0002;
    public const UInt32 SWP_NOACTIVATE = 0x0010;
    public const UInt32 SWP_NOZORDER = 0x0004;
    public const UInt32 SWP_SHOWWINDOW = 0x0040;
    public const UInt32 SWP_FRAMECHANGED = 0x0020;

    public const int WM_ACTIVATEAPP = 0x001C;
    public const int WM_ACTIVATE = 0x0006;
    public const int WM_SETFOCUS = 0x0007;
    public const int WM_WINDOWPOSCHANGING = 0x0046;
    public const int WM_SYSCOMMAND = 0x0112;

    public const int WS_VISIBLE = 0x10000000;
    public const int WS_OVERLAPPED = 0x00000000;

    public const int SC_MOVE = 0xF010;
    public const int SW_MAXIMIZE = 3;

    public static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
    public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

    private const int GWL_STYLE = -16;
    private const int WS_BORDER = 0x00800000;
    private const int WS_CAPTION = 0x00C00000;
    private const int WS_SYSMENU = 0x00080000;
    private const int WS_THICKFRAME = 0x00040000;
    private const int WS_MINIMIZEBOX = 0x00020000;
    private const int WS_MAXIMIZEBOX = 0x00010000;

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

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(nint hWnd);

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

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

    [DllImport("user32.dll")]
    public static extern IntPtr GetActiveWindow();

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public RECT(int left, int top, int right, int bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        public RECT(Rectangle rect) : this(rect.Left, rect.Top, rect.Right, rect.Bottom)
        {
        }
    }

    public struct POINTSTRUCT
    {
        public int x;

        public int y;

        public POINTSTRUCT(int x, int y)
        {
            this.x = x;
            this.y = y;
        }
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

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool MoveWindow(nint hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool IsProcessDPIAware();

    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromRect(ref RECT lprc, MonitorOptions dwFlags);

    [DllImport("shcore.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr GetDpiForMonitor([In] IntPtr hmonitor, [In] DpiType dpiType, out uint dpiX, out uint dpiY);

    [DllImport("user32.dll", ExactSpelling = true)]
    public static extern IntPtr MonitorFromPoint(POINTSTRUCT pt, MonitorDefault flags);

    [DllImport("user32.dll")]
    public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    private const int CS_DROPSHADOW = 0x00020000;

    [DllImport("user32.dll")]
    public static extern int SetClassLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    public static extern int GetClassLong(nint hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int GetDpiForWindow(nint hwnd);


    [Flags]
    public enum MonitorOptions : uint
    {
        MONITOR_DEFAULTTONULL = 0x00000000,
        MONITOR_DEFAULTTOPRIMARY = 0x00000001,
        MONITOR_DEFAULTTONEAREST = 0x00000002
    }

    public enum DpiType
    {
        EFFECTIVE,
        ANGULAR,
        RAW
    }

    public enum MonitorDefault
    {
        MONITOR_DEFAULTTONEAREST = 2,
        MONITOR_DEFAULTTONULL = 0,
        MONITOR_DEFAULTTOPRIMARY = 1
    }

    public static IntPtr GetScreenHandle(Screen screen)
    {
        RECT rect = new RECT(screen.Bounds);
        IntPtr hMonitor = MonitorFromRect(ref rect, MonitorOptions.MONITOR_DEFAULTTONEAREST);
        return hMonitor;
    }

    public static void MakeBorderless(nint hWnd, bool IsBorderless)
    {
        int currentStyle = GetWindowLong(hWnd, GWL_STYLE);

        if (IsBorderless)
        {
            // Remove the border, caption, and system menu styles
            int newStyle = currentStyle & ~(WS_BORDER | WS_CAPTION | WS_SYSMENU | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX);
            SetWindowLong(hWnd, GWL_STYLE, newStyle);
        }
        else
        {
            // Restore the border, caption, and system menu styles
            int newStyle = currentStyle | WS_BORDER | WS_CAPTION | WS_SYSMENU | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX;
            SetWindowLong(hWnd, GWL_STYLE, newStyle);
        }
    }

    public static void MoveWindow(nint hWnd, Screen targetScreen, WindowPositions position)
    {
        if (hWnd == IntPtr.Zero)
            return;

        // WpfScreenHelper.Screen WpfScreen = WpfScreenHelper.Screen.AllScreens.FirstOrDefault(s => s.DeviceName.Equals(targetScreen.DeviceName));
        // IntPtr monitor = GetScreenHandle(targetScreen);
        // double taskbarHeight = SystemParameters.MaximizedPrimaryScreenHeight - SystemParameters.FullPrimaryScreenHeight;
        Rectangle workingArea = targetScreen.WorkingArea;

        double newWidth = workingArea.Width;
        double newHeight = workingArea.Height;
        double newX = 0;
        double newY = 0;

        switch (position)
        {
            case WindowPositions.Left:
                newWidth /= 2;
                newX = workingArea.Left;
                newY = workingArea.Top;
                break;
            case WindowPositions.Top:
                newHeight /= 2;
                newX = workingArea.Left;
                newY = workingArea.Top;
                break;
            case WindowPositions.Right:
                newWidth /= 2;
                newX = workingArea.Right - newWidth;
                newY = workingArea.Top;
                break;
            case WindowPositions.Bottom:
                newHeight /= 2;
                newX = workingArea.Left;
                newY = workingArea.Top + newHeight;
                break;
            case WindowPositions.TopLeft:
                newWidth /= 2;
                newHeight /= 2;
                newX = workingArea.Left;
                newY = workingArea.Top;
                break;
            case WindowPositions.TopRight:
                newWidth /= 2;
                newHeight /= 2;
                newX = workingArea.Right - newWidth;
                newY = workingArea.Top;
                break;
            case WindowPositions.BottomRight:
                newWidth /= 2;
                newHeight /= 2;
                newX = workingArea.Right - newWidth;
                newY = workingArea.Bottom - newHeight;
                break;
            case WindowPositions.BottomLeft:
                newWidth /= 2;
                newHeight /= 2;
                newX = workingArea.Left;
                newY = workingArea.Bottom - newHeight;
                break;
            default:
            case WindowPositions.Maximize:
                newX = workingArea.Left;
                newY = workingArea.Top;
                break;
        }

        ShowWindow(hWnd, 9);
        MoveWindow(hWnd, (int)newX, (int)newY, (int)newWidth, (int)newHeight, true);

        if (position == WindowPositions.Maximize)
            ShowWindow(hWnd, 3);
    }
}