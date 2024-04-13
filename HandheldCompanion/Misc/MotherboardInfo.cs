using HandheldCompanion.Devices;
using System;
using System.Collections.Generic;
using System.Management;

namespace HandheldCompanion;

public static class MotherboardInfo
{
    private static readonly ManagementObjectSearcher baseboardSearcher = new("root\\CIMV2", "SELECT * FROM Win32_BaseBoard");
    private static ManagementObjectCollection baseboardCollection;

    private static readonly ManagementObjectSearcher motherboardSearcher = new("root\\CIMV2", "SELECT * FROM Win32_MotherboardDevice");
    private static ManagementObjectCollection motherboardCollection;

    private static readonly ManagementObjectSearcher processorSearcher = new("root\\CIMV2", "SELECT * FROM Win32_Processor");
    private static ManagementObjectCollection processorCollection;

    private static readonly ManagementObjectSearcher displaySearcher = new("root\\CIMV2", "SELECT * FROM Win32_DisplayConfiguration");
    private static ManagementObjectCollection displayCollection;

    private static readonly ManagementObjectSearcher videoControllerSearcher = new("root\\CIMV2", "SELECT * FROM Win32_VideoController");
    private static ManagementObjectCollection videoControllerCollection;

    public static void Collect()
    {
        // TODO: Cache those details to speed-up next startup
        baseboardCollection = baseboardSearcher.Get();
        motherboardCollection = motherboardSearcher.Get();
        processorCollection = processorSearcher.Get();
        displayCollection = displaySearcher.Get();
        videoControllerCollection = videoControllerSearcher.Get();
    }

    public static string Availability
    {
        get
        {
            var result = queryCollectionString(motherboardCollection, "Availability");
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
            return queryCollectionStringList(displayCollection, "Description");
        }
    }

    public static bool HostingBoard
    {
        get
        {
            return queryCollectionBool(baseboardCollection, "HostingBoard");
        }
    }

    public static string InstallDate
    {
        get
        {
            var result = queryCollectionString(baseboardCollection, "InstallDate");
            if (!string.IsNullOrEmpty(result))
                return ConvertToDateTime(result);
            else
                return result;
        }
    }

    private static string _Manufacturer = string.Empty;
    public static string Manufacturer
    {
        get
        {
            if (!string.IsNullOrEmpty(_Manufacturer))
                return _Manufacturer;

            if (baseboardCollection is not null)
                foreach (ManagementObject queryObj in baseboardCollection)
                {
                    var query = queryObj["Manufacturer"];
                    if (query is not null)
                    {
                        _Manufacturer = query.ToString();
                        break;
                    }
                }

            return _Manufacturer;
        }
    }

    public static string Model
    {
        get
        {
            return queryCollectionString(baseboardCollection, "Model");
        }
    }

    private static int _NumberOfCores = 0;
    public static int NumberOfCores
    {
        get
        {
            if (_NumberOfCores != 0)
                return _NumberOfCores;

            if (processorCollection is not null)
                foreach (ManagementObject queryObj in processorCollection)
                {
                    var query = queryObj["NumberOfCores"];
                    if (query is not null)
                    {
                        if (int.TryParse(query.ToString(), out var value))
                            _NumberOfCores = value;
                        break;
                    }
                }

            return _NumberOfCores;
        }
    }

    public static string PartNumber
    {
        get
        {
            return queryCollectionString(baseboardCollection, "PartNumber");
        }
    }

    public static string PNPDeviceID
    {
        get
        {
            return queryCollectionString(motherboardCollection, "PNPDeviceID");
        }
    }

    public static string PrimaryBusType
    {
        get
        {
            return queryCollectionString(motherboardCollection, "PrimaryBusType");
        }
    }

    public static string ProcessorID
    {
        get
        {
            return queryCollectionString(processorCollection, "processorID").TrimEnd();
        }
    }

    public static string ProcessorName
    {
        get
        {
            return queryCollectionString(processorCollection, "Name").TrimEnd();
        }
    }

    public static string ProcessorManufacturer
    {
        get
        {
            return queryCollectionString(processorCollection, "Manufacturer").TrimEnd();
        }
    }

    private static uint _ProcessorMaxClockSpeed = 0;
    public static uint ProcessorMaxClockSpeed
    {
        get
        {
            if (_ProcessorMaxClockSpeed != 0)
                return _ProcessorMaxClockSpeed;

            if (processorCollection is not null)
                foreach (ManagementObject queryObj in processorCollection)
                {
                    var query = queryObj["MaxClockSpeed"];
                    if (query is not null)
                    {
                        if (uint.TryParse(query.ToString(), out var value))
                            _ProcessorMaxClockSpeed = value;
                        break;
                    }
                }

            return _ProcessorMaxClockSpeed;
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
            return queryCollectionString(baseboardCollection, "Product");
        }
    }

    public static bool Removable
    {
        get
        {
            return queryCollectionBool(baseboardCollection, "Removable");
        }
    }

    public static bool Replaceable
    {
        get
        {
            return queryCollectionBool(baseboardCollection, "Replaceable");
        }
    }

    public static string RevisionNumber
    {
        get
        {
            return queryCollectionString(motherboardCollection, "RevisionNumber");
        }
    }

    public static string SecondaryBusType
    {
        get
        {
            return queryCollectionString(motherboardCollection, "SecondaryBusType");
        }
    }

    public static string SerialNumber
    {
        get
        {
            return queryCollectionString(baseboardCollection, "SerialNumber");
        }
    }

    public static string Status
    {
        get
        {
            return queryCollectionString(baseboardCollection, "Status");
        }
    }

    public static string SystemName
    {
        get
        {
            return queryCollectionString(motherboardCollection, "SystemName");
        }
    }

    public static string Version
    {
        get
        {
            return queryCollectionString(baseboardCollection, "Version");
        }
    }

    private static string queryCollectionString(ManagementObjectCollection collection, string query)
    {
        if (collection is not null)
            foreach (ManagementObject queryObj in collection)
            {
                var queryResult = queryObj[query];
                if (queryResult is not null)
                    return queryResult.ToString();
            }

        return string.Empty;
    }

    private static List<string> queryCollectionStringList(ManagementObjectCollection collection, string query)
    {
        List<string> strings = new();
        if (collection is not null)
            foreach (ManagementObject queryObj in collection)
            {
                var queryResult = queryObj[query];
                if (queryResult is not null)
                    strings.Add(queryResult.ToString().ToUpper());
            }

        return strings;
    }

    private static bool queryCollectionBool(ManagementObjectCollection collection, string query)
    {
        if (collection is not null)
            foreach (ManagementObject queryObj in collection)
            {
                var queryResult = queryObj[query];
                if (queryResult is not null)
                    if (queryResult.ToString() == "True")
                        return true;
            }

        return false;
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
}