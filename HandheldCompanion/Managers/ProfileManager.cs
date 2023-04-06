using ControllerCommon;
using ControllerCommon.Controllers;
using ControllerCommon.Managers;
using ControllerCommon.Pipes;
using ControllerCommon.Utils;
using Force.Crc32;
using HandheldCompanion.Controllers;
using HandheldCompanion.Controls;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static ControllerCommon.Utils.ProcessUtils;

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
        public static event InitializedEventHandler Initialized;
        public delegate void InitializedEventHandler();

        public static event AppliedEventHandler Applied;
        public delegate void AppliedEventHandler(Profile profile);

        public static event DiscardedEventHandler Discarded;
        public delegate void DiscardedEventHandler(Profile profile, bool isCurrent, bool isUpdate);
        #endregion

        private static Profile currentProfile;

        public static string InstallPath;
        private static bool IsInitialized;

        static ProfileManager()
        {
            // initialiaze path
            InstallPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "HandheldCompanion", "profiles");
            if (!Directory.Exists(InstallPath))
                Directory.CreateDirectory(InstallPath);

            ProcessManager.ForegroundChanged += ProcessManager_ForegroundChanged;
            ProcessManager.ProcessStarted += ProcessManager_ProcessStarted;
            ProcessManager.ProcessStopped += ProcessManager_ProcessStopped;

            ControllerManager.ControllerSelected += ControllerManager_ControllerSelected;
        }

        public static void Start()
        {
            // monitor profile file deletions
            profileWatcher = new FileSystemWatcher()
            {
                Path = InstallPath,
                EnableRaisingEvents = true,
                IncludeSubdirectories = true,
                Filter = "*.json",
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size
            };
            profileWatcher.Deleted += ProfileDeleted;

            // process existing profiles
            string[] fileEntries = Directory.GetFiles(InstallPath, "*.json", SearchOption.AllDirectories);
            foreach (string fileName in fileEntries)
                ProcessProfile(fileName);

            // check for default profile
            if (!HasDefault())
            {
                Profile defaultProfile = new()
                {
                    Name = DefaultName,
                    Default = true,
                    Enabled = true,
                    Layout = new Layout("Profile")
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
                if (pr.Executable == profile.Executable)
                    return true;

            return false;
        }

        public static int GetProfileIndex(Profile profile)
        {
            int idx = -1;

            for (int i = 0; i < profiles.Count; i++)
            {
                Profile pr = profiles.Values.ToList()[i];
                if (pr.Executable == profile.Executable)
                    return i;
            }

            return idx;
        }

        private static void ProcessManager_ProcessStopped(ProcessEx processEx)
        {
            try
            {
                Profile profile = GetProfileFromExec(processEx.Executable);

                // do not discard default profile
                if (profile is null || profile.Default)
                    return;

                if (profile.Running)
                {
                    // warn owner
                    bool isCurrent = profile.Executable == currentProfile.Executable;

                    // (re)set current profile
                    if (isCurrent)
                        currentProfile = profile;

                    // raise event
                    Discarded?.Invoke(profile, isCurrent, false);

                    // update profile
                    UpdateOrCreateProfile(profile);
                }
            }
            catch { }
        }

        private static void ProcessManager_ProcessStarted(ProcessEx processEx, bool OnStartup)
        {
            try
            {
                Profile profile = GetProfileFromExec(processEx.Executable);

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
                var profile = GetProfileFromExec(proc.Executable);

                // raise event
                Applied?.Invoke(profile);

                // skip if is current profile
                if (currentProfile == profile)
                    return;

                // raise event
                Discarded?.Invoke(currentProfile, true, true);

                // update current profile
                currentProfile = profile;

                LogManager.LogInformation("Profile {0} applied", profile.Name);

                // inform service
                PipeClient.SendMessage(new PipeClientProfile(profile));

                // send toast
                // todo: localize me
                ToastManager.SendToast($"Profile {profile.Name} applied");

                // update profile executable path
                if (!profile.Default)
                    profile.Path = proc.Path;

                UpdateOrCreateProfile(profile);
            }
            catch { }
        }

        private static void ProfileDeleted(object sender, FileSystemEventArgs e)
        {
            string ProfileName = e.Name.Replace(".json", "");

            if (!profiles.ContainsKey(ProfileName))
                return;

            Profile profile = profiles[ProfileName];

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
            return currentProfile;
        }

        private static void ProcessProfile(string fileName)
        {
            Profile profile = null;
            try
            {
                string outputraw = File.ReadAllText(fileName);
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

            // default specific
            if (profile.Default)
            {
                // update current profile
                currentProfile = profile;

                // raise event
                Applied?.Invoke(profile);

                // ping service
                PipeClient.SendMessage(new PipeClientProfile(profile));
            }

            UpdateOrCreateProfile(profile, ProfileUpdateSource.Serialiazer);
        }

        public static void DeleteProfile(Profile profile)
        {
            string settingsPath = Path.Combine(InstallPath, profile.GetFileName());

            if (profiles.ContainsKey(profile.Name))
            {
                // Unregister application from HidHide
                HidHide.UnregisterApplication(profile.Path);

                profiles.Remove(profile.Name);

                // warn owner
                bool isCurrent = profile.Executable == currentProfile.Executable;

                // (re)set current profile
                if (isCurrent)
                    currentProfile = GetDefault();

                // raise event(s)
                Deleted?.Invoke(profile);
                Discarded?.Invoke(profile, isCurrent, false);

                // send toast
                // todo: localize me
                ToastManager.SendToast($"Profile {profile.Name} deleted");

                LogManager.LogInformation("Deleted profile {0}", settingsPath);
            }

            File.Delete(settingsPath);
        }

        public static void SerializeProfile(Profile profile)
        {
            string jsonString = JsonConvert.SerializeObject(profile, Formatting.Indented, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.All
            });

            string settingsPath = Path.Combine(InstallPath, profile.GetFileName());
            File.WriteAllText(settingsPath, jsonString);
        }

        private static ProfileErrorCode SanitizeProfile(Profile profile)
        {
            string processpath = Path.GetDirectoryName(profile.Path);

            if (profile.Default)
                return ProfileErrorCode.Default;
            else
            {
                if (!Directory.Exists(processpath))
                    return ProfileErrorCode.MissingPath;
                else if (!File.Exists(profile.Path))
                    return ProfileErrorCode.MissingExecutable;
                else if (!CommonUtils.IsDirectoryWritable(processpath))
                    return ProfileErrorCode.MissingPermission;
                else if (profile.Running)
                    return ProfileErrorCode.Running;
            }

            return ProfileErrorCode.None;
        }

        public static void UpdateOrCreateProfile(Profile profile, ProfileUpdateSource source = ProfileUpdateSource.Background)
        {
            switch (source)
            {
                // update current profile on creation
                case ProfileUpdateSource.Creation:
                    currentProfile = profile;
                    break;
            }

            // check if application is running
            profile.Running = ProcessManager.GetProcesses(profile.Executable).Capacity > 0;

            // check if this is current profile
            bool isCurrent = currentProfile is null ? false : profile.Executable == currentProfile.Executable;

            // refresh error code
            profile.ErrorCode = SanitizeProfile(profile);

            // update database
            profiles[profile.Name] = profile;

            // raise event(s)
            Updated?.Invoke(profile, source, isCurrent);

            // inform service
            if (isCurrent)
                PipeClient.SendMessage(new PipeClientProfile(profile));

            if (profile.ErrorCode != ProfileErrorCode.None && !profile.Default)
            {
                LogManager.LogError("Profile {0} returned error code {1}", profile.Name, profile.ErrorCode);
                return;
            }

            if (source == ProfileUpdateSource.Serialiazer)
                return;

            // serialize profile
            SerializeProfile(profile);

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

            foreach(Profile profile in profiles.Values)
                UpdateProfileWrapper(profile);
        }

        public static Profile GetProfileFromExec(string executable)
        {
            var profile = profiles.Values.Where(a => a.Executable.Equals(executable, StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();
            return profile is not null ? profile : GetDefault();
        }
    }
}
