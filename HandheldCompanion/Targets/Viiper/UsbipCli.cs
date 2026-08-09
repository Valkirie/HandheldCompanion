using HandheldCompanion.Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace HandheldCompanion.Targets.Viiper
{
    /// <summary>
    /// Thin wrapper over usbip-win2's usbip.exe, used to explicitly attach libviiper's
    /// exported devices to the local UDE bus (and detach them on teardown).
    ///
    /// WHY THIS EXISTS: libviiper's viiper_device_add is *documented* to auto-attach the
    /// device to the local UDE driver, but on some usbip-win2 UDE installs that internal
    /// attach silently no-ops — viiper_device_add returns success and the USBIP server
    /// exports the device on 127.0.0.1:3241, yet no child ever appears under the UDE host
    /// controller and "usbip.exe port" stays empty, so no gamepad reaches Windows.
    /// Driving the standard usbip client against libviiper's own loopback server lands it.
    ///
    /// Verified on-device 2026-07-08 (LeGo2, usbip-win2 0.9.7.8): the internal auto-attach
    /// produced nothing (empty UDE host-controller child list, empty "usbip.exe port"), but
    ///   usbip.exe -t 3241 attach -r 127.0.0.1 -b 1-1
    /// plugged in the emulated DualSense and gamepads worked. This class reproduces that
    /// call after every device add. It is idempotent (devices already attached are skipped),
    /// so on machines where the internal auto-attach *does* work it is a harmless no-op
    /// rather than a duplicate attachment.
    /// </summary>
    internal static class UsbipCli
    {
        // Must match ViiperService.Initialize's listen address.
        private const string Host = "127.0.0.1";
        private const int Port = 3241;

        private static readonly string[] ExePaths =
        {
            @"C:\Program Files\USBip\usbip.exe",
            @"C:\Program Files (x86)\USBip\usbip.exe",
        };

        private static string ResolveExe()
        {
            foreach (var p in ExePaths)
            {
                if (File.Exists(p)) return p;
            }
            return null;
        }

        // Serializes attach passes. Two passes racing (e.g. two hot-swaps a few
        // seconds apart) each saw an empty port list — `usbip port` only reflects an
        // import after Windows finishes enumerating it (1-2s) — and both attached the
        // same exported busid, giving two live imports of one virtual pad (identical
        // duplicate controllers in every tester). Field-confirmed 2026-07-16:
        // two "usbip attach -b 1-1 succeeded" 3s apart, both "newly attached=1".
        private static readonly object AttachSync = new object();

        // Set when the in-process USBIP server (re)starts. Any import of our busids
        // that predates the server is a ZOMBIE: it belongs to a dead helper's server
        // (helper killed/upgraded without DetachAll), shows up as a working pad in
        // XInput, but receives no data - guide presses vanished into it (field report
        // 2026-07-23 after the 2723 upgrade's triple helper restart). The first attach
        // pass after server start detaches such imports and re-attaches fresh.
        private static bool _staleSweepPending;
        public static void NoteServerStarted() { lock (AttachSync) { _staleSweepPending = true; } }

        /// <summary>
        /// Attaches every device libviiper is exporting on its loopback USBIP server that
        /// isn't already imported into the local UDE bus. Best-effort: logs and returns
        /// quietly if usbip.exe is missing or the CLI misbehaves.
        /// </summary>
        public static void AttachExportedDevices()
        {
            string exe = ResolveExe();
            if (exe == null)
            {
                LogManager.LogWarning("usbip.exe not found; cannot attach VIIPER device to the UDE bus.");
                return;
            }

            lock (AttachSync)
            {
                // The server registers the device synchronously inside viiper_device_add, but
                // allow a couple of short retries in case the export list lags right after add.
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    var exported = ListExportedBusIds(exe);
                    if (exported.Count == 0)
                    {
                        System.Threading.Thread.Sleep(200);
                        continue;
                    }

                    var attachedPorts = ListAttachedPortsByBusId(exe);
                    int attachedNow = 0;
                    bool sweepStale = _staleSweepPending;
                    _staleSweepPending = false;
                    foreach (var busId in exported)
                    {
                        if (sweepStale && attachedPorts.TryGetValue(busId, out var stalePorts) && stalePorts.Count > 0)
                        {
                            LogManager.LogInformation("usbip: detaching {0} stale import(s) of {1} from a previous server session", stalePorts.Count, busId);
                            foreach (var stalePort in stalePorts)
                            {
                                string o = Run(exe, $"detach -p {stalePort}");
                                LogManager.LogInformation("usbip detach stale -p {0} -> {1}", stalePort, (o ?? string.Empty).Trim());
                            }
                            attachedPorts.Remove(busId);
                        }
                        if (attachedPorts.TryGetValue(busId, out var ports) && ports.Count > 0)
                        {
                            // Already imported. If it's imported MORE than once (a past race,
                            // or a stale localhost-spelled import from an older build / the
                            // usbip GUI), detach the extras — each duplicate import is a
                            // fully functional clone pad producing identical input.
                            DetachDuplicatePorts(exe, busId, ports);
                            continue;
                        }
                        if (Attach(exe, busId))
                        {
                            attachedNow++;
                            WaitForPortListing(exe, busId);
                        }
                    }
                    LogManager.LogInformation("usbip: exported={0}, newly attached={1}.", exported.Count, attachedNow);
                    return;
                }
                LogManager.LogWarning("usbip: libviiper exported no devices after add (attach skipped).");
            }
        }

        /// <summary>
        /// Blocks (bounded) until `usbip port` reflects the import we just created, so a
        /// subsequent attach pass can't mistake the enumeration window for "not attached"
        /// and import the same busid a second time.
        /// </summary>
        private static void WaitForPortListing(string exe, string busId)
        {
            for (int i = 0; i < 10; i++)
            {
                var ports = ListAttachedPortsByBusId(exe);
                if (ports.TryGetValue(busId, out var list) && list.Count > 0) return;
                System.Threading.Thread.Sleep(300);
            }
            LogManager.LogWarning("usbip: {0} attach succeeded but never appeared in the port listing (3s).", busId);
        }

        private static void DetachDuplicatePorts(string exe, string busId, List<string> ports)
        {
            if (ports.Count <= 1)
            {
                LogManager.LogInformation("usbip: {0} already attached, skipping.", busId);
                return;
            }

            LogManager.LogWarning("usbip: {0} is imported {1} times (duplicate virtual pads) — detaching extras.", busId, ports.Count);
            for (int i = 1; i < ports.Count; i++)
            {
                string o = Run(exe, $"detach -p {ports[i]}");
                LogManager.LogInformation("usbip detach duplicate -p {0} -> {1}", ports[i], (o ?? string.Empty).Trim());
            }
        }

        /// <summary>Detaches every UDE port imported from libviiper's loopback server. Best-effort.</summary>
        // A line references OUR server when it names our port on any loopback spelling.
        // usbip-win2 renders the remote exactly as it was attached, so an import created
        // with "-r localhost" (older builds, the usbip GUI, manual attach) shows as
        // "localhost:3241" while ours show "127.0.0.1:3241" — matching only the literal
        // 127.0.0.1 made those imports invisible to both the attach dedupe and DetachAll,
        // leaving a permanent duplicate pad no restart could clear (field report
        // 2026-07-16: usbip GUI showing localhost:3241 AND 127.0.0.1:3241, each with its
        // own Sony device, three pads in the gamepad tester).
        private static bool IsOurServerLine(string line)
        {
            if (string.IsNullOrEmpty(line)) return false;
            if (line.IndexOf(":" + Port, StringComparison.Ordinal) < 0
                && line.IndexOf("port=" + Port, StringComparison.OrdinalIgnoreCase) < 0)
            {
                // Some usbip-win2 builds print host and port on separate detail lines;
                // fall back to host-only matching for those.
                return line.IndexOf(Host, StringComparison.OrdinalIgnoreCase) >= 0
                    || line.IndexOf("localhost", StringComparison.OrdinalIgnoreCase) >= 0
                    || line.IndexOf("::1", StringComparison.Ordinal) >= 0;
            }
            return line.IndexOf(Host, StringComparison.OrdinalIgnoreCase) >= 0
                || line.IndexOf("localhost", StringComparison.OrdinalIgnoreCase) >= 0
                || line.IndexOf("::1", StringComparison.Ordinal) >= 0;
        }

        /// <summary>Detaches every UDE port imported from libviiper's loopback server (any
        /// loopback spelling), including duplicates. Best-effort.</summary>
        public static void DetachAll()
        {
            string exe = ResolveExe();
            if (exe == null) return;

            string output = Run(exe, $"-t {Port} port");
            if (string.IsNullOrEmpty(output)) return;

            // Walk "Port NN:" blocks, collect the numbers of blocks referencing our loopback
            // server so we never detach an unrelated usbip import the user set up themselves.
            var ports = new List<string>();
            string currentPort = null;
            bool currentIsOurs = false;
            foreach (var raw in output.Split('\n'))
            {
                var pm = Regex.Match(raw, @"Port\s+(\d+)\s*:");
                if (pm.Success)
                {
                    if (currentPort != null && currentIsOurs) ports.Add(currentPort);
                    currentPort = pm.Groups[1].Value;
                    currentIsOurs = false;
                }
                if (IsOurServerLine(raw)) currentIsOurs = true;
            }
            if (currentPort != null && currentIsOurs) ports.Add(currentPort);

            foreach (var p in ports)
            {
                string o = Run(exe, $"detach -p {p}");
                LogManager.LogInformation("usbip detach -p {0} -> {1}", p, (o ?? string.Empty).Trim());
            }
        }

        private static List<string> ListExportedBusIds(string exe)
        {
            var result = new List<string>();
            string output = Run(exe, $"-t {Port} list -r {Host}");
            if (string.IsNullOrEmpty(output)) return result;
            // Exported lines look like: "    1-1    : Sony Corp. : DualSense ... (054c:0df2)"
            foreach (var line in output.Split('\n'))
            {
                var m = Regex.Match(line, @"^\s*(\d+-\d+)\s*:");
                if (m.Success) result.Add(m.Groups[1].Value);
            }
            return result;
        }

        /// <summary>
        /// Maps each of our exported busids to the vhci port numbers currently importing
        /// it. More than one port for the same busid means duplicate imports of the same
        /// virtual pad (see AttachSync / IsOurServerLine) — the caller detaches extras.
        /// </summary>
        private static Dictionary<string, List<string>> ListAttachedPortsByBusId(string exe)
        {
            var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            string output = Run(exe, $"-t {Port} port");
            if (string.IsNullOrEmpty(output)) return map;

            // usbip-win2 port output groups details under "Port NN:" headers, with the
            // remote reference on a line like "1-1 -> usbip://127.0.0.1:3241/1-1". Only
            // trust busids on lines that reference our loopback server, so an unrelated
            // usbip import is never mistaken for one of ours (which would skip a real
            // attach) — but accept any loopback spelling (127.0.0.1/localhost/::1).
            string currentPort = null;
            foreach (var raw in output.Split('\n'))
            {
                var pm = Regex.Match(raw, @"Port\s+(\d+)\s*:");
                if (pm.Success)
                {
                    currentPort = pm.Groups[1].Value;
                }
                if (!IsOurServerLine(raw)) continue;
                var m = Regex.Match(raw, @"(\d+-\d+)");
                if (!m.Success) continue;
                string busId = m.Groups[1].Value;
                if (!map.TryGetValue(busId, out var list))
                {
                    list = new List<string>();
                    map[busId] = list;
                }
                string port = currentPort ?? $"line{list.Count}";
                if (!list.Contains(port)) list.Add(port);
            }
            return map;
        }

        private static bool Attach(string exe, string busId)
        {
            string output = Run(exe, $"-t {Port} attach -r {Host} -b {busId}");
            // usbip attach prints nothing meaningful on success; an "error" line signals failure.
            bool failed = output != null && output.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0;
            if (failed)
            {
                LogManager.LogWarning("usbip attach -b {0} failed: {1}", busId, output.Trim());
                return false;
            }
            LogManager.LogInformation("usbip attach -b {0} succeeded.", busId);
            return true;
        }

        private static string Run(string exe, string args)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };
                using (var proc = Process.Start(psi))
                {
                    if (proc == null) return string.Empty;

                    // Read both streams concurrently, not sequentially - reading stdout to
                    // completion before even starting to drain stderr is the classic .NET
                    // Process deadlock: if usbip.exe writes enough to the unread stream to
                    // fill its OS pipe buffer, it blocks on that write forever, so it never
                    // closes stdout, so ReadToEnd() here never returns either. That hung this
                    // call indefinitely on a misbehaving usbip.exe, which meant the helper's
                    // ProcessExit handler (Environment.Exit -> viiperEmulationManager.Stop() ->
                    // DetachAll() -> here) never completed - the helper process became a
                    // permanent zombie still holding the single-instance mutex and the
                    // scheduled task's "running" state (MultipleInstances=IgnoreNew), so every
                    // later RunTaskNow() silently no-opped until a full reboot or reinstall
                    // reset that state. Reading both streams via Task avoids the deadlock, and
                    // killing the process on timeout stops it becoming a zombie in the first
                    // place.
                    var stdoutTask = proc.StandardOutput.ReadToEndAsync();
                    var stderrTask = proc.StandardError.ReadToEndAsync();
                    bool exited = proc.WaitForExit(15000);

                    if (!exited)
                    {
                        LogManager.LogWarning("usbip {0} did not exit within 15s - killing it.", args);
                        try { proc.Kill(); } catch (Exception killEx) { LogManager.LogDebug("usbip kill failed: {0}", killEx.Message); }
                    }

                    // Killing (or a normal exit) closes the pipes, so these resolve promptly;
                    // still bound the wait so a pathological case can't hang the caller.
                    Task.WaitAll(new Task[] { stdoutTask, stderrTask }, 3000);
                    string stdout = stdoutTask.Status == TaskStatus.RanToCompletion ? stdoutTask.Result : string.Empty;
                    string stderr = stderrTask.Status == TaskStatus.RanToCompletion ? stderrTask.Result : string.Empty;
                    return (stdout ?? string.Empty) + "\n" + (stderr ?? string.Empty);
                }
            }
            catch (Exception ex)
            {
                LogManager.LogDebug("usbip exec failed ({0}): {1}", args, ex.Message);
                return string.Empty;
            }
        }
    }
}
