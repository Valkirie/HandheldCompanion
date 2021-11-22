using ControllerService;
using Force.Crc32;
using Microsoft.Extensions.Logging;
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
        public string name { get; set; }
        public string path { get; set; }
        public bool whitelisted { get; set; }       // if true, can see through the HidHide cloak
        public bool legacy { get; set; }            // not yet implemented
        public bool use_wrapper { get; set; }       // if true, deploy xinput1_3.dll
        public float gyrometer { get; set; }        // gyroscope multiplicator (remove me)
        public float accelerometer { get; set; }    // accelerometer multiplicator (remove me)

        [JsonIgnore] private const uint CRC32_X64 = 0x906f6806;
        [JsonIgnore] private const uint CRC32_X86 = 0x456b57cc;

        public Profile(string name, string path)
        {
            this.name = name;
            this.path = path;
        }

        public void Serialize()
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string jsonString = JsonSerializer.Serialize(this, options);

            string settingsPath = Path.Combine(ControllerHelper.CurrentPathProfiles, $"{name}.json");
            File.WriteAllText(settingsPath, jsonString);
        }

        public void Update()
        {
            // update cloaking
            UpdateCloacking();

            // update wrapper
            UpdateWrapper();
        }

        private void UpdateCloacking()
        {
            if (!File.Exists(path))
                return;

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
                args = new Dictionary<string, string> { { "path", path } }
            });
        }

        public void RegisterApplication()
        {
            ControllerHelper.PipeClient.SendMessage(new PipeMessage
            {
                Code = PipeCode.CLIENT_HIDDER_REG,
                args = new Dictionary<string, string> { { "path", path } }
            });
        }

        private void UpdateWrapper()
        {
            // deploy xinput wrapper
            string processpath = Path.GetDirectoryName(path);
            string dllpath = Path.Combine(processpath, "xinput1_3.dll");
            string inipath = Path.Combine(processpath, "x360ce.ini");
            bool wrapped = File.Exists(dllpath);
            bool is_x360ce = false;
            byte[] data;
            uint CRC32;

            // get binary type (x64, x86)
            BinaryType bt; GetBinaryType(path, out bt);

            // has dll, check if that's ours
            if (wrapped)
            {
                data = File.ReadAllBytes(dllpath);
                CRC32 = Crc32Algorithm.Compute(data);
                is_x360ce = bt == BinaryType.SCS_64BIT_BINARY ? (CRC32 == CRC32_X64) : (CRC32 == CRC32_X86);
            }

            data = bt == BinaryType.SCS_64BIT_BINARY ? Properties.Resources.xinput1_3_64 : Properties.Resources.xinput1_3_86;

            // has dll, not x360ce : backup
            if (use_wrapper && wrapped && !is_x360ce)
                File.Move(dllpath, $"{dllpath}.back");

            // no dll : create
            if (use_wrapper && !wrapped)
            {
                File.WriteAllBytes(dllpath, data);
                // todo: tweak guid / instance id
                File.WriteAllText(inipath, Properties.Resources.x360ce);
            }
            // has dll, is x360ce : remove
            else if (!use_wrapper && wrapped && is_x360ce)
            {
                File.Delete(dllpath);
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
                profile.Update();

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
