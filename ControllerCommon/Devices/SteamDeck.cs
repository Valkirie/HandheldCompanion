using System.Collections.Generic;
using System.Numerics;
using WindowsInput.Events;

namespace ControllerCommon.Devices
{
    public class SteamDeck : Device
    {
        public SteamDeck() : base()
        {
            this.ProductSupported = true;

            // device specific settings
            this.ProductIllustration = "device_valve_jupiter";
            this.ProductModel = "SteamDeck";

            // Steam Controller Neptune
            this.hasSensors[Utils.DeviceUtils.SensorFamily.Controller] = true;

            // https://www.amd.com/fr/products/apu/amd-ryzen-7-5800u
            this.nTDP = new double[] { 15, 15, 20 };
            this.cTDP = new double[] { 10, 25 };
            this.GfxClock = new double[] { 100, 2000 };
        }
    }
}
