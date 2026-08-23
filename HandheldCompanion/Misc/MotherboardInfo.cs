using HandheldCompanion.Devices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;

namespace HandheldCompanion;

public static class MotherboardInfo
{
    private static readonly ManagementObjectSearcher baseboardSearcher = new("root\\CIMV2", "SELECT * FROM Win32_BaseBoard");
    private static readonly ManagementObjectSearcher motherboardSearcher = new("root\\CIMV2", "SELECT * FROM Win32_MotherboardDevice");
    private static readonly ManagementObjectSearcher processorSearcher = new("root\\CIMV2", "SELECT * FROM Win32_Processor");
    private static readonly ManagementObjectSearcher displaySearcher = new("root\\CIMV2", "SELECT * FROM Win32_DisplayConfiguration");
    private static readonly ManagementObjectSearcher videoControllerSearcher = new("root\\CIMV2", "SELECT * FROM Win32_VideoController");
    private static readonly ManagementObjectSearcher computerSearcher = new("root\\CIMV2", "SELECT * FROM Win32_ComputerSystem");

    private static readonly object cacheLock = new();
    private static Dictionary<string, object> cache = [];

    private static bool? _hasHeterogeneousCpuCores;

    private static readonly string cacheDirectory;
    private const string fileName = "motherboard.json";

    static MotherboardInfo()
    {
        cacheDirectory = Path.Combine(App.SettingsPath, "cache");
        if (!Directory.Exists(cacheDirectory))
            Directory.CreateDirectory(cacheDirectory);
    }

    private static readonly Dictionary<string, ManagementObjectSearcher> collections = new()
    {
        { "baseboard", baseboardSearcher },
        { "motherboard", motherboardSearcher },
        { "processor", processorSearcher },
        { "display", displaySearcher },
        { "video", videoControllerSearcher },
        { "computer", computerSearcher },
    };

    public static string Manufacturer => Convert.ToString(queryCacheValue("baseboard", "Manufacturer")) ?? string.Empty;
    public static int NumberOfCores => Convert.ToInt32(queryCacheValue("processor", "NumberOfCores"));
    public static string ProcessorID => (Convert.ToString(queryCacheValue("processor", "processorID")) ?? string.Empty).TrimEnd();
    public static string ProcessorName => (Convert.ToString(queryCacheValue("processor", "Name")) ?? string.Empty).TrimEnd();
    public static string ProcessorManufacturer => (Convert.ToString(queryCacheValue("processor", "Manufacturer")) ?? string.Empty).TrimEnd();

