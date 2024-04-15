using HandheldCompanion.Devices;
using HandheldCompanion.Views;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Management;
using System.Runtime.CompilerServices;

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

    public static void Collect()
    {
        if (!loadCache())
        {
            baseboardCollection = baseboardSearcher.Get();
            motherboardCollection = motherboardSearcher.Get();
            processorCollection = processorSearcher.Get();
            displayCollection = displaySearcher.Get();
            videoControllerCollection = videoControllerSearcher.Get();
        }
    }

    public static string Availability
    {
        get
        {
            string result = Convert.ToString(queryCacheValue(motherboardCollection, "Availability"));
            if (int.TryParse(result, out var value))
                return GetAvailability(value);
            else
                return result;
        }
    }

    public static List<string> DisplayDescription
    {
        get
        {
            return (List<string>)queryCacheValue(displayCollection, "Description");
        }
    }

    public static bool HostingBoard
    {
        get
        {
            return Convert.ToBoolean(queryCacheValue(baseboardCollection, "HostingBoard"));
        }
    }

    public static string InstallDate
    {
        get
        {
            string result = Convert.ToString(queryCacheValue(baseboardCollection, "InstallDate"));
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
            return Convert.ToString(queryCacheValue(baseboardCollection, "Manufacturer"));
        }
    }

    public static string Model
    {
        get
        {
            return Convert.ToString(queryCacheValue(baseboardCollection, "Model"));
        }
    }

    public static int NumberOfCores
    {
        get
        {
            return Convert.ToInt32(queryCacheValue(processorCollection, "NumberOfCores"));
        }
    }

    public static string PartNumber
    {
        get
        {
            return Convert.ToString(queryCacheValue(baseboardCollection, "PartNumber"));
        }
    }

    public static string PNPDeviceID
    {
        get
        {
            return Convert.ToString(queryCacheValue(motherboardCollection, "PNPDeviceID"));
        }
    }

    public static string PrimaryBusType
    {
        get
        {
            return Convert.ToString(queryCacheValue(motherboardCollection, "PrimaryBusType"));
        }
    }

    public static string ProcessorID
    {
        get
        {
            return Convert.ToString(queryCacheValue(processorCollection, "processorID")).TrimEnd();
        }
    }

    public static string ProcessorName
    {
        get
        {
            return Convert.ToString(queryCacheValue(processorCollection, "Name")).TrimEnd();
        }
    }

    public static string ProcessorManufacturer
    {
        get
        {
            return Convert.ToString(queryCacheValue(processorCollection, "Manufacturer")).TrimEnd();
        }
    }

    public static uint ProcessorMaxClockSpeed
    {
        get
        {
            return Convert.ToUInt32(queryCacheValue(processorCollection, "MaxClockSpeed"));
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
            return Convert.ToString(queryCacheValue(baseboardCollection, "Product"));
        }
    }

    public static bool Removable
    {
        get
        {
            return Convert.ToBoolean(queryCacheValue(baseboardCollection, "Removable"));
        }
    }

    public static bool Replaceable
    {
        get
        {
            return Convert.ToBoolean(queryCacheValue(baseboardCollection, "Replaceable"));
        }
    }

    public static string RevisionNumber
    {
        get
        {
            return Convert.ToString(queryCacheValue(motherboardCollection, "RevisionNumber"));
        }
    }

    public static string SecondaryBusType
    {
        get
        {
            return Convert.ToString(queryCacheValue(motherboardCollection, "SecondaryBusType"));
        }
    }

    public static string SerialNumber
    {
        get
        {
            return Convert.ToString(queryCacheValue(baseboardCollection, "SerialNumber"));
        }
    }

    public static string Status
    {
        get
        {
            return Convert.ToString(queryCacheValue(baseboardCollection, "Status"));
        }
    }

    public static string SystemName
    {
        get
        {
            return Convert.ToString(queryCacheValue(motherboardCollection, "SystemName"));
        }
    }

    public static string Version
    {
        get
        {
            return Convert.ToString(queryCacheValue(baseboardCollection, "Version"));
        }
    }

    private static object queryCacheValue(ManagementObjectCollection collection, string query, [CallerArgumentExpression("collection")] string collectionName = "")
    {
        if (!cache.ContainsKey($"{collectionName}-{query}"))
        {
            if (collection is not null)
            {
                foreach (ManagementObject queryObj in collection)
                {
                    object queryResult = queryObj[query];
                    if (queryResult is not null)
                    {
                        cache.Add($"{collectionName}-{query}", queryResult);
                        writeCache();
                        break;
                    }
                }
            }
        }

        if (cache.TryGetValue($"{collectionName}-{query}", out var result))
            return result;

        return string.Empty;
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

    private static bool loadCache()
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