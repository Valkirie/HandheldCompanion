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
        if (dateTime == default)
            return string.Empty;

        TimeSpan elapsed = DateTime.Now - dateTime;
        if (elapsed < TimeSpan.Zero)
            elapsed = TimeSpan.Zero;

        double value;
        string format;
        if (elapsed.TotalMinutes < 1)
        {
            value = Math.Max(0, elapsed.Seconds);
            format = Properties.Resources.RelativeTime_SecondsAgo;
        }
        else if (elapsed.TotalHours < 1)
        {
            value = (int)elapsed.TotalMinutes;
            format = Properties.Resources.RelativeTime_MinutesAgo;
        }
        else if (elapsed.TotalDays < 1)
        {
            value = (int)elapsed.TotalHours;
            format = Properties.Resources.RelativeTime_HoursAgo;
        }
        else if (elapsed.TotalDays < 7)
        {
            value = (int)elapsed.TotalDays;
            format = Properties.Resources.RelativeTime_DaysAgo;
        }
        else if (elapsed.TotalDays < 365)
        {
            value = (int)(elapsed.TotalDays / 7);
            format = Properties.Resources.RelativeTime_WeeksAgo;
        }
        else
        {
            value = (int)(elapsed.TotalDays / 365);
            format = Properties.Resources.RelativeTime_YearsAgo;
        }

        return string.Format(CultureInfo.CurrentCulture, format, value);
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