using System.Management;

namespace ControllerCommon;

public static class MotherboardInfo
{
    private static readonly ManagementObjectSearcher baseboardSearcher =
        new("root\\CIMV2", "SELECT * FROM Win32_BaseBoard");

    private static readonly ManagementObjectSearcher motherboardSearcher =
        new("root\\CIMV2", "SELECT * FROM Win32_MotherboardDevice");

    public static string Availability
    {
        get
        {
            foreach (ManagementObject queryObj in motherboardSearcher.Get())
            {
                var query = queryObj["Availability"];
                if (query is not null)
                    if (int.TryParse(query.ToString(), out var value))
                        return GetAvailability(value);
            }

            return string.Empty;
        }
    }

    public static bool HostingBoard
    {
        get
        {
            foreach (ManagementObject queryObj in baseboardSearcher.Get())
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
            foreach (ManagementObject queryObj in baseboardSearcher.Get())
            {
                var query = queryObj["InstallDate"];
                if (query is not null)
                    return ConvertToDateTime(query.ToString());
            }

            return string.Empty;
        }
    }

    public static string Manufacturer
    {
        get
        {
            foreach (ManagementObject queryObj in baseboardSearcher.Get())
            {
                var query = queryObj["Manufacturer"];
                if (query is not null)
                    return query.ToString();
            }

            return string.Empty;
        }
    }

    public static string Model
    {
        get
        {
            foreach (ManagementObject queryObj in baseboardSearcher.Get())
            {
                var query = queryObj["Model"];
                if (query is not null)
                    return query.ToString();
            }

            return string.Empty;
        }
    }

    public static string PartNumber
    {
        get
        {
            foreach (ManagementObject queryObj in baseboardSearcher.Get())
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
            foreach (ManagementObject queryObj in motherboardSearcher.Get())
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
            foreach (ManagementObject queryObj in motherboardSearcher.Get())
            {
                var query = queryObj["PrimaryBusType"];
                if (query is not null)
                    return query.ToString();
            }

            return string.Empty;
        }
    }

    public static string Processor
    {
        get
        {
            foreach (ManagementObject queryObj in baseboardSearcher.Get())
            {
                var query = queryObj["Processor"];
                if (query is not null)
                    return query.ToString();
            }

            return string.Empty;
        }
    }

    public static string Product
    {
        get
        {
            foreach (ManagementObject queryObj in baseboardSearcher.Get())
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
            foreach (ManagementObject queryObj in baseboardSearcher.Get())
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
            foreach (ManagementObject queryObj in baseboardSearcher.Get())
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
            foreach (ManagementObject queryObj in motherboardSearcher.Get())
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
            foreach (ManagementObject queryObj in motherboardSearcher.Get())
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
            foreach (ManagementObject queryObj in baseboardSearcher.Get())
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
            foreach (ManagementObject queryObj in baseboardSearcher.Get())
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
            foreach (ManagementObject queryObj in motherboardSearcher.Get())
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
            foreach (ManagementObject queryObj in baseboardSearcher.Get())
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