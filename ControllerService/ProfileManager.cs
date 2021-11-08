using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using static ControllerService.ControllerClient;

namespace ControllerService
{
    public class Profile
    {
        public string name { get; set; }
        public string path { get; set; }
        public bool whitelisted { get; set; }   // if true, can see through the HidHide cloak
        public bool legacy { get; set; }        // not yet implemented
        public bool use_wrapper { get; set; }   // if true, deploy xinput1_3.dll
        public float accelerometer { get; set; } // accelerometer multiplicator

        public void Serialize()
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string jsonString = JsonSerializer.Serialize(this, options);

            string settingsPath = Path.Combine(ControllerService.CurrentPathProfiles, this.name, "Settings.json");
            File.WriteAllText(settingsPath, jsonString);
        }
    }

    public class ProfileManager
    {
        public Dictionary<string, Profile> profiles = new Dictionary<string, Profile>();
        public FileSystemWatcher profileWatcher { get; set; }

        public ProfileManager(string path, string location)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            profileWatcher = new FileSystemWatcher()
            {
                Path = path,
                EnableRaisingEvents = true,
                IncludeSubdirectories = true
            };
            profileWatcher.Created += ProfileCreated;
            profileWatcher.Deleted += ProfileDeleted;
            profileWatcher.Changed += ProfileChanged;

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
            // Waits until a file can be opened with write permission
            Thread.Sleep(250);

            string outputraw = File.ReadAllText(fileName);
            Profile output = JsonSerializer.Deserialize<Profile>(outputraw);

            if (File.Exists(fileName))
            {
                string ProcessName = Path.GetFileName(output.path);
                profiles[ProcessName] = output;

                // cloak or uncloak application
                if (output.whitelisted)
                    ControllerService.Hidder.RegisterApplication(output.path);
                else
                    ControllerService.Hidder.UnregisterApplication(output.path);

                // deploy xinput wrapper
                string processpath = Path.GetDirectoryName(output.path);
                string dllpath = Path.Combine(processpath, "xinput1_3.dll");
                string inipath = Path.Combine(processpath, "x360ce.ini");
                bool wrapped = File.Exists(dllpath);

                if (output.use_wrapper && !wrapped)
                {
                    BinaryType bt;
                    GetBinaryType(output.path, out bt);

                    byte[] wrapper = bt == BinaryType.SCS_64BIT_BINARY ? Properties.Resources.xinput1_3_x64 : Properties.Resources.xinput1_3_x86;

                    File.WriteAllBytes(dllpath, wrapper);
                    File.WriteAllBytes(inipath, Properties.Resources.x360ce);
                }
                else if (!output.use_wrapper && wrapped)
                {
                    File.Delete(dllpath);
                    File.Delete(inipath);
                }
            }
        }

        private void ProfileChanged(object sender, FileSystemEventArgs e)
        {
            ProcessProfile(e.FullPath);
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
        }
    }
}
