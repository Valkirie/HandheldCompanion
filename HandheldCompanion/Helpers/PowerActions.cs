using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HandheldCompanion.Helpers
{
    public static class PowerActions
    {
        // ---------- Public API ----------
        public static void Lock()
        {
            if (!LockWorkStation())
                ThrowLastError("LockWorkStation failed");
        }

        /// <summary>
        /// Put the system to Sleep (S3). If hybrid sleep is enabled by policy, Windows may hibernate instead.
        /// </summary>
        public static void Sleep(bool force = false, bool disableWakeEvent = false)
        {
            // SetSuspendState: (hibernate, forceCritical, disableWakeEvent)
            if (!SetSuspendState(false, force, disableWakeEvent))
            {
                // If this returns false with no last error, Windows often returns "operation canceled" (e.g., veto by a driver).
                var err = Marshal.GetLastWin32Error();
                if (err != 0)
                    ThrowLastError("SetSuspendState (Sleep) failed", err);
            }
        }

        /// <summary>
        /// Hibernate (S4).
        /// </summary>
        public static void Hibernate(bool force = false, bool disableWakeEvent = false)
        {
            if (!SetSuspendState(true, force, disableWakeEvent))
            {
                var err = Marshal.GetLastWin32Error();
                if (err != 0)
                    ThrowLastError("SetSuspendState (Hibernate) failed", err);
            }
        }

        public static void Restart(bool force = false)
        {
            EnsureShutdownPrivilege();
            uint flags = EWX_REBOOT | (force ? EWX_FORCE : EWX_FORCEIFHUNG);
            if (!ExitWindowsEx(flags, SHTDN_REASON_MAJOR_APPLICATION | SHTDN_REASON_FLAG_PLANNED))
                ThrowLastError("ExitWindowsEx (Restart) failed");
        }

        public static void Shutdown(bool force = false, bool powerOff = true)
        {
            EnsureShutdownPrivilege();
            uint flags = (powerOff ? EWX_POWEROFF : EWX_SHUTDOWN) | (force ? EWX_FORCE : EWX_FORCEIFHUNG);
            if (!ExitWindowsEx(flags, SHTDN_REASON_MAJOR_APPLICATION | SHTDN_REASON_FLAG_PLANNED))
                ThrowLastError("ExitWindowsEx (Shutdown) failed");
        }

        // ---------- Privilege enable (SeShutdownPrivilege) ----------
        private static void EnsureShutdownPrivilege()
        {
            if (!OpenProcessToken(Process.GetCurrentProcess().Handle, TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out var hToken))
                ThrowLastError("OpenProcessToken failed");

            try
            {
                if (!LookupPrivilegeValue(null, "SeShutdownPrivilege", out var luid))
                    ThrowLastError("LookupPrivilegeValue(SeShutdownPrivilege) failed");

                var tp = new TOKEN_PRIVILEGES
                {
                    PrivilegeCount = 1,
                    Privileges = new LUID_AND_ATTRIBUTES[1]
                };
                tp.Privileges[0].Luid = luid;
                tp.Privileges[0].Attributes = SE_PRIVILEGE_ENABLED;

                if (!AdjustTokenPrivileges(hToken, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero))
                    ThrowLastError("AdjustTokenPrivileges failed");

                // Even if AdjustTokenPrivileges returns true, it can set ERROR_NOT_ALL_ASSIGNED
                int err = Marshal.GetLastWin32Error();
                if (err == ERROR_NOT_ALL_ASSIGNED)
                    throw new Win32Exception(err, "SeShutdownPrivilege not assigned to this process (are you elevated and allowed?)");
            }
            finally
            {
                CloseHandle(hToken);
            }
        }

        // ---------- Error helper ----------
        private static void ThrowLastError(string message, int? code = null)
        {
            int err = code ?? Marshal.GetLastWin32Error();
            throw new Win32Exception(err, $"{message}. Win32Error={err}");
        }

        // ---------- P/Invoke ----------
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ExitWindowsEx(uint uFlags, uint dwReason);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool LockWorkStation();

        [DllImport("powrprof.dll", SetLastError = true)]
        private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool LookupPrivilegeValue(string lpSystemName, string lpName, out LUID lpLuid);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool AdjustTokenPrivileges(IntPtr TokenHandle, bool DisableAllPrivileges,
            ref TOKEN_PRIVILEGES NewState, int BufferLength, IntPtr PreviousState, IntPtr ReturnLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        // ---------- Win32 constants & structs ----------
        private const uint EWX_LOGOFF = 0x00000000;
        private const uint EWX_SHUTDOWN = 0x00000001;
        private const uint EWX_REBOOT = 0x00000002;
        private const uint EWX_FORCE = 0x00000004;
        private const uint EWX_POWEROFF = 0x00000008;
        private const uint EWX_FORCEIFHUNG = 0x00000010;

        private const uint SHTDN_REASON_MAJOR_APPLICATION = 0x00040000;
        private const uint SHTDN_REASON_FLAG_PLANNED = 0x80000000;

        private const int ERROR_NOT_ALL_ASSIGNED = 1300;

        private const uint TOKEN_QUERY = 0x0008;
        private const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
        private const uint SE_PRIVILEGE_ENABLED = 0x00000002;

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID
        {
            public uint LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID_AND_ATTRIBUTES
        {
            public LUID Luid;
            public uint Attributes;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TOKEN_PRIVILEGES
        {
            public uint PrivilegeCount;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
            public LUID_AND_ATTRIBUTES[] Privileges;
        }
    }
}
