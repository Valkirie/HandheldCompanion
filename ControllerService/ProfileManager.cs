using Force.Crc32;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using static ControllerService.Utils;

namespace ControllerService
{
    public class Profile
    {
        public string name { get; set; }
        public string path { get; set; }
        public bool whitelisted { get; set; }   // if true, can see through the HidHide cloak
        public bool legacy { get; set; }        // not yet implemented
        public bool use_wrapper { get; set; }   // if true, deploy xinput1_3.dll
        public float gyrometer { get; set; } // gyroscope multiplicator
        public float accelerometer { get; set; } // accelerometer multiplicator

        private const uint CRC32_X64 = 0x906f6806;
        private const uint CRC32_X86 = 0x456b57cc;

        public void Serialize()
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string jsonString = JsonSerializer.Serialize(this, options);

            string settingsPath = Path.Combine(ControllerService.CurrentPathProfiles, $"{name}.json");
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
                ControllerService.Hidder.RegisterApplication(path);
            else
                ControllerService.Hidder.UnregisterApplication(path);
        }

        private void UpdateWrapper()
        {
            // deploy xinput wrapper
            string processpath = Path.GetDirectoryName(path);
            string dllpath = Path.Combine(processpath, "xinput1_3.dll");
            string inipath = Path.Combine(processpath, "x360ce.ini");
            bool wrapped = File.Exists(dllpath);

            // compute CRC32
            BinaryType bt; GetBinaryType(path, out bt);
            byte[] data = bt == BinaryType.SCS_64BIT_BINARY ? Properties.Resources.xinput1_3_x64 : Properties.Resources.xinput1_3_x86;
            uint CRC32 = Crc32Algorithm.Compute(data);
            bool x360ce = bt == BinaryType.SCS_64BIT_BINARY ? (CRC32 == CRC32_X64) : (CRC32 == CRC32_X86);

            // has dll, not x360ce : backup
            if (use_wrapper && wrapped && !x360ce)
                File.Move(dllpath, $"{dllpath}.back");

            // no dll : create
            if (use_wrapper && !wrapped)
            {
                File.WriteAllBytes(dllpath, data);
                File.WriteAllBytes(inipath, Properties.Resources.x360ce);
            }
            // has dll, is x360ce : remove
            else if (!use_wrapper && wrapped && x360ce)
            {
                File.Delete(dllpath);
                File.Delete(inipath);
            }
        }
    }

    public class ProfileManager
    {
        public Dictionary<string, Profile> profiles = new Dictionary<string, Profile>();
        public FileSystemWatcher profileWatcher { get; set; }

        private Dictionary<string, DateTime> dateTimeDictionary = new Dictionary<string, DateTime>();

        private readonly ILogger<ControllerService> logger;

        public ProfileManager(string path, string location, ILogger<ControllerService> logger)
        {
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

            // import profiles from HidHide
            List<string> hid_pathes = ControllerService.Hidder.GetRegisteredApplications();
            List<string> pro_pathes = profiles.Values.Select(a => a.path).ToList();
            pro_pathes.Add(location);

            foreach (string fileName in hid_pathes.Where(a => !pro_pathes.Contains(a)))
                ControllerService.Hidder.UnregisterApplication(fileName);
        }

        private void ProcessProfile(string fileName)
        {
            Profile output = null;
            try
            {
                string outputraw = File.ReadAllText(fileName);
                output = JsonSerializer.Deserialize<Profile>(outputraw);
            }
            catch (Exception ex)
            {
                logger.LogError($"Could not parse {fileName}. {ex.Message}");
            }

            // failed to parse
            if (output == null)
                return;

            if (File.Exists(fileName))
            {
                string ProcessName = Path.GetFileName(output.path);
                profiles[ProcessName] = output;
                output.Update();
            }
        }

        private void ProfileChanged(object sender, FileSystemEventArgs e)
        {
            if (!dateTimeDictionary.ContainsKey(e.FullPath) || (dateTimeDictionary.ContainsKey(e.FullPath) && System.IO.File.GetLastWriteTime(e.FullPath) != dateTimeDictionary[e.FullPath]))
            {
                dateTimeDictionary[e.FullPath] = System.IO.File.GetLastWriteTime(e.FullPath);

                ProcessProfile(e.FullPath);
                logger.LogInformation($"Updated profile {e.FullPath}");
            }
        }

        private void ProfileDeleted(object sender, FileSystemEventArgs e)
        {
            string fileName = Path.GetFileName(e.FullPath);
            string ProcessName = Path.GetFileName(fileName);

            if (profiles.ContainsKey(ProcessName))
                profiles.Remove(ProcessName);

            ControllerService.Hidder.UnregisterApplication(e.FullPath);
        }

        private void ProfileCreated(object sender, FileSystemEventArgs e)
        {
            ProcessProfile(e.FullPath);
            logger.LogInformation($"Created profile {e.FullPath}");
        }
    }
}
