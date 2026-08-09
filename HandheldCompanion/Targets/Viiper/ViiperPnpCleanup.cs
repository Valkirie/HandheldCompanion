using HandheldCompanion.Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace HandheldCompanion.Targets.Viiper;

internal static class ViiperPnpCleanup
{
    private static readonly (ushort Vid, ushort Pid)[] KnownVidPids =
    {
        (0x045E, 0x028E),
        (0x045E, 0x02D1),
        (0x045E, 0x02D2),
        (0x045E, 0x0B00),
        (0x045E, 0x0B13),
        (0x054C, 0x05C4),
        (0x054C, 0x0CE6),
        (0x054C, 0x0DF2),
        (0x057E, 0x2006),
        (0x057E, 0x2007),
        (0x057E, 0x2009),
        (0x057E, 0x2069),
        (0x28DE, 0x1102),
        (0x28DE, 0x1205),
        (0x28DE, 0x12F0),
        (0x28DE, 0x12FA),
        (0x28DE, 0x12FB),
        (0x28DE, 0x12FC),
        (0x28DE, 0x12FD),
        (0x28DE, 0x12FE),
        (0x28DE, 0x12FF),
    };

    public static void CleanupAllKnownGhosts()
    {
        _ = Task.Run(() => RunCleanup(KnownVidPids));
    }

    public static void CleanupGhosts(ushort vendorId, ushort productId)
    {
        _ = Task.Run(() => RunCleanup(new[] { (vendorId, productId) }));
    }

    public static void CleanupPresentViiperPhantomsBlocking()
    {
        try
        {
            var raw = RunPnputil("/enum-devices /connected");
            if (string.IsNullOrWhiteSpace(raw))
                return;

            var prefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (vid, pid) in KnownVidPids)
                prefixes.Add($"VID_{vid:X4}&PID_{pid:X4}");

            var instanceIdRegex = new Regex(@"Instance ID:\s+(\S+)", RegexOptions.IgnoreCase);
            var candidates = new List<string>();
            foreach (var block in raw.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                var match = instanceIdRegex.Match(block);
                if (!match.Success)
                    continue;

                var instanceId = match.Groups[1].Value;
                if (!instanceId.StartsWith("USB\\", StringComparison.OrdinalIgnoreCase))
                    continue;

                var matchesTarget = false;
                foreach (var prefix in prefixes)
                {
                    if (instanceId.IndexOf(prefix, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        matchesTarget = true;
                        break;
                    }
                }

                if (matchesTarget && IsSoftwareBusRooted(instanceId))
                    candidates.Add(instanceId);
            }

            foreach (var instanceId in candidates)
                RemoveDevice(instanceId);
        }
        catch (Exception ex)
        {
            LogManager.LogDebug("VIIPER connected-device cleanup failed: {0}", ex.Message);
        }
    }

    private static void RunCleanup(IEnumerable<(ushort Vid, ushort Pid)> vidPids)
    {
        try
        {
            var raw = RunPnputil("/enum-devices /disconnected");
            if (string.IsNullOrWhiteSpace(raw))
                return;

            var prefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (vid, pid) in vidPids)
                prefixes.Add($"VID_{vid:X4}&PID_{pid:X4}");

            var instanceIdRegex = new Regex(@"Instance ID:\s+(\S+)", RegexOptions.IgnoreCase);
            var candidates = new List<string>();
            foreach (Match match in instanceIdRegex.Matches(raw))
            {
                var instanceId = match.Groups[1].Value;
                foreach (var prefix in prefixes)
                {
                    if (instanceId.IndexOf(prefix, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        candidates.Add(instanceId);
                        break;
                    }
                }
            }

            foreach (var instanceId in candidates)
                RemoveDevice(instanceId);
        }
        catch (Exception ex)
        {
            LogManager.LogDebug("VIIPER disconnected-device cleanup failed: {0}", ex.Message);
        }
    }

    private static void RemoveDevice(string instanceId)
    {
        var output = RunPnputil($"/remove-device \"{instanceId}\"");
        LogManager.LogDebug("pnputil removed VIIPER PnP device {0}", instanceId);
    }

    private static string RunPnputil(string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "pnputil.exe",
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            });
            if (process is null)
                return string.Empty;

            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(15000))
            {
                try { process.Kill(); } catch { }
                return string.Empty;
            }

            Task.WaitAll(new Task[] { stdout, stderr }, 3000);
            var output = stdout.Status == TaskStatus.RanToCompletion ? stdout.Result : string.Empty;
            var error = stderr.Status == TaskStatus.RanToCompletion ? stderr.Result : string.Empty;
            if (process.ExitCode != 0 && !string.IsNullOrWhiteSpace(error))
                LogManager.LogDebug("pnputil {0} failed: {1}", arguments, error.Trim());
            return output ?? string.Empty;
        }
        catch (Exception ex)
        {
            LogManager.LogDebug("pnputil {0} failed to execute: {1}", arguments, ex.Message);
            return string.Empty;
        }
    }

    private static bool IsSoftwareBusRooted(string deviceInstanceId)
    {
        try
        {
            if (CM_Locate_DevNodeW(out var devInst, deviceInstanceId, 0) != 0)
                return false;

            var idBuffer = new StringBuilder(512);
            for (var depth = 0; depth < 16; depth++)
            {
                idBuffer.Clear();
                if (CM_Get_Device_IDW(devInst, idBuffer, idBuffer.Capacity, 0) != 0)
                    return false;

                var nodeId = idBuffer.ToString();
                if (nodeId.StartsWith("PCI\\", StringComparison.OrdinalIgnoreCase))
                    return false;
                if (nodeId.StartsWith("ROOT\\USB\\", StringComparison.OrdinalIgnoreCase))
                    return true;

                if (CM_Get_Parent(out var parent, devInst, 0) != 0 || parent == devInst)
                    return false;
                devInst = parent;
            }
        }
        catch (Exception ex)
        {
            LogManager.LogDebug("VIIPER PnP parent lookup failed: {0}", ex.Message);
        }

        return false;
    }

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern int CM_Locate_DevNodeW(out uint devInst, string deviceInstanceId, int flags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern int CM_Get_Device_IDW(uint devInst, StringBuilder buffer, int length, int flags);

    [DllImport("cfgmgr32.dll")]
    private static extern int CM_Get_Parent(out uint parent, uint devInst, int flags);
}
