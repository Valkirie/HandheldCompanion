using System;
using System.Diagnostics;
using System.IO;
using HandheldCompanion.Managers;

namespace HandheldCompanion.Processors.Intel;

public class RW
{
    // Package Power Limit (PACKAGE_RAPL_LIMIT_0_0_0_MCHBAR_PCU) — Offset 59A0h
    private const string pnt_limit = "59";
    private const string pnt_clock = "94";
    private const int delay_value = 1000;

    private string mchbar;
    private readonly string path;
    private readonly ProcessStartInfo startInfo;

    public RW()
    {
        path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Intel", "RW", "Rw.exe");

        if (!File.Exists(path))
        {
            LogManager.LogError("Rw.exe is missing. Power Manager won't work");
            return;
        }

        startInfo = new ProcessStartInfo(path)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
    }

    internal bool init()
    {
        if (startInfo is null)
            return false;

        try
        {
            startInfo.Arguments = "/Min /Nologo /Stdout /command=\"rpci32 0 0 0 0x48;rwexit\"";
            using (var ProcessOutput = Process.Start(startInfo))
            {
                while (!ProcessOutput.StandardOutput.EndOfStream)
                {
                    var line = ProcessOutput.StandardOutput.ReadLine();

                    if (!line.Contains("0x"))
                        continue;

                    line = line.Substring(line.Length - 10);
                    mchbar = line.Substring(0, 6);
                    return true;
                }
            }
        }
        catch
        {
        }

        return false;
    }

    internal int get_short_limit()
    {
        return get_limit("a4");
    }

    internal int get_long_limit()
    {
        return get_limit("a0");
    }

    internal int get_limit(string pointer)
    {
        startInfo.Arguments = $"/Min /Nologo /Stdout /command=\"r16 {mchbar}{pnt_limit}{pointer};rwexit\"";
        using (var ProcessOutput = Process.Start(startInfo))
        {
            while (!ProcessOutput.StandardOutput.EndOfStream)
            {
                var line = ProcessOutput.StandardOutput.ReadLine();

                if (!line.Contains("0x"))
                    continue;

                line = line.Substring(line.Length - 6);
                var value = Convert.ToInt32(line, 16);
                var output = ((double)value + short.MinValue) / 8.0d;
                return (int)output;
            }
        }

        return 0;
    }

    internal int get_short_value()
    {
        return 0;
    }

    internal int get_long_value()
    {
        return 0;
    }

    internal void set_short_limit(int limit)
    {
        set_limit("a4", "0x00438", limit);
    }

    internal void set_long_limit(int limit)
    {
        set_limit("a0", "0x00dd8", limit);
    }

    internal void set_limit(string pointer1, string pointer2, int limit)
    {
        var command = $"/Min /Nologo /Stdout /command=\"Delay {delay_value};";
        var hex = TDPToHex(limit);

        command += $"w16 {mchbar}{pnt_limit}{pointer1} 0x8{hex.Substring(0, 1)}{hex.Substring(1)};";
        command += $"wrmsr 0x610 0x0 {pointer2}{hex};";

        command += $"Delay {delay_value};rwexit\"";

        startInfo.Arguments = command;
        using (var ProcessOutput = Process.Start(startInfo))
        {
            while (!ProcessOutput.StandardOutput.EndOfStream)
            {
                var line = ProcessOutput.StandardOutput.ReadLine();
            }
        }
    }

    internal void set_all_limit(int limit)
    {
        var command = $"/Min /Nologo /Stdout /command=\"Delay {delay_value};";
        var hex = TDPToHex(limit);

        // long
        command += $"w16 {mchbar}{pnt_limit}a4 0x8{hex.Substring(0, 1)}{hex.Substring(1)};";
        command += $"wrmsr 0x610 0x0 0x00438{hex.Substring(hex.Length - 3)};";

        // short
        command += $"w16 {mchbar}{pnt_limit}a0 0x8{hex.Substring(0, 1)}{hex.Substring(1)};";
        command += $"wrmsr 0x610 0x0 0x00dd8{hex.Substring(hex.Length - 3)};";

        command += $"Delay {delay_value};rwexit\"";

        startInfo.Arguments = command;
        using (var ProcessOutput = Process.Start(startInfo))
        {
            while (!ProcessOutput.StandardOutput.EndOfStream)
            {
                var line = ProcessOutput.StandardOutput.ReadLine();
            }
        }
    }

    private string TDPToHex(int decValue)
    {
        decValue *= 8;
        var output = decValue.ToString("X3");
        return output;
    }

    private string ClockToHex(int decValue)
    {
        decValue /= 50;
        var output = "0x" + decValue.ToString("X2");
        return output;
    }

    internal void set_gfx_clk(int clock)
    {
        var command = $"/Min /Nologo /Stdout /command=\"Delay {delay_value};";
        var hex = ClockToHex(clock);

        command += $"w {mchbar}{pnt_clock} {hex};";
        command += $"Delay {delay_value};rwexit\"";

        startInfo.Arguments = command;
        using (var ProcessOutput = Process.Start(startInfo))
        {
            while (!ProcessOutput.StandardOutput.EndOfStream)
            {
                var line = ProcessOutput.StandardOutput.ReadLine();
            }
        }
    }
}