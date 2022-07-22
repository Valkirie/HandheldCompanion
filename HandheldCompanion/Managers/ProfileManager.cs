using ControllerCommon;
using ControllerCommon.Managers;
using ControllerCommon.Utils;
using Force.Crc32;
using HandheldCompanion.Views;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Resources;
using System.Text.Json;
using static ControllerCommon.Utils.ProcessUtils;

namespace HandheldCompanion.Managers
{
    public class ProfileManager
    {
        private Dictionary<bool, uint> CRCs = new Dictionary<bool, uint>()
        {
            { false, 0xcd4906cc },
            { true, 0x1e9df650 },
        };

        public Dictionary<string, Profile> profiles = new Dictionary<string, Profile>(StringComparer.InvariantCultureIgnoreCase);
        public FileSystemWatcher profileWatcher { get; set; }

        #region events
        public event DeletedEventHandler Deleted;
        public delegate void DeletedEventHandler(Profile profile);
        public event UpdatedEventHandler Updated;
        public delegate void UpdatedEventHandler(Profile profile, bool backgroundtask);
        public event LoadedEventHandler Ready;
        public delegate void LoadedEventHandler();

        public event AppliedEventHandler Applied;
        public delegate void AppliedEventHandler(Profile profile);

        public event DiscardedEventHandler Discarded;
        public delegate void DiscardedEventHandler(Profile profile);
        #endregion

        public Profile CurrentProfile = new();

        private string path;

        public ProfileManager()
        {
            MainWindow.processManager.ForegroundChanged += ProcessManager_ForegroundChanged;
            MainWindow.processManager.ProcessStarted += ProcessManager_ProcessStarted;
            MainWindow.processManager.ProcessStopped += ProcessManager_ProcessStopped;
        }

        public void Start(string filter = "*.json")
        {
            path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "HandheldCompanion", "profiles");

            // initialize folder
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            // initialize profile files
            ResourceSet resourceSet = Properties.Resources.ResourceManager.GetResourceSet(CultureInfo.CurrentUICulture, true, true);
            foreach (DictionaryEntry entry in resourceSet)
            {
                string resourceKey = entry.Key.ToString();
                if (!resourceKey.Contains(".json"))
                    continue;

                object resource = entry.Value;
                string profile_path = Path.Combine(path, resourceKey);

                if (!File.Exists(profile_path))
                    File.WriteAllText(profile_path, (string)resource);
            }

            // monitor profile file deletions
            profileWatcher = new FileSystemWatcher()
            {
                Path = path,
                EnableRaisingEvents = true,
                IncludeSubdirectories = true,
                Filter = "*.json",
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size
            };
            profileWatcher.Deleted += ProfileDeleted;

            // process existing profiles
            string[] fileEntries = Directory.GetFiles(path, filter, SearchOption.AllDirectories);
            foreach (string fileName in fileEntries)
                ProcessProfile(fileName);

            // warn owner
            Ready?.Invoke();
        }

        public void Stop()
        {
            profileWatcher.Deleted -= ProfileDeleted;
            profileWatcher.Dispose();
        }

        public bool Contains(Profile profile)
        {
            foreach (Profile pr in profiles.Values)
                if (pr.executable == profile.executable)
                    return true;

            return false;
        }

        public int GetProfileIndex(Profile profile)
        {
            int idx = -1;

            for (int i = 0; i < profiles.Count; i++)
            {
                Profile pr = profiles.Values.ToList()[i];
                if (pr.executable == profile.executable)
                    return i;
            }

            return idx;
        }

        private void ProcessManager_ProcessStopped(ProcessEx processEx)
        {
            try
            {
                Profile profile = GetProfileFromExec(processEx.Name);

                if (profile == null)
                    return;

                if (profile.isRunning)
                {
                    profile.isRunning = false;

                    // raise event
                    Discarded?.Invoke(profile);

                    // update profile
                    UpdateOrCreateProfile(profile);
                }
            }
            catch (Exception) { }
        }

        private void ProcessManager_ProcessStarted(ProcessEx processEx)
        {
            try
            {
                Profile profile = GetProfileFromExec(processEx.Name);

                if (profile == null)
                    return;

                profile.fullpath = processEx.Path;
                profile.isRunning = true;

                // update profile
                UpdateOrCreateProfile(profile);
            }
            catch (Exception) { }
        }

        private void ProcessManager_ForegroundChanged(ProcessEx processEx)
        {
            try
            {
                var profile = GetProfileFromExec(processEx.Name);

                if (profile == null)
                    profile = GetDefault();

                if (!profile.isEnabled)
                    return;

                // update current profile
                CurrentProfile = profile;

                // raise event
                Applied?.Invoke(profile);

                LogManager.LogDebug("Profile {0} applied", profile.name);

                // do not update default profile path
                if (profile.isDefault)
                    return;

                profile.isRunning = true;
                profile.fullpath = processEx.Path;
                UpdateOrCreateProfile(profile);
            }
            catch (Exception) { }
        }

