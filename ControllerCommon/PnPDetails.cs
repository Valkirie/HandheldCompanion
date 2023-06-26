using System;
using System.Runtime.InteropServices;
using ControllerCommon.Managers.Hid;
using Nefarius.Utilities.DeviceManagement.Extensions;
using Nefarius.Utilities.DeviceManagement.PnP;

namespace ControllerCommon;

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
    public bool isXInput;
    public string Name;
    public string Path;
    public string SymLink;

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

    public UsbPnPDevice GetUsbPnPDevice()
    {
        var pnpDevice = PnPDevice.GetDeviceByInstanceId(baseContainerDeviceInstanceId);
        if (pnpDevice is null)
            return null;

        // is this a USB device
        var enumerator = pnpDevice.GetProperty<string>(DevicePropertyKey.Device_EnumeratorName);
        if (!Equals(enumerator, "USB"))
            return null;

        return pnpDevice.ToUsbPnPDevice();
    }

    public bool CyclePort()
    {
        var usbDevice = GetUsbPnPDevice();

        if (usbDevice is null)
            return false;

        try
        {
            usbDevice.CyclePort();
            return true;
        }
        catch { }

        return false;
    }
}