using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.RegularExpressions;

namespace HandheldCompanion.Utils;

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

    public static int rgb_to_int(byte led_r, byte led_g, byte led_b)
    {
        int colour = 0;
        colour = (led_r << 16) | (led_g << 8) | led_b;
        return colour;
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
}