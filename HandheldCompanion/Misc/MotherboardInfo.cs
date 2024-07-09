using HandheldCompanion.Devices;
using HandheldCompanion.Views;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;

namespace HandheldCompanion;

public static class MotherboardInfo
{
    private static readonly ManagementObjectSearcher baseboardSearcher = new("root\\CIMV2", "SELECT * FROM Win32_BaseBoard");
    private static ManagementObjectCollection? baseboardCollection;

    private static readonly ManagementObjectSearcher motherboardSearcher = new("root\\CIMV2", "SELECT * FROM Win32_MotherboardDevice");
    private static ManagementObjectCollection? motherboardCollection;

    private static readonly ManagementObjectSearcher processorSearcher = new("root\\CIMV2", "SELECT * FROM Win32_Processor");
    private static ManagementObjectCollection? processorCollection;

    private static readonly ManagementObjectSearcher displaySearcher = new("root\\CIMV2", "SELECT * FROM Win32_DisplayConfiguration");
    private static ManagementObjectCollection? displayCollection;

    private static readonly ManagementObjectSearcher videoControllerSearcher = new("root\\CIMV2", "SELECT * FROM Win32_VideoController");
    private static ManagementObjectCollection? videoControllerCollection;

    private static readonly ManagementObjectSearcher computerSearcher = new("root\\CIMV2", "SELECT * FROM Win32_ComputerSystem");
    private static ManagementObjectCollection? computerCollection;

    private static object cacheLock = new();
    private static Dictionary<string, object> cache = new();

    private static readonly string cacheDirectory;
    private const string fileName = "motherboard.json";

    static MotherboardInfo()
    {
        cacheDirectory = Path.Combine(MainWindow.SettingsPath, "cache");
        if (!Directory.Exists(cacheDirectory))
            Directory.CreateDirectory(cacheDirectory);
    }

    private static Dictionary<string, KeyValuePair<ManagementObjectCollection, ManagementObjectSearcher>> collections = new()
    {
        { "baseboard", new(baseboardCollection, baseboardSearcher) },
        { "motherboard", new(motherboardCollection, motherboardSearcher) },
        { "processor", new(processorCollection, processorSearcher) },
        { "display", new(displayCollection, displaySearcher) },
        { "video", new(videoControllerCollection, videoControllerSearcher) },
        { "computer", new(computerCollection, computerSearcher) },
    };

    // unused
    public static string Availability
    {
        get
        {
            string result = Convert.ToString(queryCacheValue("motherboard", "Availability"));
            if (int.TryParse(result, out var value))
                return GetAvailability(value);
            else
                return result;
        }
    }

    // unused
    public static List<string> DisplayDescription
    {
        get
        {
            return (List<string>)queryCacheValue("display", "Description");
        }
    }

    // unused
    public static bool HostingBoard
    {
        get
        {
            return Convert.ToBoolean(queryCacheValue("baseboard", "HostingBoard"));
        }
    }

    // unused
    public static string InstallDate
    {
        get
        {
            string result = Convert.ToString(queryCacheValue("baseboard", "InstallDate"));
            if (!string.IsNullOrEmpty(result))
                return ConvertToDateTime(result);
            else
                return result;
        }
    }

    public static string Manufacturer
    {
        get
        {
            return Convert.ToString(queryCacheValue("baseboard", "Manufacturer"));
        }
    }

    // unused
    public static string Model
    {
        get
        {
            return Convert.ToString(queryCacheValue("baseboard", "Model"));
        }
    }

    // unused
    public static string SystemModel
    {
        get
        {
            return Convert.ToString(queryCacheValue("computer", "Model"));
        }
    }

    public static int NumberOfCores
    {
        get
        {
            return Convert.ToInt32(queryCacheValue("processor", "NumberOfCores"));
        }
    }

    // unused
    public static string PartNumber
    {
        get
        {
            return Convert.ToString(queryCacheValue("baseboard", "PartNumber"));
        }
    }

    // unused
    public static string PNPDeviceID
    {
        get
        {
            return Convert.ToString(queryCacheValue("motherboard", "PNPDeviceID"));
        }
    }

    // unused
    public static string PrimaryBusType
    {
        get
        {
            return Convert.ToString(queryCacheValue("motherboard", "PrimaryBusType"));
        }
    }

    public static string ProcessorID
    {
        get
        {
            return Convert.ToString(queryCacheValue("processor", "processorID")).TrimEnd();
        }
    }

    public static string ProcessorName
    {
        get
        {
            return Convert.ToString(queryCacheValue("processor", "Name")).TrimEnd();
        }
    }

    public static string ProcessorManufacturer
    {
        get
        {
            return Convert.ToString(queryCacheValue("processor", "Manufacturer")).TrimEnd();
        }
    }

    // unused
    public static uint ProcessorMaxClockSpeed
    {
        get
        {
            return Convert.ToUInt32(queryCacheValue("processor", "MaxClockSpeed"));
        }
    }

