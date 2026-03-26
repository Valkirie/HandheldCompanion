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

        // For pre-allocated instances inside HidDevice — takes ownership, no clone.
        internal HidDeviceInputReceivedEventArgs(HidDevice device, byte[] ownedBuffer, bool _)
        {
            Device = device;
            Buffer = ownedBuffer;
        }
    }
}
