using HandheldCompanion.Devices;
using HandheldCompanion.Misc;
using HandheldCompanion.Utils;
using HandheldCompanion.Views;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace HandheldCompanion.Managers
{
    static class PowerProfileManager
    {
        private static PowerProfile currentProfile;

        public static Dictionary<Guid, PowerProfile> profiles = [];

        private static string ProfilesPath;

        public static bool IsInitialized;

        static PowerProfileManager()
        {
            // initialiaze path(s)
            ProfilesPath = Path.Combine(MainWindow.SettingsPath, "powerprofiles");
            if (!Directory.Exists(ProfilesPath))
                Directory.CreateDirectory(ProfilesPath);

            PlatformManager.LibreHardwareMonitor.CPUTemperatureChanged += LibreHardwareMonitor_CpuTemperatureChanged;

            ProfileManager.Applied += ProfileManager_Applied;
            ProfileManager.Discarded += ProfileManager_Discarded;
            SystemManager.PowerStatusChanged += SystemManager_PowerStatusChanged;
        }

        public static void Start()
        {
            // process existing profiles
            var fileEntries = Directory.GetFiles(ProfilesPath, "*.json", SearchOption.AllDirectories);
            foreach (var fileName in fileEntries)
                ProcessProfile(fileName);

            foreach (PowerProfile devicePowerProfile in IDevice.GetCurrent().DevicePowerProfiles)
            {
                if (!profiles.ContainsKey(devicePowerProfile.Guid))
                    UpdateOrCreateProfile(devicePowerProfile, UpdateSource.Serializer);
            }

            IsInitialized = true;
            Initialized?.Invoke();

            LogManager.LogInformation("{0} has started", "PowerProfileManager");
        }

        public static void Stop()
        {
            if (!IsInitialized)
                return;

            IsInitialized = false;

            LogManager.LogInformation("{0} has stopped", "PowerProfileManager");
        }

        private static void LibreHardwareMonitor_CpuTemperatureChanged(float? value)
        {
            if (currentProfile is null || currentProfile.FanProfile is null || value is null)
                return;

            // update fan profile
            currentProfile.FanProfile.SetTemperature((float)value);

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

        private static void SystemManager_PowerStatusChanged(PowerStatus status)
        {
            // Get current profile
            Profile profile = ProfileManager.GetCurrent();

            ProfileManager_Applied(profile, UpdateSource.Background);
        }

        private static void ProfileManager_Applied(Profile profile, UpdateSource source)
        {
            // Get the power status
            PowerStatus powerStatus = SystemInformation.PowerStatus;

            // Get the power profile
            PowerProfile powerProfile = GetProfile(profile.PowerProfiles[(int)powerStatus.PowerLineStatus]);
            if (powerProfile is null)
                return;

            // update current profile
            currentProfile = powerProfile;

            Applied?.Invoke(powerProfile, source);
        }

        private static void ProfileManager_Discarded(Profile profile, bool swapped)
        {
            // reset current profile
            currentProfile = null;

            // Get the power status
            PowerStatus powerStatus = SystemInformation.PowerStatus;

            // Get the power profile
            PowerProfile powerProfile = GetProfile(profile.PowerProfiles[(int)powerStatus.PowerLineStatus]);
            if (powerProfile is null)
                return;

            // don't bother discarding settings, new one will be enforce shortly
            if (!swapped)
                Discarded?.Invoke(powerProfile);
        }

        private static void ProcessProfile(string fileName)
        {
            PowerProfile profile = null;

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
                LogManager.LogError("Could not parse power profile {0}. {1}", fileName, ex.Message);
            }

            // failed to parse
            if (profile is null || profile.Name is null)
            {
                LogManager.LogError("Failed to parse power profile {0}", fileName);
                return;
            }

            UpdateOrCreateProfile(profile, UpdateSource.Serializer);
        }

        public static void UpdateOrCreateProfile(PowerProfile profile, UpdateSource source)
        {
            // update database
            profiles[profile.Guid] = profile;

            // raise event
            Updated?.Invoke(profile, source);

            if (source == UpdateSource.Serializer)
                return;

            // warn owner
            bool isCurrent = profile.Guid == currentProfile?.Guid;

            if (isCurrent)
                Applied?.Invoke(profile, source);

            // serialize profile
            SerializeProfile(profile);
        }

        public static bool Contains(Guid guid)
        {
            return profiles.ContainsKey(guid);
        }

        public static bool Contains(PowerProfile profile)
        {
            return profiles.ContainsValue(profile);
        }

        public static PowerProfile GetProfile(Guid guid)
        {
            if (profiles.TryGetValue(guid, out var profile))
                return profile;

            return GetDefault();
        }

        private static bool HasDefault()
        {
            return profiles.Values.Count(a => a.Default) != 0;
        }

        public static PowerProfile GetDefault()
        {
            if (HasDefault())
                return profiles.Values.FirstOrDefault(a => a.Default);
            return new PowerProfile();
        }

        public static PowerProfile GetCurrent()
        {
            if (currentProfile is not null)
                return currentProfile;

            return GetDefault();
        }

        public static void SerializeProfile(PowerProfile profile)
        {
            // update profile version to current build
            profile.Version = new Version(MainWindow.fileVersionInfo.FileVersion);

            var jsonString = JsonConvert.SerializeObject(profile, Formatting.Indented, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.All
            });

            // prepare for writing
            var profilePath = Path.Combine(ProfilesPath, profile.GetFileName());

            try
            {
                if (FileUtils.IsFileWritable(profilePath))
                    File.WriteAllText(profilePath, jsonString);
            }
            catch { }
        }

        public static void DeleteProfile(PowerProfile profile)
        {
            string profilePath = Path.Combine(ProfilesPath, profile.GetFileName());

            if (profiles.ContainsKey(profile.Guid))
            {
                profiles.Remove(profile.Guid);

                // warn owner
                bool isCurrent = profile.Guid == currentProfile?.Guid;

                // raise event
                Discarded?.Invoke(profile);

                // raise event(s)
                Deleted?.Invoke(profile);

                // send toast
                // todo: localize me
                ToastManager.SendToast($"Power Profile {profile.FileName} deleted");

                LogManager.LogInformation("Deleted power profile {0}", profilePath);
            }

            FileUtils.FileDelete(profilePath);
        }

        #region events
        public static event DeletedEventHandler Deleted;
        public delegate void DeletedEventHandler(PowerProfile profile);

        public static event UpdatedEventHandler Updated;
        public delegate void UpdatedEventHandler(PowerProfile profile, UpdateSource source);

        public static event AppliedEventHandler Applied;
        public delegate void AppliedEventHandler(PowerProfile profile, UpdateSource source);

        public static event DiscardedEventHandler Discarded;
        public delegate void DiscardedEventHandler(PowerProfile profile);

        public static event InitializedEventHandler Initialized;
        public delegate void InitializedEventHandler();
        #endregion
    }
}
