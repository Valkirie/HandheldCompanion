namespace controller_hidapi.net
{
    public class LegionController : GenericController
    {
        public LegionController(ushort vid, ushort pid, ushort inputBufferLen = 64, short mi = -1) : base(vid, pid, inputBufferLen, mi)
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