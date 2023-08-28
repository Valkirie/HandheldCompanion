using System;

namespace hidapi
{
    public class HidDeviceInputReceivedEventArgs : EventArgs
    {
        public HidDevice Device { get; private set; }
        public byte[] Buffer { get; private set; }

        public HidDeviceInputReceivedEventArgs(HidDevice device, byte[] buffer)
        {
            Device = device;
            Buffer = (byte[])buffer.Clone();
        }
    }
}
