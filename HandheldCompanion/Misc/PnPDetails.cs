<<<<<<< HEAD
﻿using Nefarius.Utilities.DeviceManagement.Extensions;
using Nefarius.Utilities.DeviceManagement.PnP;
=======
﻿using HandheldCompanion.Managers.Hid;
using Nefarius.Utilities.DeviceManagement.Extensions;
using Nefarius.Utilities.DeviceManagement.PnP;
using System;
>>>>>>> f8fea3c25fb5fd254f5020d43305b7356ec9770d
using System.Runtime.InteropServices;

namespace HandheldCompanion;

[StructLayout(LayoutKind.Sequential)]
<<<<<<< HEAD
public class PnPDetails
{
    public string deviceInstanceId;
    public string baseContainerDeviceInstanceId;

=======
public class PnPDetails : IDisposable
{
    public DateTimeOffset arrivalDate;

    public Attributes attributes;
    public string baseContainerDeviceInstanceId;
    public Capabilities capabilities;

    public string deviceInstanceId;
>>>>>>> f8fea3c25fb5fd254f5020d43305b7356ec9770d
    public bool isGaming;
    public bool isHooked;

    public bool isVirtual;
<<<<<<< HEAD
    public bool isPhysical => !isVirtual;

    public string devicePath;
    public string baseContainerDevicePath;

    public string Name;
    public string SymLink;
    public string Enumerator;

    public ushort ProductID;
    public ushort VendorID;
=======
    public string Name;
    public string Path;
    public string SymLink;

    // dirty
    public int DeviceIdx;
>>>>>>> f8fea3c25fb5fd254f5020d43305b7356ec9770d

    // XInput
    public bool isXInput;
    public byte XInputUserIndex = byte.MaxValue;
<<<<<<< HEAD
    public int XInputDeviceIdx;

    public string GetProductID()
    {
        return "0x" + ProductID.ToString("X4");
=======

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    public string GetProductID()
    {
        return "0x" + attributes.ProductID.ToString("X4");
>>>>>>> f8fea3c25fb5fd254f5020d43305b7356ec9770d
    }

    public string GetVendorID()
    {
<<<<<<< HEAD
        return "0x" + VendorID.ToString("X4");
=======
        return "0x" + attributes.VendorID.ToString("X4");
>>>>>>> f8fea3c25fb5fd254f5020d43305b7356ec9770d
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
<<<<<<< HEAD
        return Enumerator;
=======
        PnPDevice device = GetBasePnPDevice();
        if (device is not null)
            return device.GetProperty<string>(DevicePropertyKey.Device_EnumeratorName);

        return string.Empty;
>>>>>>> f8fea3c25fb5fd254f5020d43305b7356ec9770d
    }

    public UsbPnPDevice GetUsbPnPDevice()
    {
        PnPDevice device = GetBasePnPDevice();
        if (device is null)
            return null;

        // is this a USB device
<<<<<<< HEAD
        switch (Enumerator)
=======
        string enumerator = GetEnumerator();

        switch (enumerator)
>>>>>>> f8fea3c25fb5fd254f5020d43305b7356ec9770d
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