using HandheldCompanion.Devices;
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

    public static void UpdateMotherboard()
    {
        // slow task, don't call me more than once
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
            if (motherboardCollection is not null)
                foreach (ManagementObject queryObj in motherboardCollection)
                {
                    var query = queryObj["Availability"];
                    if (query is not null)
                        if (int.TryParse(query.ToString(), out var value))
                            return GetAvailability(value);
                }

            return string.Empty;
        }
    }

    public static List<string> DisplayDescription
    {
        get
        {
            List<string> strings = new List<string>();
            if (displayCollection is not null)
                foreach (ManagementObject queryObj in displayCollection)
                {
                    var query = queryObj["Description"];
                    if (query is not null)
                        strings.Add(query.ToString().ToUpper());
                }

            return strings;
        }
    }

    public static bool HostingBoard
    {
        get
        {
            if (baseboardCollection is not null)
                foreach (ManagementObject queryObj in baseboardCollection)
                {
                    var query = queryObj["HostingBoard"];
                    if (query is not null)
                        if (query.ToString() == "True")
                            return true;
                }

            return false;
        }
    }

    public static string InstallDate
    {
        get
        {
            if (baseboardCollection is not null)
                foreach (ManagementObject queryObj in baseboardCollection)
                {
                    var query = queryObj["InstallDate"];
                    if (query is not null)
                        return ConvertToDateTime(query.ToString());
                }

            return string.Empty;
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
            if (baseboardCollection is not null)
                foreach (ManagementObject queryObj in baseboardCollection)
                {
                    var query = queryObj["Model"];
                    if (query is not null)
                        return query.ToString();
                }

            return string.Empty;
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
            if (baseboardCollection is not null)
                foreach (ManagementObject queryObj in baseboardCollection)
                {
                    var query = queryObj["PartNumber"];
                    if (query is not null)
                        return query.ToString();
                }

            return string.Empty;
        }
    }

    public static string PNPDeviceID
    {
        get
        {
            if (motherboardCollection is not null)
                foreach (ManagementObject queryObj in motherboardCollection)
                {
                    var query = queryObj["PNPDeviceID"];
                    if (query is not null)
                        return query.ToString();
                }

            return string.Empty;
        }
    }

    public static string PrimaryBusType
    {
        get
        {
            if (motherboardCollection is not null)
                foreach (ManagementObject queryObj in motherboardCollection)
                {
                    var query = queryObj["PrimaryBusType"];
                    if (query is not null)
                        return query.ToString();
                }

            return string.Empty;
        }
    }

    public static string ProcessorID
    {
        get
        {
            if (processorCollection is not null)
                foreach (ManagementObject queryObj in processorCollection)
                {
                    var query = queryObj["processorID"];
                    if (query is not null)
                        return query.ToString().TrimEnd();
                }

            return string.Empty;
        }
    }

    public static string ProcessorName
    {
        get
        {
            if (processorCollection is not null)
                foreach (ManagementObject queryObj in processorCollection)
                {
                    var query = queryObj["Name"];
                    if (query is not null)
                        return query.ToString().TrimEnd();
                }

            return string.Empty;
        }
    }

    public static string ProcessorManufacturer
    {
        get
        {
            if (processorCollection is not null)
                foreach (ManagementObject queryObj in processorCollection)
                {
                    var query = queryObj["Manufacturer"];
                    if (query is not null)
                        return query.ToString().TrimEnd();
                }

            return string.Empty;
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
            if (baseboardCollection is not null)
                foreach (ManagementObject queryObj in baseboardCollection)
                {
                    var query = queryObj["Product"];
                    if (query is not null)
                        return query.ToString();
                }

            return string.Empty;
        }
    }

    public static bool Removable
    {
        get
        {
            if (baseboardCollection is not null)
                foreach (ManagementObject queryObj in baseboardCollection)
                {
                    var query = queryObj["Removable"];
                    if (query is not null)
                        if (query.ToString() == "True")
                            return true;
                }

            return false;
        }
    }

    public static bool Replaceable
    {
        get
        {
            if (baseboardCollection is not null)
                foreach (ManagementObject queryObj in baseboardCollection)
                {
                    var query = queryObj["Replaceable"];
                    if (query is not null)
                        if (query.ToString() == "True")
                            return true;
                }

            return false;
        }
    }

    public static string RevisionNumber
    {
        get
        {
            if (motherboardCollection is not null)
                foreach (ManagementObject queryObj in motherboardCollection)
                {
                    var query = queryObj["RevisionNumber"];
                    if (query is not null)
                        return query.ToString();
                }

            return string.Empty;
        }
    }

    public static string SecondaryBusType
    {
        get
        {
            if (motherboardCollection is not null)
                foreach (ManagementObject queryObj in motherboardCollection)
                {
                    var query = queryObj["SecondaryBusType"];
                    if (query is not null)
                        return query.ToString();
                }

            return string.Empty;
        }
    }

    public static string SerialNumber
    {
        get
        {
            if (baseboardCollection is not null)
                foreach (ManagementObject queryObj in baseboardCollection)
                {
                    var query = queryObj["SerialNumber"];
                    if (query is not null)
                        return query.ToString();
                }

            return string.Empty;
        }
    }

    public static string Status
    {
        get
        {
            if (baseboardCollection is not null)
                foreach (ManagementObject queryObj in baseboardCollection)
                {
                    var query = queryObj["Status"];
                    if (query is not null)
                        return query.ToString();
                }

            return string.Empty;
        }
    }

    public static string SystemName
    {
        get
        {
            if (motherboardCollection is not null)
                foreach (ManagementObject queryObj in motherboardCollection)
                {
                    var query = queryObj["SystemName"];
                    if (query is not null)
                        return query.ToString();
                }

            return string.Empty;
        }
    }

    public static string Version
    {
        get
        {
            if (baseboardCollection is not null)
                foreach (ManagementObject queryObj in baseboardCollection)
                {
                    var query = queryObj["Version"];
                    if (query is not null)
                        return query.ToString();
                }

            return string.Empty;
        }
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