using System;
using System.Drawing;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
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
    public const int WM_KILLFOCUS = 0x0008;
    public const int WM_SHOWWINDOW = 0x0018;
    public const int WM_WINDOWPOSCHANGING = 0x0046;
    public const int WM_SYSCOMMAND = 0x0112;
    public const int WM_INPUTLANGCHANGE = 0x0051;
    public const int WM_DISPLAYCHANGE = 0x007E;
    public const int WM_NCACTIVATE = 0x0086;
    public const int WM_NCHITTEST = 0x0084;
    public const int WM_KEYDOWN = 0x0100;
    public const int WM_CHANGEUISTATE = 0x0127;
    public const int WM_POWERBROADCAST = 0x0218;
    public const int WM_MOUSELEAVE = 0x02A3;
    public const int WM_DPICHANGED = 0x02E0;
    public const int WM_PAINT = 0x000F;
    public const int WM_MOUSEACTIVATE = 0x0021;

    public const int WS_VISIBLE = 0x10000000;
    public const int WS_OVERLAPPED = 0x00000000;

    public const int SC_MOVE = 0xF010;
    public const int SC_CLOSE = 0xF060;
    public const int SW_MAXIMIZE = 3;
    public const int HTCAPTION = 0x02;
    public const int MA_NOACTIVATE = 0x0003;
    public const int MA_NOACTIVATEANDEAT = 4;
    public const int UIS_SET = 1;
    public const int UIS_CLEAR = 2;
    public const int UISF_HIDEFOCUS = 0x1;

    public static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
    public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

    public const int GWL_STYLE = -16;
    private const int WS_BORDER = 0x00800000;
    private const int WS_CAPTION = 0x00C00000;
    private const int WS_SYSMENU = 0x00080000;
    private const int WS_THICKFRAME = 0x00040000;
    private const int WS_MINIMIZEBOX = 0x00020000;
    private const int WS_MAXIMIZEBOX = 0x00010000;
    private const int WS_MAXIMIZE = 0x01000000;

    /// <summary>
    /// Clears the WS_MAXIMIZE style bit from the HWND so that Windows does not show the window
    /// maximized when it is first displayed. Unlike SetWindowPlacement, this does not call
    /// ShowWindow and therefore has no side-effects on WPF's own show pipeline.
    /// Call from OnSourceInitialized, after the HWND exists but before Show().
    /// </summary>
    public static void ClearMaximizeStyle(IntPtr hwnd)
    {
        int style = GetWindowLong(hwnd, GWL_STYLE);
        if ((style & WS_MAXIMIZE) != 0)
            SetWindowLong(hwnd, GWL_STYLE, style & ~WS_MAXIMIZE);
    }

    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_NOACTIVATE = 0x08000000;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int WS_SIZEBOX = 0x00040000;

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

    [DllImport("setupapi.dll", CallingConvention = CallingConvention.Winapi, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetupDiCallClassInstaller(DiFunction installFunction, SafeDeviceInfoSetHandle deviceInfoSet, [In] ref DeviceInfoData deviceInfoData);

    [DllImport("setupapi.dll", CallingConvention = CallingConvention.Winapi, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetupDiEnumDeviceInfo(SafeDeviceInfoSetHandle deviceInfoSet, int memberIndex, ref DeviceInfoData deviceInfoData);

    [DllImport("setupapi.dll", CallingConvention = CallingConvention.Winapi, CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern SafeDeviceInfoSetHandle SetupDiGetClassDevs([In] ref Guid classGuid, [MarshalAs(UnmanagedType.LPWStr)] string? enumerator, IntPtr hwndParent, SetupDiGetClassDevsFlags flags);

    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetupDiGetDeviceInstanceId(IntPtr DeviceInfoSet, ref DeviceInfoData did, [MarshalAs(UnmanagedType.LPTStr)] StringBuilder DeviceInstanceId, int DeviceInstanceIdSize, out int RequiredSize);

    [SuppressUnmanagedCodeSecurity]
    [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
    [DllImport("setupapi.dll", CallingConvention = CallingConvention.Winapi, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

    [DllImport("setupapi.dll", CallingConvention = CallingConvention.Winapi, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetupDiSetClassInstallParams(SafeDeviceInfoSetHandle deviceInfoSet, [In] ref DeviceInfoData deviceInfoData, [In] ref PropertyChangeParameters classInstallParams, int classInstallParamsSize);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern HANDLE GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(nint hWnd);

    public const uint GW_HWNDNEXT = 2;

    [DllImport("user32.dll")]
    public static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    [DllImport("user32.dll")]
    public static extern bool IsWindow(IntPtr hWnd);

    [return: MarshalAs(UnmanagedType.Bool)]
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

    [DllImport("user32.dll", SetLastError = false)]
    public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string className, string? title = null);

    [DllImport("user32.dll")]
    public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    public static IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam)
    {
        return SendMessage(hWnd, unchecked((uint)Msg), wParam, lParam);
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern int GetWindowThreadProcessId(
        HANDLE hWnd,
        out int lpdwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern uint GetWindowThreadProcessId(
        HANDLE hWnd,
        out uint lpdwProcessId);

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

    [DllImport("user32.dll", EntryPoint = "GetDesktopWindow", SetLastError = false)]
    public static extern IntPtr GetDesktopWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DeleteObject(IntPtr hObject);

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

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

    /// <summary>
    /// Returns the top-level window belonging to <paramref name="processId"/> whose title exactly matches
    /// <paramref name="windowTitle"/>, or IntPtr.Zero if none found.
    /// Unlike Process.MainWindowHandle or FindWindow-by-title, this works even when the window is hidden
    /// (tray mode) because it enumerates all top-level windows regardless of visibility.
    /// </summary>
    public static IntPtr FindWindowByProcessId(int processId, string windowTitle)
    {
        IntPtr found = IntPtr.Zero;
        var sb = new System.Text.StringBuilder(256);
        EnumWindows((hWnd, _) =>
        {
            if (GetWindowProcessId(hWnd) != processId)
                return true;

            sb.Clear();
            GetWindowText(hWnd, sb, sb.Capacity);
            if (sb.ToString() == windowTitle)
            {
                found = hWnd;
                return false; // stop enumeration
            }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    public static HANDLE GetforegroundWindow()
    {
        return GetForegroundWindow();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool MoveWindow(nint hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool ShowWindow(nint hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool IsIconic(IntPtr handle);

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

        // get current screen
        Screen currentScreen = Screen.FromHandle(hWnd);
        if ((targetScreen is null || currentScreen.DeviceName.Equals(targetScreen.DeviceName)) && position == WindowPositions.Center)
            return;

        if (targetScreen is null)
            targetScreen = currentScreen;

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