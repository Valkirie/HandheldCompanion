using hidapi;
using System;
using System.Threading.Tasks;

namespace controller_hidapi.net
{
    public class GenericController
    {
        // device data
        protected ushort _vid, _pid;
        protected short _index;
        // subclass is responsible for opening the device
        protected HidDevice _hidDevice;

        public bool Reading => _hidDevice.Reading;
        public bool IsDeviceValid => _hidDevice.IsDeviceValid;

        public event OnControllerInputReceivedEventHandler OnControllerInputReceived;
        public delegate void OnControllerInputReceivedEventHandler(byte[] Data);

        public GenericController(ushort vid, ushort pid, ushort inputBufferLen = 64, short mi = -1)
        {
            _vid = vid;
            _pid = pid;

            _hidDevice = new HidDevice(_vid, _pid, inputBufferLen, mi)
            {
                OnInputReceived = input =>
                {
                    OnInputReceived(input);
                    return Task.CompletedTask;
                }
            };
        }

        internal virtual void OnInputReceived(HidDeviceInputReceivedEventArgs e)
        {
            OnControllerInputReceived?.Invoke(e.Buffer);
        }

        public virtual bool Open()
        {
            bool isOpen = _hidDevice.OpenDevice();
            if (!isOpen)
                throw new Exception("Could not open device!");
            _hidDevice.BeginRead();

            return isOpen;
        }

        public virtual void Close()
        {
            EndRead();
            _hidDevice.Close();
        }

        public virtual void EndRead()
        {
            if (_hidDevice.IsDeviceValid)
                _hidDevice.EndRead();
        }

        public void HidWrite(byte[] data)
        {
            try
            {
                _hidDevice.Write(data);
            }
            catch { }
        }
    }
}
