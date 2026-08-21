using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.RegularExpressions;

namespace HandheldCompanion.Utils;

public static class CommonUtils
{
    public static string GetTime(DateTime dateTime)
    {
        return dateTime == default
            ? string.Empty
            : dateTime.ToString("g", CultureInfo.CurrentCulture);
    }

    public static string? Between(string source, string left, string? right = null, bool keepLeftRight = false)
    {
        if (string.IsNullOrEmpty(source))
            return null;

        int leftIdx = source.IndexOf(left, System.StringComparison.Ordinal);
        if (leftIdx < 0)
            return null;

        leftIdx += left.Length;

        int rightIdx = source.Length;
        if (right is not null)
        {
            rightIdx = source.IndexOf(right, leftIdx, System.StringComparison.Ordinal);
            if (rightIdx < 0)
                return null;
        }

        string output = source.Substring(leftIdx, rightIdx - leftIdx);
        return keepLeftRight ? left + output + right : output;
    }

    public static string RegexReplace(string inputRaw, string pattern, string replacement)
    {
        List<string> outputRaw = [];
        using (var reader = new StringReader(inputRaw))
        {
            string? line;
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