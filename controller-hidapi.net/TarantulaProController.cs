using controller_hidapi.net.Util;
using hidapi;
using System.Drawing;
using System.Threading.Tasks;

namespace controller_hidapi.net
{
    public class TarantulaProController : GenericController
    {

        //                                                                  (test: 0x01, normal: 0x02)
        private byte[] ControllerMode = new byte[33] { 0x7, 0x04, 0x0a, 0x02, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0 };

        //                                                                  brigthness  mode
        private byte[] LEDMode = new byte[33] { 0x7, 0x06, 0x07, 0x01, 0x64, 0x32, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0 };

        //                                                                        H     V     S
        private byte[] LEDColor = new byte[33] { 0x7, 0x10, 0x07, 0x03, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0 };

        //                                                                                                      button            action            button
        private byte[] ButtonMode = new byte[33] { 0x7, 0x13, 0x05, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0 };

        //                                         T3    C1    C2    T1    T2    C3    C4    M1    M2
        private byte[] ExtraButtons = new byte[] { 0x28, 0x29, 0x2a, 0x26, 0x27, 0x2b, 0x2c, 0x24, 0x25 };

        //                                                                                 (nintendo: 0x02, xbox: 0x01)
        private byte[] ControllerLayout = new byte[] { 0x07, 0x07, 0x09, 0x01, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0 };

        public TarantulaProController(ushort vid, ushort pid) : base(vid, pid)
        {
            _hidDevice = new HidDevice(_vid, _pid, 64)
            {
                OnInputReceived = input =>
                {
                    OnInputReceived(input);
                    return Task.CompletedTask;
                }
            };

            /*
            // LED ON
            LEDMode[6] = 1;
            hidDevice.Write(LEDMode);
            */
        }

        public void SetLightColor(byte R, byte G, byte B)
        {
            Color color = Color.FromArgb(R, G, B);
            ColorUtils.ColorToHSV(color, out double hue, out double saturation, out double value);

            LEDColor[5] = (byte)hue;
            LEDColor[6] = (byte)saturation;
            LEDColor[7] = (byte)value;

            HidWrite(LEDColor);
        }

        public void SetXboxMode()
        {
            SetLayoutMode(0x01);
        }

        public void SetNintendoMode()
        {
            SetLayoutMode(0x02);
        }

        private void SetLayoutMode(byte mode)
        {
            ControllerLayout[6] = mode;
            HidWrite(ControllerLayout);
        }

        public void SetVerboseMode()
        {
            SetControllerMode(0x02);
        }

        public void SetTestMode()
        {
            SetControllerMode(0x01);
        }

        private void SetControllerMode(byte mode)
        {
            ControllerMode[4] = mode;
            HidWrite(ControllerMode);

            // reset all buttons behaviors
            ResetController();
        }

        private void ResetController()
        {
            foreach (byte button in ExtraButtons)
            {
                ButtonMode[10] = button;
                HidWrite(ButtonMode);
            }
        }

        private void HidWrite(byte[] data)
        {
            try
            {
                _hidDevice.Write(data);
            }
            catch { }
        }
    }
}