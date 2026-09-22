using HandheldCompanion.Shared;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace HandheldCompanion.Targets.Viiper
{
    /// <summary>
    /// Manages the libviiper lifecycle: initialization, bus/device management, and shutdown.
    /// Ported from ViiperController reference implementation.
    /// </summary>
    public sealed class ViiperService : IDisposable
    {
        private bool _initialized;
        private readonly object _lock = new object();

        // Keep a reference to each delegate to prevent GC from collecting it while the
        // native side still holds the function pointer.
        private readonly Dictionary<Tuple<uint, uint>, LibViiper.FeedbackCallback> _feedbackDelegates
            = new Dictionary<Tuple<uint, uint>, LibViiper.FeedbackCallback>();

        /// <summary>
        /// Fired when the emulated device receives an output report (rumble, LEDs, etc.)
        /// from the consuming application. Raised on a native thread — handlers should
        /// not block.
        /// </summary>
        public event Action<uint, uint, byte[]> FeedbackReceived;

        public bool IsInitialized
        {
            get { lock (_lock) return _initialized; }
        }

        /// <summary>
        /// Initializes the USBIP server on the given address.
        /// Default address is localhost:3241 — we don't expose beyond loopback.
        /// </summary>
        public bool Initialize(string listenAddr = "127.0.0.1:3241")
        {
            lock (_lock)
            {
                if (_initialized) return true;
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
                    LogManager.LogError("viiper_init threw unexpectedly: {0}", ex.Message);
                    return false;
                }
                if (result != 0)
                {
                    LogManager.LogError("viiper_init failed: {0}", LibViiper.GetLastError());
                    return false;
                }
                _initialized = true;
                LogManager.LogInformation("VIIPER USBIP server started on {0}", listenAddr);
                UsbipCli.NoteServerStarted(); // imports predating this server are zombies - sweep on first attach
                return true;
            }
        }

        public bool CreateBus(uint busId)
        {
            var result = LibViiper.viiper_bus_create(busId);
            if (result != 0)
            {
                string err = LibViiper.GetLastError() ?? string.Empty;
                // "bus number N already allocated" means a previous CreateBus
                // (this session) already established it — treat as success so
                // re-entrant Start() calls don't think the service is broken
                // and tear it down. The bus is genuinely usable; libviiper just
                // rejects the duplicate creation. See helper_2026-05-20_23.log
                // around 23:13:14 for the race that motivated this.
                if (err.IndexOf("already allocated", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    LogManager.LogInformation("VIIPER bus {0} already created (idempotent), continuing", busId);
                    return true;
                }
                LogManager.LogError("viiper_bus_create({0}) failed: {1}", busId, err);
                return false;
            }
            LogManager.LogInformation("VIIPER bus {0} created", busId);
            return true;
        }

        public bool RemoveBus(uint busId)
        {
            var result = LibViiper.viiper_bus_remove(busId);
            if (result != 0)
            {
                LogManager.LogError("viiper_bus_remove({0}) failed: {1}", busId, LibViiper.GetLastError());
                return false;
            }
            LogManager.LogInformation("VIIPER bus {0} removed", busId);
            return true;
        }

        /// <summary>
        /// Adds a device of the specified type to a bus. Optionally override VID/PID
        /// (used for Steam sub-device selection). Returns (success, assigned device id).
        /// </summary>
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
                    LogManager.LogWarning("viiper_device_add_ex not available, falling back (VID/PID override ignored)");
                    result = LibViiper.viiper_device_add(busId, typeName, out deviceId);
                    vid = 0; pid = 0;
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
            LogManager.LogInformation("VIIPER device added: {0} (bus={1}, dev={2}, vid=0x{3:X4}, pid=0x{4:X4})", typeName, busId, deviceId, vid, pid);

            // libviiper's viiper_device_add is *supposed* to auto-attach the device to the
            // local UDE bus, but on some usbip-win2 UDE installs that internal attach silently
            // no-ops: add returns success and the device is exported on 127.0.0.1:3241, yet no
            // child ever appears under the UDE host controller and no gamepad reaches Windows
            // (verified on-device 2026-07-08, LeGo2 + usbip-win2 0.9.7.8). Drive the standard
            // usbip client explicitly to land it. UsbipCli is idempotent — a device already
            // attached (e.g. the internal auto-attach DID fire) is skipped, so this never
            // produces the duplicate USBIP attachment the old auto-attach-only path warned of.
            if (!UsbipCli.AttachExportedDevices(vid, pid))
            {
                LibViiper.viiper_device_remove(busId, deviceId);
                LogManager.LogWarning("VIIPER device attach failed: {0} (bus={1}, dev={2})", typeName, busId, deviceId);
                return new ViiperAddDeviceResult(false, 0);
            }

            RegisterFeedbackCallback(busId, deviceId);
            return new ViiperAddDeviceResult(true, deviceId);
        }

        public bool RemoveDevice(uint busId, uint deviceId)
        {
            _feedbackDelegates.Remove(Tuple.Create(busId, deviceId));
            var result = LibViiper.viiper_device_remove(busId, deviceId);
            if (result != 0)
            {
                LogManager.LogError("viiper_device_remove({0}, {1}) failed: {2}", busId, deviceId, LibViiper.GetLastError());
                return false;
            }
            LogManager.LogInformation("VIIPER device removed (bus={0}, dev={1})", busId, deviceId);
            return true;
        }

        /// <summary>Sends a raw input state report to the emulated device.</summary>
        public bool SetInput(uint busId, uint deviceId, byte[] data)
        {
            var ok = LibViiper.viiper_device_set_input(busId, deviceId, data, data.Length) == 0;
            if (!ok)
            {
                LogManager.LogWarning("viiper_device_set_input failed (bus={0}, dev={1}, len={2}): {3}", busId, deviceId, data.Length, LibViiper.GetLastError());
            }
            return ok;
        }

        public string[] GetDeviceTypes()
        {
            return LibViiper.GetDeviceTypes();
        }

        /// <summary>Hot-swap a device type without tearing down the bus.</summary>
        public ViiperAddDeviceResult SwitchDeviceType(uint busId, uint oldDeviceId, string newTypeName, ushort vid = 0, ushort pid = 0)
        {
            LogManager.LogInformation("VIIPER switching device type: bus={0}, dev={1} -> {2}", busId, oldDeviceId, newTypeName);
            if (!RemoveDevice(busId, oldDeviceId))
            {
                return new ViiperAddDeviceResult(false, 0);
            }
            return AddDevice(busId, newTypeName, vid, pid);
        }

        private void RegisterFeedbackCallback(uint busId, uint deviceId)
        {
            LibViiper.FeedbackCallback cb = OnFeedback;
            _feedbackDelegates[Tuple.Create(busId, deviceId)] = cb;

            var result = LibViiper.viiper_device_set_feedback_callback(busId, deviceId, cb, IntPtr.Zero);
            if (result != 0)
            {
                LogManager.LogWarning("Failed to register feedback callback: {0}", LibViiper.GetLastError());
            }
        }

        private void OnFeedback(uint busId, uint deviceId, IntPtr data, int len, IntPtr userData)
        {
            if (len <= 0 || data == IntPtr.Zero) return;
            var bytes = new byte[len];
            Marshal.Copy(data, bytes, 0, len);
            var handler = FeedbackReceived;
            if (handler != null)
            {
                try { handler(busId, deviceId, bytes); }
                catch (Exception ex) { LogManager.LogError("VIIPER FeedbackReceived handler threw {0}", ex.Message); }
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (!_initialized) return;
                // Shutdown first so the Go side stops invoking callbacks,
                // then clear delegate references so they can be GC'd.
                try { LibViiper.viiper_shutdown(); }
                catch (Exception ex) { LogManager.LogWarning("viiper_shutdown threw {0}", ex.Message); }
                _feedbackDelegates.Clear();
                _initialized = false;
                LogManager.LogInformation("VIIPER shut down");
            }
        }
    }

    /// <summary>
    /// Result of an AddDevice / SwitchDeviceType call.
    /// </summary>
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
