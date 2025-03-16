using HandheldCompanion.Managers;
using HandheldCompanion.Shared;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;

namespace HandheldCompanion.Helpers
{
    public static class DriverStore
    {
        private static string DriversPath = Path.Combine(App.SettingsPath, "drivers.json");
        private static Dictionary<string, string> Drivers = [];

        private const string SettingsName = "KnownDrivers";

        static DriverStore()
        {
            // get driver store
            Drivers = DeserializeDriverStore();
        }

        public static IEnumerable<string> GetDrivers()
        {
            return Drivers.Values;
        }

        public static StringCollection GetKnownDrivers()
        {
            StringCollection stringCollection = ManagerFactory.settingsManager.GetStringCollection(SettingsName);
            if (stringCollection is null)
                stringCollection = new();

            return stringCollection;
        }

        public static void StoreKnownDriver(string driverName)
        {
            StringCollection stringCollection = GetKnownDrivers();
            if (!stringCollection.Contains(driverName))
            {
                stringCollection.Add(driverName);
                ManagerFactory.settingsManager.SetProperty(SettingsName, stringCollection);
            }
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
            Dictionary<string, string>? result = new();
            if (!File.Exists(DriversPath))
                return result;

            try
            {
                string json = File.ReadAllText(DriversPath);
                result = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
            }
            catch (Exception ex)
            {
                LogManager.LogError("Could not retrieve drivers store {0}", ex.Message);
            }

            return result ?? new Dictionary<string, string>(); // Ensure it's never null
        }

        public static string GetDriverFromDriverStore(string path)
        {
            if (Drivers.TryGetValue(path, out string driver))
                return driver;

            return "xusb22.inf";
        }

        public static void AddOrUpdateDriverStore(string path, string driverName)
        {
            // upcase
            path = path.ToUpper();

            // update array
            Drivers[path] = driverName;

            // update settings (failsafe)
            StoreKnownDriver(driverName);

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