    private static uint _ProcessorMaxTurboSpeed = 0;
    public static uint ProcessorMaxTurboSpeed
    {
        get
        {
            if (_ProcessorMaxTurboSpeed != 0)
                return _ProcessorMaxTurboSpeed;

            _ProcessorMaxTurboSpeed = IDevice.GetCurrent().CpuClock;

            return _ProcessorMaxTurboSpeed;
        }
    }

    public static string Product
    {
        get
        {
            return Convert.ToString(queryCacheValue("baseboard", "Product"));
        }
    }

    // unused
    public static bool Removable
    {
        get
        {
            return Convert.ToBoolean(queryCacheValue("baseboard", "Removable"));
        }
    }

    // unused
    public static bool Replaceable
    {
        get
        {
            return Convert.ToBoolean(queryCacheValue("baseboard", "Replaceable"));
        }
    }

    // unused
    public static string RevisionNumber
    {
        get
        {
            return Convert.ToString(queryCacheValue("motherboard", "RevisionNumber"));
        }
    }

    // unused
    public static string SecondaryBusType
    {
        get
        {
            return Convert.ToString(queryCacheValue("motherboard", "SecondaryBusType"));
        }
    }

    // unused
    public static string SerialNumber
    {
        get
        {
            return Convert.ToString(queryCacheValue("baseboard", "SerialNumber"));
        }
    }

    // unused
    public static string Status
    {
        get
        {
            return Convert.ToString(queryCacheValue("baseboard", "Status"));
        }
    }

    public static string SystemName
    {
        get
        {
            return Convert.ToString(queryCacheValue("motherboard", "SystemName"));
        }
    }

    public static string Version
    {
        get
        {
            return Convert.ToString(queryCacheValue("baseboard", "Version"));
        }
    }

    private static object queryCacheValue(string collectionName, string query)
    {
        bool hasvalue = false;

        // pull value if it exsts and check if correct
        if (cache.TryGetValue($"{collectionName}-{query}", out object? result))
        {
            switch (result)
            {
                case string s when !string.IsNullOrEmpty(s):
                case int i when i != 0:
                case uint ui when ui != 0:
                    hasvalue = true;
                    break;
            }
        }

        if (!hasvalue)
        {
            ManagementObjectCollection collection = collections[collectionName].Key;
            ManagementObjectSearcher searcher = collections[collectionName].Value;

            // use searcher if collection is null
            collection ??= searcher.Get();

            // set or update result
            result = collection.Cast<ManagementObject>().Select(queryObj => queryObj[query]).FirstOrDefault(result => result != null);

            if (result != null)
            {
                // update cache
                cache[$"{collectionName}-{query}"] = result;
                writeCache();
            }
            else return string.Empty;
        }

        return result;
    }

    private static string GetAvailability(int availability)
    {
        switch (availability)
        {
            case 1: return "Other";
            case 2: return "Unknown";
            case 3: return "Running or Full Power";
            case 4: return "Warning";
            case 5: return "In Test";
            case 6: return "Not Applicable";
            case 7: return "Power Off";
            case 8: return "Off Line";
            case 9: return "Off Duty";
            case 10: return "Degraded";
            case 11: return "Not Installed";
            case 12: return "Install Error";
            case 13: return "Power Save - Unknown";
            case 14: return "Power Save - Low Power Mode";
            case 15: return "Power Save - Standby";
            case 16: return "Power Cycle";
            case 17: return "Power Save - Warning";
            default: return "Unknown";
        }
    }

    private static string ConvertToDateTime(string unconvertedTime)
    {
        var convertedTime = "";
        var year = int.Parse(unconvertedTime.Substring(0, 4));
        var month = int.Parse(unconvertedTime.Substring(4, 2));
        var date = int.Parse(unconvertedTime.Substring(6, 2));
        var hours = int.Parse(unconvertedTime.Substring(8, 2));
        var minutes = int.Parse(unconvertedTime.Substring(10, 2));
        var seconds = int.Parse(unconvertedTime.Substring(12, 2));
        var meridian = "AM";
        if (hours > 12)
        {
            hours -= 12;
            meridian = "PM";
        }

        convertedTime = date + "/" + month + "/" + year + " " +
                        hours + ":" + minutes + ":" + seconds + " " + meridian;
        return convertedTime;
    }

    public static bool Collect()
    {
        lock (cacheLock)
        {
            string cacheFile = Path.Combine(cacheDirectory, fileName);
            if (File.Exists(cacheFile))
            {
                string cacheJSON = File.ReadAllText(cacheFile);

                Dictionary<string, object>? cache = JsonConvert.DeserializeObject<Dictionary<string, object>>(cacheJSON, new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.All
                });

                if (cache is not null)
                {
                    MotherboardInfo.cache = cache;
                    return true;
                }
            }
            
            return false;
        }
    }

    private static void writeCache()
    {
        lock (cacheLock)
        {
            string cacheFile = Path.Combine(cacheDirectory, fileName);

            string jsonString = JsonConvert.SerializeObject(cache, Formatting.Indented, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.All
            });

            File.WriteAllText(cacheFile, jsonString);
        }
    }
}