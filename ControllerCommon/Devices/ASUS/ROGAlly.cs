using ControllerCommon.Inputs;
using HidSharp;
using HidSharp.Reports;
using HidSharp.Reports.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ControllerCommon.Devices
{
    public class ROGAlly : IDevice
    {
        public ROGAlly() : base()
        {
            // device specific settings
            this.ProductIllustration = "device_rog_ally";

            // https://www.amd.com/en/products/apu/amd-ryzen-z1
            // https://www.amd.com/en/products/apu/amd-ryzen-z1-extreme
            // https://www.amd.com/en/products/apu/amd-ryzen-7-7840u
            this.nTDP = new double[] { 15, 15, 20 };
            this.cTDP = new double[] { 5, 53 };
            this.GfxClock = new double[] { 100, 2700 };

            this.AngularVelocityAxis = new Vector3(-1.0f, 1.0f, 1.0f);
            this.AngularVelocityAxisSwap = new()
            {
                { 'X', 'X' },
                { 'Y', 'Z' },
                { 'Z', 'Y' },
            };

            this.AccelerationAxis = new Vector3(1.0f, 1.0f, 1.0f);
            this.AccelerationAxisSwap = new()
            {
                { 'X', 'X' },
                { 'Y', 'Z' },
                { 'Z', 'Y' },
            };

            OEMChords.Add(new DeviceChord("CC",
                new(), new(),
                false, ButtonFlags.OEM1
                ));

            OEMChords.Add(new DeviceChord("AC",
                new(), new(),
                false, ButtonFlags.OEM2
                ));

            OEMChords.Add(new DeviceChord("M1/M2",
                new(), new(),
                false, ButtonFlags.OEM3
                ));

            // hid specific
            _vid = 0x0B05;
            _pid = 0x1ABE;

            // todo: improve me
            _hidDevice = DeviceList.Local.GetHidDevices().Where(d => d.ProductID == _pid && d.VendorID == _vid && d.DevicePath.Contains("mi_02&col01")).First();
        }

        public override bool Open()
        {
            if (_hidDevice is not null)
            {
                // get descriptor
                var deviceDescriptor = _hidDevice.GetReportDescriptor();

                // prepare configuration
                OpenConfiguration deviceConfiguration = new OpenConfiguration();
                deviceConfiguration.SetOption(OpenOption.Exclusive, true);
                deviceConfiguration.SetOption(OpenOption.Transient, true);

                if (_hidDevice.TryOpen(deviceConfiguration, out var inputStream))
                {
                    var inputReport = deviceDescriptor.InputReports.First();
                    _hiddeviceInputParser = inputReport.DeviceItem.CreateDeviceItemInputParser();
                    _hidDeviceInputReceiver = deviceDescriptor.CreateHidDeviceInputReceiver();

                    _hidDeviceInputReceiver.Received += InputReportReciever_Received;
                    _hidDeviceInputReceiver.Start(inputStream);
                }
            }

            return base.Open();
        }

        public override void Close()
        {
            if (_hidDeviceInputReceiver is not null)
                _hidDeviceInputReceiver.Received -= InputReportReciever_Received;

            base.Close();
        }

        private bool previousWasEmpty = false;
        private Dictionary<byte, ButtonFlags> keyMapping = new()
        {
            { 0, ButtonFlags.None },
            { 56, ButtonFlags.OEM2 },
            { 162, ButtonFlags.None },
            { 166, ButtonFlags.OEM1 },
            { 165, ButtonFlags.OEM3 },
            { 167, ButtonFlags.OEM4 },
            { 168, ButtonFlags.OEM4 },
            { 236, ButtonFlags.None },
        };

        private void InputReportReciever_Received(object sender, EventArgs e)
        {
            byte[] inputReportBuffer = new byte[_hidDevice.GetMaxInputReportLength()];

            while (_hidDeviceInputReceiver.TryRead(inputReportBuffer, 0, out Report report))
            {
                // Parse the report if possible.
                // This will return false if (for example) the report applies to a different DeviceItem.
                if (_hiddeviceInputParser.TryParseReport(inputReportBuffer, 0, report))
                {
                    byte key = inputReportBuffer[1];
                    ButtonFlags button = keyMapping[key];

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
    }
}
