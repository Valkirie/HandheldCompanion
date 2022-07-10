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
            this.WidthHeightRatio = 2.4f;
            this.ProductIllustration = "device_aya_2021";
            this.ProductModel = "AYANEO2021";

            this.DefaultTDP = 20;

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

            listeners.Add("WIN key", new List<KeyCode>() { KeyCode.LWin });
            //listeners.Add("TM key", new ChordClick(KeyCode.RAlt, KeyCode.RControlKey, KeyCode.Delete)); // Conflicts with OS
            listeners.Add("ESC key", new List<KeyCode>() { KeyCode.Escape });
            listeners.Add("KB key", new List<KeyCode>() { KeyCode.RControlKey, KeyCode.LWin }); // Conflicts with Ayaspace when installed
        }
    }
}
