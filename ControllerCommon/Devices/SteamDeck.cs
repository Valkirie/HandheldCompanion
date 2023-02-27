using ControllerCommon.Inputs;

namespace ControllerCommon.Devices
{
    public class SteamDeck : IDevice
    {
        public SteamDeck() : base()
        {
            this.ProductSupported = true;

            // device specific settings
            this.ProductIllustration = "device_valve_jupiter";
            this.ProductModel = "SteamDeck";

            // Steam Controller Neptune
            this.Capacities |= DeviceCapacities.ControllerSensor;

            // https://www.steamdeck.com/en/tech
            this.nTDP = new double[] { 10, 10, 15 };
            this.cTDP = new double[] { 4, 15 };

            // https://www.techpowerup.com/gpu-specs/steam-deck-gpu.c3897
            this.GfxClock = new double[] { 100, 1600 };

            OEMChords.Add(new DeviceChord("STEAM",
                new(), new(),
                false, ButtonFlags.OEM1
                ));

            OEMChords.Add(new DeviceChord("...",
                new(), new(),
                false, ButtonFlags.OEM2
                ));
        }
    }
}
