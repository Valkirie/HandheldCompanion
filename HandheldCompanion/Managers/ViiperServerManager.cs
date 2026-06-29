using HandheldCompanion.Shared;
using HandheldCompanion.Targets.Viiper;
using System;
using System.Threading.Tasks;

namespace HandheldCompanion.Managers;

public static class ViiperServerManager
{
    private static readonly object _lock = new();

    private static uint? _busId;
    private static int _activeDeviceCount;
    private static ViiperService? _service;

    public static string Host { get; private set; } = "127.0.0.1";
    public static int Port { get; private set; } = 3241;
    public static bool IsRunning { get; private set; }
    public static int ActiveDeviceCount => _activeDeviceCount;
    internal static ViiperService? Service => _service;

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
            if (IsRunning)
                RestartInternal();
        }
    }

    public static void Start()
    {
        lock (_lock)
        {
            if (IsRunning)
                return;

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
                    IsRunning = false;
                    Failed?.Invoke("Failed to initialize libviiper");
                    return;
                }

                _activeDeviceCount = 0;
                IsRunning = true;

                Started?.Invoke();
                LogManager.LogInformation("VIIPER libviiper server started on {0}:{1}", Host, Port);
                return;
            }
            catch (Exception ex)
            {
                _service?.Dispose();
                _service = null;
                IsRunning = false;
                Failed?.Invoke($"Failed to start VIIPER: {ex.Message}");
                LogManager.LogError("Failed to start VIIPER libviiper server: {0}", ex.Message);
            }
        }
    }

    public static void Stop()
    {
        lock (_lock)
        {
            if (!IsRunning && _service is null)
                return;

            try
            {
                _service?.Dispose();
            }
            catch { }
            finally
            {
                _service = null;
                _busId = null;
                _activeDeviceCount = 0;
                IsRunning = false;
            }

            Stopped?.Invoke();
            LogManager.LogInformation("VIIPER server stopped");
        }
    }

    public static (uint BusId, bool CreatedBus) GetOrCreateBusId()
    {
        lock (_lock)
        {
            if (_busId.HasValue)
            {
                return (_busId.Value, false);
            }

            if (_service is null || !_service.CreateBus(1))
                throw new InvalidOperationException("Failed to create VIIPER bus.");

            _busId = 1;
            return (_busId.Value, true);
        }
    }

    public static void InvalidateBusId(uint busId)
    {
        lock (_lock)
        {
            if (_busId == busId)
                _busId = null;
        }
    }

    private static void RestartInternal()
    {
        Stop();
        Start();
    }
}