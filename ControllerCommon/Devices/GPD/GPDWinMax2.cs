using ControllerCommon.Inputs;
using System.Collections.Generic;
using WindowsInput.Events;

namespace ControllerCommon.Devices
{
    public class GPDWinMax2 : IDevice
    {
        public GPDWinMax2() : base()
        {
            // device specific settings
            this.ProductIllustration = "device_gpd_winmax2";

            // device specific capacities
            this.Capacities = DeviceCapacities.FanControl;

            this.FanDetails = new FanDetails()
            {
                AddressControl = 0x275,
                AddressDuty = 0x1809,
                AddressRegistry = 0x4E,
                AddressData = 0x4F,
                ValueMin = 0,
                ValueMax = 184
            };

            // Disabled this one as Win Max 2 also sends an Xbox guide input when Menu key is pressed.
            OEMChords.Add(new DeviceChord("Menu",
                new List<KeyCode>() { KeyCode.LButton | KeyCode.XButton2 },
                new List<KeyCode>() { KeyCode.LButton | KeyCode.XButton2 },
                true, ButtonFlags.OEM1
                ));

            OEMChords.Add(new DeviceChord("Bottom button left",
                new List<KeyCode>() { KeyCode.D9 },
                new List<KeyCode>() { KeyCode.D9 },
                false, ButtonFlags.OEM2
                ));

            OEMChords.Add(new DeviceChord("Bottom button right",
                new List<KeyCode>() { KeyCode.D0 },
                new List<KeyCode>() { KeyCode.D0 },
                false, ButtonFlags.OEM3
                ));
        }
    }
}
