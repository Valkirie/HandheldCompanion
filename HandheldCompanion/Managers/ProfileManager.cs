using ControllerCommon;
using ControllerCommon.Managers;
using ControllerCommon.Utils;
using Force.Crc32;
using HandheldCompanion.Views;
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
        private static Dictionary<bool, uint> CRCs = new Dictionary<bool, uint>()
        {
            { false, 0xcd4906cc },
            { true, 0x1e9df650 },
        };

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
        public delegate void DiscardedEventHandler(Profile profile, bool isCurrent);
        #endregion

        private static Profile currentProfile = new();

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
                Profile profile = GetProfileFromExec(processEx.Name);

                if (profile is null || profile.Default)
                    return;

                if (profile.Running)
                {
                    profile.Running = false;

                    // warn owner
                    bool isCurrent = profile.Executable == currentProfile.Executable;

                    // (re)set current profile
                    if (isCurrent)
                        currentProfile = profile;

                    // raise event
                    Discarded?.Invoke(profile, isCurrent);

                    // update profile
                    UpdateOrCreateProfile(profile);
                }
            }
            catch { }
        }

        private static void ProcessManager_ProcessStarted(ProcessEx processEx, bool startup)
        {
            try
            {
                Profile profile = GetProfileFromExec(processEx.Name);

                if (profile is null || profile.Default)
                    return;

                profile.ExecutablePath = processEx.Path;
                profile.Running = true;

                // update profile
                UpdateOrCreateProfile(profile);
            }
            catch { }
        }

        private static void ProcessManager_ForegroundChanged(ProcessEx proc, ProcessEx back)
        {
            try
            {
                var profile = GetProfileFromExec(proc.Name);

                if (!profile.Enabled)
                    return;

                // skip if is current profile
                if (currentProfile == profile)
                    return;

                // raise event
                Discarded?.Invoke(currentProfile, true);
                Applied?.Invoke(profile);

                // update current profile
                currentProfile = profile;

                LogManager.LogInformation("Profile {0} applied", profile.Name);

                // inform service
                PipeClient.SendMessage(new PipeClientProfile { profile = profile });

                // do not update default profile path
                if (profile.Default)
                    return;

                // send toast
                // todo: localize me
                MainWindow.toastManager.SendToast($"Profile {profile.Name} applied");

                profile.Running = true;
                profile.ExecutablePath = proc.Path;
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

            switch (ProfileName)
            {
                // prevent default profile from being deleted
                case DefaultName:
                    SerializeProfile(profile);
                    break;
                default:
                    DeleteProfile(profile);
                    break;
            }
        }

        private static bool HasDefault()
        {
            return profiles.ContainsKey(DefaultName);
        }

        public static Profile GetDefault()
        {
            if (HasDefault())
                return profiles[DefaultName];
            return new();
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
                profile.ExecutablePath = profile.Path;
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

            if (profile.Default)
                currentProfile = profile;

            UpdateOrCreateProfile(profile, ProfileUpdateSource.Serialiazer);
        }

        public static void DeleteProfile(Profile profile)
        {
            string settingsPath = Path.Combine(InstallPath, profile.GetFileName());

            if (profiles.ContainsKey(profile.Name))
            {
                // Unregister application from HidHide
                HidHide.UnregisterApplication(profile.ExecutablePath);

                profiles.Remove(profile.Name);

                // warn owner
                bool isCurrent = profile.Executable == currentProfile.Executable;

                // (re)set current profile
                if (isCurrent)
                    currentProfile = GetDefault();

                // raise event(s)
                Deleted?.Invoke(profile);
                Discarded?.Invoke(profile, isCurrent);

                // send toast
                // todo: localize me
                MainWindow.toastManager.SendToast($"Profile {profile.Name} deleted");

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
            string processpath = Path.GetDirectoryName(profile.ExecutablePath);

            if (profile.Default)
                return ProfileErrorCode.Default;
            else
            {
                if (!Directory.Exists(processpath))
                    return ProfileErrorCode.MissingPath;
                else if (!File.Exists(profile.ExecutablePath))
                    return ProfileErrorCode.MissingExecutable;
                else if (!CommonUtils.IsDirectoryWritable(processpath))
                    return ProfileErrorCode.MissingPermission;
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
            bool hasprocesses = ProcessManager.GetProcesses(profile.Executable).Capacity > 0;
            profile.Running = hasprocesses;

            // check if this is current profile
            bool isCurrent = profile.Executable == currentProfile.Executable;

            // refresh error code
            profile.ErrorCode = SanitizeProfile(profile);

            // update database
            profiles[profile.Name] = profile;

            // raise event(s)
            Updated?.Invoke(profile, source, isCurrent);

            // inform service
            if (isCurrent)
                PipeClient.SendMessage(new PipeClientProfile { profile = currentProfile });

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
            if (profile.ErrorCode == ProfileErrorCode.MissingExecutable || profile.ErrorCode == ProfileErrorCode.MissingPath)
                return;

            if (profile.Whitelisted)
            {
                // Register application on HidHide
                HidHide.RegisterApplication(profile.ExecutablePath);
            }
            else
            {
                // Unregister application from HidHide
                HidHide.UnregisterApplication(profile.ExecutablePath);
            }
        }

        public static void UpdateProfileWrapper(Profile profile)
        {
            // deploy xinput wrapper
            string XinputPlus = Properties.Resources.XInputPlus;

            string[] fullpaths = new string[] { profile.ExecutablePath };

            // for testing purposes, this should not happen!
            if (profile.Default)
            {
                fullpaths = new string[]
                {
                    @"C:\Windows\System32\cmd.exe",
                    @"C:\Windows\SysWOW64\cmd.exe"
                };
            }

            foreach (string fullpath in fullpaths)
            {
                string processpath = Path.GetDirectoryName(fullpath);
                string inipath = Path.Combine(processpath, "XInputPlus.ini");
                bool iniexist = File.Exists(inipath);

                // get binary type (x64, x86)
                BinaryType bt; GetBinaryType(fullpath, out bt);
                bool x64 = bt == BinaryType.SCS_64BIT_BINARY;

                if (profile.XInputPlus)
                    File.WriteAllText(inipath, XinputPlus);
                else if (iniexist)
                    File.Delete(inipath);

                for (int i = 0; i < 5; i++)
                {
                    string dllpath = Path.Combine(processpath, $"xinput1_{i + 1}.dll");
                    string backpath = Path.Combine(processpath, $"xinput1_{i + 1}.back");

                    // dll has a different naming format
                    if (i == 4)
                    {
                        dllpath = Path.Combine(processpath, $"xinput9_1_0.dll");
                        backpath = Path.Combine(processpath, $"xinput9_1_0.back");
                    }

                    bool dllexist = File.Exists(dllpath);
                    bool backexist = File.Exists(backpath);

                    byte[] data = new byte[] { 0 };

                    // check CRC32
                    if (dllexist) data = File.ReadAllBytes(dllpath);
                    var crc = Crc32Algorithm.Compute(data);
                    bool is_x360ce = CRCs[x64] == crc;

                    // pull data from dll
                    data = x64 ? Properties.Resources.xinput1_x64 : Properties.Resources.xinput1_x86;

                    // do not try to write/erase files when profile is used
                    if (profile.Running)
                        return;

                    switch (profile.ErrorCode)
                    {
                        // do not try to write/erase files when access is denied
                        case ProfileErrorCode.MissingPermission:
                        case ProfileErrorCode.MissingPath:
                            return;
                    }

                    if (profile.XInputPlus)
                    {
                        if (dllexist && is_x360ce)
                            continue; // skip to next file
                        else if (!dllexist)
                            File.WriteAllBytes(dllpath, data);
                        else if (dllexist && !is_x360ce)
                        {
                            // create backup of current dll
                            if (!backexist)
                                File.Move(dllpath, backpath);

                            // deploy wrapper
                            File.WriteAllBytes(dllpath, data);
                        }
                    }
                    else
                    {
                        // delete wrapper dll
                        if (dllexist && is_x360ce)
                            File.Delete(dllpath);

                        // restore backup is exists
                        if (backexist)
                            File.Move(backpath, dllpath);
                    }
                }
            }
        }

        public static Profile GetProfileFromExec(string executable)
        {
            var profile = profiles.Values.Where(a => a.Executable.Equals(executable, StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();
            return profile is not null ? profile : GetDefault();
        }
    }
}
