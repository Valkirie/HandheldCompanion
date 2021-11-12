using System;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;

namespace ControllerService
{
    public class ControllerClient
    {
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

        public static string Between(string STR, string FirstString, string LastString)
        {
            string FinalString;
            int Pos1 = STR.IndexOf(FirstString) + FirstString.Length;
            int Pos2 = STR.IndexOf(LastString, Pos1);
            FinalString = STR.Substring(Pos1, Pos2 - Pos1);
            return FinalString;
        }

        private static STARTUPINFO si;
        private static PROCESS_INFORMATION pi;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct STARTUPINFO
        {
            public Int32 cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public Int32 dwX;
            public Int32 dwY;
            public Int32 dwXSize;
            public Int32 dwYSize;
            public Int32 dwXCountChars;
            public Int32 dwYCountChars;
            public Int32 dwFillAttribute;
            public Int32 dwFlags;
            public Int16 wShowWindow;
            public Int16 cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public Int32 dwProcessId;
            public Int32 dwThreadId;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct SECURITY_ATTRIBUTES
        {
            public int nLength;
            public IntPtr lpSecurityDescriptor;
            public int bInheritHandle;
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern bool CreateProcessAsUser(
            IntPtr hToken,
            string lpApplicationName,
            string lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            IntPtr lpCurrentDirectory,
            ref STARTUPINFO lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern bool CreatePipe(
            ref IntPtr hReadPipe,
            ref IntPtr hWritePipe,
            ref SECURITY_ATTRIBUTES lpPipeAttributes,
            uint nSize);
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern bool ReadFile(
            IntPtr hFile,
            byte[] lpBuffer,
            uint nNumberOfBytesToRead,
            out uint lpNumberOfBytesRead,
            IntPtr lpOverlapped);
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern bool CloseHandle(IntPtr hObject);
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern uint WTSGetActiveConsoleSessionId();
        [DllImport("Wtsapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern bool WTSQueryUserToken(UInt32 SessionId, out IntPtr hToken);
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern uint WaitForSingleObject(IntPtr hProcess, uint dwMilliseconds);


        public static bool CreateHelper()
        {
            si = new STARTUPINFO();

            IntPtr hToken;
            bool err = WTSQueryUserToken(WTSGetActiveConsoleSessionId(), out hToken);

            string cmdLine = "";
            if (CreateProcessAsUser(hToken, ControllerService.CurrentPathHelper, $@"""{ControllerService.CurrentPathHelper}"" {cmdLine}", IntPtr.Zero, IntPtr.Zero, true, 0x08000000, IntPtr.Zero, IntPtr.Zero, ref si, out pi))
            {
                uint ret = WaitForSingleObject(pi.hProcess, 2000); //wait for the child process exit.
                if (ret == 0)
                    return true;
            }

            return false;
        }
    }

    public class ControllerHelper
    {
        public static byte NormalizeInput(short input)
        {
            input = (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, input));
            float output = (float)input / (float)ushort.MaxValue * (float)byte.MaxValue + (float)(byte.MaxValue / 2.0f);
            return (byte)Math.Round(output);
        }
    }

    public class Utils
    {
        public static string Between(string STR, string FirstString, string LastString)
        {
            string FinalString;
            int Pos1 = STR.IndexOf(FirstString) + FirstString.Length;
            int Pos2 = STR.IndexOf(LastString, Pos1);
            FinalString = STR.Substring(Pos1, Pos2 - Pos1);
            return FinalString;
        }

        public static string GetMainModuleFilepath(int processId)
        {
            string wmiQueryString = "SELECT ProcessId, ExecutablePath FROM Win32_Process WHERE ProcessId = " + processId;
            using (var searcher = new ManagementObjectSearcher(wmiQueryString))
            {
                using (var results = searcher.Get())
                {
                    ManagementObject mo = results.Cast<ManagementObject>().FirstOrDefault();
                    if (mo != null)
                    {
                        return (string)mo["ExecutablePath"];
                    }
                }
            }
            return null;
        }
    }
}
