using Nefarius.Utilities.DeviceManagement.Extensions;
using Nefarius.Utilities.DeviceManagement.PnP;
using System;
using System.Runtime.InteropServices;

namespace HandheldCompanion;

[StructLayout(LayoutKind.Sequential)]
public class PnPDetails
{
    public string deviceInstanceId;
    public string baseContainerDeviceInstanceId;

    public bool isGaming;
    public bool isHooked;
    public bool isExternal;
    public bool isInternal => !isExternal;

    public bool isVirtual;
    public bool isPhysical => !isVirtual;

    public string devicePath;
    public string baseContainerDevicePath;

    public string Name;
    public string SymLink;
    public string EnumeratorName;
    public DateTimeOffset FirstInstallDate;

    public ushort ProductID;
    public ushort VendorID;

    // XInput
    public bool isXInput;
    public byte XInputUserIndex = byte.MaxValue;
    public int XInputDeviceIdx;

    public string GetProductID()
    {
        return "0x" + ProductID.ToString("X4");
    }

    public string GetVendorID()
    {
        return "0x" + VendorID.ToString("X4");
    }

    public short GetMI()
    {
        string low = SymLink.ToLower();
        int index = low.IndexOf("mi_");
        if (index == -1)
            return -1;
        string mi = low.Substring(index + 3, 2);

        if (short.TryParse(mi, out short number))
            return number;

        return -1;
    }

    public string GetEnumerator()
    {
        return EnumeratorName;
    }

    public UsbPnPDevice GetUsbPnPDevice()
    {
        PnPDevice device = GetBasePnPDevice();
        if (device is null)
            return null;

        // is this a USB device
        switch (EnumeratorName)
        {
            default:
            case "BTHENUM":
                return null;
            case "USB":
                break;
        }

        return device.ToUsbPnPDevice();
    }

    public PnPDevice GetPnPDevice()
    {
        try
        {
            return PnPDevice.GetDeviceByInstanceId(deviceInstanceId);
        }
        catch { }

        return null;
    }

    public PnPDevice GetBasePnPDevice()
    {
        try
        {
            return PnPDevice.GetDeviceByInstanceId(baseContainerDeviceInstanceId);
        }
        catch { }

        return null;
    }

    public bool CyclePort()
    {
        UsbPnPDevice device = GetUsbPnPDevice();

        try
        {
            if (device is not null)
            {
                device.CyclePort();
                return true;
            }
        }
        catch { }

        return false;
    }

    public bool InstallNullDrivers(bool basedevice = true)
    {
        PnPDevice device;

        switch (basedevice)
        {
            case true:
                device = GetBasePnPDevice();
                break;
            case false:
                device = GetPnPDevice();
                break;
        }

        try
        {
            if (device is not null)
            {
                device.InstallNullDriver();
                return true;
            }
        }
        catch { }

        return false;
    }

    public bool InstallCustomDriver(string driverName, bool basedevice = true)
    {
        PnPDevice device;
        
        switch(basedevice)
        {
            case true:
                device = GetBasePnPDevice();
                break;
            case false:
                device = GetPnPDevice();
                break;
        }

        try
        {
            if (device is not null)
            {
                device.InstallCustomDriver(driverName);
                return true;
            }
        }
        catch { }

        return false;
    }

    public bool Uninstall(bool basedevice = true, bool parent = false)
    {
        PnPDevice device;

        switch (basedevice)
        {
            case true:
                device = GetBasePnPDevice();

                if (parent)
                {
                    var parentId = device.GetProperty<string>(DevicePropertyKey.Device_Parent);
                    device = PnPDevice.GetDeviceByInstanceId(parentId);
                }

                break;
            case false:
                device = GetPnPDevice();
                break;
        }

        try
        {
            if (device is not null)
            {
                device.Uninstall();
                return true;
            }
        }
        catch { }

        return false;
    }
}