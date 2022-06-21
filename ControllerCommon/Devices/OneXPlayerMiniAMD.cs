using System.Numerics;
using WindowsInput.Events;
using static ControllerCommon.OneEuroFilter;

namespace ControllerCommon.Devices
{
    public class OneXPlayerMiniAMD : Device
    {
        public OneXPlayerMiniAMD() : base()
        {
            this.ProductSupported = true;

            // device specific settings
            this.WidthHeightRatio = 2.4f;
            this.ProductIllustration = "device_onexplayer_mini";

            this.AngularVelocityAxisSwap = new()
            {
                { 'X', 'X' },
                { 'Y', 'Z' },
                { 'Z', 'Y' },
            };

            this.AccelerationAxis = new Vector3(-1.0f, -1.0f, 1.0f);
            this.AccelerationAxisSwap = new()
            {
                { 'X', 'X' },
                { 'Y', 'Z' },
                { 'Z', 'Y' },
            };

            listeners.Add("Keyboard key", new ChordClick(KeyCode.LWin, KeyCode.RControlKey, KeyCode.O));
            listeners.Add("Function key", new ChordClick(KeyCode.LWin, KeyCode.D));
        }
    }
}
