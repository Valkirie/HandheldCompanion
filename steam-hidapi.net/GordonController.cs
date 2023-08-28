using hidapi;
using steam_hidapi.net.Hid;
using steam_hidapi.net.Util;
using System;
using System.Threading.Tasks;

namespace steam_hidapi.net
{
    public class GordonController : SteamController
    {
        // device configuration
        protected bool _gyro = false;
        protected ushort _idle = 0;
        protected ushort _volt = 0;
        protected byte _battery = 0;

        public Func<GordonControllerInputEventArgs, Task> OnControllerInputReceived;

        public GordonController(ushort vid, ushort pid, short index) : base(vid, pid, index)
        {
            _hidDevice = new HidDevice(_vid, _pid, 64, index);
            _hidDevice.OnInputReceived = input => Task.Run(() => OnInputReceived(input));
        }

        internal override void OnInputReceived(HidDeviceInputReceivedEventArgs e)
        {
            // this should always be so
            if ((e.Buffer[0] != 1) || (e.Buffer[1] != 0))
                return;

            switch (e.Buffer[2])
            {
                case (byte)SCEventType.INPUT_DATA:
                    {
                        GCInput input = e.Buffer.ToStructure<GCInput>();
                        GordonControllerInputState state = new GordonControllerInputState(input);
                        if (OnControllerInputReceived != null)
                            OnControllerInputReceived(new GordonControllerInputEventArgs(state));
                    }
                    break;
                case (byte)SCEventType.CONNECT:
                    {
                        // TODO: how does this event work for wired?
                        byte status = e.Buffer[4];  // 0x01: disconnected, 0x02: connected
                        if (status == 0x02)
                        {
                            // restore previously set configuration
                            SetLizardMode(_lizard);
                            SetGyroscope(_gyro);
                            SetIdleTimeout(_idle);
                        }
                        // TODO: Inform ControllerManager about D/C? Right now connected status in the main UI
                        // is being connected to the wireless dongle rather than the controller itself.
                    }
                    break;
                case (byte)SCEventType.BATTERY:
                    {
                        _volt = BitConverter.ToUInt16(e.Buffer, 12);
                        _battery = e.Buffer[14];  // in %
                    }
                    break;
                case (byte)SCEventType.DECK_INPUT_DATA:
                    break;
            }
        }

        public void SetGyroscope(bool gyro)
        {
            _gyro = gyro;

            if (gyro)
            {
                WriteRegister(SCRegister.GYRO_MODE, (ushort)GCGyroMode.ACCEL | (ushort)GCGyroMode.GYRO);
            }
            else
            {
                WriteRegister(SCRegister.GYRO_MODE, (ushort)GCGyroMode.NONE);
            }
        }

        public void SetIdleTimeout(ushort idle)
        {
            _idle = idle;

            WriteRegister(SCRegister.IDLE_TIMEOUT, idle);
        }

        public void TurnOff()
        {
            if (!_hidDevice.IsDeviceValid)
                return;

            byte[] req = new byte[] {
                (byte)SCPacketType.OFF,
                0x04,  // payload size
                0x6f, 0x66, 0x66, 0x21 };

            _hidDevice.RequestFeatureReport(req);
        }

        public void SetLedIntensity(byte value)
        {
            if (value > 100)
                value = 100;

            WriteRegister(SCRegister.LED_INTENSITY, value);
        }

        // TODO: how does it work for wired?
        public (byte, ushort) GetPowerData()
        {
            return (_battery, _volt);
        }
    }
}
