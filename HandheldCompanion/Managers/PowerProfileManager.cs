using HandheldCompanion.Devices;
using HandheldCompanion.Misc;
using HandheldCompanion.Shared;
using HandheldCompanion.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace HandheldCompanion.Managers
{
    public class PowerProfileManager : IManager
    {
        private object profileLock = new();
        private readonly object profileTransitionLock = new();
        private PowerProfile? currentProfile;
        private Profile? currentGameProfile;
        private int currentPowerLineStatus;
        private bool streamingPowerOverride;

        public ConcurrentDictionary<Guid, PowerProfile> profiles = [];

        public PowerProfileManager()
        {
            // initialize path
            ManagerPath = Path.Combine(App.SettingsPath, "powerprofiles");

            // create path
            if (!Directory.Exists(ManagerPath))
                Directory.CreateDirectory(ManagerPath);
        }

        public override void Start()
        {
            if (Status.HasFlag(ManagerStatus.Initializing) || Status.HasFlag(ManagerStatus.Initialized))
                return;

            base.PrepareStart();

            // process existing profiles
            string[] fileEntries = Directory.GetFiles(ManagerPath, "*.json", SearchOption.AllDirectories);
            foreach (var fileName in fileEntries)
                ProcessProfile(fileName);

            foreach (PowerProfile profile in IDevice.GetCurrent().DevicePowerProfiles)
            {
                if ((profile.Default || profile.DeviceDefault) && !profiles.ContainsKey(profile.Guid))
                    UpdateOrCreateProfile(profile, UpdateSource.Serializer);
            }

            // manage events
            SystemManager.Initialized += SystemManager_Initialized;

            // raise events
            if (SystemManager.IsInitialized)
                SystemManager_Initialized();

            // raise events
            switch (ManagerFactory.profileManager.Status)
            {
                default:
                case ManagerStatus.Initializing:
                    ManagerFactory.profileManager.Initialized += ProfileManager_Initialized;
                    break;
                case ManagerStatus.Initialized:
                    QueryProfile();
                    break;
            }

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

            switch (ManagerFactory.platformManager.Status)
            {
                default:
                case ManagerStatus.Initializing:
                    ManagerFactory.platformManager.Initialized += PlatformManager_Initialized;
                    break;
                case ManagerStatus.Initialized:
                    QueryPlatforms();
                    break;
            }

            base.Start();
        }

        private void SystemManager_Initialized()
        {
            // manage events
            SystemManager.PowerLineStatusChanged += SystemManager_PowerLineStatusChanged;

            SystemManager_PowerLineStatusChanged(SystemInformation.PowerStatus.PowerLineStatus, SystemInformation.PowerStatus.PowerLineStatus);
        }

        private void QueryPlatforms()
        {
            // manage events
            PlatformManager.LibreHardware.CPUTemperatureChanged += LibreHardwareMonitor_CpuTemperatureChanged;
            PlatformManager.LibreHardware.GPUTemperatureChanged += LibreHardwareMonitor_GpuTemperatureChanged;
        }

        private void PlatformManager_Initialized()
        {
            QueryPlatforms();
        }

        private void QueryProfile()
        {
            // manage events
            ManagerFactory.profileManager.Applied += ProfileManager_Applied;
            ManagerFactory.profileManager.Discarded += ProfileManager_Discarded;
            ManagerFactory.profileManager.Updated += ProfileManager_Updated;

            ProfileManager_Applied(ManagerFactory.profileManager.GetCurrent(), UpdateSource.Background);
        }

        private void ProfileManager_Initialized()
        {
            QueryProfile();
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
            SettingsManager_SettingValueChanged("ConfigurableTDPOverrideDown", ManagerFactory.settingsManager.GetString("ConfigurableTDPOverrideDown"), false, true);
            SettingsManager_SettingValueChanged("ConfigurableTDPOverrideUp", ManagerFactory.settingsManager.GetString("ConfigurableTDPOverrideUp"), false, true);
        }

        public override void Stop()
        {
            if (Status.HasFlag(ManagerStatus.Halting) || Status.HasFlag(ManagerStatus.Halted))
                return;

            base.PrepareStop();

            // manage events
            PlatformManager.LibreHardware.CPUTemperatureChanged -= LibreHardwareMonitor_CpuTemperatureChanged;
            PlatformManager.LibreHardware.GPUTemperatureChanged -= LibreHardwareMonitor_GpuTemperatureChanged;
            ManagerFactory.profileManager.Applied -= ProfileManager_Applied;
            ManagerFactory.profileManager.Discarded -= ProfileManager_Discarded;
            ManagerFactory.profileManager.Updated -= ProfileManager_Updated;
            ManagerFactory.profileManager.Initialized -= ProfileManager_Initialized;
            SystemManager.Initialized -= SystemManager_Initialized;
            SystemManager.PowerLineStatusChanged -= SystemManager_PowerLineStatusChanged;
            ManagerFactory.settingsManager.SettingValueChanged -= SettingsManager_SettingValueChanged;

            base.Stop();
        }

        private void SettingsManager_SettingValueChanged(string name, object? value, bool temporary, bool initializing)
        {
            // Only process relevant setting names.
            if (name != "ConfigurableTDPOverrideDown" && name != "ConfigurableTDPOverrideUp")
                return;

            if (value is null)
                return;

            double threshold = Convert.ToDouble(value);

            // Define the condition based on the setting name.
            Func<double, bool> shouldUpdate = name == "ConfigurableTDPOverrideDown"
                ? (current => current < threshold)
                : (current => current > threshold);

            foreach (PowerProfile profile in profiles.Values)
            {
                bool updated = false;

                // Prevent null reference if TDPOverrideValues is null.
                if (profile.TDPOverrideValues == null)
                    continue;

                /*
                if (profile.IsDeviceDefault())
                    continue;
                */

                // Loop through all override values
                for (int i = 0; i < profile.TDPOverrideValues.Length; i++)
                {
                    if (shouldUpdate(profile.TDPOverrideValues[i]))
                    {
                        profile.TDPOverrideValues[i] = threshold;
                        updated = true;
                    }
                }

                if (updated)
                    UpdateOrCreateProfile(profile, UpdateSource.Background);
            }
        }

        private void LibreHardwareMonitor_CpuTemperatureChanged(float? value)
        {
            UpdateSoftwareFan();
        }

        private void LibreHardwareMonitor_GpuTemperatureChanged(float? value)
        {
            if (GPUManager.GetCurrent()?.IsEmbedded() ?? false)
                UpdateSoftwareFan();
        }

        private void UpdateSoftwareFan()
        {
            float? temperature = PlatformManager.LibreHardware.GetCPUTemperature();
            if (GPUManager.GetCurrent()?.IsEmbedded() ?? false)
            {
                float? gpuTemperature = PlatformManager.LibreHardware.GetGPUTemperature();
                if (gpuTemperature.HasValue && (!temperature.HasValue || gpuTemperature > temperature))
                    temperature = gpuTemperature;
            }

            lock (profileLock)
            {
                if (currentProfile is null || currentProfile.FanProfile is null || !temperature.HasValue)
                    return;

                // update fan profile
                currentProfile.FanProfile.SetTemperature(temperature.Value);

                switch (currentProfile.FanProfile.fanMode)
                {
                    default:
                    case FanMode.Hardware:
                        return;
                    case FanMode.Software:
                        double fanSpeed = currentProfile.FanProfile.GetFanSpeed();
                        IDevice.GetCurrent().SetFanDuty(fanSpeed);
                        return;
                }
            }
        }

        private void SystemManager_PowerLineStatusChanged(PowerLineStatus prevPowerLineStatus, PowerLineStatus powerLineStatus)
        {
            if (!ManagerFactory.profileManager.IsReady)
                return;

            // Get current profile
            Profile profile = ManagerFactory.profileManager.GetCurrent();
            ProfileManager_Applied(profile, UpdateSource.Background);
        }

        private void ApplyProfile(PowerProfile profile, UpdateSource source)
        {
            lock (profileTransitionLock)
            {
                lock (profileLock)
                    currentProfile = profile;

                // apply device-specific power profile behavior before other subscribers
                IDevice.GetCurrent().PowerProfileManager_Applied(profile, source);

                Applied?.Invoke(profile, source);

                LogManager.LogInformation("Power profile {0} applied", profile.Name);
            }
        }

        private void ProfileManager_Applied(Profile profile, UpdateSource source)
        {
            lock (profileTransitionLock)
            {
                // Get the power status
                PowerStatus powerStatus = SystemInformation.PowerStatus;

                // Get the power profile
                int powerLineStatus;
                lock (profileLock)
                {
                    powerLineStatus = streamingPowerOverride ? (int)PowerLineStatus.Offline : (int)powerStatus.PowerLineStatus;

                    // the per power state overrides follow the game profile and power state, with or without a power profile
                    currentGameProfile = profile;
                    currentPowerLineStatus = powerLineStatus;
                }

                // ApplyProfile() sets currentProfile under profileLock
                PowerProfile powerProfile = GetProfile(profile.PowerProfiles[powerLineStatus]);
                if (powerProfile is not null)
                    ApplyProfile(powerProfile, source);

                // raised with or without a power profile
                OverridesApplied?.Invoke(profile, powerLineStatus, source);
            }
        }

        private void ProfileManager_Updated(Profile profile, UpdateSource source, bool isCurrent)
        {
            // the default profile is a layer of the current game profile overrides, a current profile is applied again anyway
            if (!profile.Default || isCurrent || source == UpdateSource.Serializer)
                return;

            // serialized with profile and power state transitions so stale overrides can't be applied
            lock (profileTransitionLock)
            {
                Profile? gameProfile;
                int powerLineStatus;
                lock (profileLock)
                {
                    gameProfile = currentGameProfile;
                    powerLineStatus = currentPowerLineStatus;
                }

                if (gameProfile is null)
                    return;

                OverridesApplied?.Invoke(gameProfile, powerLineStatus, source);
            }
        }

        private void ProfileManager_Discarded(Profile profile, bool swapped, Profile nextProfile)
        {
            lock (profileTransitionLock)
            {
                PowerProfile? powerProfile;
                lock (profileLock)
                {
                    powerProfile = currentProfile;
                    currentProfile = null;
                }

                if (powerProfile is null)
                    return;

                // don't bother discarding settings, new one will be enforce shortly
                Discarded?.Invoke(powerProfile, swapped);
            }
        }

        private void ProcessProfile(string fileName)
        {
            PowerProfile? profile = null;

            try
            {
                string rawName = Path.GetFileNameWithoutExtension(fileName);
                if (string.IsNullOrEmpty(rawName))
                    throw new Exception("Profile has an incorrect file name.");

                string outputraw = File.ReadAllText(fileName);
                JObject jObject = JObject.Parse(outputraw);

                // latest pre-versionning release
                Version version = new("0.15.0.4");
                if (jObject.TryGetValue("Version", out var value))
                    version = new Version(value.ToString());

                profile = JsonConvert.DeserializeObject<PowerProfile>(outputraw, new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.All
                });
            }
            catch (Exception ex)
            {
                LogManager.LogError("Could not parse power profile: {0}. {1}", fileName, ex.Message);
            }

            // failed to parse
            if (profile is null || profile.Name is null)
            {
                LogManager.LogError("Failed to parse power profile: {0}", fileName);
                return;
            }

            // store fileName (needed for rename-safe serialization)
            profile.FileName = Path.GetFileName(fileName);

            UpdateOrCreateProfile(profile, UpdateSource.Serializer);
        }

        public void UpdateOrCreateProfile(PowerProfile profile, UpdateSource source)
        {
            if (string.IsNullOrWhiteSpace(profile.Name))
                return;

            switch (source)
            {
                case UpdateSource.Serializer:
                    LogManager.LogInformation("Loaded power profile: {0}", profile.Name);
                    break;

                default:
                    LogManager.LogInformation("Attempting to update/create power profile: {0}", profile.Name);
                    break;
            }

            // update database
            profiles[profile.Guid] = profile;

            // raise event
            Updated?.Invoke(profile, source);

            if (source == UpdateSource.Serializer)
                return;

            lock (profileTransitionLock)
            {
                // warn owner
                bool isCurrent;
                lock (profileLock)
                    isCurrent = profile.Guid == currentProfile?.Guid;

                if (isCurrent)
                    ApplyProfile(profile, source);
            }

            // serialize profile
            SerializeProfile(profile);
        }

        public string GetProfileName(string nameTemplate)
        {
            string template = (nameTemplate ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(template))
                template = "Manual mode {0}";

            bool hasPlaceholder = template.Contains("{0}");

            // Build a case-insensitive set of existing filenames (current + disk)
            HashSet<string> existingFiles = new(StringComparer.OrdinalIgnoreCase);

            foreach (PowerProfile p in profiles.Values)
                existingFiles.Add(p.GetFileName());

            foreach (string path in Directory.GetFiles(ManagerPath, "*.json", SearchOption.TopDirectoryOnly))
                existingFiles.Add(Path.GetFileName(path));

            int idx = 1;
            while (true)
            {
                string candidateName = hasPlaceholder ? string.Format(template, idx) : $"{template} {idx}";
                string candidateFile = $"{FileUtils.MakeValidFileName(candidateName)}.json";
                if (string.IsNullOrWhiteSpace(candidateFile) || candidateFile.Equals(".json", StringComparison.OrdinalIgnoreCase))
                    candidateFile = $"Manual mode {idx}.json";

                if (!existingFiles.Contains(candidateFile))
                    return candidateName;

                idx++;
            }
        }

        public bool Contains(Guid guid)
        {
            return profiles.ContainsKey(guid);
        }

        public bool Contains(PowerProfile profile)
        {
            return profiles.ContainsKey(profile.Guid);
        }

        public PowerProfile GetProfile(Guid guid)
        {
            if (profiles.TryGetValue(guid, out var profile))
                return profile;

            return GetDefault();
        }

        private bool HasDefault()
        {
            return profiles.Values.Count(a => a.Default && a.Guid == Guid.Empty) != 0;
        }

        public PowerProfile GetDefault()
        {
            if (HasDefault())
                return profiles.Values.FirstOrDefault(a => a.Default && a.Guid == Guid.Empty) ?? new PowerProfile();

            return new PowerProfile();
        }

        public PowerProfile GetCurrent()
        {
            if (!Monitor.TryEnter(profileLock, TimeSpan.FromSeconds(2)))
                return GetDefault();

            try
            {
                if (currentProfile is not null)
                    return currentProfile;

                return GetDefault();
            }
            finally
            {
                Monitor.Exit(profileLock);
            }
        }

        /// <summary>
        /// Resolves the framerate limit for a game profile and power state.
        /// The first layer set to Override wins: game profile, default game profile, power profile.
        /// An Override value of 0 removes the limit. The power profile always decides, a disabled limiter means no limit.
        /// </summary>
        public (FramerateLimitMode Mode, int Value, FramerateLimitSource Source) ResolveFramerateLimit(Profile? gameProfile, int powerLineStatus, PowerProfile? powerProfile)
        {
            (Profile? Layer, FramerateLimitSource Source)[] layers =
            [
                (gameProfile, FramerateLimitSource.GameProfile),
                (ManagerFactory.profileManager.GetDefault(), FramerateLimitSource.DefaultProfile),
            ];

            foreach ((Profile? layer, FramerateLimitSource source) in layers)
            {
                if (layer?.PerformanceOverrides is null)
                    continue;

                if (layer.PerformanceOverrides.TryGetValue(powerLineStatus, out PerformanceOverrides? overrides)
                    && overrides is not null
                    && overrides.FramerateMode.Normalize() == FramerateLimitMode.Override)
                {
                    int value = overrides.FramerateMode.IsLegacyNoLimit() ? 0 : Math.Max(0, overrides.FramerateValue);
                    return (FramerateLimitMode.Override, value, source);
                }
            }

            // the power profile is the baseline: an enabled limiter writes its value, a disabled one removes the limit,
            // so a limit set by a previous profile never stays in RTSS
            return (FramerateLimitMode.Override, GetFramerateBaseline(powerProfile), FramerateLimitSource.PowerProfile);
        }

        /// <summary>
        /// Framerate limit the power profile gives when no profile overrides it, 0 means no limit.
        /// </summary>
        public static int GetFramerateBaseline(PowerProfile? powerProfile)
        {
            return powerProfile is not null && (powerProfile.FramerateLimitEnabled ?? powerProfile.FramerateValue != 0)
                ? Math.Max(0, powerProfile.FramerateValue)
                : 0;
        }

        /// <summary>
        /// Resolves the framerate limit for the current game profile and power state, 0 means no limit.
        /// </summary>
        public bool GetFramerateLimit(out int framerate)
        {
            Profile? gameProfile;
            int powerLineStatus;
            lock (profileLock)
            {
                gameProfile = currentGameProfile;
                powerLineStatus = currentPowerLineStatus;
            }

            (FramerateLimitMode mode, int value, _) = ResolveFramerateLimit(gameProfile, powerLineStatus, GetCurrent());
            framerate = mode == FramerateLimitMode.Override ? value : 0;
            return mode == FramerateLimitMode.Override;
        }

        /// <summary>
        /// Resolves the display mode of one monitor for a game profile and power state.
        /// Resolution and refresh rate are resolved independently; the first layer set to Override wins:
        /// game profile, then default game profile. There is no power profile layer, Inherit on every layer means no change.
        /// </summary>
        public ResolvedDisplayMode ResolveDisplayMode(Profile? gameProfile, int powerLineStatus, Guid monitorId)
        {
            (Profile? Layer, DisplayModeSource Source)[] layers =
            [
                (gameProfile, DisplayModeSource.GameProfile),
                (ManagerFactory.profileManager.GetDefault(), DisplayModeSource.DefaultProfile),
            ];

            ResolvedDisplayMode resolved = default;

            foreach ((Profile? layer, DisplayModeSource source) in layers)
            {
                if (layer?.PerformanceOverrides is null)
                    continue;

                if (!layer.PerformanceOverrides.TryGetValue(powerLineStatus, out PerformanceOverrides? overrides)
                    || overrides?.DisplayModes is null
                    || !overrides.DisplayModes.TryGetValue(monitorId, out DisplayModeOverride? displayMode)
                    || displayMode is null)
                    continue;

                if (resolved.ResolutionSource == DisplayModeSource.None && displayMode.HasResolution)
                    resolved = resolved with { Width = displayMode.Width, Height = displayMode.Height, ResolutionSource = source };

                if (resolved.RefreshSource == DisplayModeSource.None && displayMode.HasRefreshRate)
                    resolved = resolved with { RefreshRate = displayMode.RefreshRate, RefreshSource = source };
            }

            return resolved;
        }

        /// <summary>
        /// Resolves the graphics groups for a game profile and power state.
        /// Each group is resolved on its own; the first layer set to Override wins: game profile, then default game profile.
        /// There is no power profile layer, the default game profile overrides every group.
        /// </summary>
        public ResolvedGraphics ResolveGraphics(Profile? gameProfile, int powerLineStatus)
        {
            GraphicsOverrides? game = GetGraphicsOverrides(gameProfile, powerLineStatus);
            GraphicsOverrides? fallback = GetGraphicsOverrides(ManagerFactory.profileManager.GetDefault(), powerLineStatus);

            return new(
                ResolveGraphicsGroup(game?.Scaling, fallback?.Scaling),
                ResolveGraphicsGroup(game?.RSR, fallback?.RSR),
                ResolveGraphicsGroup(game?.IntegerScaling, fallback?.IntegerScaling),
                ResolveGraphicsGroup(game?.RIS, fallback?.RIS),
                ResolveGraphicsGroup(game?.AFMF, fallback?.AFMF));
        }

        /// <summary>
        /// Resolves the graphics groups for the current game profile and power state, every group is unset before the first apply.
        /// </summary>
        public ResolvedGraphics GetGraphics()
        {
            Profile? gameProfile;
            int powerLineStatus;
            lock (profileLock)
            {
                gameProfile = currentGameProfile;
                powerLineStatus = currentPowerLineStatus;
            }

            // nothing applied yet, nothing to enforce
            if (gameProfile is null)
                return default;

            return ResolveGraphics(gameProfile, powerLineStatus);
        }

        private static GraphicsOverrides? GetGraphicsOverrides(Profile? profile, int powerLineStatus)
        {
            if (profile?.PerformanceOverrides is null
                || !profile.PerformanceOverrides.TryGetValue(powerLineStatus, out PerformanceOverrides? overrides)
                || overrides is null)
                return null;

            return overrides.Graphics;
        }

        private static ResolvedGraphicsGroup<T> ResolveGraphicsGroup<T>(T? game, T? fallback) where T : GraphicsGroupOverride
        {
            if (game is not null && game.IsOverride)
                return new(game, GraphicsSource.GameProfile);

            if (fallback is not null && fallback.IsOverride)
                return new(fallback, GraphicsSource.DefaultProfile);

            return default;
        }

        /// <summary>
        /// Returns the game profile and power state the current overrides were applied for.
        /// </summary>
        public bool GetCurrentGameProfile(out Profile? gameProfile, out int powerLineStatus)
        {
            lock (profileLock)
            {
                gameProfile = currentGameProfile;
                powerLineStatus = currentPowerLineStatus;
            }

            return gameProfile is not null;
        }

        public void SetStreamingPowerOverride(bool enabled)
        {
            lock (profileTransitionLock)
            {
                lock (profileLock)
                {
                    if (streamingPowerOverride == enabled)
                        return;

                    streamingPowerOverride = enabled;
                }

                if (ManagerFactory.profileManager.IsReady)
                    ProfileManager_Applied(ManagerFactory.profileManager.GetCurrent(), UpdateSource.Background);
            }
        }

        public void SerializeProfile(PowerProfile profile)
        {
            // prepare for writing
            string profilePath = Path.Combine(ManagerPath, profile.GetFileName());

            // update profile version to current build
            profile.Version = App.CurrentVersion;

            var jsonString = JsonConvert.SerializeObject(profile, Formatting.Indented, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.All
            });

            try
            {
                // delete old file if fileName has changed (rename scenario)
                if (!string.IsNullOrEmpty(profile.FileName) &&
                    !profile.FileName.Equals(profile.GetFileName(), StringComparison.InvariantCultureIgnoreCase))
                {
                    string oldPath = Path.Combine(ManagerPath, profile.FileName);
                    File.Delete(oldPath);
                }

                if (FileUtils.IsFileWritable(profilePath))
                {
                    // update profile filename
                    profile.FileName = profile.GetFileName();

                    File.WriteAllText(profilePath, jsonString);
                }
            }
            catch { }
        }

        public void DeleteProfile(PowerProfile profile)
        {
            string profilePath = Path.Combine(ManagerPath, profile.GetFileName());

            if (profiles.ContainsKey(profile.Guid))
            {
                lock (profileTransitionLock)
                {
                    profiles.Remove(profile.Guid, out _);

                    // raise event
                    Discarded?.Invoke(profile, false);

                    // raise event(s)
                    Deleted?.Invoke(profile);
                }

                // send toast
                // todo: localize me
                ToastManager.SendToast(new ToastRequest
                {
                    Title = $"Power Profile {profile.FileName} deleted",
                    ActivationCommand = "OpenPerformancePage"
                });

                LogManager.LogInformation("Deleted power profile {0}", profilePath);

                // actually delete the file
                FileUtils.FileDelete(profilePath);
            }
        }

        public PowerProfile CloneProfile(PowerProfile source)
        {
            // Deep clone the profile by serializing and deserializing
            string json = JsonConvert.SerializeObject(source);
            return JsonConvert.DeserializeObject<PowerProfile>(json) ?? new PowerProfile();
        }

        #region events
        public event DeletedEventHandler? Deleted;
        public delegate void DeletedEventHandler(PowerProfile profile);

        public event UpdatedEventHandler? Updated;
        public delegate void UpdatedEventHandler(PowerProfile profile, UpdateSource source);

        public event AppliedEventHandler? Applied;
        public delegate void AppliedEventHandler(PowerProfile profile, UpdateSource source);

        public event DiscardedEventHandler? Discarded;
        public delegate void DiscardedEventHandler(PowerProfile profile, bool swapped);

        // the game profile or power state the per power state overrides resolve against was applied,
        // or the default profile layer changed; raised with or without a power profile
        public event OverridesAppliedEventHandler? OverridesApplied;
        public delegate void OverridesAppliedEventHandler(Profile gameProfile, int powerLineStatus, UpdateSource source);
        #endregion
    }
}
