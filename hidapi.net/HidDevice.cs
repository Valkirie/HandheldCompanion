using hidapi.Native;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace hidapi
{
    public class HidDevice : IDisposable
    {
        private ushort _vid, _pid, _inputBufferLen;
        private IntPtr _deviceHandle;
        private object _lock = new object();
        private bool _reading = false;
        private Thread _readThread;
        private long MillisecondsSinceEpoch => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        public bool IsDeviceValid => _deviceHandle != IntPtr.Zero;
        public bool Reading => _reading;
        public Func<HidDeviceInputReceivedEventArgs, Task> OnInputReceived;
        public HidDevice(ushort vendorId, ushort productId, ushort inputBufferLen = 64)
        {
            _vid = vendorId;
            _pid = productId;
            _inputBufferLen = inputBufferLen;
        }

        private void ThrowIfDeviceInvalid()
        {
            if (!IsDeviceValid)
                throw new HidDeviceInvalidException();
        }

        public Task<bool> OpenDeviceAsync() => Task.Run(() => OpenDevice());
        public bool OpenDevice()
        {
            lock (_lock)
            {
                IntPtr devEnum = HidApiNative.hid_enumerate(_vid, _pid);
                IntPtr deviceInfo = devEnum;

                while (deviceInfo != IntPtr.Zero)
                {
                    HidDeviceInfo hidDeviceInfo = new HidDeviceInfo(deviceInfo);

                    _deviceHandle = HidApiNative.hid_open_path(hidDeviceInfo.Path);

                    if (_deviceHandle != IntPtr.Zero)
                        break;

                    deviceInfo = hidDeviceInfo.NextDevicePtr;
                }

                HidApiNative.hid_free_enumeration(deviceInfo);
                return _deviceHandle != IntPtr.Zero;
            }
        }

        public Task<byte[]> ReadAsync(int timeout = 100) => Task.Run(() => Read(timeout));
        public byte[] Read(int timeout = 100)
        {
            ThrowIfDeviceInvalid();
            lock (_lock)
            {
                byte[] buffer = new byte[_inputBufferLen];
                int length = HidApiNative.hid_read_timeout(_deviceHandle, buffer, (uint)buffer.Length, timeout);
                return buffer;
            }

        }

        public Task<int> ReadAsync(byte[] data) => Task.Run(() => Read(data));
        public int Read(byte[] buffer, int timeout = 100)
        {
            if (buffer.Length < _inputBufferLen)
                throw new ArgumentException("Buffer length is lower than input buffer length.");

            ThrowIfDeviceInvalid();
            lock (_lock)
            {
                int length = HidApiNative.hid_read_timeout(_deviceHandle, buffer, _inputBufferLen, timeout);
                return length;
            }

        }

        public Task<byte[]> RequestFeatureReportAsync(byte[] request) => Task.Run(() => RequestFeatureReport(request));
        public byte[] RequestFeatureReport(byte[] request)
        {
            if (request.Length > _inputBufferLen)
                throw new ArgumentException("Request length is greater than input buffer length.");

            ThrowIfDeviceInvalid();

            byte[] request_full = new byte[_inputBufferLen + 1];
            Array.Copy(request, 0, request_full, 1, request.Length);
            byte[] response = new byte[_inputBufferLen + 1];

            int err = HidApiNative.hid_send_feature_report(_deviceHandle, request_full, (uint)(_inputBufferLen + 1));
            if (err < 0)
            {
                throw new Exception($"Could not send report to hid device. Error: {err}");
            }
            err = HidApiNative.hid_get_feature_report(_deviceHandle, response, (uint)(_inputBufferLen + 1));
            if (err < 0)
            {
                throw new Exception($"Could not get report from hid device. Error: {err}");
            }

            return response;
        }

        private void ReadLoop()
        {
            byte[] buffer = new byte[_inputBufferLen];
            int len = 0;
            while (_reading)
            {
                len = Read(buffer);
                if (len > 0)
                {
                    if (OnInputReceived != null)
                        _ = OnInputReceived(new HidDeviceInputReceivedEventArgs(this, buffer));
                }
            }
        }
        public Task WriteAsync(byte[] data) => Task.Run(() => Write(data));
        public void Write(byte[] data)
        {
            if (data.Length > _inputBufferLen)
                throw new ArgumentException("Data length is greater than input buffer length.");

            ThrowIfDeviceInvalid();
            byte[] buffer = new byte[_inputBufferLen];
            Array.Copy(data, buffer, data.Length);

            if (HidApiNative.hid_write(_deviceHandle, buffer, (uint)buffer.Length) < 0)
            {
                throw new Exception("Failed to write to HID device.");
            }
        }

        public void BeginRead()
        {
            _reading = true;
            _readThread = new Thread(new ThreadStart(ReadLoop));
            _readThread.IsBackground = true;
            _readThread.Start();
        }

        public void EndRead()
        {
            _reading = false;
        }

        public void Close()
        {
            HidApiNative.hid_close(_deviceHandle);
        }

        public void Dispose()
        {
            Close();
        }
    }
}
