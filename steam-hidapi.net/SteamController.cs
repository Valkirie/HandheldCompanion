using hidapi;
using steam_hidapi.net.Hid;
using steam_hidapi.net.Util;
using System;
using System.Text;

namespace steam_hidapi.net
{
    public class SteamController
    {
        // device data
        protected ushort _vid, _pid;
        protected short _index;
        // subclass is responsible for opening the device
        protected HidDevice _hidDevice;

        // device configuration
        protected bool _lizard = true;

        public string SerialNumber { get; private set; }

        public SteamController(ushort vid, ushort pid, short index)
        {
            _vid = vid;
            _pid = pid;
            _index = index;
        }

        internal virtual void OnInputReceived(HidDeviceInputReceivedEventArgs e)
        {
        }

        internal virtual byte[] WriteSingleCmd(SCPacketType cmd)
        {
            if (!_hidDevice.IsDeviceValid)
                return null;

            return _hidDevice.RequestFeatureReport(new byte[] { (byte)cmd, 0x00 });
        }

        internal virtual byte[] WriteRegister(SCRegister reg, ushort value)
        {
            if (!_hidDevice.IsDeviceValid)
                return null;

            byte[] req = new byte[] {
                (byte)SCPacketType.WRITE_REGISTER,
                0x03,  // payload size
                (byte)reg,
                (byte)(value & 0xFF),  // lo
                (byte)(value >> 8) };  // hi

            return _hidDevice.RequestFeatureReport(req);
        }

        public virtual byte[] SetHaptic(byte position, ushort amplitude, ushort period, ushort count)
        {
            if (!_hidDevice.IsDeviceValid)
                return null;

            SCHapticPacket haptic = new SCHapticPacket
            {
                packet_type = (byte)SCPacketType.SET_HAPTIC,
                len = 0x07,
                position = position,
                amplitude = amplitude,
                period = period,
                count = count
            };

            byte[] data = haptic.ToBytes();
            return _hidDevice.RequestFeatureReport(data);
        }

        public virtual void SetLizardMode(bool lizard)
        {
            _lizard = lizard;

            if (lizard)
            {
                WriteSingleCmd(SCPacketType.DEFAULT_MAPPINGS);
                WriteSingleCmd(SCPacketType.DEFAULT_MOUSE);
            }
            else
            {
                WriteSingleCmd(SCPacketType.CLEAR_MAPPINGS);
                WriteRegister(SCRegister.RPAD_MODE, (ushort)SCLizardMode.OFF);
                WriteRegister(SCRegister.LPAD_MODE, (ushort)SCLizardMode.OFF);
                // Steam Deck
                //WriteRegister(SCRegister.LPAD_CLICK_PRESSURE, 0xFFFF);
                //WriteRegister(SCRegister.RPAD_CLICK_PRESSURE, 0xFFFF);
            }
        }

        public virtual string ReadSerialNumber()
        {
            // ignore if reading serial fails
            byte[] serial = Encoding.UTF8.GetBytes("XXXXX");
            try
            {
                byte[] request = new byte[] { (byte)SCPacketType.GET_SERIAL, 0x15, 0x01 };
                byte[] response = _hidDevice.RequestFeatureReport(request);
                serial = new byte[response.Length - 5];
                Array.Copy(response, 4, serial, 0, serial.Length);
            }
            catch { }

            return Encoding.ASCII.GetString(serial).TrimEnd((Char)0);
        }

        public virtual void Open()
        {
            if (!_hidDevice.OpenDevice())
                throw new Exception("Could not open device!");
            SerialNumber = ReadSerialNumber();
            _hidDevice.BeginRead();
        }

        public virtual void Close()
        {
            if (_hidDevice.IsDeviceValid)
                _hidDevice.EndRead();
            _hidDevice.Dispose();
        }
    }
}
