using HandheldCompanion.Shared;
using HandheldCompanion.Targets.Viiper;
using System;

namespace HandheldCompanion.Managers;

public static class ViiperServerManager
{
    private static readonly object _lock = new();

    private static uint? _busId;
    private static int _activeDeviceCount;
    private static ViiperService? _service;
    private static bool _isRunning;

    public static string Host { get; private set; } = "127.0.0.1";
    public static int Port { get; private set; } = 3241;
    public static bool IsRunning
    {
        get
        {
            lock (_lock)
                return _isRunning;
        }
    }

    public static int ActiveDeviceCount
    {
        get
        {
            lock (_lock)
                return _activeDeviceCount;
        }
    }

    internal static event Action<uint, uint, byte[]>? FeedbackReceived;

    public delegate void StartedEventHandler();
    public static event StartedEventHandler? Started;

    public delegate void StoppedEventHandler();
    public static event StoppedEventHandler? Stopped;

    public delegate void FailedEventHandler(string reason);
    public static event FailedEventHandler? Failed;

    public static void SetPort(int port)
    {
        lock (_lock)
        {
            Port = port;
            if (_isRunning)
                RestartInternal();
        }
    }

    public static void Start()
    {
        lock (_lock)
        {
            if (_isRunning)
                return;

            ViiperPnpCleanup.CleanupPresentViiperPhantomsBlocking();
            ViiperPnpCleanup.CleanupAllKnownGhosts();

            Host = ManagerFactory.settingsManager.GetString("VIIPERHost");
            if (string.IsNullOrWhiteSpace(Host))
                Host = "127.0.0.1";

            Port = ManagerFactory.settingsManager.GetInt("VIIPERPort");
            if (Port <= 0)
                Port = 3241;

            try
            {
                _service = new ViiperService();
                if (!_service.Initialize($"{Host}:{Port}"))
                {
                    _service.Dispose();
                    _service = null;
                    _isRunning = false;
                    Failed?.Invoke("Failed to initialize libviiper");
                    return;
                }

                _service.FeedbackReceived += Service_FeedbackReceived;

                _activeDeviceCount = 0;
                _isRunning = true;

                Started?.Invoke();
                LogManager.LogInformation("VIIPER libviiper server started on {0}:{1}", Host, Port);
                return;
            }
            catch (Exception ex)
            {
                _service?.Dispose();
                _service = null;
                _isRunning = false;
                Failed?.Invoke($"Failed to start VIIPER: {ex.Message}");
                LogManager.LogError("Failed to start VIIPER libviiper server: {0}", ex.Message);
            }
        }
    }

    public static void Stop()
    {
        lock (_lock)
        {
            if (!_isRunning && _service is null)
                return;

            try
            {
                UsbipCli.DetachAll();
                if (_service is not null)
                    _service.FeedbackReceived -= Service_FeedbackReceived;
                _service?.Dispose();
            }
            catch { }
            finally
            {
                _service = null;
                _busId = null;
                _activeDeviceCount = 0;
                _isRunning = false;
            }

            Stopped?.Invoke();
            LogManager.LogInformation("VIIPER server stopped");
        }
    }

    internal static bool TryCreateDevice(string typeName, ushort vendorId, ushort productId, out ViiperDeviceHandle handle)
    {
        lock (_lock)
        {
            handle = default;
            if (!_isRunning || _service is null)
                return false;

            uint busId = GetOrCreateBusIdLocked();
            var addedDevice = _service.AddDevice(busId, typeName, vendorId, productId);
            if (!addedDevice.Success)
            {
                _busId = null;
                busId = GetOrCreateBusIdLocked();
                addedDevice = _service.AddDevice(busId, typeName, vendorId, productId);
            }

            if (!addedDevice.Success)
                return false;

            _activeDeviceCount++;
            handle = new ViiperDeviceHandle(busId, addedDevice.DeviceId, vendorId, productId);
            return true;
        }
    }

    internal static bool SetInput(ViiperDeviceHandle handle, byte[] data)
    {
        ViiperService? service;
        lock (_lock)
        {
            service = _service;
        }

        return service is not null && service.SetInput(handle.BusId, handle.DeviceId, data);
    }

    internal static bool TrySwitchDeviceType(ViiperDeviceHandle handle, string typeName, ushort vendorId, ushort productId, out ViiperDeviceHandle switchedHandle)
    {
        lock (_lock)
        {
            switchedHandle = default;
            if (!_isRunning || _service is null)
                return false;

            var switchedDevice = _service.SwitchDeviceType(handle.BusId, handle.DeviceId, typeName, vendorId, productId);
            if (!switchedDevice.Success)
                return false;

            switchedHandle = new ViiperDeviceHandle(handle.BusId, switchedDevice.DeviceId, vendorId, productId);
            return true;
        }
    }

    internal static bool RemoveDevice(ViiperDeviceHandle handle)
    {
        lock (_lock)
        {
            // Detach while libviiper is still exporting the device. Once the native
            // device is removed, `usbip port` can no longer identify the import and
            // the UDE controller remains mounted.
            if (_activeDeviceCount == 1)
                UsbipCli.DetachAll();

            if (_service is null || !_service.RemoveDevice(handle.BusId, handle.DeviceId))
                return false;

            if (_activeDeviceCount > 0)
                _activeDeviceCount--;

            ViiperPnpCleanup.CleanupGhosts(handle.VendorId, handle.ProductId);
            if (_activeDeviceCount == 0)
            {
                if (!_service.RemoveBus(handle.BusId))
                    LogManager.LogWarning("VIIPER bus {0} could not be removed after its last device", handle.BusId);
                _busId = null;
            }

            return true;
        }
    }

    private static uint GetOrCreateBusIdLocked()
    {
        if (_busId.HasValue)
            return _busId.Value;

        if (_service is null || !_service.CreateBus(1))
            throw new InvalidOperationException("Failed to create VIIPER bus.");

        _busId = 1;
        return _busId.Value;
    }

    private static void Service_FeedbackReceived(uint busId, uint deviceId, byte[] data)
    {
        FeedbackReceived?.Invoke(busId, deviceId, data);
    }

    private static void RestartInternal()
    {
        Stop();
        Start();
    }
}