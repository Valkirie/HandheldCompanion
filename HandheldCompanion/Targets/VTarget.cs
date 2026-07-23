using HandheldCompanion.Controllers;
using HandheldCompanion.Helpers;
using HandheldCompanion.Shared;
using HandheldCompanion.Utils;
using System;

namespace HandheldCompanion.Targets
{
    /// <summary>
    /// Abstract base class for virtual controller targets, supporting both VIIPER and ViGEM backends.
    /// </summary>
    public abstract class VTarget : IDisposable
    {
        public HIDmode HID = HIDmode.NoController;

        public event ConnectedEventHandler? Connected;
        public delegate void ConnectedEventHandler(VTarget target);

        public event DisconnectedEventHandler? Disconnected;
        public delegate void DisconnectedEventHandler(VTarget target);

        public event VibratedEventHandler? Vibrated;
        public delegate void VibratedEventHandler(byte LargeMotor, byte SmallMotor);

        public event ConnectStatusChangedEventHandler? StatusChanged;
        public delegate void ConnectStatusChangedEventHandler(VTarget target, VirtualManagerStatus status, int attempt, int maxAttempts);

        protected void RaiseConnected() => Connected?.Invoke(this);
        protected void RaiseDisconnected() => Disconnected?.Invoke(this);
        protected void RaiseStatusChanged(VirtualManagerStatus status, int attempt, int maxAttempts) => StatusChanged?.Invoke(this, status, attempt, maxAttempts);

        protected bool isDisconnecting;
        protected readonly ushort vendorId;
        protected readonly ushort productId;

        /// <summary>
        /// The fixed byte length of the HID input report for this target.
        /// Each subclass must override this to return its specific report size.
        /// </summary>
        protected abstract int InputLength { get; }

        /// <summary>Pre-allocated report buffer sized to <see cref="InputLength"/>. Allocated once in each subclass constructor.</summary>
        protected byte[] _reportBuffer = Array.Empty<byte>();

        protected virtual string DeviceType => string.Empty;
        public virtual int? MasterIntervalOverrideHz => null;

        public bool IsConnected = false;

        private bool _disposed = false;

        public VTarget(ushort vendorId, ushort productId)
        {
            this.vendorId = vendorId;
            this.productId = productId;
        }

        ~VTarget()
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

        public virtual bool Connect()
        {
            LogManager.LogError("{0} does not implement Connect()", ToString());
            return false;
        }

        public virtual bool Disconnect()
        {
            if (!IsConnected && !isDisconnecting)
                return false;

            isDisconnecting = true;
            IsConnected = false;
            RaiseDisconnected();
            LogManager.LogInformation("{0} disconnected", ToString());
            return true;
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

        protected virtual byte[] BuildReport(ControllerState inputs, GamepadMotion gamepadMotion)
        {
            throw new NotImplementedException($"{ToString()} does not implement BuildReport()");
        }

        protected virtual bool SendInput(byte[] data)
        {
            return true;
        }

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
