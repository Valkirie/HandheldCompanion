using ControllerCommon;
using Force.Crc32;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using static ControllerCommon.Utils;

namespace ControllerHelper
{
    public class ProfileManager
    {
        public Dictionary<string, Profile> profiles = new Dictionary<string, Profile>();
        public FileSystemWatcher profileWatcher { get; set; }

        private Dictionary<string, DateTime> dateTimeDictionary = new Dictionary<string, DateTime>();

        private readonly ControllerHelper helper;
        private readonly ILogger logger;
        private string path;

        public ProfileManager(string path, ControllerHelper helper, ILogger logger)
        {
            this.helper = helper;
            this.logger = logger;
            this.path = path;

            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            // monitor changes, deletions and creations of profiles
            profileWatcher = new FileSystemWatcher()
            {
                Path = path,
                EnableRaisingEvents = true,
                IncludeSubdirectories = true,
                Filter = "*.json",
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size
            };

            // profileWatcher.Created += ProfileCreated;
            profileWatcher.Deleted += ProfileDeleted;
            // profileWatcher.Changed += ProfileChanged;

            // process existing profiles
            string[] fileEntries = Directory.GetFiles(path, "*.json", SearchOption.AllDirectories);
            foreach (string fileName in fileEntries)
                ProcessProfile(fileName);

            // create default profile if missing
            SetDefault();
        }

        private void ProfileChanged(object sender, FileSystemEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void ProfileDeleted(object sender, FileSystemEventArgs e)
        {
            string ProfileName = e.Name.Replace(".json", "");

            if (profiles.ContainsKey(ProfileName))
            {
                // you should not delete default profile, you fool !
                Profile profile = profiles[ProfileName];
                if (profile.IsDefault)
                    SerializeProfile(profile);
            }
        }

        private void ProfileCreated(object sender, FileSystemEventArgs e)
        {
            throw new NotImplementedException();
        }

        public void SetDefault()
        {
            SerializeProfile(new Profile("Default", ""));
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
                logger.LogError("Could not parse {0}. {1}", fileName, ex.Message);
            }

            // failed to parse
            if (profile == null || profile.name == null || profile.path == null)
            {
                logger.LogError("Could not parse {0}.", fileName);
                return;
            }

            if (profile.name == "Default")
                profile.IsDefault = true;

            UpdateProfile(profile);
        }

        /* private void ProfileChanged(object sender, FileSystemEventArgs e)
        {
            if (dateTimeDictionary.ContainsKey(e.FullPath) && File.GetLastWriteTime(e.FullPath) == dateTimeDictionary[e.FullPath])
                return;

            dateTimeDictionary[e.FullPath] = File.GetLastWriteTime(e.FullPath);

            ProcessProfile(e.FullPath);
            logger.LogInformation("Updated profile {0}", e.FullPath);
        }

        private void ProfileDeleted(object sender, FileSystemEventArgs e)
        {
            string ProfileName = e.Name.Replace(".json", "");

            if (profiles.ContainsKey(ProfileName))
            {
                Profile profile = profiles[ProfileName];
                UnregisterApplication(profile);
                profiles.Remove(ProfileName);

                helper.DeleteProfile(profile);
                logger.LogInformation("Deleted profile {0}", e.FullPath);
            }
        }

        private void ProfileCreated(object sender, FileSystemEventArgs e)
        {
            dateTimeDictionary[e.FullPath] = File.GetLastWriteTime(e.FullPath);

            ProcessProfile(e.FullPath);
            logger.LogInformation("Created profile {0}", e.FullPath);
        } */

        public void DeleteProfile(Profile profile)
        {
            string settingsPath = Path.Combine(path, $"{profile.name}.json");

            if (profiles.ContainsKey(profile.name))
            {
                UnregisterApplication(profile);
                profiles.Remove(profile.name);

                helper.DeleteProfile(profile);
                logger.LogInformation("Deleted profile {0}", settingsPath);
            }

            File.Delete(settingsPath);
        }

