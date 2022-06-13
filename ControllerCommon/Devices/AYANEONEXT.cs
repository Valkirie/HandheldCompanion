using WindowsInput.Events;
using static ControllerCommon.OneEuroFilter;

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

            oneEuroSettings = new OneEuroSettings(0.002d, 0.008d);

            /*
             * AYA NEO NEXT Big Button:
             * KeyPress: 	RControlKey
             * KeyPress: 	LWin
             * KeyPress: 	F12

             * AYA NEO NEXT Small Button:
             * KeyPress: 	LWin
             * KeyPress: 	D
            */

            listeners.Add("Custom key BIG", new ChordClick(KeyCode.RControlKey, KeyCode.LWin, KeyCode.F12));
            listeners.Add("Custom key Small", new ChordClick(KeyCode.LWin, KeyCode.D));
        }
    }
}
