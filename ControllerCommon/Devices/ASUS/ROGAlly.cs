using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using ControllerCommon.Inputs;
using HidSharp;
using HidSharp.Reports.Input;
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
        // prepare configuration
        var deviceConfiguration = new OpenConfiguration();
        deviceConfiguration.SetOption(OpenOption.Exclusive, true);
        deviceConfiguration.SetOption(OpenOption.Transient, true);

        foreach (var _hidDevice in DeviceList.Local.GetHidDevices()
                     .Where(d => d.ProductID == _pid && d.VendorID == _vid))
        {
            // get descriptor
            var deviceDescriptor = _hidDevice.GetReportDescriptor();

            if (!_hidDevice.TryOpen(deviceConfiguration, out var inputStream)) continue;
            foreach (var inputReport in deviceDescriptor.InputReports)
            {
                var hiddeviceInputParser = inputReport.DeviceItem.CreateDeviceItemInputParser();
                var hidDeviceInputReceiver = deviceDescriptor.CreateHidDeviceInputReceiver();

                hidDeviceInputReceiver.Received += (sender, e) =>
                    InputReportReciever_Received(_hidDevice, hiddeviceInputParser, hidDeviceInputReceiver);
                hidDeviceInputReceiver.Start(inputStream);
            }
        }

        return base.Open();
    }

    public override void Close()
    {
        base.Close();
    }

    private void InputReportReciever_Received(HidDevice hidDevice, DeviceItemInputParser hiddeviceInputParser,
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