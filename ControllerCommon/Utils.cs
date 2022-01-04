using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using Windows.System.Diagnostics;

namespace ControllerCommon
{
    public class WinAPIFunctions
    {
        //Used to get Handle for Foreground Window
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr GetForegroundWindow();

        //Used to get ID of any Window
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);
        public delegate bool WindowEnumProc(IntPtr hwnd, IntPtr lparam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumChildWindows(IntPtr hwnd, WindowEnumProc callback, IntPtr lParam);

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

    public class FindHostedProcess
    {
        public ProcessDiagnosticInfo Process { get; private set; }

        public FindHostedProcess()
        {
            try
            {
                var foregroundProcessID = WinAPIFunctions.GetforegroundWindow();
                Process = ProcessDiagnosticInfo.TryGetForProcessId((uint)WinAPIFunctions.GetWindowProcessId(foregroundProcessID));

                // Get real process
                if (Process.ExecutableFileName == "ApplicationFrameHost.exe")
                    WinAPIFunctions.EnumChildWindows(foregroundProcessID, ChildWindowCallback, IntPtr.Zero);
            }
            catch (Exception)
            {
                Process = null;
            }
        }

        private bool ChildWindowCallback(IntPtr hwnd, IntPtr lparam)
        {
            var process = ProcessDiagnosticInfo.TryGetForProcessId((uint)WinAPIFunctions.GetWindowProcessId(hwnd));

            if (process.ExecutableFileName != "ApplicationFrameHost.exe")
                Process = process;

            return true;
        }
    }

    public enum GamepadButtonFlags : uint
    {
        [Description("DPad Up")]
        DPadUp = 1,
        [Description("DPad Down")]
        DPadDown = 2,
        [Description("DPad Left")]
        DPadLeft = 4,
        [Description("DPad Right")]
        DPadRight = 8,
        [Description("Start")]
        Start = 16,
        [Description("Back")]
        Back = 32,
        [Description("Left Thumb")]
        LeftThumb = 64,
        [Description("Right Thumb")]
        RightThumb = 128,
        [Description("Left Shoulder")]
        LeftShoulder = 256,
        [Description("Right Shoulder")]
        RightShoulder = 512,
        [Description("Left Trigger")]
        LeftTrigger = 1024,     // specific
        [Description("Right Trigger")]
        RightTrigger = 2048,    // specific
        [Description("A")]
        A = 4096,
        [Description("B")]
        B = 8192,
        [Description("X")]
        X = 16384,
        [Description("Y")]
        Y = 32768,
        [Description("Always On")]
        AlwaysOn = 65536        // specific
    }

    public static class Utils
    {
        #region imports
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
        [DllImport("Kernel32.dll")]
        static extern uint QueryFullProcessImageName(IntPtr hProcess, uint flags, StringBuilder text, out uint size);
        #endregion

        public static string GetDescriptionFromEnumValue(Enum value)
        {
            DescriptionAttribute attribute = value.GetType()
                .GetField(value.ToString())
                .GetCustomAttributes(typeof(DescriptionAttribute), false)
                .SingleOrDefault() as DescriptionAttribute;
            return attribute == null ? value.ToString() : attribute.Description;
        }

        public static T GetEnumValueFromDescription<T>(string description)
        {
            var type = typeof(T);
            if (!type.IsEnum)
                throw new ArgumentException();
            FieldInfo[] fields = type.GetFields();
            var field = fields
                            .SelectMany(f => f.GetCustomAttributes(
                                typeof(DescriptionAttribute), false), (
                                    f, a) => new { Field = f, Att = a })
                            .Where(a => ((DescriptionAttribute)a.Att)
                                .Description == description).SingleOrDefault();
            return field == null ? default(T) : (T)field.Field.GetRawConstantValue();
        }

        public static void SendToast(string title, string content, string img = "Toast")
        {
            string url = $"file:///{AppDomain.CurrentDomain.BaseDirectory}Resources\\{img}.png";
            var uri = new Uri(url);

            new ToastContentBuilder()
                .AddText(title)
                .AddText(content)
                .AddAppLogoOverride(uri, ToastGenericAppLogoCrop.Circle)
                .SetToastDuration(ToastDuration.Short)
                .Show();
        }

        public static string Between(string STR, string FirstString, string LastString)
        {
            string FinalString;
            int Pos1 = STR.IndexOf(FirstString) + FirstString.Length;
            int Pos2 = STR.IndexOf(LastString, Pos1);
            FinalString = STR.Substring(Pos1, Pos2 - Pos1);
            return FinalString;
        }

        public static byte NormalizeInput(short input)
        {
            input = (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, input));
            float output = (float)input / (float)ushort.MaxValue * (float)byte.MaxValue + (float)(byte.MaxValue / 2.0f);
            return (byte)Math.Round(output);
        }

        public static short ComputeInput(short value, float input, float sensivity, float curve)
        {
            float compute = (float)(Math.Sign(input) * Math.Pow(Math.Abs(input) / 20.0f, curve) * 20.0f);
            return (short)Math.Max(-32767, Math.Min(32767, value + compute * sensivity));
        }

        public static bool IsTextAValidIPAddress(string text)
        {
            return IPAddress.TryParse(text, out _);
        }

        public static string GetPathToApp(Process process)
        {
            try
            {
                return process.MainModule.FileName;
            }
            catch
            {
                string query = "SELECT ExecutablePath, ProcessID FROM Win32_Process";
                ManagementObjectSearcher searcher = new(query);

                foreach (ManagementObject item in searcher.Get())
                {
                    object id = item["ProcessID"];
                    object path = item["ExecutablePath"];

                    if (path != null && id.ToString() == process.Id.ToString())
                    {
                        return path.ToString();
                    }
                }
            }

            return "";
        }

        public static bool IsAdministrator()
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        public static void OpenUrl(string url)
        {
            try
            {
                Process.Start(url);
            }
            catch
            {
                // hack because of this: https://github.com/dotnet/corefx/issues/10361
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    url = url.Replace("&", "^&");
                    Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("xdg-open", url);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", url);
                }
                else
                {
                    throw;
                }
            }
        }

