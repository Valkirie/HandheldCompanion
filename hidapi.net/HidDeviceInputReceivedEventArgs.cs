using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
