using WindowsInput.Events;

namespace ControllerCommon.Devices
{
    public class AYANEONEXT : Device
    {
        public AYANEONEXT() : base()
        {
            this.ProductSupported = true;

            // device specific settings
            this.WidthHeightRatio = 2.4f;
            this.ProductIllustration = "device_aya_next";
            this.ProductModel = "AYANEONext";

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

            listeners.Add("Custom key BIG", new ChordClick(KeyCode.RControlKey, KeyCode.LWin, KeyCode.F12));
            listeners.Add("Custom key Small", new ChordClick(KeyCode.LWin, KeyCode.D));
        }
    }
}
