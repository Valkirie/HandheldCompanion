using System.Numerics;
using WindowsInput.Events;
using static ControllerCommon.OneEuroFilter;

namespace ControllerCommon.Devices
{
    public class OneXPlayerMiniIntel : Device
    {
        public OneXPlayerMiniIntel() : base()
        {
            this.ProductSupported = true;

            // device specific settings
            this.WidthHeightRatio = 2.4f;
            this.ProductIllustration = "device_onexplayer_mini";

            this.AngularVelocityAxis = new Vector3(1.0f, 1.0f, -1.0f);
            this.AngularVelocityAxisSwap = new()
            {
                { 'X', 'Y' },
                { 'Y', 'Z' },
                { 'Z', 'X' },
            };

            this.AccelerationAxis = new Vector3(-1.0f, 1.0f, -1.0f);
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
