using HandheldCompanion.Managers;
using HandheldCompanion.Misc;
using HandheldCompanion.Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace HandheldCompanion.Helpers;

/// <summary>
/// While the device sleeps on battery, temporarily write Windows DC hibernate settings
/// (critical battery level/action, optional "Hibernate after") and restore them on wake.
/// Originals are persisted to disk so a crash or HC restart cannot leave them behind.
/// </summary>
public static class SleepPowerPolicy
{
    public const int NeverMinutes = -1;
    public const int DefaultBatteryPercent = 5;
    public const int DefaultAtMinutes = 4 * 60;
    public const string EnabledHiberfilSetting = "SleepHibernateEnabledHiberfil";

    private static readonly TimeSpan MinHibernateDelay = TimeSpan.FromHours(1);
    private static readonly object gate = new();

    private static string CacheFile => Path.Combine(App.SettingsPath, "cache", "sleep_power_snapshot.json");

    private sealed class Snapshot
    {
        public Guid Scheme { get; set; }
        public Dictionary<string, uint> Values { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private static Snapshot? snapshot;
    private static bool applied;

    /// <summary>
    /// Re-check at HC start and on wake: if a snapshot is still on disk, restore it.
    /// Safe to call when nothing was changed.
    /// </summary>
    public static void RestoreIfNeeded()
    {
        lock (gate)
        {
            LoadSnapshot();
            if (snapshot is null || snapshot.Values.Count == 0)
            {
                applied = false;
                return;
            }

            RestoreLocked();
        }
    }

    /// <summary>
    /// On sleep entry: if the hibernate toggle is on and the device is on battery, write DC settings.
    /// Idempotent. On AC, or with the toggle off, does nothing.
    /// If the snapshot cannot be persisted, Windows settings are left unchanged.
    /// </summary>
    public static void ApplyForSleep()
    {
        if (!ManagerFactory.settingsManager.GetBoolean("SleepHibernate"))
            return;

        if (PowerActionsHelper.IsOnAC())
            return;

        lock (gate)
        {
            if (applied)
                return;

            LoadSnapshot();
            if (snapshot is null || snapshot.Values.Count == 0)
                TakeSnapshotLocked();

            if (snapshot is null || snapshot.Values.Count == 0)
                return;

            if (!PersistSnapshotLocked())
            {
                LogManager.LogWarning("[SleepPowerPolicy] Snapshot was not persisted; leaving Windows power settings unchanged.");
                snapshot = null;
                applied = false;
                return;
            }

            int percent = ManagerFactory.settingsManager.GetInt("SleepHibernateBatteryPercent");
            if (percent < 1)
                percent = 1;
            if (percent > 50)
                percent = 50;

            WriteCaptured("CriticalLevel", PowerSubGroup.SUB_BATTERY, BatterySetting.CRITICAL_LEVEL, (uint)percent);
            WriteCaptured("CriticalAction", PowerSubGroup.SUB_BATTERY, BatterySetting.CRITICAL_ACTION, BatterySetting.ACTION_HIBERNATE);

            int atMinutes = ManagerFactory.settingsManager.GetInt("SleepHibernateAtMinutes");
            if (atMinutes != NeverMinutes)
            {
                uint seconds = HibernateAfterSeconds(atMinutes);
                WriteCaptured("HibernateAfter", PowerSubGroup.SUB_SLEEP, SleepSetting.HIBERNATE_TIMEOUT, seconds);
                LogManager.LogInformation("[SleepPowerPolicy] DC hibernate after {0}s, critical battery {1}% action Hibernate.", seconds, percent);
            }
            else
            {
                LogManager.LogInformation("[SleepPowerPolicy] DC critical battery {0}% action Hibernate. Time condition Never.", percent);
            }

            applied = true;
        }
    }

    /// <summary>
    /// When the toggle turns on and Windows hibernation is off, enable it (HC is elevated)
    /// and remember that we did. When the toggle turns off, disable it only if we enabled it.
    /// </summary>
    public static void SetHibernateToggle(bool enabled)
    {
        if (enabled)
            EnsureHibernation();
        else
            RestoreHibernationIfWeEnabled();
    }

    private static void EnsureHibernation()
    {
        if (PowerActionsHelper.IsHibernateAvailable())
            return;

        if (!RunPowerCfg("/h on"))
        {
            LogManager.LogWarning("[SleepPowerPolicy] Could not enable Windows hibernation (powercfg /h on).");
            return;
        }

        ManagerFactory.settingsManager.SetProperty(EnabledHiberfilSetting, true);
        LogManager.LogInformation("[SleepPowerPolicy] Enabled Windows hibernation (hiberfil.sys).");
    }

    private static void RestoreHibernationIfWeEnabled()
    {
        if (!ManagerFactory.settingsManager.GetBoolean(EnabledHiberfilSetting))
            return;

        if (RunPowerCfg("/h off"))
        {
            ManagerFactory.settingsManager.SetProperty(EnabledHiberfilSetting, false);
            LogManager.LogInformation("[SleepPowerPolicy] Disabled Windows hibernation that we had enabled.");
        }
        else
        {
            LogManager.LogWarning("[SleepPowerPolicy] Could not disable Windows hibernation (powercfg /h off).");
        }
    }

    private static bool RunPowerCfg(string arguments)
    {
        try
        {
            using Process process = new()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powercfg.exe",
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                }
            };

            if (!process.Start())
                return false;

            Task<string> stdout = process.StandardOutput.ReadToEndAsync();
            Task<string> stderr = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(10000))
            {
                try { process.Kill(); }
                catch { }
                process.WaitForExit();
                try
                {
                    stdout.GetAwaiter().GetResult();
                    stderr.GetAwaiter().GetResult();
                }
                catch { }
                return false;
            }

