using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Utils;
using HidLibrary;
using Nefarius.Utilities.DeviceManagement.PnP;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Input;
using WindowsInput.Events;
using Task = System.Threading.Tasks.Task;

namespace HandheldCompanion.Devices;

public class LegionGo : IDevice
{
    public Dictionary<byte, HidDevice> hidDevices = new();
    public const byte INPUT_HID_ID = 0x04;

    private PnPDevice Touchpad;
    private PnPDevice Mouse;

    public override bool IsOpen => hidDevices.ContainsKey(INPUT_HID_ID) && hidDevices[INPUT_HID_ID].IsOpen;

    public LegionGo()
    {
        // device specific settings
        ProductIllustration = "device_legion_go";

        // used to monitor OEM specific inputs
        _vid = 0x17EF;
        _pid = 0x6182;

        // https://www.amd.com/en/products/apu/amd-ryzen-z1
        // https://www.amd.com/en/products/apu/amd-ryzen-z1-extreme
        // https://www.amd.com/en/products/apu/amd-ryzen-7-7840u
        nTDP = new double[] { 15, 15, 20 };
        cTDP = new double[] { 5, 30 };
        GfxClock = new double[] { 100, 2700 };

        AngularVelocityAxis = new Vector3(-1.0f, -1.0f, 1.0f);
        AngularVelocityAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'X' },
            { 'Y', 'Z' },
            { 'Z', 'Y' }
        };

        AccelerationAxis = new Vector3(1.0f, 1.0f, 1.0f);
        AccelerationAxisSwap = new SortedDictionary<char, char>
        {
            { 'X', 'X' },
            { 'Y', 'Z' },
            { 'Z', 'Y' }
        };

        // device specific capacities
        Capabilities |= DeviceCapabilities.None;

        OEMChords.Add(new DeviceChord("LegionR",
            new List<KeyCode>(), new List<KeyCode>(),
            false, ButtonFlags.OEM1
        ));

        OEMChords.Add(new DeviceChord("LegionL",
            new List<KeyCode>(), new List<KeyCode>(),
            false, ButtonFlags.OEM2
        ));
    }

    public override bool Open()
    {
        var success = base.Open();
        if (!success)
            return false;

        // Legion XInput controller and other Legion devices shares the same USBHUB
        while(ControllerManager.PowerCyclers.Count > 0)
            Thread.Sleep(500);

        // disable touchpad and mouse, until we find a better way to manage them
        if (Touchpad is not null)
            PnPUtil.DisableDevice(Touchpad.InstanceId);
        if (Mouse is not null)
            PnPUtil.DisableDevice(Mouse.InstanceId);

        return true;
    }

    public override void Close()
    {
        // Legion XInput controller and other Legion devices shares the same USBHUB
        while (ControllerManager.PowerCyclers.Count > 0)
            Thread.Sleep(500);

        // enable back touchpad and mouse, until we find a better way to manage them
        if (Touchpad is not null)
            PnPUtil.EnableDevice(Touchpad.InstanceId);
        if (Mouse is not null)
            PnPUtil.EnableDevice(Mouse.InstanceId);

        // close devices
        foreach (KeyValuePair<byte, HidDevice> hidDevice in hidDevices)
        {
            byte key = hidDevice.Key;
            HidDevice device = hidDevice.Value;

            device.CloseDevice();
        }

        base.Close();
    }

    public override bool IsReady()
    {
        IEnumerable<HidDevice> devices = GetHidDevices(_vid, _pid, 0);
        foreach (HidDevice device in devices)
        {
            if (!device.IsConnected)
                continue;

            if (device.Description.Equals("HID-compliant vendor-defined device"))
            {
                hidDevices[INPUT_HID_ID] = device;
            }
            else if (device.Description.Equals("HID-compliant mouse"))
            {
                Mouse = PnPDevice.GetDeviceByInterfaceId(device.DevicePath);
            }
            else if (device.Description.Equals("HID-compliant touch pad"))
            {
                Touchpad = PnPDevice.GetDeviceByInterfaceId(device.DevicePath);
            }
        }

        hidDevices.TryGetValue(INPUT_HID_ID, out HidDevice hidDevice);
        if (hidDevice is null || !hidDevice.IsConnected)
            return false;

        PnPDevice pnpDevice = PnPDevice.GetDeviceByInterfaceId(hidDevice.DevicePath);
        string device_parent = pnpDevice.GetProperty<string>(DevicePropertyKey.Device_Parent);

        PnPDevice pnpParent = PnPDevice.GetDeviceByInstanceId(device_parent);
        Guid parent_guid = pnpParent.GetProperty<Guid>(DevicePropertyKey.Device_ClassGuid);
        string parent_instanceId = pnpParent.GetProperty<string>(DevicePropertyKey.Device_InstanceId);

        return DeviceHelper.IsDeviceAvailable(parent_guid, parent_instanceId);
    }

    public override string GetGlyph(ButtonFlags button)
    {
        switch (button)
        {
            case ButtonFlags.OEM1:
                return "\u2205";
            case ButtonFlags.OEM2:
                return "\uE004";
        }

        return defaultGlyph;
    }
}