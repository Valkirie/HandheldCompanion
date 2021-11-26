using ControllerService;
using Force.Crc32;
using Serilog.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using static ControllerService.Utils;

namespace ControllerHelper
{
    [Serializable]
    public class Profile
    {
        public enum ErrorCode
        {
            None = 0,
            MissingExecutable = 1,
            MissingPath = 2
        }

        public string name { get; set; }
        public string path { get; set; }
        public bool whitelisted { get; set; }               // if true, can see through the HidHide cloak
        public bool legacy { get; set; }                    // not yet implemented
        public bool use_wrapper { get; set; }               // if true, deploy xinput1_3.dll
        public float gyrometer { get; set; } = 1.0f;        // gyroscope multiplicator (remove me)
        public float accelerometer { get; set; } = 1.0f;    // accelerometer multiplicator (remove me)

        [JsonIgnore] private const uint CRC32_X64 = 0x906f6806;
        [JsonIgnore] private const uint CRC32_X86 = 0x456b57cc;
        [JsonIgnore] public ErrorCode error;
        [JsonIgnore] public string fullpath { get; set; }

        public Profile(string name, string path)
        {
            this.name = name;
            this.path = path;
            this.fullpath = path;
        }

        public void Delete()
        {
            string settingsPath = Path.Combine(ControllerHelper.CurrentPathProfiles, $"{name}.json");
            File.Delete(settingsPath);
        }

        public void Serialize()
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string jsonString = JsonSerializer.Serialize(this, options);

            string settingsPath = Path.Combine(ControllerHelper.CurrentPathProfiles, $"{name}.json");
            File.WriteAllText(settingsPath, jsonString);
        }

        public void Update(Logger logger)
        {
            error = SanityCheck();

            if (error != ErrorCode.None)
            {
                logger.Error("Profile {0} returned error code {1}", this.name, this.error);
                return;
            }

            UpdateCloacking();
            UpdateWrapper();
        }

        private ErrorCode SanityCheck()
        {
            string processpath = Path.GetDirectoryName(fullpath);

            if (!Directory.Exists(processpath))
                return ErrorCode.MissingPath;
            else if (!File.Exists(fullpath))
                return ErrorCode.MissingExecutable;

            return ErrorCode.None;
        }

        private void UpdateCloacking()
        {
            if (whitelisted)
                RegisterApplication();
            else
                UnregisterApplication();
        }

        public void UnregisterApplication()
        {
            ControllerHelper.PipeClient.SendMessage(new PipeMessage
            {
                Code = PipeCode.CLIENT_HIDDER_UNREG,
                args = new Dictionary<string, string> { { "path", fullpath } }
            });
        }

        public void RegisterApplication()
        {
            ControllerHelper.PipeClient.SendMessage(new PipeMessage
            {
                Code = PipeCode.CLIENT_HIDDER_REG,
                args = new Dictionary<string, string> { { "path", fullpath } }
            });
        }

        private void UpdateWrapper()
        {
            // deploy xinput wrapper
            string processpath = Path.GetDirectoryName(fullpath);

            string dllpath = Path.Combine(processpath, "xinput1_3.dll");
            string inipath = Path.Combine(processpath, "x360ce.ini");
            bool wrapped = File.Exists(dllpath);
            bool is_x360ce = false;
            byte[] data;
            uint CRC32;

            // get binary type (x64, x86)
            BinaryType bt; GetBinaryType(fullpath, out bt);

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
            if (use_wrapper && wrapped && !is_x360ce)
                File.Move(dllpath, $"{dllpath}.back");

            if (use_wrapper)
            {
                // no xinput1_3.dll : deploy wrapper
                if (!wrapped)
                    File.WriteAllBytes(dllpath, data);

                string x360ce = Properties.Resources.x360ce;
                File.WriteAllText(inipath, x360ce);
            }
            else if (!use_wrapper && wrapped && is_x360ce)
            {
                // has wrapped : delete it
                if (File.Exists(dllpath))
                    File.Delete(dllpath);
                if (File.Exists(inipath))
                    File.Delete(inipath);
            }
        }

        public override string ToString()
        {
            return name;
        }
    }

    public class ProfileManager
    {
        public Dictionary<string, Profile> profiles = new Dictionary<string, Profile>();
        public FileSystemWatcher profileWatcher { get; set; }

        private Dictionary<string, DateTime> dateTimeDictionary = new Dictionary<string, DateTime>();

        private readonly ControllerHelper helper;
        private readonly Logger logger;

        public ProfileManager(string path, ControllerHelper helper, Logger logger)
        {
            this.helper = helper;
            this.logger = logger;

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

            profileWatcher.Created += ProfileCreated;
            profileWatcher.Deleted += ProfileDeleted;
            profileWatcher.Changed += ProfileChanged;

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
                logger.Error("Could not parse {0}. {1}", fileName, ex.Message);
            }

            // failed to parse
            if (profile == null)
                return;

            if (File.Exists(fileName))
            {
                string ProcessName = Path.GetFileName(profile.path);
                profiles[ProcessName] = profile;
                profile.Update(logger);

                helper.UpdateProfile(profile);
            }
        }

        private void ProfileChanged(object sender, FileSystemEventArgs e)
        {
            if (dateTimeDictionary.ContainsKey(e.FullPath) && File.GetLastWriteTime(e.FullPath) == dateTimeDictionary[e.FullPath])
                return;

            dateTimeDictionary[e.FullPath] = File.GetLastWriteTime(e.FullPath);

            ProcessProfile(e.FullPath);
            logger.Information("Updated profile {0}", e.FullPath);
        }

        private void ProfileDeleted(object sender, FileSystemEventArgs e)
        {
            string ProfileName = e.Name.Replace(".json", "");

            if (profiles.ContainsKey(ProfileName))
            {
                Profile profile = profiles[ProfileName];
                profile.UnregisterApplication();
                profiles.Remove(ProfileName);

                helper.DeleteProfile(profile);
                logger.Information("Deleted profile {0}", e.FullPath);
            }
        }

        private void ProfileCreated(object sender, FileSystemEventArgs e)
        {
            dateTimeDictionary[e.FullPath] = File.GetLastWriteTime(e.FullPath);

            ProcessProfile(e.FullPath);
            logger.Information("Created profile {0}", e.FullPath);
        }
    }
}