            stdout.GetAwaiter().GetResult();
            stderr.GetAwaiter().GetResult();
            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            LogManager.LogWarning("[SleepPowerPolicy] powercfg {0} failed: {1}", arguments, ex.Message);
            return false;
        }
    }

    private static uint HibernateAfterSeconds(int atMinutes)
    {
        int hour = Math.Clamp(atMinutes / 60, 0, 23);
        int minute = Math.Clamp(atMinutes % 60, 0, 59);

        DateTime now = DateTime.Now;
        DateTime target = DateTime.Today.AddHours(hour).AddMinutes(minute);
        if (target <= now)
            target = target.AddDays(1);

        TimeSpan until = target - now;
        if (until < MinHibernateDelay)
            until = MinHibernateDelay;

        return (uint)Math.Max(1, until.TotalSeconds);
    }

    private static void TakeSnapshotLocked()
    {
        snapshot = new Snapshot();

        if (!PowerScheme.GetActiveScheme(out Guid scheme))
        {
            snapshot = null;
            return;
        }

        snapshot.Scheme = scheme;

        Save("CriticalLevel", scheme, PowerSubGroup.SUB_BATTERY, BatterySetting.CRITICAL_LEVEL);
        Save("CriticalAction", scheme, PowerSubGroup.SUB_BATTERY, BatterySetting.CRITICAL_ACTION);

        int atMinutes = ManagerFactory.settingsManager.GetInt("SleepHibernateAtMinutes");
        if (atMinutes != NeverMinutes)
            Save("HibernateAfter", scheme, PowerSubGroup.SUB_SLEEP, SleepSetting.HIBERNATE_TIMEOUT);

        if (snapshot.Values.Count == 0)
            snapshot = null;
    }

    private static void Save(string key, Guid scheme, Guid subgroup, Guid setting)
    {
        if (snapshot is null)
            return;

        if (!PowerScheme.GetValue(PowerIndexType.DC, scheme, subgroup, setting, out uint value))
            return;

        snapshot.Values[key] = value;
        LogManager.LogDebug("[SleepPowerPolicy] Snapshot {0} DC={1} scheme={2}", key, value, scheme);
    }

    private static void WriteCaptured(string key, Guid subgroup, Guid setting, uint value)
    {
        if (snapshot is null || !snapshot.Values.ContainsKey(key))
            return;

        PowerScheme.WritePowerCfg(snapshot.Scheme, PowerIndexType.DC, subgroup, setting, value);
    }

    private static void RestoreLocked()
    {
        if (snapshot is null)
            return;

        LogManager.LogInformation("[SleepPowerPolicy] Restoring DC power settings on scheme {0}.", snapshot.Scheme);

        Restore("CriticalLevel", PowerSubGroup.SUB_BATTERY, BatterySetting.CRITICAL_LEVEL);
        Restore("CriticalAction", PowerSubGroup.SUB_BATTERY, BatterySetting.CRITICAL_ACTION);
        Restore("HibernateAfter", PowerSubGroup.SUB_SLEEP, SleepSetting.HIBERNATE_TIMEOUT);

        snapshot = null;
        applied = false;
        TryDeleteCache();
    }

    private static void Restore(string key, Guid subgroup, Guid setting)
    {
        if (snapshot is null || !snapshot.Values.TryGetValue(key, out uint value))
            return;

        PowerScheme.WritePowerCfg(snapshot.Scheme, PowerIndexType.DC, subgroup, setting, value);
    }

    private static void LoadSnapshot()
    {
        if (snapshot is not null && snapshot.Values.Count > 0)
            return;

        try
        {
            if (!File.Exists(CacheFile))
                return;

            string json = File.ReadAllText(CacheFile);
            snapshot = JsonSerializer.Deserialize<Snapshot>(json);
            if (snapshot is null)
                return;

            snapshot.Values ??= new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
            snapshot.Values = new Dictionary<string, uint>(snapshot.Values, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            LogManager.LogWarning("[SleepPowerPolicy] Could not load snapshot: {0}", ex.Message);
            snapshot = null;
        }
    }

    private static bool PersistSnapshotLocked()
    {
        if (snapshot is null)
            return false;

        try
        {
            string cacheFile = CacheFile;
            string? dir = Path.GetDirectoryName(cacheFile);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(cacheFile, json);
            return File.Exists(cacheFile);
        }
        catch (Exception ex)
        {
            LogManager.LogWarning("[SleepPowerPolicy] Could not persist snapshot: {0}", ex.Message);
            return false;
        }
    }

    private static void TryDeleteCache()
    {
        try
        {
            if (File.Exists(CacheFile))
                File.Delete(CacheFile);
        }
        catch
        {
            // next start will try again
        }
    }
}
