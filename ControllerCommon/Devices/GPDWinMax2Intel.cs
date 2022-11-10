using System.Collections.Generic;
using System.Numerics;
using WindowsInput.Events;

namespace ControllerCommon.Devices
{
    public class GPDWinMax2Intel : Device
    {
        public GPDWinMax2Intel() : base()
        {
            this.ProductSupported = true;

            // device specific settings
            this.WidthHeightRatio = 2.4f;
            this.ProductIllustration = "device_gpd_winmax2";

            this.nTDP = new double[] { 15, 15, 20 };
            this.cTDP = new double[] { 15, 28 };

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
            listeners.Add(new DeviceChord("Menu",
                new List<KeyCode>() { KeyCode.LButton | KeyCode.XButton2 },
                new List<KeyCode>() { KeyCode.LButton | KeyCode.XButton2 },
                true
                ));

            listeners.Add(new DeviceChord("Bottom button left",
                new List<KeyCode>() { KeyCode.D9 },
                new List<KeyCode>() { KeyCode.D9 },
                false, Controllers.ControllerButtonFlags.Special2
                ));

            listeners.Add(new DeviceChord("Bottom button right",
                new List<KeyCode>() { KeyCode.D0 },
                new List<KeyCode>() { KeyCode.D0 },
                false, Controllers.ControllerButtonFlags.Special3
                ));
        }
    }
}
