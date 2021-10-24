using System;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;

namespace ControllerService
{
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

        public static int GetProcessIdByPath(string path)
        {
            IntPtr read = new IntPtr();
            IntPtr write = new IntPtr();
            IntPtr read2 = new IntPtr();
            IntPtr write2 = new IntPtr();
            SECURITY_ATTRIBUTES saAttr = new SECURITY_ATTRIBUTES();
            saAttr.nLength = Marshal.SizeOf(typeof(SECURITY_ATTRIBUTES));
            saAttr.bInheritHandle = 1;
            saAttr.lpSecurityDescriptor = IntPtr.Zero;

            CreatePipe(ref read, ref write, ref saAttr, 0);
            CreatePipe(ref read2, ref write2, ref saAttr, 0);

            uint CREATE_NO_WINDOW = 0x08000000;
            int STARTF_USESTDHANDLES = 0x00000100;
            STARTUPINFO si = new STARTUPINFO();
            si.cb = Marshal.SizeOf(typeof(STARTUPINFO));
            si.hStdOutput = write;
            si.hStdError = write;
            si.hStdInput = read2;
            si.lpDesktop = "Winsta0\\default";
            si.dwFlags = STARTF_USESTDHANDLES;
            PROCESS_INFORMATION pi;

            IntPtr hToken;
            bool err = WTSQueryUserToken(WTSGetActiveConsoleSessionId(), out hToken);
            if (CreateProcessAsUser(hToken, path, null, IntPtr.Zero, IntPtr.Zero, true, CREATE_NO_WINDOW, IntPtr.Zero, IntPtr.Zero, ref si, out pi))
            {
                uint ret = WaitForSingleObject(pi.hProcess, 2000); //wait for the child process exit.
                if (ret == 0)
                {
                    byte[] title = new byte[10];
                    uint reads = 0;
                    CloseHandle(write);
                    err = ReadFile(read, title, 10, out reads, IntPtr.Zero);
                    string result = System.Text.Encoding.UTF8.GetString(title).Replace("\0", "").Replace("\r", "").Replace("\n", "");

                    int ProcessId;
                    int.TryParse(result, out ProcessId);
                    return ProcessId;
                }
            }
            CloseHandle(read2);
            CloseHandle(write2);
            CloseHandle(read);

            return 0;
        }

        public static string GetProcessPath(int ProcessId)
        {
            var wmiQueryString = $"SELECT ProcessId, ExecutablePath, CommandLine FROM Win32_Process WHERE ProcessId = {ProcessId}";
            using (var searcher = new ManagementObjectSearcher(wmiQueryString))
            using (var results = searcher.Get())
            {
                var query = from p in Process.GetProcesses()
                            join mo in results.Cast<ManagementObject>()
                            on p.Id equals (int)(uint)mo["ProcessId"]
                            select new
                            {
                                Process = p,
                                Path = (string)mo["ExecutablePath"],
                                CommandLine = (string)mo["CommandLine"],
                            };
                foreach (var item in query)
                    return item.Path.ToLower();
            }
            return "";
        }

        public static byte NormalizeInput(short input)
        {
            input = (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, input));
            float output = (float)input / (float)ushort.MaxValue * (float)byte.MaxValue + (float)(byte.MaxValue / 2.0f);
            return (byte)Math.Round(output);
        }
    }
}
