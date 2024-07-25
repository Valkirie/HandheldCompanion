using hidapi;
using steam_hidapi.net.Hid;
using steam_hidapi.net.Util;
using System;
using System.Threading.Tasks;

namespace steam_hidapi.net
{
    public class NeptuneController : SteamController
    {
        // TODO: why task not thread? HID read loop is a thread, rumble is a thread
        protected Task _configureTask;
        protected bool _active = false;

        public Func<NeptuneControllerInputEventArgs, Task> OnControllerInputReceived;

        public NeptuneController(ushort vid, ushort pid, short index) : base(vid, pid, index)
        {
            _hidDevice = new HidDevice(_vid, _pid, 64, index)
            {
                OnInputReceived = input => Task.Run(() => OnInputReceived(input))
            };
        }

        internal override void OnInputReceived(HidDeviceInputReceivedEventArgs e)
        {
            if (!_hidDevice.IsDeviceValid || !_active)
                return;

            // this should always be so
            if ((e.Buffer[0] != 1) || (e.Buffer[1] != 0))
                return;

            switch (e.Buffer[2])
            {
                case (byte)SCEventType.INPUT_DATA:
                    break;
                case (byte)SCEventType.CONNECT:
                case (byte)SCEventType.BATTERY:
                    // TODO: useful?
                    break;
                case (byte)SCEventType.DECK_INPUT_DATA:
                    {
                        NCInput input = e.Buffer.ToStructure<NCInput>();
                        NeptuneControllerInputState state = new NeptuneControllerInputState(input);
                        if (OnControllerInputReceived != null)
                            OnControllerInputReceived(new NeptuneControllerInputEventArgs(state));
                    }
                    break;
            }
        }

        public override void Open()
        {
            base.Open();

            // neptune needs hearbeat loop
            _active = true;
            _configureTask = Task.Run(ConfigureLoop);
        }

        public override void Close()
        {
            // stop the loop
            _active = false;
            _configureTask.Wait();

            // make sure lizard is as requested, loop might've not done the last request
            SetLizardMode(_lizard);

            base.Close();
        }

        internal async void ConfigureLoop()
        {
            while (_active)
            {
                SetLizardMode(_lizard);
                await Task.Delay(1000);
            }
        }

        public void RequestLizardMode(bool lizard)
        {
            _lizard = lizard;
        }

        public byte[] SetHaptic2(SCHapticMotor position, NCHapticStyle style, sbyte intensity)
        {
            if (!_hidDevice.IsDeviceValid || !_active)
                return null;

            NCHapticPacket2 haptic = new NCHapticPacket2
            {
                packet_type = (byte)SCPacketType.SET_HAPTIC2,
                len = 0xd,
                position = position,
                style = style,
                unsure3 = 0x4,
                intensity = intensity
            };
            var ts = Environment.TickCount;
            haptic.tsA = ts;
            haptic.tsB = ts;

            byte[] data = haptic.ToBytes();
            return _hidDevice.RequestFeatureReport(data);
        }
    }
}
