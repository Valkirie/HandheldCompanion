using HandheldCompanion.Managers.Hid;
using Nefarius.Utilities.DeviceManagement.Extensions;
using Nefarius.Utilities.DeviceManagement.PnP;
using System;
using System.Runtime.InteropServices;

namespace HandheldCompanion;

[StructLayout(LayoutKind.Sequential)]
public class PnPDetails : IDisposable
{
    public DateTimeOffset arrivalDate;

    public Attributes attributes;
    public string baseContainerDeviceInstanceId;
    public Capabilities capabilities;

    public string deviceInstanceId;
    public bool isGaming;
    public bool isHooked;

    public bool isVirtual;
    public string Name;
    public string Path;
    public string SymLink;

    // dirty
    public int DeviceIdx;

    // XInput
    public bool isXInput;
    public byte XInputUserIndex = byte.MaxValue;

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    public string GetProductID()
    {
        return "0x" + attributes.ProductID.ToString("X4");
    }

    public string GetVendorID()
    {
        return "0x" + attributes.VendorID.ToString("X4");
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
        PnPDevice device = GetPnPDevice();
        if (device is not null)
            return device.GetProperty<string>(DevicePropertyKey.Device_EnumeratorName);

        return string.Empty;
    }

    public UsbPnPDevice GetUsbPnPDevice()
    {
        PnPDevice device = GetPnPDevice();
        if (device is null)
            return null;

        // is this a USB device
        string enumerator = GetEnumerator();

        switch (enumerator)
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

    public bool InstallNullDrivers()
    {
        PnPDevice device = GetPnPDevice();

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

    public bool InstallCustomDriver(string driverName)
    {
        PnPDevice device = GetPnPDevice();

        try
        {
            if (device is not null)
                device.InstallCustomDriver(driverName);

            return true;
        }
        catch { }

        return false;
    }
}