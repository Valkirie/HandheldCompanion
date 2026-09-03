using HandheldCompanion.Helpers;
using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using static HandheldCompanion.Managers.SystemManager;

namespace HandheldCompanion.Platforms.Misc;

public sealed class WindowsPlatform : IPlatform
{
    public override string Name { get; set; } = "Windows";
    public bool EnhancedSleepEnabled { get; private set; }
    public bool GoBackToSleepEnabled { get; private set; }

    private readonly EnhancedSleepPolicy _enhancedSleep = new();
    private ModernStandbyResleepMonitor? _monitor;

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

        lock (updateLock)
        {
            try
            {
                _monitor?.Stop();
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to stop GoBackToSleep: {0}", ex.Message);
            }
            finally
            {
                _monitor = null;
                GoBackToSleepEnabled = false;
            }
        }

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
        SettingsManager_SettingValueChanged("EnhancedSleep", ManagerFactory.settingsManager.GetString("EnhancedSleep"), false, true);
        SettingsManager_SettingValueChanged("GoBackToSleep", ManagerFactory.settingsManager.GetString("GoBackToSleep"), false, true);
    }

    private void SettingsManager_SettingValueChanged(string name, object? value, bool temporary, bool initializing)
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
            catch (Exception)
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
            if (GoBackToSleepEnabled == enabled && (enabled == (_monitor is not null)))
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
                    _monitor = null;
                }

                GoBackToSleepEnabled = enabled;
                return true;
            }
            catch (Exception ex)
            {
                if (enabled)
                {
                    try { _monitor?.Stop(); } catch { }
                    _monitor = null;
                }

                LogManager.LogError("Failed to set GoBackToSleep = {0}: {1}", enabled, ex.Message);
                return false;
            }
        }
    }

    private bool ShouldResleepOnWakeReason(WakeReason reason)
    {
        string? settingKey = reason switch
        {
            WakeReason.PowerButton => "GoBackToSleepOnPowerButton",
            WakeReason.FingerprintReader => "GoBackToSleepOnFingerprintReader",
            WakeReason.Joystick => "GoBackToSleepOnJoystick",
            WakeReason.ChargerConnected => "GoBackToSleepOnChargerConnected",
            WakeReason.Unknown => null, // On S4-only devices (no Modern Standby), only Unknown is available; don't resleep
            _ => null,
        };

        // If no setting key, don't resleep (safer default for unknown/unsupported reasons)
        if (string.IsNullOrEmpty(settingKey))
            return false;

        // For recognized reasons, check if user configured resleep for that reason
        return ManagerFactory.settingsManager.GetBoolean(settingKey);
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

        private static string cacheDirectory = string.Empty, cacheFile = string.Empty;
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

        private Dictionary<string, Snapshot>? LoadSnapshotFromDisk()
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
            catch (Exception)
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
        private readonly Func<WakeReason, bool> _shouldResleep;
        private readonly object _stateLock = new();

        private System.Timers.Timer? _batchTimer;
        private readonly HashSet<WakeReason> _batchedReasons = new();
        private const int BATCH_WINDOW_MS = 750;
        private bool _isRunning;

        private int _consecutiveResleepAttempts;
        private const int MAX_CONSECUTIVE_RESLEEP_ATTEMPTS = 3;
        private static readonly TimeSpan RESLEEP_ATTEMPT_COOLDOWN = TimeSpan.FromSeconds(30);
        private long _limitReachedTimestamp;

        public ModernStandbyResleepMonitor(Func<WakeReason, bool> shouldResleep)
        {
            _shouldResleep = shouldResleep ?? throw new ArgumentNullException(nameof(shouldResleep));
            ValidateWakeReasonFiltering();
        }

        public void Start()
        {
            lock (_stateLock)
            {
                if (_isRunning)
                    return;

                _batchTimer = new(BATCH_WINDOW_MS) { AutoReset = false };
                _batchTimer.Elapsed += OnBatchTimerElapsed;
                _isRunning = true;
                PowerModeChanged += OnSystemPowerModeChanged;
            }

            LogManager.LogInformation("[GoBackToSleep] Started. Using SystemManager's power detection.");
        }

        public void Stop()
        {
            lock (_stateLock)
            {
                if (!_isRunning && _batchTimer is null)
                    return;

                _isRunning = false;
                PowerModeChanged -= OnSystemPowerModeChanged;

                if (_batchTimer is not null)
                {
                    _batchTimer.Stop();
                    _batchTimer.Elapsed -= OnBatchTimerElapsed;
                    _batchTimer.Dispose();
                    _batchTimer = null;
                }

                _batchedReasons.Clear();
                ResetResleepAttemptCounter("monitor stopped");
            }
        }

        private bool HasResleepLimitExceeded()
        {
            if (_consecutiveResleepAttempts < MAX_CONSECUTIVE_RESLEEP_ATTEMPTS)
                return false;

            if (Stopwatch.GetElapsedTime(_limitReachedTimestamp) >= RESLEEP_ATTEMPT_COOLDOWN)
            {
                LogManager.LogDebug("[GoBackToSleep] Resleep attempt cooldown elapsed; allowing another attempt cycle.");
                _consecutiveResleepAttempts = 0;
                _limitReachedTimestamp = 0;
                return false;
            }

            return true;
        }

        private void ResetResleepAttemptCounter(string reason)
        {
            if (_consecutiveResleepAttempts > 0)
            {
                LogManager.LogDebug("[GoBackToSleep] Resetting resleep attempt counter ({0} attempts). Reason: {1}",
                    _consecutiveResleepAttempts, reason);
                _consecutiveResleepAttempts = 0;
                _limitReachedTimestamp = 0;
            }
        }

        private void OnSystemPowerModeChanged(PowerMode mode, WakeReason wakeReason)
        {
            if (mode != PowerMode.Resume)
                return;

            LogManager.LogInformation("[GoBackToSleep] Woke from Modern Standby. Reason: {0}", wakeReason);

            lock (_stateLock)
            {
                if (!_isRunning || _batchTimer is null)
                    return;

                _batchedReasons.Add(wakeReason);
                _batchTimer.Stop();
                _batchTimer.Start();
            }
        }

        private void OnBatchTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            WakeReason[] reasons;

            lock (_stateLock)
            {
                if (!_isRunning)
                    return;

                reasons = GetEffectiveWakeReasons(_batchedReasons);
                _batchedReasons.Clear();
            }

            if (reasons.Length == 0)
                return;

            LogManager.LogDebug("[GoBackToSleep] Batch window closed. Collected reasons: {0}", string.Join(", ", reasons));

            foreach (var reason in reasons)
            {
                if (!_shouldResleep(reason))
                {
                    LogManager.LogInformation("[GoBackToSleep] Reason {0} is intentional. Keeping system awake.", reason);
                    lock (_stateLock)
                    {
                        ResetResleepAttemptCounter("legitimate wake reason");
                    }
                    return;
                }
            }

            lock (_stateLock)
            {
                if (!_isRunning)
                    return;

                if (HasResleepLimitExceeded())
                {
                    LogManager.LogWarning("[GoBackToSleep] Resleep attempt limit exceeded ({0}/{1}). Waiting for the cooldown before retrying.",
                        _consecutiveResleepAttempts, MAX_CONSECUTIVE_RESLEEP_ATTEMPTS);
                    return;
                }

                LogManager.LogInformation("[GoBackToSleep] All collected reasons are unintentional. Sending system back to sleep (attempt {0}/{1})...",
                    _consecutiveResleepAttempts + 1, MAX_CONSECUTIVE_RESLEEP_ATTEMPTS);

                try
                {
                    if (!PowerActionsHelper.Sleep())
                    {
                        LogManager.LogWarning("[GoBackToSleep] Windows rejected the sleep request.");
                        return;
                    }

                    _consecutiveResleepAttempts++;
                    if (_consecutiveResleepAttempts == MAX_CONSECUTIVE_RESLEEP_ATTEMPTS)
                        _limitReachedTimestamp = Stopwatch.GetTimestamp();
                }
                catch (Exception ex)
                {
                    LogManager.LogError("[GoBackToSleep] Failed to put the system to sleep: {0}", ex.Message);
                }
            }
        }

        private static WakeReason[] GetEffectiveWakeReasons(IEnumerable<WakeReason> reasons)
        {
            WakeReason[] capturedReasons = reasons.Distinct().ToArray();
            return capturedReasons.Any(reason => reason != WakeReason.Unknown)
                ? capturedReasons.Where(reason => reason != WakeReason.Unknown).ToArray()
                : capturedReasons;
        }

        [Conditional("DEBUG")]
        private static void ValidateWakeReasonFiltering()
        {
            Debug.Assert(GetEffectiveWakeReasons([WakeReason.Unknown, WakeReason.Joystick])
                .SequenceEqual([WakeReason.Joystick]));
            Debug.Assert(GetEffectiveWakeReasons([WakeReason.Unknown])
                .SequenceEqual([WakeReason.Unknown]));
        }
    }
}