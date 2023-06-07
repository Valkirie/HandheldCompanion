using System;
using System.Threading;
using System.Threading.Tasks;
using hidapi.Native;

namespace hidapi
{
    public class HidDevice : IDisposable
    {
        private IntPtr _deviceHandle;
        private readonly object _lock = new object();
        private Thread _readThread;
        private readonly ushort _vid;
        private readonly ushort _pid;
        private readonly ushort _inputBufferLen;
        public Func<HidDeviceInputReceivedEventArgs, Task> OnInputReceived;

        public HidDevice(ushort vendorId, ushort productId, ushort inputBufferLen = 64)
        {
            _vid = vendorId;
            _pid = productId;
            _inputBufferLen = inputBufferLen;
        }

        private long MillisecondsSinceEpoch => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        public bool IsDeviceValid => _deviceHandle != IntPtr.Zero;
        public bool Reading { get; private set; }

        public void Dispose()
        {
            Close();
        }

        private void ThrowIfDeviceInvalid()
        {
            if (!IsDeviceValid)
                throw new HidDeviceInvalidException();
        }

        public Task<bool> OpenDeviceAsync()
        {
            return Task.Run(() => OpenDevice());
        }

        public bool OpenDevice()
        {
            lock (_lock)
            {
                var devEnum = HidApiNative.hid_enumerate(_vid, _pid);
                var deviceInfo = devEnum;

                while (deviceInfo != IntPtr.Zero)
                {
                    var hidDeviceInfo = new HidDeviceInfo(deviceInfo);

                    _deviceHandle = HidApiNative.hid_open_path(hidDeviceInfo.Path);

                    if (_deviceHandle != IntPtr.Zero)
                        break;

                    deviceInfo = hidDeviceInfo.NextDevicePtr;
                }

                HidApiNative.hid_free_enumeration(deviceInfo);
                return _deviceHandle != IntPtr.Zero;
            }
        }

        public Task<byte[]> ReadAsync(int timeout = 100)
        {
            return Task.Run(() => Read(timeout));
        }

        public byte[] Read(int timeout = 100)
        {
            ThrowIfDeviceInvalid();
            lock (_lock)
            {
                var buffer = new byte[_inputBufferLen];
                var length = HidApiNative.hid_read_timeout(_deviceHandle, buffer, (uint)buffer.Length, timeout);
                return buffer;
            }
        }

        public Task<int> ReadAsync(byte[] data)
        {
            return Task.Run(() => Read(data));
        }

        public int Read(byte[] buffer, int timeout = 100)
        {
            if (buffer.Length < _inputBufferLen)
                throw new ArgumentException("Buffer length is lower than input buffer length.");

            ThrowIfDeviceInvalid();
            lock (_lock)
            {
                var length = HidApiNative.hid_read_timeout(_deviceHandle, buffer, _inputBufferLen, timeout);
                return length;
            }
        }

        public Task<byte[]> RequestFeatureReportAsync(byte[] request)
        {
            return Task.Run(() => RequestFeatureReport(request));
        }

        public byte[] RequestFeatureReport(byte[] request)
        {
            if (request.Length > _inputBufferLen)
                throw new ArgumentException("Request length is greater than input buffer length.");

            ThrowIfDeviceInvalid();

            var request_full = new byte[_inputBufferLen + 1];
            Array.Copy(request, 0, request_full, 1, request.Length);
            var response = new byte[_inputBufferLen + 1];

            var err = HidApiNative.hid_send_feature_report(_deviceHandle, request_full, (uint)(_inputBufferLen + 1));
            if (err < 0) throw new Exception($"Could not send report to hid device. Error: {err}");
            err = HidApiNative.hid_get_feature_report(_deviceHandle, response, (uint)(_inputBufferLen + 1));
            if (err < 0) throw new Exception($"Could not get report from hid device. Error: {err}");

            return response;
        }

        private void ReadLoop()
        {
            var buffer = new byte[_inputBufferLen];
            var len = 0;
            while (Reading)
            {
                len = Read(buffer);
                if (len > 0)
                    if (OnInputReceived != null)
                        _ = OnInputReceived(new HidDeviceInputReceivedEventArgs(this, buffer));
            }
        }

        public Task WriteAsync(byte[] data)
        {
            return Task.Run(() => Write(data));
        }

        public void Write(byte[] data)
        {
            if (data.Length > _inputBufferLen)
                throw new ArgumentException("Data length is greater than input buffer length.");

            ThrowIfDeviceInvalid();
            var buffer = new byte[_inputBufferLen];
            Array.Copy(data, buffer, data.Length);

            if (HidApiNative.hid_write(_deviceHandle, buffer, (uint)buffer.Length) < 0)
                throw new Exception("Failed to write to HID device.");
        }

        public void BeginRead()
        {
            Reading = true;
            _readThread = new Thread(ReadLoop);
            _readThread.IsBackground = true;
            _readThread.Start();
        }

        public void EndRead()
        {
            Reading = false;
        }

        public void Close()
        {
            HidApiNative.hid_close(_deviceHandle);
        }
    }
}