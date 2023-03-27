using ControllerCommon.Inputs;
using System.Collections.Generic;
using System.Numerics;
using WindowsInput.Events;

namespace ControllerCommon.Devices
{
    public class AYANEO2 : IDevice
    {
        public AYANEO2() : base()
        {
            // device specific settings
            this.ProductIllustration = "device_aya_2";
            this.ProductModel = "AYANEO2";

            // https://www.amd.com/en/products/apu/amd-ryzen-7-6800u
            this.nTDP = new double[] { 15, 15, 20 };
            this.cTDP = new double[] { 3, 33 };
            this.GfxClock = new double[] { 100, 2200 };

            this.AccelerationAxis = new Vector3(1.0f, 1.0f, 1.0f);
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

            // device specific capacities
            this.Capacities = DeviceCapacities.FanControl;

            this.FanDetails = new FanDetails()
            {
                AddressControl = 0x44A,
                AddressDuty = 0x44B,
                AddressRegistry = 0x4E,
                AddressData = 0x4F,
                ValueMin = 0,
                ValueMax = 100
            };

            OEMChords.Add(new DeviceChord("Custom Key Top Right",
                new List<KeyCode>() { KeyCode.RControlKey, KeyCode.LWin, KeyCode.F16 },
                new List<KeyCode>() { KeyCode.F16, KeyCode.LWin, KeyCode.RControlKey },
                false, ButtonFlags.OEM3
                ));

            OEMChords.Add(new DeviceChord("Custom Key Top Left",
                new List<KeyCode>() { KeyCode.RControlKey, KeyCode.LWin, KeyCode.F15 },
                new List<KeyCode>() { KeyCode.F15, KeyCode.LWin, KeyCode.RControlKey },
                false, ButtonFlags.OEM4
                ));

            OEMChords.Add(new DeviceChord("Custom Key Big",
                new List<KeyCode>() { KeyCode.RControlKey, KeyCode.LWin, KeyCode.F17 },
                new List<KeyCode>() { KeyCode.F17, KeyCode.LWin, KeyCode.RControlKey },
                false, ButtonFlags.OEM1
                ));

            OEMChords.Add(new DeviceChord("Custom Key Small",
                new List<KeyCode>() { KeyCode.LWin, KeyCode.D },
                new List<KeyCode>() { KeyCode.LWin, KeyCode.D },
                false, ButtonFlags.OEM2
                ));
        }
    }
}
