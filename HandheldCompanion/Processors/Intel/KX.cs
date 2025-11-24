using HandheldCompanion.Shared;
using HandheldCompanion.Utils;
using System;
using System.Diagnostics;
using System.IO;

namespace HandheldCompanion.Processors.Intel;

public class KX
{
    // Package Power Limit (PACKAGE_RAPL_LIMIT_0_0_0_MCHBAR_PCU) — Offset 59A0h
    private const string pnt_limit = "59";
    private const string pnt_clock = "94";

    private string[] mchbar_addresses = new string[] { "0xfedc0000", "0xfed10000" };
    private string mchbar = string.Empty;
    private readonly string path;
    private readonly ProcessStartInfo startInfo;

    public enum IntelUndervoltRail
    {
        Core,
        Gpu,
        Cache,
        SystemAgent
    }

    public KX()
    {
        path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Intel", "KX", "KX.exe");

        if (!File.Exists(path))
        {
            LogManager.LogError("KX.exe is missing. Power Manager won't work");
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
            foreach (string address in mchbar_addresses)
            {
                startInfo.Arguments = $"/rdmem32 {address}";
                using (Process? ProcessOutput = Process.Start(startInfo))
                {
                    while (!ProcessOutput.StandardOutput.EndOfStream)
                    {
                        string line = ProcessOutput.StandardOutput.ReadLine();
                        if (string.IsNullOrEmpty(line))
                            continue;

                        if (!line.Contains("Return"))
                            continue;

                        // parse result
                        line = CommonUtils.Between(line, "Return ");
                        long returned = long.Parse(line);

                        // check if mchbar is null
                        if (returned == 0xFFFFFFFF)
                            continue;

                        // store mchbar and leave loop
                        mchbar = address + pnt_limit;
                        return true;
                    }
                }
            }
        }
        catch { }

        return false;
    }

    [Obsolete("This function is deprecated and will be removed in future versions.")]
    internal bool init_legacy()
    {
        if (startInfo is null)
            return false;

        try
        {
            startInfo.Arguments = "/RdPci32 0 0 0 0x48";
            using (var ProcessOutput = Process.Start(startInfo))
            {
                while (!ProcessOutput.StandardOutput.EndOfStream)
                {
                    string? line = ProcessOutput.StandardOutput.ReadLine();
                    if (string.IsNullOrEmpty(line))
                        continue;

                    if (line.Contains("Return"))
                        continue;

                    // parse result
                    line = CommonUtils.Between(line, "Return ");
                    long returned = long.Parse(line);
                    string output = "0x" + returned.ToString("X2").Substring(0, 4);

                    // store mchbar and leave loop
                    mchbar = output + pnt_limit;
                    return true;
                }
            }
        }
        catch { }

        return false;
    }

    internal int get_short_limit(bool msr)
    {
        switch (msr)
        {
            default:
            case false:
                return get_limit("a4");
            case true:
                return get_msr_limit(0);
        }
    }

    internal int get_long_limit(bool msr)
    {
        switch (msr)
        {
            default:
            case false:
                return get_limit("a0");
            case true:
                return get_msr_limit(1);
        }
    }

    internal int get_limit(string pointer)
    {
        if (string.IsNullOrEmpty(mchbar))
            return -1; // failed

        startInfo.Arguments = $"/rdmem16 {mchbar}{pointer}";
        using (var ProcessOutput = Process.Start(startInfo))
        {
            try
            {
                while (!ProcessOutput.StandardOutput.EndOfStream)
                {
                    string? line = ProcessOutput.StandardOutput.ReadLine();
                    if (string.IsNullOrEmpty(line))
                        continue;

                    if (line.Contains("Return"))
                        continue;

                    // parse result
                    line = CommonUtils.Between(line, "Return ");
                    long returned = long.Parse(line);
                    double output = ((double)returned + short.MinValue) / 8.0d;

                    return (int)output;
                }
            }
            catch { }
        }

        return -1; // failed
    }

    internal int get_msr_limit(int pointer)
    {
        if (string.IsNullOrEmpty(mchbar))
            return -1; // failed

        startInfo.Arguments = "/rdmsr 0x610";
        using (var ProcessOutput = Process.Start(startInfo))
        {
            try
            {
                while (!ProcessOutput.StandardOutput.EndOfStream)
                {
                    string? line = ProcessOutput.StandardOutput.ReadLine();
                    if (string.IsNullOrEmpty(line))
                        continue;

                    if (line.Contains("Return"))
                        continue;

                    // parse result
                    line = CommonUtils.Between(line, "Msr Data     : ");

                    string[] values = line.Split(" ");
                    string hex = values[pointer];
                    hex = values[pointer].Substring(hex.Length - 3);
                    int output = Convert.ToInt32(hex, 16) / 8;

                    return output;
                }
            }
            catch { }
        }

        return -1; // failed
    }

