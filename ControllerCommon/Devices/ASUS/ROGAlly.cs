using ControllerCommon.Inputs;
using ControllerCommon.Managers;
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

            // used to monitor OEM specific inputs
            this._vid = 0x0B05;
            this._pid = 0x1ABE;

            // https://www.amd.com/en/products/apu/amd-ryzen-z1
            // https://www.amd.com/en/products/apu/amd-ryzen-z1-extreme
            // https://www.amd.com/en/products/apu/amd-ryzen-7-7840u
            this.nTDP = new double[] { 15, 15, 20 };
            this.cTDP = new double[] { 5, 30 };
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
        }

        public override bool Open()
        {
            // prepare configuration
            OpenConfiguration deviceConfiguration = new OpenConfiguration();
            deviceConfiguration.SetOption(OpenOption.Exclusive, true);
            deviceConfiguration.SetOption(OpenOption.Transient, true);

            foreach (var _hidDevice in DeviceList.Local.GetHidDevices().Where(d => d.ProductID == _pid && d.VendorID == _vid))
            {
                // get descriptor
                var deviceDescriptor = _hidDevice.GetReportDescriptor();

                if (_hidDevice.TryOpen(deviceConfiguration, out var inputStream))
                {
                    foreach (var inputReport in deviceDescriptor.InputReports)
                    {
                        DeviceItemInputParser hiddeviceInputParser = inputReport.DeviceItem.CreateDeviceItemInputParser();
                        HidDeviceInputReceiver hidDeviceInputReceiver = deviceDescriptor.CreateHidDeviceInputReceiver();

                        hidDeviceInputReceiver.Received += (sender, e) => InputReportReciever_Received(_hidDevice, hiddeviceInputParser, hidDeviceInputReceiver);
                        hidDeviceInputReceiver.Start(inputStream);
                    }
                }
            }

            return base.Open();
        }

        public override void Close()
        {
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

        private void InputReportReciever_Received(HidDevice hidDevice, DeviceItemInputParser hiddeviceInputParser, HidDeviceInputReceiver hidDeviceInputReceiver)
        {
            byte[] inputReportBuffer = new byte[hidDevice.GetMaxInputReportLength()];

            while (hidDeviceInputReceiver.TryRead(inputReportBuffer, 0, out Report report))
            {
                switch(report.ReportID)
                {
                    case 90:
                        break;
                    default:
                        return;
                }

                if (hiddeviceInputParser.TryParseReport(inputReportBuffer, 0, report))
                {
                    byte key = inputReportBuffer[1];

                    if (!keyMapping.ContainsKey(key))
                        return;

                    // get button
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
