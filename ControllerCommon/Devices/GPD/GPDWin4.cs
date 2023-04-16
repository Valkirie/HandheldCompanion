using ControllerCommon.Inputs;
using System.Collections.Generic;
using System.Numerics;
using WindowsInput.Events;

namespace ControllerCommon.Devices
{
    public class GPDWin4 : IDevice
    {
        public GPDWin4() : base()
        {
            // device specific settings
            this.ProductIllustration = "device_gpd4";

            // https://www.amd.com/fr/products/apu/amd-ryzen-7-6800u
            this.nTDP = new double[] { 15, 15, 28 };
            this.cTDP = new double[] { 5, 28 };
            this.GfxClock = new double[] { 100, 2200 };

            // Note, requires new feature to enable, somehow, TBD.            
            // win4 EC address（2E，2F）：Speed Read：0xC880,0xC881，C880 - high byte，C881 - low byte；Control：0xC311，0 - auto，> 0:manual，Set Speed from 1 to 127，127 - 100 %.

            // device specific capacities
            //this.Capacities = DeviceCapacities.FanControl;

            /*
            this.FanDetails = new FanDetails()
            {
                AddressControl = 0xC311,// done
                AddressDuty = 0x1809,   // unsure
                AddressRegistry = 0x2E, // done
                AddressData = 0x2F,     // done
                ValueMin = 0,           // done
                ValueMax = 127          // done
            };
            */

            this.AngularVelocityAxis = new Vector3(-1.0f, -1.0f, 1.0f);
            this.AngularVelocityAxisSwap = new()
            {
                { 'X', 'X' },
                { 'Y', 'Z' },
                { 'Z', 'Y' },
            };

            this.AccelerationAxisSwap = new()
            {
                { 'X', 'X' },
                { 'Y', 'Z' },
                { 'Z', 'Y' },
            };

            // Note, OEM1 not configured as this device has it's own Menu button for guide button

            // Note, chords need to be manually configured in GPD app first by end user

            // GPD Back buttons do not have a "hold", configured buttons are key down and up immediately
            // Holding back buttons will result in same key down and up input every 2-3 seconds
            // Configured chords in GPD app need unique characters otherwise this leads to a
            // "mixed" result when pressing both buttons at the same time
            OEMChords.Add(new DeviceChord("Bottom button left",
                new List<KeyCode>() { KeyCode.F11, KeyCode.L },
                new List<KeyCode>() { KeyCode.F11, KeyCode.L },
                false, ButtonFlags.OEM2
                ));

            OEMChords.Add(new DeviceChord("Bottom button right",
                new List<KeyCode>() { KeyCode.F12, KeyCode.R },
                new List<KeyCode>() { KeyCode.F12, KeyCode.R },
                false, ButtonFlags.OEM3
                ));
        }
    }
}
