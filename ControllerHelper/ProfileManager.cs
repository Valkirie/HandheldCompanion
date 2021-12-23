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

        private const uint CRC32_X64 = 0x906f6806;
        private const uint CRC32_X86 = 0x456b57cc;

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

            /* monitor changes, deletions and creations of profiles
            profileWatcher = new FileSystemWatcher()
            {
                Path = path,
                EnableRaisingEvents = true,
                IncludeSubdirectories = true,
                Filter = "*.json",
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size
            };

            profileWatcher.Created += ProfileCreated;
            profileWatcher.Deleted += ProfileDeleted;
            profileWatcher.Changed += ProfileChanged; */

            // process existing profiles
            string[] fileEntries = Directory.GetFiles(path, "*.json", SearchOption.AllDirectories);
            foreach (string fileName in fileEntries)
                ProcessProfile(fileName);
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
            if (IsDirectoryWritable(path))
            {
                File.WriteAllText(settingsPath, jsonString);
                UpdateProfile(profile);
            }
        }

        private ProfileErrorCode SanitizeProfile(Profile profile)
        {
            string processpath = Path.GetDirectoryName(profile.fullpath);

            if (!Directory.Exists(processpath))
                return ProfileErrorCode.MissingPath;
            else if (!File.Exists(profile.fullpath))
                return ProfileErrorCode.MissingExecutable;

            return ProfileErrorCode.None;
        }

        public void UpdateProfile(Profile profile)
        {
            profiles[profile.name] = profile;
            profile.error = SanitizeProfile(profile);

            // update GUI
            helper.UpdateProfileList(profile);

            if (profile.error != ProfileErrorCode.None)
            {
                logger.LogError("Profile {0} returned error code {1}", profile.name, profile.error);
                return;
            }

            UpdateProfileCloaking(profile);
            UpdateProfileWrapper(profile);
        }

        public void UpdateProfileCloaking(Profile profile)
        {
            if (profile.whitelisted)
                RegisterApplication(profile);
            else
                UnregisterApplication(profile);
        }

        public void UpdateProfileWrapper(Profile profile)
        {
            // deploy xinput wrapper
            string x360ce = Properties.Resources.x360ce;
            string processpath = Path.GetDirectoryName(profile.fullpath);
            string inipath = Path.Combine(processpath, "x360ce.ini");

            if (!IsDirectoryWritable(processpath))
                return;

            if (profile.use_wrapper)
                File.WriteAllText(inipath, x360ce);

            // get binary type (x64, x86)
            BinaryType bt; GetBinaryType(profile.fullpath, out bt);

            for (int i = 1; i < 5; i++)
            {
                string dllpath = Path.Combine(processpath, $"xinput1_{i}.dll");
                string backpath = Path.Combine(processpath, $"xinput1_{i}.back");
                byte[] data;

                switch (i)
                {
                    case 1:
                        data = bt == BinaryType.SCS_64BIT_BINARY ? Properties.Resources.xinput1_1_64 : Properties.Resources.xinput1_1_86;
                        break;
                    case 2:
                        data = bt == BinaryType.SCS_64BIT_BINARY ? Properties.Resources.xinput1_2_64 : Properties.Resources.xinput1_2_86;
                        break;
                    default:
                    case 3:
                        data = bt == BinaryType.SCS_64BIT_BINARY ? Properties.Resources.xinput1_3_64 : Properties.Resources.xinput1_3_86;
                        break;
                    case 4:
                        data = bt == BinaryType.SCS_64BIT_BINARY ? Properties.Resources.xinput1_4_64 : Properties.Resources.xinput1_4_86;
                        break;
                }

                if (profile.use_wrapper)
                {
                    // create backup if does not exist
                    if (!File.Exists(backpath))
                        File.Move(dllpath, backpath);

                    // deploy wrapper
                    if (!File.Exists(dllpath))
                        File.WriteAllBytes(dllpath, data);
                }
                else
                {
                    // delete wrapper if exists
                    if (File.Exists(dllpath))
                        File.Delete(dllpath);

                    // restore backup is exists
                    if (File.Exists(backpath))
                        File.Move(backpath, dllpath);
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
