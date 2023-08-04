using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using ControllerCommon.Inputs;
using ControllerCommon.Managers;
using ControllerCommon.Utils;
using HandheldCompanion;
using HidSharp;
using HidSharp.Reports.Input;
using Nefarius.Utilities.DeviceManagement.PnP;
using WindowsInput.Events;

namespace ControllerCommon.Devices;

public class ROGAlly : IDevice
{
    private readonly Dictionary<byte, ButtonFlags> keyMapping = new()
    {
        { 0, ButtonFlags.None },
        { 56, ButtonFlags.OEM2 },
        { 162, ButtonFlags.None },
        { 166, ButtonFlags.OEM1 },
        { 165, ButtonFlags.OEM3 },
        { 167, ButtonFlags.OEM4 },
        { 168, ButtonFlags.OEM4 },
        { 236, ButtonFlags.None }
    };

    private bool previousWasEmpty;
    private IEnumerable<HidDevice> _hidDevices;
    private List<HidStream> _hidStreams = new();

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

        /*
         * ROG Ally has these default TDP values in boost mode:
         * Slow  = 43
         * Stamp = 30
         * Fast  = 53
         */
        DefaultTDP = new double[] {43, 30, 53};
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

        // set exclusive connection with highest priority
        var deviceConfiguration = new OpenConfiguration();
        deviceConfiguration.SetOption(OpenOption.Exclusive, true);
        deviceConfiguration.SetOption(OpenOption.Transient, true);
        deviceConfiguration.SetOption(OpenOption.Priority, OpenPriority.VeryHigh);

        foreach (var _hidDevice in _hidDevices)
        {
            try
            {
                // connect to hid device
                var _stream = _hidDevice.Open();

                // add stream to array
                _hidStreams.Add(_stream);

                // get descriptor
                var deviceDescriptor = _hidDevice.GetReportDescriptor();

                foreach (var inputReport in deviceDescriptor.InputReports)
                {
                    DeviceItemInputParser hiddeviceInputParser = inputReport.DeviceItem.CreateDeviceItemInputParser();
                    HidDeviceInputReceiver hidDeviceInputReceiver = deviceDescriptor.CreateHidDeviceInputReceiver();

                    // listen for event(s)
                    hidDeviceInputReceiver.Received += (sender, e) =>
                        InputReportReceiver_Received(_hidDevice, hiddeviceInputParser, hidDeviceInputReceiver);

                    // start receiver
                    hidDeviceInputReceiver.Start(_stream);

                    LogManager.LogInformation("HID connected: {0}", _stream.Device.DevicePath);
                }
            }
            catch
            {
                LogManager.LogError("HID error: {0}", _hidDevice.DevicePath);
            }
        }
        return true;
    }

    public override void Close()
    {
        // close stream(s)
        foreach (HidStream stream in _hidStreams)
            stream.Close();

        // clear array
        _hidStreams.Clear();

        base.Close();
    }
    public override bool IsReady()
    {
        // get hid devices
        _hidDevices = DeviceList.Local.GetHidDevices()
            .Where(d => d.ProductID == _pid && d.VendorID == _vid);

        var _hidDevice = _hidDevices.FirstOrDefault();

        if (_hidDevice is null)
            return false;

        var pnpDevice = PnPDevice.GetDeviceByInterfaceId(_hidDevice.DevicePath);
        var device_parent = pnpDevice.GetProperty<string>(DevicePropertyKey.Device_Parent);

        var pnpParent = PnPDevice.GetDeviceByInstanceId(device_parent);
        var parent_guid = pnpParent.GetProperty<Guid>(DevicePropertyKey.Device_ClassGuid);
        var parent_instanceId = pnpParent.GetProperty<string>(DevicePropertyKey.Device_InstanceId);

        return DeviceHelper.IsDeviceAvailable(parent_guid, parent_instanceId);
    }

    public override void SetKeyPressDelay(HIDmode controllerMode)
    {
        switch(controllerMode)
        {
            case HIDmode.DualShock4Controller:
                KeyPressDelay = 180;
                break;
            default:
                KeyPressDelay = 20;
                break;
        }
    }

    private void InputReportReceiver_Received(HidDevice hidDevice, DeviceItemInputParser hiddeviceInputParser,
        HidDeviceInputReceiver hidDeviceInputReceiver)
    {
        var inputReportBuffer = new byte[hidDevice.GetMaxInputReportLength()];

        while (hidDeviceInputReceiver.TryRead(inputReportBuffer, 0, out var report))
        {
            switch (report.ReportID)
            {
                case 90:
                    break;
                default:
                    return;
            }

            if (!hiddeviceInputParser.TryParseReport(inputReportBuffer, 0, report)) continue;
            var key = inputReportBuffer[1];

            if (!keyMapping.ContainsKey(key))
                return;

            // get button
            var button = keyMapping[key];

            switch (key)
            {
                case 236:
                    return;

                case 0:
                {
                    // two empty packets in a row means back button was released
                    if (previousWasEmpty)
                        KeyRelease(ButtonFlags.OEM3);

                    previousWasEmpty = true;
                }
                    return;

                case 56:
                case 166:
                {
                    // OEM1 and OEM2 key needs a key press delay based on emulated controller
                    Task.Factory.StartNew(async () =>
                    {
                        KeyPress(button);
                        await Task.Delay(KeyPressDelay);
                        KeyRelease(button);
                    });
                }
                break;

                case 165:
                case 167:
                    KeyPress(button);
                    break;

                case 168:
                    KeyRelease(button);
                    break;

                default:
                {
                    Task.Factory.StartNew(async () =>
                    {
                        KeyPress(button);
                        await Task.Delay(20);
                        KeyRelease(button);
                    });
                }
                    break;
            }

            previousWasEmpty = false;
        }
    }
}