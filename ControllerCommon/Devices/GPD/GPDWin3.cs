﻿using System.Collections.Generic;
using ControllerCommon.Inputs;
using WindowsInput.Events;

namespace ControllerCommon.Devices;

public class GPDWin3 : IDevice
{
    public GPDWin3()
    {
        // device specific settings
        ProductIllustration = "device_gpd3";

        // https://www.intel.com/content/www/us/en/products/sku/217187/intel-core-i71195g7-processor-12m-cache-up-to-5-00-ghz/specifications.html
        nTDP = new double[] { 20, 20, 25 };
        cTDP = new double[] { 7, 25 };
        GfxClock = new double[] { 100, 1400 };

        // note, need to manually configured in GPD app
        OEMChords.Add(new DeviceChord("Bottom button left",
            new List<KeyCode> { KeyCode.F11, KeyCode.L },
            new List<KeyCode> { KeyCode.F11, KeyCode.L },
            false, ButtonFlags.OEM1
        ));

        OEMChords.Add(new DeviceChord("Bottom button right",
            new List<KeyCode> { KeyCode.F12, KeyCode.R },
            new List<KeyCode> { KeyCode.F12, KeyCode.R },
            false, ButtonFlags.OEM2
        ));
    }
}