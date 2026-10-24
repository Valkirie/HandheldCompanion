using HandheldCompanion.Shared;
using Refractor;
using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;

namespace HandheldCompanion.Helpers
{
    /// <summary>
    /// Speaks text through the user's running screen reader (NVDA, JAWS, ...) using prism.
    /// Used by windows that never take keyboard focus (e.g. QuickTools), where the screen
    /// reader cannot follow UI Automation focus on its own.
    /// </summary>
    public static class ScreenReader
    {
        private const uint SPI_GETSCREENREADER = 0x0046;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref bool pvParam, uint fWinIni);

        // only real screen readers: we never want to fall back to SAPI/OneCore and talk over sighted users
        private static readonly BackendId[] ScreenReaderBackends =
        [
            BackendId.Nvda,
            BackendId.Jaws,
            BackendId.ZoomText,
            BackendId.SystemAccess,
            BackendId.Zdsr,
            BackendId.BoyPCReader,
            BackendId.PCTalker,
            BackendId.SenseReader,
        ];

        private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

        private static readonly BlockingCollection<(string Text, bool Interrupt)> queue = new();
        private static readonly object threadLock = new();
        private static Thread? worker;

        // only touched from the worker thread (prism backends are not thread safe)
        private static Prism? prism;
        private static Backend? backend;
        private static DateTime nextAttempt = DateTime.MinValue;
        // set by the worker thread, read by IsActive on the UI thread
        private static volatile bool unavailable;

        /// <summary>
        /// True when Windows reports a running screen reader (NVDA, JAWS and Narrator set this flag).
        /// Cheap enough to call on every focus change.
        /// </summary>
        public static bool IsActive
        {
            get
            {
                if (unavailable)
                    return false;

                bool running = false;
                return SystemParametersInfo(SPI_GETSCREENREADER, 0, ref running, 0) && running;
            }
        }

        public static void Speak(string? text, bool interrupt = true)
        {
            if (string.IsNullOrWhiteSpace(text) || !IsActive)
                return;

            EnsureWorker();

            // an interrupting message makes anything still queued obsolete
            if (interrupt)
                while (queue.TryTake(out _)) { }

            queue.Add((text.Trim(), interrupt));
        }

        private static void EnsureWorker()
        {
            if (worker is not null)
                return;

            lock (threadLock)
            {
                if (worker is not null)
                    return;

                worker = new Thread(Run)
                {
                    IsBackground = true,
                    Name = "ScreenReader",
                };
                worker.Start();
            }
        }

        private static void Run()
        {
            foreach ((string text, bool interrupt) in queue.GetConsumingEnumerable())
            {
                try
                {
                    Backend? current = GetBackend();
                    if (current is null)
                        continue;

                    if (current.Supports(BackendFeatures.Output))
                        current.Output(text, interrupt);
                    else
                        current.Speak(text, interrupt);
                }
                catch (DllNotFoundException ex)
                {
                    // prism.dll missing: disable for this session
                    unavailable = true;
                    LogManager.LogWarning("ScreenReader: prism unavailable: {0}", ex.Message);
                }
                catch (Exception ex)
                {
                    // the screen reader was probably closed or restarted: pick a backend again next time
                    LogManager.LogDebug("ScreenReader: speak failed: {0}", ex.Message);
                    ResetBackend();
                }
            }
        }

        private static Backend? GetBackend()
        {
            if (backend is not null)
                return backend;

            if (DateTime.UtcNow < nextAttempt)
                return null;

            nextAttempt = DateTime.UtcNow + RetryDelay;
            prism ??= new Prism(new PrismOptions());

            foreach (BackendId id in ScreenReaderBackends)
            {
                if (!prism.HasBackend(id))
                    continue;

                Backend? candidate = null;
                try
                {
                    candidate = prism.Create(id);
                    candidate.Initialize();

                    if (candidate.GetFeatures().HasFlag(BackendFeatures.IsSupportedAtRuntime))
                    {
                        LogManager.LogInformation("ScreenReader: using {0}", candidate.Name);
                        backend = candidate;
                        return backend;
                    }
                }
                catch { }

                candidate?.Dispose();
            }

            return null;
        }

        private static void ResetBackend()
        {
            try { backend?.Dispose(); } catch { }
            backend = null;
            nextAttempt = DateTime.MinValue;
        }
    }
}
