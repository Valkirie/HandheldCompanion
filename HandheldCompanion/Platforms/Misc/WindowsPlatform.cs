using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Xml.Linq;

namespace HandheldCompanion.Platforms.Misc;

public sealed class WindowsPlatform : IPlatform
{
    public override string Name { get; set; } = "Windows";
    public bool EnhancedSleepEnabled { get; private set; }
    public bool GoBackToSleepEnabled { get; private set; }

    private readonly EnhancedSleepPolicy _enhancedSleep = new();
    private ModernStandbyResleepMonitor _monitor;

    public WindowsPlatform()
    {
        IsInstalled = true;
    }

    public override bool Start()
    {
        // raise events
        switch (ManagerFactory.settingsManager.Status)
        {
            default:
            case ManagerStatus.Initializing:
                ManagerFactory.settingsManager.Initialized += SettingsManager_Initialized;
                break;
            case ManagerStatus.Initialized:
                QuerySettings();
                break;
        }

        return base.Start();
    }

    public override bool Stop(bool kill = false)
    {
        ManagerFactory.settingsManager.SettingValueChanged -= SettingsManager_SettingValueChanged;
        ManagerFactory.settingsManager.Initialized -= SettingsManager_Initialized;

        try { _monitor?.Stop(); } catch { }
        _monitor = null;

        return base.Stop(kill);
    }

    public override void Dispose()
    {
        Stop();
        base.Dispose();
    }

    private void SettingsManager_Initialized()
    {
        QuerySettings();
    }

    private void QuerySettings()
    {
        // manage events
        ManagerFactory.settingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

        // raise events
        SettingsManager_SettingValueChanged("EnhancedSleep", ManagerFactory.settingsManager.GetString("EnhancedSleep"), false);
        SettingsManager_SettingValueChanged("GoBackToSleep", ManagerFactory.settingsManager.GetString("GoBackToSleep"), false);
    }

    private void SettingsManager_SettingValueChanged(string name, object value, bool temporary)
    {
        switch (name)
        {
            case "EnhancedSleep":
                SetEnhancedSleep(Convert.ToBoolean(value));
                break;
            case "GoBackToSleep":
                SetGoBackToSleep(Convert.ToBoolean(value));
                break;
        }
    }

    public bool SetEnhancedSleep(bool enabled)
    {
        lock (updateLock)
        {
            if (EnhancedSleepEnabled == enabled)
                return true;

            try
            {
                if (enabled)
                    _enhancedSleep.ApplyAll();
                else
                    _enhancedSleep.RestoreAll();

                EnhancedSleepEnabled = enabled;
                return true;
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to set EnhancedSleep = {0}", enabled);
                return false;
            }
        }
    }

    public bool SetGoBackToSleep(bool enabled)
    {
        lock (updateLock)
        {
            if (GoBackToSleepEnabled == enabled)
                return true;

            try
            {
                if (enabled)
                {
                    _monitor ??= new ModernStandbyResleepMonitor(ShouldResleepOnWakeReason);
                    _monitor.Start();
                }
                else
                {
                    _monitor?.Stop();
                }

                GoBackToSleepEnabled = enabled;
                return true;
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to set GoBackToSleep = {0}", enabled);
                return false;
            }
        }
    }

    private bool ShouldResleepOnWakeReason(ModernStandbyResleepMonitor.WakeReason reason)
    {
        return reason != ModernStandbyResleepMonitor.WakeReason.PowerButton;
    }

    private sealed class EnhancedSleepPolicy
    {
        // Mirrors SuspendedNTime’s GUID set. :contentReference[oaicite:3]{index=3}
        private static readonly Guid SUB_SLEEP = new("238C9FA8-0AAD-41ED-83F4-97BE242C8F20");
        private static readonly Guid SUB_PROCESSOR = new("54533251-82BE-4824-96C1-47B60B740D00");
        private static readonly Guid SUB_PCIEXPRESS = new("501A4D13-42AF-4429-9FD1-A8218C268E20");
        private static readonly Guid SUB_NONE = new("fea3413e-7e05-4911-9a71-700331f1c294");

