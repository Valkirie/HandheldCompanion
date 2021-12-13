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
            string processpath = Path.GetDirectoryName(profile.fullpath);

            string dllpath = Path.Combine(processpath, "xinput1_3.dll");
            string inipath = Path.Combine(processpath, "x360ce.ini");
            bool wrapped = File.Exists(dllpath);
            bool is_x360ce = false;
            byte[] data;
            uint CRC32;

            // get binary type (x64, x86)
            BinaryType bt; GetBinaryType(profile.fullpath, out bt);

            // has dll, check if that's ours
            if (wrapped)
            {
                data = File.ReadAllBytes(dllpath);
                CRC32 = Crc32Algorithm.Compute(data);
                is_x360ce = bt == BinaryType.SCS_64BIT_BINARY ? (CRC32 == CRC32_X64) : (CRC32 == CRC32_X86);
            }

            // update data array to appropriate resource
            data = bt == BinaryType.SCS_64BIT_BINARY ? Properties.Resources.xinput1_3_64 : Properties.Resources.xinput1_3_86;

            // has xinput1_3.dll but failed CRC check : create backup
            if (profile.use_wrapper && wrapped && !is_x360ce)
                File.Move(dllpath, $"{dllpath}.back");

            if (profile.use_wrapper)
            {
                // no xinput1_3.dll : deploy wrapper
                if (!wrapped)
                {
                    if (IsDirectoryWritable(processpath))
                        File.WriteAllBytes(dllpath, data);
                }

                string x360ce = Properties.Resources.x360ce;
                if (IsDirectoryWritable(processpath))
                    File.WriteAllText(inipath, x360ce);
            }
            else if (!profile.use_wrapper && wrapped && is_x360ce)
            {
                // has wrapped : delete it
                if (File.Exists(dllpath))
                    File.Delete(dllpath);
                if (File.Exists(inipath))
                    File.Delete(inipath);
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
