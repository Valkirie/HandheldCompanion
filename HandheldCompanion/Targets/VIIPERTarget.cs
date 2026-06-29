using HandheldCompanion.Controllers;
using HandheldCompanion.Helpers;
using HandheldCompanion.Managers;
using HandheldCompanion.Shared;
using HandheldCompanion.Targets.Viiper;
using HandheldCompanion.Utils;
using System;
using System.Threading.Tasks;

namespace HandheldCompanion.Targets
{
    public abstract class VIIPERTarget : IDisposable
    {
        public HIDmode HID = HIDmode.NoController;

        public event ConnectedEventHandler? Connected;
        public delegate void ConnectedEventHandler(VIIPERTarget target);

        public event DisconnectedEventHandler? Disconnected;
        public delegate void DisconnectedEventHandler(VIIPERTarget target);

        public event VibratedEventHandler? Vibrated;
        public delegate void VibratedEventHandler(byte LargeMotor, byte SmallMotor);

        public event ConnectStatusChangedEventHandler? StatusChanged;
        public delegate void ConnectStatusChangedEventHandler(VIIPERTarget target, VirtualManagerStatus status, int attempt, int maxAttempts);

        protected void RaiseConnected() => Connected?.Invoke(this);
        protected void RaiseDisconnected() => Disconnected?.Invoke(this);
        protected void RaiseStatusChanged(VirtualManagerStatus status, int attempt, int maxAttempts) => StatusChanged?.Invoke(this, status, attempt, maxAttempts);

        protected ViiperService? viiperService;
        protected bool isDisconnecting;
        protected uint deviceId;
        protected uint? busId;

        /// <summary>
        /// The fixed byte length of the HID input report for this target.
        /// Each subclass must override this to return its specific report size.
        /// Used to allocate <see cref="_reportBuffer"/> once in the subclass constructor.
        /// </summary>
        protected abstract int InputLength { get; }

        /// <summary>Pre-allocated report buffer sized to <see cref="InputLength"/>. Allocated once in each subclass constructor.</summary>
        protected byte[] _reportBuffer = Array.Empty<byte>();

        protected readonly ushort vendorId;
        protected readonly ushort productId;

        protected virtual string DeviceType => string.Empty;
        public virtual int? MasterIntervalOverrideHz => null;

        public bool IsConnected = false;

        private bool _disposed = false;

        public VIIPERTarget(ushort vendorId, ushort productId)
        {
            this.vendorId = vendorId;
            this.productId = productId;
        }

        ~VIIPERTarget()
        {
            Dispose(false);
        }

        public override string ToString()
        {
            return EnumUtils.GetDescriptionFromEnumValue(HID);
        }

        protected virtual void SendVibrate(byte LargeMotor, byte SmallMotor)
        {
            Vibrated?.Invoke(LargeMotor, SmallMotor);
        }

        protected bool SendInput(byte[] data)
        {
            if (!IsConnected || isDisconnecting || viiperService is null || !busId.HasValue || deviceId == 0)
                return false;

            bool ok = viiperService.SetInput(busId.Value, deviceId, data);
            if (!ok)
            {
                ViiperServerManager.InvalidateBusId(busId.Value);
                HandleDisconnect();
            }

            return ok;
        }

        protected bool CanUseViiperDevice => !string.IsNullOrEmpty(DeviceType);

        protected virtual void HandleOutput(byte[] buffer)
        {
            SendVibrate(buffer[0], buffer[1]);
        }

        private void HandleOutput(uint callbackBusId, uint callbackDeviceId, byte[] buffer)
        {
            if (!busId.HasValue || callbackBusId != busId.Value || callbackDeviceId != deviceId)
                return;

            HandleOutput(buffer);
        }

        private void HandleDisconnect()
        {
            if (isDisconnecting || !IsConnected)
                return;

            IsConnected = false;
            RaiseDisconnected();
            LogManager.LogInformation("{0} disconnected by VIIPER server", ToString());
        }

        private void Close()
        {
            viiperService?.FeedbackReceived -= HandleOutput;
        }

        public virtual bool Connect()
        {
            if (IsConnected)
                return true;

            try
            {
                if (!ViiperServerManager.IsRunning)
                {
                    RaiseStatusChanged(VirtualManagerStatus.Failed, 1, 1);
                    LogManager.LogWarning("Failed to connect {0}: VIIPER server is not running", ToString());
                    ManagerFactory.settingsManager.SetProperty("HIDstatus", 0);
                    return false;
                }

                viiperService = ViiperServerManager.Service;
                if (viiperService is null)
                    throw new InvalidOperationException("VIIPER service is not available.");

                var bus = ViiperServerManager.GetOrCreateBusId();
                uint currentBusId = bus.BusId;
                busId = currentBusId;

                var addedDevice = viiperService.AddDevice(currentBusId, DeviceType, vendorId, productId);
                if (!addedDevice.Success)
                {
                    ViiperServerManager.InvalidateBusId(currentBusId);
                    bus = ViiperServerManager.GetOrCreateBusId();
                    currentBusId = bus.BusId;
                    busId = currentBusId;
                    addedDevice = viiperService.AddDevice(currentBusId, DeviceType, vendorId, productId);
                }

                if (!addedDevice.Success)
                    throw new InvalidOperationException("VIIPER device creation failed.");

                deviceId = addedDevice.DeviceId;
                viiperService.FeedbackReceived += HandleOutput;

                IsConnected = true;
                RaiseConnected();
                RaiseStatusChanged(VirtualManagerStatus.Connected, 1, 1);
                LogManager.LogInformation("{0} connected via VIIPER", ToString());
                return true;
            }
            catch (Exception ex)
            {
                LogManager.LogWarning("Failed to connect {0}: {1}", ToString(), ex.Message);
                RaiseStatusChanged(VirtualManagerStatus.Failed, 1, 1);
                Cleanup();
                ManagerFactory.settingsManager.SetProperty("HIDstatus", 0);
                return false;
            }
        }

        public virtual bool Disconnect()
        {
            if (!IsConnected && deviceId == 0)
                return false;

            isDisconnecting = true;
            Cleanup();
            IsConnected = false;
            RaiseDisconnected();
            LogManager.LogInformation("{0} disconnected", ToString());
            return true;
        }

        private bool Cleanup()
        {
            bool success = false;
            Close();

            try
            {
                if (viiperService is not null && busId.HasValue && deviceId != 0)
                    success = viiperService.RemoveDevice(busId.Value, deviceId);

                deviceId = 0;
                busId = null;
                viiperService = null;
                isDisconnecting = false;
            }
            catch { }

            return success;
        }

        public virtual void UpdateInputs(ControllerState inputs, GamepadMotion gamepadMotion)
        {
            if (!IsConnected)
                return;

            try
            {
                SendInput(BuildReport(inputs, gamepadMotion));
            }
            catch (Exception ex)
            {
                LogManager.LogError(ex.Message);
            }
        }

        protected abstract byte[] BuildReport(ControllerState inputs, GamepadMotion gamepadMotion);

        public virtual unsafe void UpdateReport(long ticks, float delta)
        { }

        public virtual void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                isDisconnecting = true;
                Disconnect();
                Connected = null;
                Disconnected = null;
                Vibrated = null;
                StatusChanged = null;
            }

            _disposed = true;
        }
    }
}