        private static readonly Guid GUID_ALLOW_HYBRID_SLEEP = new("94ac6d29-73ce-41a6-809f-6363ba21b47e");
        private static readonly Guid GUID_ALLOW_AWAY_MODE = new("25dfa149-5dd1-4736-b5ab-e8a37b5b8187");
        private static readonly Guid GUID_ALLOW_WAKE_TIMERS = new("BD3B718A-0680-4D9D-8AB2-E1D2B4AC806D");

        private static readonly Guid GUID_MODERN_DISCONNECTED_STANDBY = new("68afb2d9-ee95-47a8-8f50-4115088073b1");
        private static readonly Guid GUID_MODERN_STANDBY_NETWORK = new("F15576E8-98B7-4186-B944-EAFA664402D9");

        private static readonly Guid GUID_PCIEXPRESS_ASPM = new("EE12F906-D277-404B-B6DA-E5FA1A576DF5");

        private static readonly Guid GUID_IDLE_DISABLE = new("5D76A2CA-E8C0-402F-A133-2158492D58AD");
        private static readonly Guid GUID_PROCTHROTTLEMIN = new("893DEE8E-2BEF-41E0-89C6-B55D0929964C");

        private Dictionary<string, Snapshot> _snapshot;

        private static string cacheDirectory, cacheFile;
        private const string fileName = "enhanced_sleep_snapshot.json";

        private readonly record struct Snapshot(uint AC, uint DC);

        public EnhancedSleepPolicy()
        {
            cacheDirectory = Path.Combine(App.SettingsPath, "cache");
            cacheFile = Path.Combine(cacheDirectory, fileName);
            if (!Directory.Exists(cacheDirectory))
                Directory.CreateDirectory(cacheDirectory);

            _snapshot = LoadSnapshotFromDisk() ?? new Dictionary<string, Snapshot>(StringComparer.OrdinalIgnoreCase);
        }

        public void ApplyAll()
        {
            EnsureSnapshotTaken();

            LogManager.LogInformation("[EnhancedSleep] Applying optimized Modern Standby power policies...");

            // Disable Hybrid Sleep
            PowerScheme.WritePowerCfg(SUB_SLEEP, GUID_ALLOW_HYBRID_SLEEP, 0, 0);

            // Disable wake timers + away mode
            PowerScheme.WritePowerCfg(SUB_SLEEP, GUID_ALLOW_WAKE_TIMERS, 0, 0);
            PowerScheme.WritePowerCfg(SUB_SLEEP, GUID_ALLOW_AWAY_MODE, 0, 0);

            // Disable modern standby network/disconnected standby
            PowerScheme.WritePowerCfg(SUB_NONE, GUID_MODERN_STANDBY_NETWORK, 0, 0);
            PowerScheme.WritePowerCfg(SUB_NONE, GUID_MODERN_DISCONNECTED_STANDBY, 0, 0);

            // PCIe ASPM = Maximum power savings (2)
            PowerScheme.WritePowerCfg(SUB_PCIEXPRESS, GUID_PCIEXPRESS_ASPM, 2, 2);

            // Processor: allow idle + min throttle 0%
            PowerScheme.WritePowerCfg(SUB_PROCESSOR, GUID_IDLE_DISABLE, 0, 0);
            PowerScheme.WritePowerCfg(SUB_PROCESSOR, GUID_PROCTHROTTLEMIN, 0, 0);
        }