        public static bool IsFileWritable(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    using (var fs = new FileStream(filePath, FileMode.Open))
                        return fs.CanWrite;
                }
                else
                {
                    bool CanWrite = false;
                    using (var fs = new FileStream(filePath, FileMode.Create))
                        CanWrite = fs.CanWrite;
                    File.Delete(filePath);
                    return CanWrite;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool IsDirectoryWritable(string dirPath)
        {
            try
            {
                using (FileStream fs = File.Create(Path.Combine(dirPath, Path.GetRandomFileName()), 1, FileOptions.DeleteOnClose))
                    return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public class OneEuroFilterPair
        {
            public const double DEFAULT_WHEEL_CUTOFF = 0.1;
            public const double DEFAULT_WHEEL_BETA = 0.1;

            public OneEuroFilter axis1Filter = new(minCutoff: DEFAULT_WHEEL_CUTOFF, beta: DEFAULT_WHEEL_BETA);
            public OneEuroFilter axis2Filter = new(minCutoff: DEFAULT_WHEEL_CUTOFF, beta: DEFAULT_WHEEL_BETA);
        }

        public class OneEuroFilter3D
        {
            public const double DEFAULT_WHEEL_CUTOFF = 0.4;
            public const double DEFAULT_WHEEL_BETA = 0.2;

            public OneEuroFilter axis1Filter = new(minCutoff: DEFAULT_WHEEL_CUTOFF, beta: DEFAULT_WHEEL_BETA);
            public OneEuroFilter axis2Filter = new(minCutoff: DEFAULT_WHEEL_CUTOFF, beta: DEFAULT_WHEEL_BETA);
            public OneEuroFilter axis3Filter = new(minCutoff: DEFAULT_WHEEL_CUTOFF, beta: DEFAULT_WHEEL_BETA);

            public void SetFilterAttrs(double minCutoff, double beta)
            {
                axis1Filter.MinCutoff = axis2Filter.MinCutoff = axis3Filter.MinCutoff = minCutoff;
                axis1Filter.Beta = axis2Filter.Beta = axis3Filter.Beta = beta;
            }
        }

        public static void SetDirectoryWritable(string processpath)
        {
            var rootDirectory = new DirectoryInfo(processpath);
            var directorySecurity = rootDirectory.GetAccessControl();

            SecurityIdentifier everyone = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
            SecurityIdentifier adminitrators = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);

            directorySecurity.AddAccessRule(
                new FileSystemAccessRule(
                        everyone,
                        FileSystemRights.FullControl,
                        InheritanceFlags.None,
                        PropagationFlags.NoPropagateInherit,
                        AccessControlType.Allow));

            directorySecurity.AddAccessRule(
                new FileSystemAccessRule(
                WindowsIdentity.GetCurrent().Name,
                FileSystemRights.FullControl,
                InheritanceFlags.None,
                PropagationFlags.NoPropagateInherit,
                AccessControlType.Allow));

            directorySecurity.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

            rootDirectory.SetAccessControl(directorySecurity);
        }

        public static void SetFileWritable(string processpath)
        {
            var rootFile = new FileInfo(processpath);
            var fileSecurity = rootFile.GetAccessControl();

            SecurityIdentifier everyone = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
            SecurityIdentifier adminitrators = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);

            fileSecurity.AddAccessRule(
                new FileSystemAccessRule(
                        everyone,
                        FileSystemRights.FullControl,
                        InheritanceFlags.None,
                        PropagationFlags.NoPropagateInherit,
                        AccessControlType.Allow));

            fileSecurity.AddAccessRule(
                new FileSystemAccessRule(
                WindowsIdentity.GetCurrent().Name,
                FileSystemRights.FullControl,
                InheritanceFlags.None,
                PropagationFlags.NoPropagateInherit,
                AccessControlType.Allow));

            fileSecurity.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

            rootFile.SetAccessControl(fileSecurity);
        }
    }
}
