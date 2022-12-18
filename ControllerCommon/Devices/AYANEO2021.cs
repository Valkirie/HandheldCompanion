using System.Collections.Generic;
using WindowsInput.Events;

namespace ControllerCommon.Devices
{
    public class AYANEO2021 : Device
    {
        public AYANEO2021() : base()
        {
            this.ProductSupported = true;

            // device specific settings
            this.ProductIllustration = "device_aya_2021";
            this.ProductModel = "AYANEO2021";

            // https://www.amd.com/fr/products/apu/amd-ryzen-5-4500u
            this.nTDP = new double[] { 15, 15, 20 };
            this.cTDP = new double[] { 10, 25 };
            this.GfxClock = new double[] { 100, 1750 };

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

            listeners.Add(new DeviceChord("WIN key",
                new List<KeyCode>() { KeyCode.LWin },
                new List<KeyCode>() { KeyCode.LWin },
                false, ControllerButtonFlags.OEM1
                ));

            // Conflicts with OS
            //listeners.Add("TM key", new ChordClick(KeyCode.RAlt, KeyCode.RControlKey, KeyCode.Delete));

            listeners.Add(new DeviceChord("ESC key",
                new List<KeyCode>() { KeyCode.Escape },
                new List<KeyCode>() { KeyCode.Escape },
                false, ControllerButtonFlags.OEM2
                ));

            // Conflicts with Ayaspace when installed
            listeners.Add(new DeviceChord("KB key",
                new List<KeyCode>() { KeyCode.RControlKey, KeyCode.LWin, KeyCode.O },
                new List<KeyCode>() { KeyCode.O, KeyCode.LWin, KeyCode.RControlKey },
                false, ControllerButtonFlags.OEM3
                ));
        }
    }
}