        public void RestoreAll()
        {
            if (_snapshot.Count == 0)
            {
                LogManager.LogWarning("[EnhancedSleep] No snapshot found; nothing to restore.");
                return;
            }

            LogManager.LogInformation("[EnhancedSleep] Restoring power policies from snapshot...");

            Restore("HybridSleep", SUB_SLEEP, GUID_ALLOW_HYBRID_SLEEP);
            Restore("WakeTimers", SUB_SLEEP, GUID_ALLOW_WAKE_TIMERS);
            Restore("AwayMode", SUB_SLEEP, GUID_ALLOW_AWAY_MODE);
            Restore("ModernStandbyNetwork", SUB_NONE, GUID_MODERN_STANDBY_NETWORK);
            Restore("ModernDisconnectedStandby", SUB_NONE, GUID_MODERN_DISCONNECTED_STANDBY);
            Restore("PCIeASPM", SUB_PCIEXPRESS, GUID_PCIEXPRESS_ASPM);
            Restore("ProcessorIdle", SUB_PROCESSOR, GUID_IDLE_DISABLE);
            Restore("ProcessorThrottle", SUB_PROCESSOR, GUID_PROCTHROTTLEMIN);
        }

        private void EnsureSnapshotTaken()
        {
            // If snapshot already exists on disk, we keep it (mirrors SuspendedNTime behavior). :contentReference[oaicite:4]{index=4}
            if (_snapshot.Count > 0)
                return;

            LogManager.LogInformation("[EnhancedSleep] Taking snapshot of current power policies...");

            SaveOriginal("HybridSleep", SUB_SLEEP, GUID_ALLOW_HYBRID_SLEEP);
            SaveOriginal("WakeTimers", SUB_SLEEP, GUID_ALLOW_WAKE_TIMERS);
            SaveOriginal("AwayMode", SUB_SLEEP, GUID_ALLOW_AWAY_MODE);
            SaveOriginal("ModernStandbyNetwork", SUB_NONE, GUID_MODERN_STANDBY_NETWORK);
            SaveOriginal("ModernDisconnectedStandby", SUB_NONE, GUID_MODERN_DISCONNECTED_STANDBY);
            SaveOriginal("PCIeASPM", SUB_PCIEXPRESS, GUID_PCIEXPRESS_ASPM);
            SaveOriginal("ProcessorIdle", SUB_PROCESSOR, GUID_IDLE_DISABLE);
            SaveOriginal("ProcessorThrottle", SUB_PROCESSOR, GUID_PROCTHROTTLEMIN);

            PersistSnapshotToDisk();
        }

        private void SaveOriginal(string key, Guid subgroup, Guid setting)
        {
            if (!PowerScheme.GetActiveScheme(out var scheme))
                return;

            if (!PowerScheme.GetValue(PowerIndexType.AC, scheme, subgroup, setting, out var ac))
                return;

            if (!PowerScheme.GetValue(PowerIndexType.DC, scheme, subgroup, setting, out var dc))
                return;

            _snapshot[key] = new Snapshot(ac, dc);
            LogManager.LogDebug("[EnhancedSleep] Snapshot {0}: AC={1} DC={2}", key, ac, dc);
        }

        private void Restore(string key, Guid subgroup, Guid setting)
        {
            if (!_snapshot.TryGetValue(key, out var snap))
                return;

            PowerScheme.WritePowerCfg(subgroup, setting, snap.AC, snap.DC);
        }

        private Dictionary<string, Snapshot> LoadSnapshotFromDisk()
        {
            try
            {
                if (!File.Exists(cacheFile))
                    return null;

                var json = File.ReadAllText(cacheFile);
                return JsonSerializer.Deserialize<Dictionary<string, Snapshot>>(json);
            }
            catch
            {
                return null;
            }
        }

        private void PersistSnapshotToDisk()
        {
            try
            {
                var json = JsonSerializer.Serialize(_snapshot, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(cacheFile, json);
            }
            catch (Exception ex)
            {
                LogManager.LogWarning("[EnhancedSleep] Failed to persist snapshot to disk.");
            }
        }
    }

    // =====================================================================
    // Re-sleep (Modern Standby wake monitor)
    // =====================================================================