    public static bool HasHeterogeneousCpuCores
    {
        get
        {
            if (_hasHeterogeneousCpuCores.HasValue)
                return _hasHeterogeneousCpuCores.Value;

            try
            {
                GetSystemCpuSetInformation(IntPtr.Zero, 0, out uint requiredLength, IntPtr.Zero, 0);
                if (requiredLength == 0)
                    return (_hasHeterogeneousCpuCores = false).Value;

                IntPtr buffer = Marshal.AllocHGlobal((int)requiredLength);
                try
                {
                    if (GetSystemCpuSetInformation(buffer, requiredLength, out uint returnedLength, IntPtr.Zero, 0) == 0)
                        return (_hasHeterogeneousCpuCores = false).Value;

                    var efficiencyClasses = new HashSet<byte>();
                    int offset = 0;
                    while (offset + sizeof(uint) <= returnedLength)
                    {
                        uint size = (uint)Marshal.ReadInt32(buffer, offset);
                        if (size < 20 || offset + size > returnedLength)
                            break;

                        uint type = (uint)Marshal.ReadInt32(buffer, offset + sizeof(uint));
                        if (type == 0)
                            efficiencyClasses.Add(Marshal.ReadByte(buffer, offset + 18));

                        offset += (int)size;
                    }

                    return (_hasHeterogeneousCpuCores = efficiencyClasses.Count > 1).Value;
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            catch
            {
                return (_hasHeterogeneousCpuCores = false).Value;
            }
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint GetSystemCpuSetInformation(
        IntPtr information,
        uint bufferLength,
        out uint returnedLength,
        IntPtr process,
        uint flags);

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

    public static string Product => Convert.ToString(queryCacheValue("baseboard", "Product")) ?? string.Empty;
    public static string SystemName => Convert.ToString(queryCacheValue("motherboard", "SystemName")) ?? string.Empty;
    public static string Version => Convert.ToString(queryCacheValue("baseboard", "Version")) ?? string.Empty;

    // unused
    public static string Availability
    {
        get
        {
            string result = Convert.ToString(queryCacheValue("motherboard", "Availability")) ?? string.Empty;
            if (int.TryParse(result, out var value))
                return GetAvailability(value);
            else
                return result;
        }
    }

    // unused
    public static List<string> DisplayDescription => (List<string>)queryCacheValue("display", "Description");
    public static bool HostingBoard => Convert.ToBoolean(queryCacheValue("baseboard", "HostingBoard"));
    public static string Model => Convert.ToString(queryCacheValue("baseboard", "Model")) ?? string.Empty;
    public static string SystemModel => Convert.ToString(queryCacheValue("computer", "Model")) ?? string.Empty;
    public static string PartNumber => Convert.ToString(queryCacheValue("baseboard", "PartNumber")) ?? string.Empty;
    public static string PNPDeviceID => Convert.ToString(queryCacheValue("motherboard", "PNPDeviceID")) ?? string.Empty;
    public static string PrimaryBusType => Convert.ToString(queryCacheValue("motherboard", "PrimaryBusType")) ?? string.Empty;
    public static uint ProcessorMaxClockSpeed => Convert.ToUInt32(queryCacheValue("processor", "MaxClockSpeed"));
    public static bool Removable => Convert.ToBoolean(queryCacheValue("baseboard", "Removable"));
    public static bool Replaceable => Convert.ToBoolean(queryCacheValue("baseboard", "Replaceable"));
    public static string RevisionNumber => Convert.ToString(queryCacheValue("motherboard", "RevisionNumber")) ?? string.Empty;
    public static string SecondaryBusType => Convert.ToString(queryCacheValue("motherboard", "SecondaryBusType")) ?? string.Empty;
    public static string SerialNumber => Convert.ToString(queryCacheValue("baseboard", "SerialNumber")) ?? string.Empty;
    public static string Status => Convert.ToString(queryCacheValue("baseboard", "Status")) ?? string.Empty;

    // unused
    public static string InstallDate
    {
        get
        {
            string result = Convert.ToString(queryCacheValue("baseboard", "InstallDate")) ?? string.Empty;
            if (!string.IsNullOrEmpty(result))
                return ConvertToDateTime(result);
            else
                return result;
        }
    }

    private static object queryCacheValue(string collectionName, string query)
    {
        bool hasvalue = false;

        // pull value if it exsts and check if correct
        object? result = null;
        lock (cacheLock)
        {
            if (cache.TryGetValue($"{collectionName}-{query}", out result))
            {
                switch (result)
                {
                    case string s when !string.IsNullOrEmpty(s):
                    case int i when i != 0:
                    case uint ui when ui != 0:
                    case long l when l != 0:
                    case double d when d != 0:
                    case short sh when sh != 0:
                        hasvalue = true;
                        break;
                }
            }
        }

        if (!hasvalue)
        {
            ManagementObjectSearcher searcher = collections[collectionName];
            using ManagementObjectCollection collection = searcher.Get();

            // set or update result
            result = collection.Cast<ManagementObject>().Select(queryObj => queryObj[query]).FirstOrDefault(result => result != null);

            if (result != null)
            {
                // update cache
                lock (cacheLock)
                    cache[$"{collectionName}-{query}"] = result;

                writeCache();
            }
            else return string.Empty;
        }

        return result ?? string.Empty;
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
        var convertedTime = string.Empty;
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
            try
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
                        // clone the cache to the static cache variable
                        MotherboardInfo.cache = cache.ToDictionary(pair => pair.Key, pair => pair.Value is JValue value ? value.Value ?? string.Empty : pair.Value);
                        return true;
                    }
                }
            }
            catch { }

            return false;
        }
    }

    private static void writeCache()
    {
        lock (cacheLock)
        {
            try
            {
                string cacheFile = Path.Combine(cacheDirectory, fileName);

                string jsonString = JsonConvert.SerializeObject(cache, Formatting.Indented, new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.All
                });

                File.WriteAllText(cacheFile, jsonString);
            }
            catch { }
        }
    }
}