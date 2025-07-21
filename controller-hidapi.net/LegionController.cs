using hidapi;
using System.Threading.Tasks;

namespace controller_hidapi.net
{
    public class LegionController : GenericController
    {
        public LegionController(ushort vid, ushort pid) : base(vid, pid)
        { }

        public byte GetStatus(int idx)
        {
            if (_hidDevice != null && IsDeviceValid)
            {
                byte[] Data = _hidDevice.Read();
                if (Data != null)
                    return Data[idx];
            }

            return 0;
        }
    }
}