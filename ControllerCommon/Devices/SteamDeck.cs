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

            // https://www.steamdeck.com/en/tech
            this.nTDP = new double[] { 10, 10, 15 };
            this.cTDP = new double[] { 4, 15 };

            // https://www.techpowerup.com/gpu-specs/steam-deck-gpu.c3897
            this.GfxClock = new double[] { 100, 1600 };
        }
    }
}
