using hidapi.Native;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace hidapi
{
    public class HidDevice : IDisposable
    {
        private ushort _vid, _pid, _inputBufferLen;
        private byte[] _buffer;
        private byte[] _writeBuffer;
        private byte[] _readReturnBuffer;
        private short _index;
        private string _devicePath;
        private IntPtr _deviceHandle;
        private object _lock = new object();
        private bool _reading = false;
        private bool _halting = false;
        private Thread _readThread;
        private HidDeviceInputReceivedEventArgs _eventArgs;
        private ushort _releaseNumber;
        public bool IsDeviceValid => _deviceHandle != IntPtr.Zero;
        public bool Reading => _reading;
        public ushort ReleaseNumber => _releaseNumber;
        public string DevicePath => _devicePath;
        public Action<HidDeviceInputReceivedEventArgs> OnInputReceived;

        public HidDevice(ushort vendorId, ushort productId, ushort inputBufferLen, short index, string devicePath = null)
        {
            _vid = vendorId;
            _pid = productId;
            _inputBufferLen = inputBufferLen;
            _buffer = new byte[inputBufferLen];
            _writeBuffer = new byte[inputBufferLen];
            _readReturnBuffer = new byte[inputBufferLen];
            _eventArgs = new HidDeviceInputReceivedEventArgs(this, new byte[inputBufferLen], true);
            _index = index;
            _devicePath = devicePath;
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

        public System.Threading.Tasks.Task<bool> OpenDeviceAsync() => System.Threading.Tasks.Task.Run(() => OpenDevice());
        public bool OpenDevice()
        {
            lock (_lock)
            {
                // If a specific device path was provided, try to open it directly first
                if (!string.IsNullOrEmpty(_devicePath))
                {
                    _deviceHandle = HidApiNative.hid_open_path(_devicePath);
                    if (_deviceHandle != IntPtr.Zero)
                    {
                        try
                        {
                            ushort inputReportLength = GetInputReportByteLength(_devicePath);
                            if (inputReportLength == _inputBufferLen)
                            {
                                // Successfully opened the device by path
                                return true;
                            }
                        }
                        catch { }

                        HidApiNative.hid_close(_deviceHandle);
                        _deviceHandle = IntPtr.Zero;
                    }
                    // If opening by path failed, fall through to enumerate by VID/PID/index
                }

                IntPtr devEnum = HidApiNative.hid_enumerate(_vid, _pid);
                IntPtr deviceInfo = devEnum;

                try
                {
                    while (deviceInfo != IntPtr.Zero)
                    {
                        HidDeviceInfo hidDeviceInfo = new HidDeviceInfo(deviceInfo);
                        if (_index != -1 && _index != GetMI(hidDeviceInfo.Path))
                            goto next;

                        _deviceHandle = HidApiNative.hid_open_path(hidDeviceInfo.Path);
                        if (_deviceHandle != IntPtr.Zero)
                        {
                            try
                            {
                                ushort inputReportLength = GetInputReportByteLength(hidDeviceInfo.Path);
                                if (inputReportLength == _inputBufferLen)
                                {
                                    _releaseNumber = hidDeviceInfo.ReleaseNumber;
                                    break;
                                }
                            }
                            catch { }

                            HidApiNative.hid_close(_deviceHandle);
                            _deviceHandle = IntPtr.Zero;
                        }

                    next:
                        deviceInfo = hidDeviceInfo.NextDevicePtr;
                    }
                }
                finally
                {
                    HidApiNative.hid_free_enumeration(devEnum);
                }

                return _deviceHandle != IntPtr.Zero;
            }
        }

        public bool OpenDevice(string path)
        {
            lock (_lock)
            {
                if (string.IsNullOrEmpty(path))
                    return false;

                _deviceHandle = HidApiNative.hid_open_path(path);
                if (_deviceHandle == IntPtr.Zero)
                    return false;

                try
                {
                    ushort inputReportLength = GetInputReportByteLength(path);
                    if (inputReportLength == _inputBufferLen)
                        return true;
                }
                catch { }

                HidApiNative.hid_close(_deviceHandle);
                _deviceHandle = IntPtr.Zero;
                return false;
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

        public System.Threading.Tasks.Task<byte[]> ReadAsync(int timeout = 100) => System.Threading.Tasks.Task.Run(() => Read(timeout));
        public byte[] Read(int timeout = 100)
        {
            ThrowIfDeviceInvalid();
            lock (_lock)
            {
                int length = HidApiNative.hid_read_timeout(_deviceHandle, _buffer, (uint)_buffer.Length, timeout);
                Buffer.BlockCopy(_buffer, 0, _readReturnBuffer, 0, _buffer.Length);
                return _readReturnBuffer;
            }
        }

        public System.Threading.Tasks.Task<int> ReadAsync(byte[] data) => System.Threading.Tasks.Task.Run(() => Read(data));
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

        public System.Threading.Tasks.Task<byte[]> RequestFeatureReportAsync(byte[] request) => System.Threading.Tasks.Task.Run(() => RequestFeatureReport(request));
        public byte[] RequestFeatureReport(byte[] request)
        {
            try
            {
                if (request is null || request.Length > _inputBufferLen)
                    return null;

                ThrowIfDeviceInvalid();

                lock (_lock)
                {
                    byte[] request_full = new byte[_inputBufferLen + 1];
                    Array.Copy(request, 0, request_full, 1, request.Length);
                    byte[] response = new byte[_inputBufferLen + 1];

                    int err = HidApiNative.hid_send_feature_report(_deviceHandle, request_full, (uint)(_inputBufferLen + 1));
                    if (err < 0)
                        return null;

                    err = HidApiNative.hid_get_feature_report(_deviceHandle, response, (uint)(_inputBufferLen + 1));
                    if (err < 0)
                        return null;

                    return response;
                }
            }
            catch
            {
                return null;
            }
        }

        public System.Threading.Tasks.Task WriteAsync(byte[] data) => System.Threading.Tasks.Task.Run(() => Write(data));
        public void Write(byte[] data)
        {
            if (data.Length > _inputBufferLen)
                throw new ArgumentException("Data length is greater than input buffer length.");

            ThrowIfDeviceInvalid();

            lock (_lock)
            {
                Array.Clear(_writeBuffer, 0, _writeBuffer.Length);
                Array.Copy(data, _writeBuffer, data.Length);

                int err = HidApiNative.hid_write(_deviceHandle, _writeBuffer, (uint)_writeBuffer.Length);
                if (err < 0)
                    throw new Exception($"Failed to write to HID device. Error: {err}");
            }
        }

        private void ReadLoop()
        {
            while (_reading && !_halting)
            {
                try
                {
                    int len = Read(_buffer);
                    Action<HidDeviceInputReceivedEventArgs> onInputReceived = OnInputReceived;
                    if (len > 0 && onInputReceived != null)
                    {
                        Buffer.BlockCopy(_buffer, 0, _eventArgs.Buffer, 0, len);
                        onInputReceived(_eventArgs);
                    }
                    else if (len < 0)
                    {
                        _reading = false;
                    }
                }
                catch
                {
                    _reading = false;
                }
            }
        }

        public void BeginRead()
        {
            _reading = true;
            _halting = false;

            _readThread = new Thread(new ThreadStart(ReadLoop))
            {
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal
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
            lock (_lock)
            {
                if (_deviceHandle != IntPtr.Zero)
                {
                    HidApiNative.hid_close(_deviceHandle);
                    _deviceHandle = IntPtr.Zero;
                }
            }
        }

        public void Dispose()
        {
            EndRead();
            Close();
        }
    }
}
