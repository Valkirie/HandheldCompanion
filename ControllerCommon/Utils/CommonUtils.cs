using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.RegularExpressions;

namespace ControllerCommon.Utils;

public static class CommonUtils
{
    public static string Between(string STR, string FirstString, string LastString = null, bool KeepBorders = false)
    {
        if (string.IsNullOrEmpty(STR))
            return string.Empty;

        string FinalString;
        var Pos1 = STR.IndexOf(FirstString) + FirstString.Length;
        var Pos2 = STR.Length;

        if (LastString is not null)
            Pos2 = STR.IndexOf(LastString, Pos1);

        FinalString = STR.Substring(Pos1, Pos2 - Pos1);
        return KeepBorders ? FirstString + FinalString + LastString : FinalString;
    }

    public static string RegexReplace(string inputRaw, string pattern, string replacement)
    {
        List<string> outputRaw = new();
        using (var reader = new StringReader(inputRaw))
        {
            string line;
            while ((line = reader.ReadLine()) != null) outputRaw.Add(Regex.Replace(line, pattern, replacement));
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

    public static bool IsFileWritable(string filePath, bool deleteMe = false)
    {
        try
        {
            if (File.Exists(filePath))
            {
                using (var fs = new FileStream(filePath, FileMode.Open))
                {
                    return fs.CanWrite;
                }
            }

            var CanWrite = false;
            using (var fs = new FileStream(filePath, FileMode.Create))
            {
                CanWrite = fs.CanWrite;
            }

            if (deleteMe)
                File.Delete(filePath);

            return CanWrite;
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
            using (var fs = File.Create(Path.Combine(dirPath, Path.GetRandomFileName()), 1, FileOptions.DeleteOnClose))
            {
                return true;
            }
        }
        catch
        {
            return false;
        }
    }

    public static void SetDirectoryWritable(string processpath)
    {
        var rootDirectory = new DirectoryInfo(processpath);
        var directorySecurity = rootDirectory.GetAccessControl();

        var everyone = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
        var adminitrators = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);

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

        directorySecurity.SetAccessRuleProtection(true, false);

        rootDirectory.SetAccessControl(directorySecurity);
    }

    public static void SetFileWritable(string processpath)
    {
        var rootFile = new FileInfo(processpath);
        var fileSecurity = rootFile.GetAccessControl();

        var everyone = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
        var adminitrators = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);

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

        fileSecurity.SetAccessRuleProtection(true, false);

        rootFile.SetAccessControl(fileSecurity);
    }
}