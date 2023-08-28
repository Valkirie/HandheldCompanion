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
    public byte XInputUserIndex;

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