    private sealed class ModernStandbyResleepMonitor
    {
        [DllImport("user32.dll")]
        private static extern int SendMessage(int hWnd, int Msg, int wParam, int lParam);

        private const int HWND_BROADCAST = 0xffff;
        private const int WM_SYSCOMMAND = 0x0112;
        private const int SC_SUSPEND = 0xF170;

        private readonly XNamespace _ns = "http://schemas.microsoft.com/win/2004/08/events/event";
        private readonly Func<WakeReason, bool> _shouldResleep;

        private EventLogWatcher _watcher;

        // Simple cooldown to avoid "wake, resleep, immediate wake, resleep ..." storms
        private long _lastResleepTicks;

        public enum WakeReason
        {
            Unknown = 0,
            PowerButton = 1,
            Joystick = 7,
            ChargerConnected = 28,
            Other = 999
        }

        public ModernStandbyResleepMonitor(Func<WakeReason, bool> shouldResleep)
        {
            _shouldResleep = shouldResleep ?? throw new ArgumentNullException(nameof(shouldResleep));
        }

        public void Start()
        {
            if (_watcher != null)
                return;

            // Mirrors SuspendedNTime query: Kernel-Power 506 (enter) / 507 (wake). :contentReference[oaicite:6]{index=6}
            string xpath = "*[System[(EventID=506 or EventID=507) and Provider[@Name='Microsoft-Windows-Kernel-Power']]]";
            var query = new EventLogQuery("System", PathType.LogName, xpath);

            _watcher = new EventLogWatcher(query);
            _watcher.EventRecordWritten += OnEventRecordWritten;
            _watcher.Enabled = true;

            LogManager.LogInformation("[GoBackToSleep] Watching for Modern Standby sleep(506)/wake(507) events...");
        }

        public void Stop()
        {
            if (_watcher == null)
                return;

            try
            {
                _watcher.Enabled = false;
                _watcher.EventRecordWritten -= OnEventRecordWritten;
                _watcher.Dispose();
            }
            finally
            {
                _watcher = null;
            }
        }

        private void OnEventRecordWritten(object sender, EventRecordWrittenEventArgs e)
        {
            if (e.EventRecord == null)
                return;

            int eventId = e.EventRecord.Id;

            if (eventId == 507) // wake
            {
                var reason = ParseWakeReason(e.EventRecord);
                LogManager.LogInformation("[GoBackToSleep] Woke from Modern Standby. Reason: {0}", reason);

                if (!_shouldResleep(reason))
                    return;

                // cooldown: 5 seconds
                long now = DateTime.UtcNow.Ticks;
                if (now - Interlocked.Read(ref _lastResleepTicks) < TimeSpan.FromSeconds(5).Ticks)
                    return;

                Interlocked.Exchange(ref _lastResleepTicks, now);

                LogManager.LogInformation("[GoBackToSleep] Wake reason != PowerButton. Sending system back to sleep...");
                SuspendSystem();
            }
        }

        private void SuspendSystem()
        {
            // Same approach as SuspendedNTime (broadcast SC_SUSPEND). :contentReference[oaicite:7]{index=7}
            SendMessage(HWND_BROADCAST, WM_SYSCOMMAND, SC_SUSPEND, 2);
        }

        private WakeReason ParseWakeReason(EventRecord evt)
        {
            try
            {
                var xml = evt.ToXml();
                var doc = XDocument.Parse(xml);

                var reasonVal = doc
                    .Descendants(_ns + "Data")
                    .FirstOrDefault(x => x.Attribute("Name")?.Value == "Reason")
                    ?.Value;

                if (!int.TryParse(reasonVal, out int code))
                    return WakeReason.Unknown;

                return code switch
                {
                    1 => WakeReason.PowerButton,
                    7 => WakeReason.Joystick,
                    28 => WakeReason.ChargerConnected,
                    0 => WakeReason.Unknown,
                    _ => WakeReason.Other
                };
            }
            catch
            {
                return WakeReason.Unknown;
            }
        }
    }
}