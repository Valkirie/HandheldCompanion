namespace controller_hidapi.net
{
    public class LegionController : GenericController
    {
        public LegionController(ushort vid, ushort pid, ushort inputBufferLen = 64, short index = -1) : base(vid, pid, inputBufferLen, index)
        { }

        public byte GetStatus(int idx)
        {
            if (_hidDevice != null && IsDeviceValid)
            {
                byte[] Data = _hidDevice.Read();
                if (Data != null && idx < Data.Length - 1)
                    return Data[idx];
            }

            return 0;
        }
    }
}