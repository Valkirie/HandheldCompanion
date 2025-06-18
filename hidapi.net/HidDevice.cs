using hidapi.Native;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace hidapi
{
    public class HidDevice : IDisposable
    {
        private ushort _vid, _pid, _inputBufferLen;
        private byte[] _buffer;
        private short _mi;
        private IntPtr _deviceHandle;
        private object _lock = new object();
        private bool _reading = false;
        private bool _halting = false;
        private Thread _readThread;
        private long MillisecondsSinceEpoch => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        public bool IsDeviceValid => _deviceHandle != IntPtr.Zero;
        public bool Reading => _reading;
        public Func<HidDeviceInputReceivedEventArgs, Task> OnInputReceived;

        public HidDevice(ushort vendorId, ushort productId, ushort inputBufferLen = 64, short mi = -1)
        {
            _vid = vendorId;
            _pid = productId;
            _inputBufferLen = inputBufferLen;
            _buffer = new byte[inputBufferLen];
            _mi = mi;
        }

        private void ThrowIfDeviceInvalid()
        {
            if (!IsDeviceValid)
                throw new HidDeviceInvalidException();
        }

        private static short GetMI(string path)
        {
            string low = path.ToLower();
            int index = low.IndexOf("mi_");
            if (index == -1)
                return -1;
            string mi = low.Substring(index + 3, 2);

            if (short.TryParse(mi, out short number))
                return number;

            return -1;
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
                    if (_mi != -1 && _mi != GetMI(hidDeviceInfo.Path))
                        goto next;

                    _deviceHandle = HidApiNative.hid_open_path(hidDeviceInfo.Path);
                    if (_deviceHandle != IntPtr.Zero)
                    {
                        ushort inputReportLength = GetInputReportByteLength(hidDeviceInfo.Path);
                        if (inputReportLength == _inputBufferLen)
                            break;
                    }

                next:
                    deviceInfo = hidDeviceInfo.NextDevicePtr;
                }

                HidApiNative.hid_free_enumeration(deviceInfo);
                return _deviceHandle != IntPtr.Zero;
            }
        }

        public static ushort GetInputReportByteLength(string devicePath)
        {
            IntPtr deviceHandle = HidApiNative.CreateFile(devicePath, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
            if (deviceHandle == IntPtr.Zero)
            {
                throw new IOException("Unable to open HID device", Marshal.GetLastWin32Error());
            }

            try
            {
                if (!HidApiNative.HidD_GetPreparsedData(deviceHandle, out IntPtr preparsedData))
                {
                    throw new IOException("Unable to get preparsed data", Marshal.GetLastWin32Error());
                }

                try
                {
                    if (HidApiNative.HidP_GetCaps(preparsedData, out HIDP_CAPS caps) != 0)
                    {
                        return caps.InputReportByteLength;
                    }
                    else
                    {
                        throw new IOException("Unable to get HID capabilities");
                    }
                }
                finally
                {
                    HidApiNative.HidD_FreePreparsedData(preparsedData);
                }
            }
            finally
            {
                HidApiNative.CloseHandle(deviceHandle);
            }
        }

        public Task<byte[]> ReadAsync(int timeout = 100) => Task.Run(() => Read(timeout));
        public byte[] Read(int timeout = 100)
        {
            ThrowIfDeviceInvalid();
            lock (_lock)
            {
                int length = HidApiNative.hid_read_timeout(_deviceHandle, _buffer, (uint)_buffer.Length, timeout);
                return _buffer;
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

            lock (_lock)
            {
                byte[] request_full = new byte[_inputBufferLen + 1];
                Array.Copy(request, 0, request_full, 1, request.Length);
                byte[] response = new byte[_inputBufferLen + 1];

                int err = HidApiNative.hid_send_feature_report(_deviceHandle, request_full, (uint)(_inputBufferLen + 1));
                /*
                if (err < 0)
                    throw new Exception($"Could not send report to hid device. Error: {err}");
                */

                err = HidApiNative.hid_get_feature_report(_deviceHandle, response, (uint)(_inputBufferLen + 1));
                /*
                if (err < 0)
                    throw new Exception($"Could not get report from hid device. Error: {err}");
                */

                return response;
            }
        }

        public Task WriteAsync(byte[] data) => Task.Run(() => Write(data));
        public void Write(byte[] data)
        {
            if (data.Length > _inputBufferLen)
                throw new ArgumentException("Data length is greater than input buffer length.");

            ThrowIfDeviceInvalid();

            lock (_lock)
            {
                Array.Copy(data, _buffer, data.Length);

                int err = HidApiNative.hid_write(_deviceHandle, _buffer, (uint)_buffer.Length);
                if (err < 0)
                    throw new Exception($"Failed to write to HID device. Error: {err}");
            }
        }

        private void ReadLoop()
        {
            while (_reading && !_halting)
                if (Read(_buffer) > 0 && OnInputReceived != null)
                    _ = OnInputReceived(new HidDeviceInputReceivedEventArgs(this, _buffer));
        }

        public void BeginRead()
        {
            _reading = true;
            _halting = false;

            _readThread = new Thread(new ThreadStart(ReadLoop))
            {
                IsBackground = true,
                Priority = ThreadPriority.Highest
            };
            _readThread.Start();
        }

        public void EndRead()
        {
            _halting = true;

            // kill read thread
            if (_readThread != null)
            {
                _reading = false;
                // Ensure the thread has finished execution
                if (_readThread.IsAlive)
                    _readThread.Join(3000);
                _readThread = null;
            }
        }

        public void Close()
        {
            HidApiNative.hid_close(_deviceHandle);
            _deviceHandle = IntPtr.Zero;
        }

        public void Dispose()
        {
            EndRead();
            Close();
        }
    }
}