        public void SerializeProfile(Profile profile)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string jsonString = JsonSerializer.Serialize(profile, options);

            string settingsPath = Path.Combine(path, $"{profile.name}.json");
            File.WriteAllText(settingsPath, jsonString);
        }

        private ProfileErrorCode SanitizeProfile(Profile profile)
        {
            string processpath = Path.GetDirectoryName(profile.fullpath);

            if (!Directory.Exists(processpath))
                return ProfileErrorCode.MissingPath;
            else if (!File.Exists(profile.fullpath))
                return ProfileErrorCode.MissingExecutable;
            else if (!IsDirectoryWritable(processpath))
                return ProfileErrorCode.MissingPermission;

            return ProfileErrorCode.None;
        }

        public void UpdateProfile(Profile profile)
        {
            profiles[profile.name] = profile;
            profile.error = SanitizeProfile(profile);

            // update GUI
            helper.UpdateProfileList(profile);

            // update profile
            UpdateProfileCloaking(profile);

            if (profile.error != ProfileErrorCode.None && !profile.IsDefault)
            {
                logger.LogError("Profile {0} returned error code {1}", profile.name, profile.error);
                return;
            }

            UpdateProfileWrapper(profile);
        }

        public void UpdateProfileCloaking(Profile profile)
        {
            if (profile.whitelisted)
                RegisterApplication(profile);
            else
                UnregisterApplication(profile);
        }

        private Dictionary<bool, uint[]> CRCs = new Dictionary<bool, uint[]>()
        {
            { false, new uint[]{ 0x456b57cc, 0x456b57cc, 0x456b57cc, 0x456b57cc, 0x456b57cc } },
            { true, new uint[]{ 0x906f6806, 0x906f6806, 0x906f6806, 0x906f6806, 0x906f6806 } },
        };

        public void UpdateProfileWrapper(Profile profile)
        {
            // deploy xinput wrapper
            string x360ce = "";

            switch(helper.HIDmode)
            {
                case HIDmode.Xbox360Controller:
                    x360ce = Properties.Resources.Xbox360;
                    break;
                case HIDmode.DualShock4Controller:
                    x360ce = Properties.Resources.DualShock4;
                    break;
            }

            string[] fullpaths = new string[] { profile.fullpath };

            // for testing purposes, this should not happen!
            if (profile.IsDefault)
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
                string inipath = Path.Combine(processpath, "x360ce.ini");
                bool iniexist = File.Exists(inipath);

                // get binary type (x64, x86)
                BinaryType bt; GetBinaryType(fullpath, out bt);
                bool x64 = bt == BinaryType.SCS_64BIT_BINARY;

                if (profile.use_wrapper)
                    File.WriteAllText(inipath, x360ce);
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
                    bool is_x360ce = CRCs[x64][i] == crc;

                    switch (i)
                    {
                        case 0:
                            data = x64 ? Properties.Resources.xinput1_11 : Properties.Resources.xinput1_1;
                            break;
                        case 1:
                            data = x64 ? Properties.Resources.xinput1_21 : Properties.Resources.xinput1_2;
                            break;
                        default:
                        case 2:
                            data = x64 ? Properties.Resources.xinput1_31 : Properties.Resources.xinput1_3;
                            break;
                        case 3:
                            data = x64 ? Properties.Resources.xinput1_41 : Properties.Resources.xinput1_4;
                            break;
                        case 4:
                            data = x64 ? Properties.Resources.xinput9_1_01 : Properties.Resources.xinput9_1_0;
                            break;
                    }

                    if (profile.use_wrapper)
                    {
                        if (!IsFileWritable(dllpath))
                            SetFileWritable(dllpath);

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

        public void UnregisterApplication(Profile profile)
        {
            ControllerHelper.PipeClient.SendMessage(new PipeClientHidder
            {
                action = 1,
                path = profile.fullpath
            });
        }

        public void RegisterApplication(Profile profile)
        {
            ControllerHelper.PipeClient.SendMessage(new PipeClientHidder
            {
                action = 0,
                path = profile.fullpath
            });
        }
    }
}
