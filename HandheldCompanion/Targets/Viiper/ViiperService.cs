using HandheldCompanion.Shared;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace HandheldCompanion.Targets.Viiper
{
    public sealed class ViiperService : IDisposable
    {
        private readonly object _lock = new();
        private readonly Dictionary<Tuple<uint, uint>, LibViiper.FeedbackCallback> _feedbackDelegates = new();
        private bool _initialized;

        public event Action<uint, uint, byte[]>? FeedbackReceived;

        public bool IsInitialized
        {
            get
            {
                lock (_lock)
                    return _initialized;
            }
        }

        public bool Initialize(string listenAddr = "127.0.0.1:3241")
        {
            lock (_lock)
            {
                if (_initialized)
                    return true;

                int result;
                try
                {
                    result = LibViiper.viiper_init(listenAddr);
                }
                catch (DllNotFoundException ex)
                {
                    LogManager.LogError("libviiper.dll not found: {0}", ex.Message);
                    return false;
                }
                catch (Exception ex)
                {
                    LogManager.LogError("viiper_init failed unexpectedly: {0}", ex.Message);
                    return false;
                }

                if (result != 0)
                {
                    LogManager.LogError("viiper_init failed: {0}", LibViiper.GetLastError());
                    return false;
                }

                _initialized = true;
                LogManager.LogInformation("VIIPER USB/IP server started on {0}", listenAddr);
                return true;
            }
        }

        public bool CreateBus(uint busId)
        {
            int result = LibViiper.viiper_bus_create(busId);
            if (result != 0)
            {
                LogManager.LogError("viiper_bus_create({0}) failed: {1}", busId, LibViiper.GetLastError());
                return false;
            }

            LogManager.LogInformation("VIIPER bus {0} created", busId);
            return true;
        }

        public bool RemoveBus(uint busId)
        {
            int result = LibViiper.viiper_bus_remove(busId);
            if (result != 0)
            {
                LogManager.LogWarning("viiper_bus_remove({0}) failed: {1}", busId, LibViiper.GetLastError());
                return false;
            }

            LogManager.LogInformation("VIIPER bus {0} removed", busId);
            return true;
        }

        public ViiperAddDeviceResult AddDevice(uint busId, string typeName, ushort vid = 0, ushort pid = 0)
        {
            int result;
            uint deviceId;

            if (vid != 0 || pid != 0)
            {
                try
                {
                    result = LibViiper.viiper_device_add_ex(busId, typeName, vid, pid, out deviceId);
                }
                catch (EntryPointNotFoundException)
                {
                    result = LibViiper.viiper_device_add(busId, typeName, out deviceId);
                    vid = 0;
                    pid = 0;
                }
            }
            else
            {
                result = LibViiper.viiper_device_add(busId, typeName, out deviceId);
            }

            if (result != 0)
            {
                LogManager.LogError("viiper_device_add({0}, {1}, vid=0x{2:X4}, pid=0x{3:X4}) failed: {4}", busId, typeName, vid, pid, LibViiper.GetLastError());
                return new ViiperAddDeviceResult(false, 0);
            }

            RegisterFeedbackCallback(busId, deviceId);
            LogManager.LogInformation("VIIPER device added: {0} (bus={1}, dev={2}, vid=0x{3:X4}, pid=0x{4:X4})", typeName, busId, deviceId, vid, pid);
            return new ViiperAddDeviceResult(true, deviceId);
        }

        public bool RemoveDevice(uint busId, uint deviceId)
        {
            _feedbackDelegates.Remove(Tuple.Create(busId, deviceId));
            int result = LibViiper.viiper_device_remove(busId, deviceId);
            if (result != 0)
            {
                LogManager.LogWarning("viiper_device_remove({0}, {1}) failed: {2}", busId, deviceId, LibViiper.GetLastError());
                return false;
            }

            LogManager.LogInformation("VIIPER device removed (bus={0}, dev={1})", busId, deviceId);
            return true;
        }

        public bool SetInput(uint busId, uint deviceId, byte[] data)
        {
            bool ok = LibViiper.viiper_device_set_input(busId, deviceId, data, data.Length) == 0;
            if (!ok)
                LogManager.LogWarning("viiper_device_set_input failed (bus={0}, dev={1}, len={2}): {3}", busId, deviceId, data.Length, LibViiper.GetLastError());

            return ok;
        }

        public string[] GetDeviceTypes()
        {
            return LibViiper.GetDeviceTypes();
        }

        private void RegisterFeedbackCallback(uint busId, uint deviceId)
        {
            LibViiper.FeedbackCallback cb = OnFeedback;
            _feedbackDelegates[Tuple.Create(busId, deviceId)] = cb;

            int result = LibViiper.viiper_device_set_feedback_callback(busId, deviceId, cb, IntPtr.Zero);
            if (result != 0)
                LogManager.LogWarning("Failed to register VIIPER feedback callback: {0}", LibViiper.GetLastError());
        }

        private void OnFeedback(uint busId, uint deviceId, IntPtr data, int len, IntPtr userData)
        {
            if (len <= 0 || data == IntPtr.Zero)
                return;

            byte[] bytes = new byte[len];
            Marshal.Copy(data, bytes, 0, len);
            FeedbackReceived?.Invoke(busId, deviceId, bytes);
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (!_initialized)
                    return;

                try
                {
                    LibViiper.viiper_shutdown();
                }
                catch (Exception ex)
                {
                    LogManager.LogWarning("viiper_shutdown failed: {0}", ex.Message);
                }

                _feedbackDelegates.Clear();
                _initialized = false;
                LogManager.LogInformation("VIIPER shut down");
            }
        }
    }

    public readonly struct ViiperAddDeviceResult
    {
        public readonly bool Success;
        public readonly uint DeviceId;

        public ViiperAddDeviceResult(bool success, uint deviceId)
        {
            Success = success;
            DeviceId = deviceId;
        }
    }
}
