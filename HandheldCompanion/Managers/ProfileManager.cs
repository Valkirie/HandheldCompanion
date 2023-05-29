using ControllerCommon;
using ControllerCommon.Controllers;
using ControllerCommon.Managers;
using ControllerCommon.Pipes;
using ControllerCommon.Utils;
using HandheldCompanion.Controllers;
using HandheldCompanion.Controls;
using HandheldCompanion.Views;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HandheldCompanion.Managers
{
    public static class ProfileManager
    {
        public const string DefaultName = "Default";

        public static Dictionary<string, Profile> profiles = new Dictionary<string, Profile>(StringComparer.InvariantCultureIgnoreCase);
        public static FileSystemWatcher profileWatcher { get; set; }

        #region events
        public static event DeletedEventHandler Deleted;
        public delegate void DeletedEventHandler(Profile profile);
        public static event UpdatedEventHandler Updated;
        public delegate void UpdatedEventHandler(Profile profile, ProfileUpdateSource source, bool isCurrent);
        public static event AppliedEventHandler Applied;
        public delegate void AppliedEventHandler(Profile profile);

        public static event InitializedEventHandler Initialized;
        public delegate void InitializedEventHandler();
        #endregion

        private static Profile currentProfile;

        public static string ProfilesPath;
        private static bool IsInitialized;

        static ProfileManager()
        {
            // initialiaze path
            ProfilesPath = Path.Combine(MainWindow.SettingsPath, "profiles");
            if (!Directory.Exists(ProfilesPath))
                Directory.CreateDirectory(ProfilesPath);

            ProcessManager.ForegroundChanged += ProcessManager_ForegroundChanged;
            ProcessManager.ProcessStarted += ProcessManager_ProcessStarted;
            ProcessManager.ProcessStopped += ProcessManager_ProcessStopped;

            ControllerManager.ControllerSelected += ControllerManager_ControllerSelected;
        }

        public static void Start()
        {
            // monitor profile files
            profileWatcher = new FileSystemWatcher()
            {
                Path = ProfilesPath,
                EnableRaisingEvents = true,
                IncludeSubdirectories = true,
                Filter = "*.json",
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size
            };
            profileWatcher.Deleted += ProfileDeleted;

            // process existing profiles
            string[] fileEntries = Directory.GetFiles(ProfilesPath, "*.json", SearchOption.AllDirectories);
            foreach (string fileName in fileEntries)
                ProcessProfile(fileName);

            // check for default profile
            if (!HasDefault())
            {
                Profile defaultProfile = new()
                {
                    Name = DefaultName,
                    Default = true,
                    Enabled = false,
                    Layout = LayoutTemplate.DefaultLayout.Layout.Clone() as Layout,
                    LayoutTitle = LayoutTemplate.DefaultLayout.Name,
                    LayoutEnabled = true,
                };

                UpdateOrCreateProfile(defaultProfile, ProfileUpdateSource.Creation);
            }

            IsInitialized = true;
            Initialized?.Invoke();

            LogManager.LogInformation("{0} has started", "ProfileManager");
        }

        public static void Stop()
        {
            if (!IsInitialized)
                return;

            IsInitialized = false;

            profileWatcher.Deleted -= ProfileDeleted;
            profileWatcher.Dispose();

            LogManager.LogInformation("{0} has stopped", "ProfileManager");
        }

        public static bool Contains(Profile profile)
        {
            foreach (Profile pr in profiles.Values)
                if (pr.Path.Equals(profile.Path, StringComparison.InvariantCultureIgnoreCase))
                    return true;

            return false;
        }

        public static bool Contains(string fileName)
        {
            foreach (Profile pr in profiles.Values)
                if (pr.Path.Equals(fileName, StringComparison.InvariantCultureIgnoreCase))
                    return true;

            return false;
        }

        public static Profile GetProfileFromPath(string path, bool ignoreStatus)
        {
            Profile profile = profiles.Values.FirstOrDefault(a => a.Path.Equals(path, StringComparison.InvariantCultureIgnoreCase));

            if (profile is null)
                return GetDefault();

            // ignore profile status (enabled/disabled)
            if (ignoreStatus)
                return profile;
            else
                return profile.Enabled ? profile : GetDefault();
        }

        private static void ApplyProfile(Profile profile, bool announce = true)
        {
            // might not be the same anymore if disabled
            profile = GetProfileFromPath(profile.Path, false);

            // we've already announced this profile
            if (currentProfile is not null)
                if (currentProfile.Path.Equals(profile.Path, StringComparison.InvariantCultureIgnoreCase))
                    announce = false;

            // raise event
            Applied?.Invoke(profile);

            // update current profile
            currentProfile = profile;

            // send toast
            // todo: localize me
            if (announce)
            {
                LogManager.LogInformation("Profile {0} applied", profile.Name);
                ToastManager.SendToast($"Profile {profile.Name} applied");
            }

            // inform service
            PipeClient.SendMessage(new PipeClientProfile(profile));
        }

        private static void ProcessManager_ProcessStopped(ProcessEx processEx)
        {
            try
            {
                Profile profile = GetProfileFromPath(processEx.Path, true);

                // do not discard default profile
                if (profile is null || profile.Default)
                    return;

                if (profile.ErrorCode.HasFlag(ProfileErrorCode.Running))
                {
                    // update profile
                    UpdateOrCreateProfile(profile);

                    // restore default profile
                    ApplyProfile(GetDefault());
                }
            }
            catch { }
        }

        private static void ProcessManager_ProcessStarted(ProcessEx processEx, bool OnStartup)
        {
            try
            {
                Profile profile = GetProfileFromPath(processEx.Path, true);

                if (profile is null || profile.Default)
                    return;

                // update profile executable path
                profile.Path = processEx.Path;

                // update profile
                UpdateOrCreateProfile(profile);
            }
            catch { }
        }

        private static void ProcessManager_ForegroundChanged(ProcessEx proc, ProcessEx back)
        {
            try
            {
                Profile profile = GetProfileFromPath(proc.Path, false);

                // update profile executable path
                if (!profile.Default)
                {
                    profile.Path = proc.Path;
                    UpdateOrCreateProfile(profile);
                }
                ApplyProfile(profile);
            }
            catch { }
        }

        private static void ProfileDeleted(object sender, FileSystemEventArgs e)
        {
            // not ideal
            string ProfileName = e.Name.Replace(".json", "");
            Profile profile = profiles.Values.Where(p => p.Name.Equals(ProfileName, StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();

            // couldn't find a matching profile
            if (profile is null)
                return;

            // you can't delete default profile !
            if (profile.Default)
            {
                SerializeProfile(profile);
                return;
            }

            DeleteProfile(profile);
        }

        private static bool HasDefault()
        {
            return profiles.Values.Where(a => a.Default).Count() != 0;
        }

        public static Profile GetDefault()
        {
            if (HasDefault())
                return profiles.Values.Where(a => a.Default).FirstOrDefault();
            return new();
        }

        public static Profile GetCurrent()
        {
            if (currentProfile is not null)
                return currentProfile;

            return GetDefault();
        }

        private static void ProcessProfile(string fileName)
        {
            Profile profile = null;
            try
            {
                string outputraw = File.ReadAllText(fileName);
                var jObject = JObject.Parse(outputraw);

                // latest pre-versionning release
                Version version = new("0.15.0.4");
                if (jObject.ContainsKey("Version"))
                    version = new(jObject["Version"].ToString());

                switch (version.ToString())
                {
                    case "0.15.0.4":
                        outputraw = CommonUtils.RegexReplace(outputraw, "Generic.Dictionary(.*)System.Private.CoreLib\"", "Generic.SortedDictionary$1System.Collections\"");
                        jObject = JObject.Parse(outputraw);
                        jObject.Remove("MotionSensivityArray");
                        outputraw = jObject.ToString();
                        break;
                }

                profile = JsonConvert.DeserializeObject<Profile>(outputraw, new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.All
                });
            }
            catch (Exception ex)
            {
                LogManager.LogError("Could not parse profile {0}. {1}", fileName, ex.Message);
            }

            // failed to parse
            if (profile is null || profile.Name is null || profile.Path is null)
            {
                LogManager.LogError("Could not parse profile {0}", fileName);
                return;
            }

            UpdateOrCreateProfile(profile, ProfileUpdateSource.Serializer);

            // default specific
            if (profile.Default)
                ApplyProfile(profile);
        }

        public static void DeleteProfile(Profile profile)
        {
            string profilePath = Path.Combine(ProfilesPath, profile.GetFileName());

            if (profiles.ContainsKey(profile.Path))
            {
                // Unregister application from HidHide
                HidHide.UnregisterApplication(profile.Path);

                // Remove XInputPlus (extended compatibility)
                XInputPlus.UnregisterApplication(profile);

                profiles.Remove(profile.Path);

                // warn owner
                bool isCurrent = profile.Path.Equals(currentProfile.Path, StringComparison.InvariantCultureIgnoreCase);

                // raise event(s)
                Deleted?.Invoke(profile);

                // send toast
                // todo: localize me
                ToastManager.SendToast($"Profile {profile.Name} deleted");

                LogManager.LogInformation("Deleted profile {0}", profilePath);

                // restore default profile
                if (isCurrent)
                    ApplyProfile(GetDefault());
            }

            File.Delete(profilePath);
        }

        public static void SerializeProfile(Profile profile)
        {
            // update profile version to current build
            profile.Version = new(MainWindow.fileVersionInfo.ProductVersion);

            string jsonString = JsonConvert.SerializeObject(profile, Formatting.Indented, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.All
            });

            string profilePath = Path.Combine(ProfilesPath, profile.GetFileName());
            File.WriteAllText(profilePath, jsonString);
        }

        private static void SanitizeProfile(Profile profile)
        {
            profile.ErrorCode = ProfileErrorCode.None;

            if (profile.Default)
                profile.ErrorCode |= ProfileErrorCode.Default;
            else
            {
                string processpath = Path.GetDirectoryName(profile.Path);

                if (!Directory.Exists(processpath))
                    profile.ErrorCode |= ProfileErrorCode.MissingPath;

                if (!File.Exists(profile.Path))
                    profile.ErrorCode |= ProfileErrorCode.MissingExecutable;

                if (!CommonUtils.IsDirectoryWritable(processpath))
                    profile.ErrorCode |= ProfileErrorCode.MissingPermission;

                if (ProcessManager.GetProcesses(profile.Executable).Capacity > 0)
                    profile.ErrorCode |= ProfileErrorCode.Running;
            }
        }

        public static void UpdateOrCreateProfile(Profile profile, ProfileUpdateSource source = ProfileUpdateSource.Background)
        {
            bool isCurrent = false;
            switch (source)
            {
                // update current profile on creation
                case ProfileUpdateSource.Creation:
                case ProfileUpdateSource.QuickProfilesPage:
                    isCurrent = true;
                    break;
                default:
                    // check if this is current profile
                    isCurrent = currentProfile is null ? false : profile.Path.Equals(currentProfile.Path, StringComparison.InvariantCultureIgnoreCase);
                    break;
            }

            // refresh error code
            SanitizeProfile(profile);

            // update database
            profiles[profile.Path] = profile;

            // raise event(s)
            Updated?.Invoke(profile, source, isCurrent);

            if (source == ProfileUpdateSource.Serializer)
                return;

            // serialize profile
            SerializeProfile(profile);

            // apply profile (silently)
            if (isCurrent)
                ApplyProfile(profile);

            // do not update wrapper and cloaking from default profile
            if (profile.Default)
                return;

            // update wrapper
            UpdateProfileWrapper(profile);

            // update cloaking
            UpdateProfileCloaking(profile);
        }

        public static void UpdateProfileCloaking(Profile profile)
        {
            switch (profile.ErrorCode)
            {
                case ProfileErrorCode.MissingExecutable:
                case ProfileErrorCode.MissingPath:
                case ProfileErrorCode.Default:
                    return;
            }

            switch (profile.Whitelisted)
            {
                case true:
                    HidHide.RegisterApplication(profile.Path);
                    break;
                case false:
                    HidHide.UnregisterApplication(profile.Path);
                    break;
            }
        }

        public static void UpdateProfileWrapper(Profile profile)
        {
            switch (profile.ErrorCode)
            {
                case ProfileErrorCode.MissingPermission:
                case ProfileErrorCode.MissingPath:
                case ProfileErrorCode.Running:
                case ProfileErrorCode.Default:
                    return;
            }

            switch (profile.XInputPlus)
            {
                case true:
                    XInputPlus.RegisterApplication(profile);
                    break;
                case false:
                    XInputPlus.UnregisterApplication(profile);
                    break;
            }
        }

        private static void ControllerManager_ControllerSelected(IController Controller)
        {
            // only XInput controllers use XInputPlus
            if (Controller.GetType() != typeof(XInputController))
                return;

            foreach (Profile profile in profiles.Values)
                UpdateProfileWrapper(profile);
        }
    }
}
