﻿using System.Collections.Generic;
using System.Numerics;
using WindowsInput.Events;

namespace ControllerCommon.Devices
{
    public class GPDWinMax2AMD : Device
    {
        public GPDWinMax2AMD() : base()
        {
            this.ProductSupported = true;

            // device specific settings
            this.ProductIllustration = "device_gpd_winmax2";

            // https://www.amd.com/fr/products/apu/amd-ryzen-7-6800u
            this.nTDP = new double[] { 15, 15, 28 };
            this.cTDP = new double[] { 15, 28 };
            this.GfxClock = new double[] { 100, 2200 };

            this.AngularVelocityAxis = new Vector3(1.0f, 1.0f, -1.0f);
            this.AngularVelocityAxisSwap = new()
            {
                { 'X', 'Y' },
                { 'Y', 'Z' },
                { 'Z', 'X' },
            };

            this.AccelerationAxis = new Vector3(1.0f, -1.0f, 1.0f);
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
                false, Controllers.ControllerButtonFlags.OEM1
                ));

            listeners.Add(new DeviceChord("Bottom button right",
                new List<KeyCode>() { KeyCode.D0 },
                new List<KeyCode>() { KeyCode.D0 },
                false, Controllers.ControllerButtonFlags.OEM2
                ));
        }
    }
}
