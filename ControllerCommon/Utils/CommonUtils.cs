using SharpDX.Multimedia;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Documents;
using static System.Net.Mime.MediaTypeNames;

namespace ControllerCommon.Utils
{
    public static class CommonUtils
    {
        public static string Between(string STR, string FirstString, string LastString = null, bool KeepBorders = false)
        {
            if (string.IsNullOrEmpty(STR))
                return string.Empty;

            string FinalString;
            int Pos1 = STR.IndexOf(FirstString) + FirstString.Length;
            int Pos2 = STR.Length;

            if (LastString is not null)
                Pos2 = STR.IndexOf(LastString, Pos1);

            FinalString = STR.Substring(Pos1, Pos2 - Pos1);
            return KeepBorders ? FirstString + FinalString + LastString : FinalString;
        }

        public static string RegexReplace(string inputRaw, string pattern, string replacement)
        {
            List<string> outputRaw = new();
            using (StringReader reader = new StringReader(inputRaw))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    outputRaw.Add(Regex.Replace(line, pattern, replacement));
                }
            }
            return string.Join("\n", outputRaw);
        }

        public static bool IsTextAValidIPAddress(string text)
        {
            return IPAddress.TryParse(text, out _);
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
                System.Diagnostics.Process.Start(url);
            }
            catch
            {
                // hack because of this: https://github.com/dotnet/corefx/issues/10361
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    url = url.Replace("&", "^&");
                    System.Diagnostics.Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    System.Diagnostics.Process.Start("xdg-open", url);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    System.Diagnostics.Process.Start("open", url);
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
            catch
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
            catch
            {
                return false;
            }
        }

        public class OneEuroFilterPair
        {
            public const double DEFAULT_WHEEL_CUTOFF = 0.005;
            public const double DEFAULT_WHEEL_BETA = 0.004;

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
