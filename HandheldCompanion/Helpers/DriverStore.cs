using HandheldCompanion.Shared;
using HandheldCompanion.Views;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using static PInvoke.Kernel32;

namespace HandheldCompanion.Helpers
{
    public static class DriverStore
    {
        private static string DriversPath = Path.Combine(MainWindow.SettingsPath, "drivers.json");
        private static Dictionary<string, string> Drivers = [];

        static DriverStore()
        {
            // get driver store
            Drivers = DeserializeDriverStore();
        }

        public static IEnumerable<string> GetDrivers()
        {
            return Drivers.Values;
        }

        public static IEnumerable<string> GetPaths()
        {
            return Drivers.Keys;
        }

        private static void SerializeDriverStore()
        {
            string json = JsonConvert.SerializeObject(Drivers, Formatting.Indented);
            File.WriteAllText(DriversPath, json);
        }

        private static Dictionary<string, string> DeserializeDriverStore()
        {
            if (!File.Exists(DriversPath))
                return [];

            try
            {
                string json = File.ReadAllText(DriversPath);
                return JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
            }
            catch (Exception ex)
            {
                LogManager.LogError("Could not retrieve drivers store {0}", ex.Message);
            }

            return [];
        }

        public static string GetDriverFromDriverStore(string path)
        {
            if (Drivers.TryGetValue(path, out string driver))
                return driver;

            return "xusb22.inf";
        }

        public static void AddOrUpdateDriverStore(string path, string calibration)
        {
            // upcase
            path = path.ToUpper();

            // update array
            Drivers[path] = calibration;

            // serialize store
            SerializeDriverStore();
        }

        public static void RemoveFromDriverStore(string path)
        {
            // upcase
            path = path.ToUpper();

            // update array
            Drivers.Remove(path);

            // serialize store
            SerializeDriverStore();
        }
    }
}
