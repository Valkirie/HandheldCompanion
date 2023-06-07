using System.Management;
using System.Text.RegularExpressions;

namespace ControllerCommon;

public class USBDeviceInfo
{
    private static readonly string vidPattern = @"VID_([0-9A-F]{4})";
    private static readonly string pidPattern = @"PID_([0-9A-F]{4})";

    public USBDeviceInfo(string deviceId = "0", string name = "N/A", string description = "N/A", string caption = "N/A",
        string pid = "0x00", string vid = "0x00")
    {
        DeviceId = deviceId;
        Name = name;
        Description = description;
        Caption = caption;
        PID = pid;
        VID = vid;
    }

    public USBDeviceInfo(ManagementBaseObject device)
    {
        DeviceId = device.GetPropertyValue("DeviceId").ToString();
        Name = device.GetPropertyValue("Name").ToString();
        Description = device.GetPropertyValue("Description").ToString();
        Caption = device.GetPropertyValue("Caption").ToString();

        var mVID = Regex.Match(DeviceId, vidPattern, RegexOptions.IgnoreCase);
        var mPID = Regex.Match(DeviceId, pidPattern, RegexOptions.IgnoreCase);

        if (mVID.Success)
            VID = mVID.Groups[1].Value;
        if (mPID.Success)
            PID = mPID.Groups[1].Value;
    }

    public string DeviceId { get; set; } = "0";
    public string Name { get; set; } = "N/A";
    public string Description { get; set; } = "N/A";
    public string Caption { get; set; } = "N/A";
    public string PID { get; set; } = "0x00";
    public string VID { get; set; } = "0x00";

    public override string ToString()
    {
        return Name;
    }
}