        private void ProfileDeleted(object sender, FileSystemEventArgs e)
        {
            string ProfileName = e.Name.Replace(".json", "");

            if (!profiles.ContainsKey(ProfileName))
                return;

            Profile profile = profiles[ProfileName];

            switch (ProfileName)
            {
                case "Default":
                    SerializeProfile(profile);
                    break;
                default:
                    DeleteProfile(profile);
                    break;
            }
        }

        public Profile GetDefault()
        {
            if (profiles.ContainsKey("Default"))
                return profiles["Default"];
            return null;
        }

        private void ProcessProfile(string fileName)
        {
            Profile profile = null;
            try
            {
                string outputraw = File.ReadAllText(fileName);
                profile = JsonSerializer.Deserialize<Profile>(outputraw);
                profile.fullpath = profile.path;
            }
            catch (Exception ex)
            {
                LogManager.LogError("Could not parse {0}. {1}", fileName, ex.Message);
            }

            // failed to parse
            if (profile == null || profile.name == null || profile.path == null)
            {
                LogManager.LogError("Could not parse {0}.", fileName);
                return;
            }

            if (profile.name == "Default")
                profile.isDefault = true;

            UpdateOrCreateProfile(profile);
        }

        public void DeleteProfile(Profile profile)
        {
            string settingsPath = Path.Combine(path, profile.json);

            if (profiles.ContainsKey(profile.name))
            {
                UnregisterApplication(profile);
                profiles.Remove(profile.name);

                // raise event(s)
                Deleted?.Invoke(profile);
                Discarded?.Invoke(profile);

                LogManager.LogInformation("Deleted profile {0}", settingsPath);
            }

            File.Delete(settingsPath);
        }

        public void SerializeProfile(Profile profile)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string jsonString = JsonSerializer.Serialize(profile, options);

            string settingsPath = Path.Combine(path, profile.json);
            File.WriteAllText(settingsPath, jsonString);
        }

        private ProfileErrorCode SanitizeProfile(Profile profile)
        {
            string processpath = Path.GetDirectoryName(profile.fullpath);

            if (profile.isDefault)
                return ProfileErrorCode.IsDefault;
            else
            {
                if (!Directory.Exists(processpath))
                    return ProfileErrorCode.MissingPath;
                else if (!File.Exists(profile.fullpath))
                    return ProfileErrorCode.MissingExecutable;
                else if (!CommonUtils.IsDirectoryWritable(processpath))
                    return ProfileErrorCode.MissingPermission;
            }

            return ProfileErrorCode.None;
        }

        public void UpdateOrCreateProfile(Profile profile, bool backgroundtask = true)
        {
            // refresh error code
            profile.error = SanitizeProfile(profile);
            profile.json = $"{Path.GetFileNameWithoutExtension(profile.executable)}.json";

            // update database
            profiles[profile.name] = profile;

            // update cloaking
            UpdateProfileCloaking(profile);

            // warn owner
            Updated?.Invoke(profile, backgroundtask);

            // warn other windows
            if (profile.executable == CurrentProfile.executable)
                Applied?.Invoke(profile);

            if (profile.error != ProfileErrorCode.None && !profile.isDefault)
            {
                LogManager.LogError("Profile {0} returned error code {1}", profile.name, profile.error);
                return;
            }

            // update wrapper
            UpdateProfileWrapper(profile);
        }

        public void UpdateProfileCloaking(Profile profile)
        {
            if (profile.error == ProfileErrorCode.MissingExecutable || profile.error == ProfileErrorCode.MissingPath)
                return;

            if (profile.whitelisted)
                RegisterApplication(profile);
            else
                UnregisterApplication(profile);
        }

        public void UpdateProfileWrapper(Profile profile)
        {
            // deploy xinput wrapper
            string XinputPlus = Properties.Resources.XInputPlus;

            string[] fullpaths = new string[] { profile.fullpath };

            // for testing purposes, this should not happen!
            if (profile.isDefault)
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

                if (profile.use_wrapper)
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

                    if (profile.use_wrapper)
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

        public Profile GetProfileFromExec(string processExec)
        {
            foreach (Profile pr in profiles.Values)
                if (pr.executable.Equals(processExec, StringComparison.InvariantCultureIgnoreCase))
                    return pr;
            return null;
        }

        public void UnregisterApplication(Profile profile)
        {
            MainWindow.pipeClient?.SendMessage(new PipeClientHidder
            {
                action = HidderAction.Unregister,
                path = profile.fullpath
            });
        }

        public void RegisterApplication(Profile profile)
        {
            MainWindow.pipeClient?.SendMessage(new PipeClientHidder
            {
                action = HidderAction.Register,
                path = profile.fullpath
            });
        }
    }
}
