using ControllerCommon.Inputs;
using System.Collections.Generic;
using System.Numerics;
using WindowsInput.Events;

namespace ControllerCommon.Devices
{
    public class GPDWinMax2Intel : IDevice
    {
        public GPDWinMax2Intel() : base()
        {
            this.ProductSupported = true;

            // device specific settings
            this.ProductIllustration = "device_gpd_winmax2";

            // https://ark.intel.com/content/www/us/en/ark/products/226254/intel-core-i71260p-processor-18m-cache-up-to-4-70-ghz.html
            this.nTDP = new double[] { 15, 15, 20 };
            this.cTDP = new double[] { 15, 28 };
            this.GfxClock = new double[] { 100, 1400 };

            this.AngularVelocityAxis = new Vector3(1.0f, -1.0f, 1.0f);
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

            // Disabled this one as Win Max 2 also sends an Xbox guide input when Menu key is pressed.
            OEMChords.Add(new DeviceChord("Menu",
                new List<KeyCode>() { KeyCode.LButton | KeyCode.XButton2 },
                new List<KeyCode>() { KeyCode.LButton | KeyCode.XButton2 },
                true
                ));

            OEMChords.Add(new DeviceChord("Bottom button left",
                new List<KeyCode>() { KeyCode.D9 },
                new List<KeyCode>() { KeyCode.D9 },
                false, ButtonFlags.OEM1
                ));

            OEMChords.Add(new DeviceChord("Bottom button right",
                new List<KeyCode>() { KeyCode.D0 },
                new List<KeyCode>() { KeyCode.D0 },
                false, ButtonFlags.OEM2
                ));
        }
    }
}