    internal int get_short_value()
    {
        return -1; // not supported
    }

    internal int get_long_value()
    {
        return -1; // not supported
    }

    internal int set_short_limit(int limit)
    {
        return set_limit("a4", limit);
    }

    internal int set_long_limit(int limit)
    {
        return set_limit("a0", limit);
    }

    internal int set_limit(string pointer1, int limit)
    {
        if (string.IsNullOrEmpty(mchbar))
            return -1; // failed

        string hex = TDPToHex(limit);

        // register command
        startInfo.Arguments = $"/wrmem16 {mchbar}{pointer1} 0x8{hex.Substring(0, 1)}{hex.Substring(1)}";
        using (var ProcessOutput = Process.Start(startInfo))
        {
            string? line = ProcessOutput.StandardOutput.ReadLine();
            if (string.IsNullOrEmpty(line))
                return 0;
        }

        return -1; // implement error code support
    }

    internal int set_msr_limits(int PL1, int PL2)
    {
        if (string.IsNullOrEmpty(mchbar))
            return -1; // failed

        string hexPL1 = TDPToHex(PL1);
        string hexPL2 = TDPToHex(PL2);

        // register command
        startInfo.Arguments = $"/wrmsr 0x610 0x00438{hexPL2} 00DD8{hexPL1}";
        using (var ProcessOutput = Process.Start(startInfo))
        {
            string? line = ProcessOutput.StandardOutput.ReadLine();
            if (string.IsNullOrEmpty(line))
                return 0; // success
        }

        return -1; // implement error code support
    }

    public int set_msr_undervolt(string commandHex, int offsetMv)
    {
        if (string.IsNullOrEmpty(mchbar))
            return -1; // failed

        // Encode mV to Cyphray-style 12-bit VID code (HWiNFO-accurate mode)
        // offsetMv is expected to be negative for undervolt, but we just use the magnitude.
        int magMv = Math.Abs(offsetMv);

        int code = 0;
        if (magMv != 0)
        {
            // dcuv5 = 4096 - (dcuv * 1024 + 500) / 1000 * 2   (integer math)
            int scaled = (magMv * 1024 + 500) / 1000;
            code = 4096 - (scaled * 2);
        }

        // 12-bit code: 3-digit hex (e.g. "F9C")
        string vidHex = code.ToString("X3");

        // Build data field: 0x<VID>00000 (e.g. 0xF9C00000)
        string dataHex = $"0x{vidHex}00000";

        // Call KX: /wrmsr 0x150 <commandHex> <dataHex>
        startInfo.Arguments = $"/wrmsr 0x150 {commandHex} {dataHex}";

        using (var processOutput = Process.Start(startInfo))
        {
            string? line = processOutput?.StandardOutput.ReadLine();
            if (string.IsNullOrEmpty(line))
                return 0; // success
        }

        return -1; // implement error code support
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

    internal int set_gfx_clk(int clock)
    {
        if (string.IsNullOrEmpty(mchbar))
            return -1; // failed

        string hex = ClockToHex(clock);

        startInfo.Arguments = $"/wrmem8 {mchbar}{pnt_clock} {hex}";
        using (var ProcessOutput = Process.Start(startInfo))
        {
            string? line = ProcessOutput.StandardOutput.ReadLine();
            if (string.IsNullOrEmpty(line))
                return 0;
        }

        return -1; // implement error code support
    }

    internal int get_gfx_clk()
    {
        if (string.IsNullOrEmpty(mchbar))
            return -1; // failed

        startInfo.Arguments = $"/rdmem8 {mchbar}{pnt_clock}";
        using (var ProcessOutput = Process.Start(startInfo))
        {
            try
            {
                while (!ProcessOutput.StandardOutput.EndOfStream)
                {
                    string? line = ProcessOutput.StandardOutput.ReadLine();
                    if (string.IsNullOrEmpty(line))
                        continue;

                    if (line.Contains("Return"))
                        continue;

                    // parse result
                    line = CommonUtils.Between(line, "Return ");
                    int returned = int.Parse(line);
                    int clock = returned * 50;

                    return clock;
                }
            }
            catch { }
        }

        return -1; // failed
    }
}