using System.Collections.Generic;
using System.Numerics;
using WindowsInput.Events;

namespace ControllerCommon.Devices
{
    public class GPDWin3 : Device
    {
        public GPDWin3() : base()
        {
            this.ProductSupported = true;

            // device specific settings
            this.ProductIllustration = "device_gpd3";

            // https://www.intel.com/content/www/us/en/products/sku/217187/intel-core-i71195g7-processor-12m-cache-up-to-5-00-ghz/specifications.html
            this.nTDP = new double[] { 20, 20, 25 };
            this.cTDP = new double[] { 7, 25 };

            listeners.Add(new DeviceChord("Bottom button left",
                new List<KeyCode>() { KeyCode.D9 },
                new List<KeyCode>() { KeyCode.D9 }
                ));

            listeners.Add(new DeviceChord("Bottom button right",
                new List<KeyCode>() { KeyCode.D0 },
                new List<KeyCode>() { KeyCode.D0 }
                ));
        }
    }
}
