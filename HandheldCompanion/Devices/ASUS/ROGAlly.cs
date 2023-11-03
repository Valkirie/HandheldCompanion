using HandheldCompanion.Devices.ASUS;
using HandheldCompanion.Inputs;
using HandheldCompanion.Utils;
using HidLibrary;
using Nefarius.Utilities.DeviceManagement.PnP;
using System;
using System.Collections.Generic;
using System.Numerics;
using WindowsInput.Events;
using Task = System.Threading.Tasks.Task;
using System.Threading.Tasks;

namespace HandheldCompanion.Devices;

public class ROGAlly : IDevice
{
    private readonly Dictionary<byte, ButtonFlags> keyMapping = new()
    {
        { 0, ButtonFlags.None },
        { 166, ButtonFlags.OEM1 },
        { 56, ButtonFlags.OEM2 },
        { 165, ButtonFlags.OEM3 },
        { 167, ButtonFlags.OEM4 },
        { 168, ButtonFlags.OEM4 },
    };

    private Dictionary<byte, HidDevice> hidDevices = new();
    private AsusACPI asusACPI;

    private const byte INPUT_HID_ID = 0x5a;

    public override bool IsOpen => hidDevices.ContainsKey(INPUT_HID_ID) && hidDevices[INPUT_HID_ID].IsOpen && asusACPI is not null && asusACPI.IsOpen();

    public ROGAlly()
    {
        // device specific settings
        ProductIllustration = "device_rog_ally";

        // used to monitor OEM specific inputs
        _vid = 0x0B05;
        _pid = 0x1ABE;

        // https://www.amd.com/en/products/apu/amd-ryzen-z1
        // https://www.amd.com/en/products/apu/amd-ryzen-z1-extreme
        // https://www.amd.com/en/products/apu/amd-ryzen-7-7840u
        nTDP = new double[] { 15, 15, 20 };
        cTDP = new double[] { 5, 30 };
        GfxClock = new double[] { 100, 2700 };

        AngularVelocityAxis = new Vector3(-1.0f, 1.0f, 1.0f);
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
        Capabilities = DeviceCapabilities.FanControl;

        OEMChords.Add(new DeviceChord("CC",
            new List<KeyCode>(), new List<KeyCode>(),
            false, ButtonFlags.OEM1
        ));

        OEMChords.Add(new DeviceChord("AC",
            new List<KeyCode>(), new List<KeyCode>(),
            false, ButtonFlags.OEM2
        ));

        OEMChords.Add(new DeviceChord("M1/M2",
            new List<KeyCode>(), new List<KeyCode>(),
            false, ButtonFlags.OEM3
        ));
    }

    public override bool Open()
    {
        var success = base.Open();
        if (!success)
            return false;

        // try open asus ACPI
        asusACPI = new AsusACPI();
        if (asusACPI is null)
            return false;

        return true;
    }

    public override void Close()
    {
        // close Asus ACPI
        if (asusACPI is not null)
            asusACPI.Close();

        // close devices
        foreach (HidDevice hidDevice in hidDevices.Values)
            hidDevice.CloseDevice();

        base.Close();
    }

    public override bool IsReady()
    {
        IEnumerable<HidDevice> devices = GetHidDevices(_vid, _pid);
        foreach (HidDevice device in devices)
        {
            if (!device.IsConnected)
                continue;

            if (device.ReadFeatureData(out byte[] data, INPUT_HID_ID))
            {
                device.OpenDevice();
                device.MonitorDeviceEvents = true;

                hidDevices[INPUT_HID_ID] = device;

                Task<HidReport> ReportDevice = Task.Run(async () => await device.ReadReportAsync());
                ReportDevice.ContinueWith(t => OnReport(ReportDevice.Result, device));
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

    private void OnReport(HidReport result, HidDevice device)
    {
        Task<HidReport> ReportDevice = Task.Run(async () => await device.ReadReportAsync());
        ReportDevice.ContinueWith(t => OnReport(ReportDevice.Result, device));

        // get key
        byte key = result.Data[0];
        HandleEvent(key);
    }

    public override void SetFanControl(bool enable)
    {
        if (!IsOpen)
            return;

        switch (enable)
        {
            case false:
                asusACPI.DeviceSet(AsusACPI.PerformanceMode, (int)AsusMode.Turbo);
                return;
        }
    }

    public override void SetFanDuty(double percent)
    {
        if (!IsOpen)
            return;

        asusACPI.SetFanSpeed(AsusFan.CPU, Convert.ToByte(percent));
        asusACPI.SetFanSpeed(AsusFan.GPU, Convert.ToByte(percent));
    }

    public override float ReadFanDuty()
    {
        if (!IsOpen)
            return 100.0f;

        int cpuFan = asusACPI.DeviceGet(AsusACPI.CPU_Fan);
        int gpuFan = asusACPI.DeviceGet(AsusACPI.GPU_Fan);
        return (cpuFan + gpuFan) / 2 * 100;
    }

    public override void SetKeyPressDelay(HIDmode controllerMode)
    {
        switch (controllerMode)
        {
            case HIDmode.DualShock4Controller:
                KeyPressDelay = 180;
                break;
            default:
                KeyPressDelay = 20;
                break;
        }
    }

    private void HandleEvent(byte key)
    {
        if (!keyMapping.ContainsKey(key))
            return;

        // get button
        ButtonFlags button = keyMapping[key];
        switch (key)
        {
            case 0:   // Back paddles: Release
                {
                    KeyRelease(ButtonFlags.OEM3);
                }
                return;

            case 165:   // Back paddles: Press
            case 167:   // Armory crate: Hold
                KeyPress(button);
                break;

            case 168:   // Armory crate: Hold, released
                KeyRelease(button);
                break;

            default:
            case 56:    // Armory crate: Click
            case 166:   // Command center: Click
                {
                    Task.Factory.StartNew(async () =>
                    {
                        KeyPress(button);
                        await Task.Delay(KeyPressDelay);
                        KeyRelease(button);
                    });
                }
                break;
        }
    }
}