using HandheldCompanion.Managers;
using HandheldCompanion.Shared;
using HandheldCompanion.Utils;
using Nefarius.ViGEm.Client;
using System;

namespace HandheldCompanion.Targets
{
    /// <summary>
    /// Abstract base class for ViGEM virtual controller targets.
    /// </summary>
    public abstract class ViGEmTarget : VTarget
    {
        private bool _disposed = false;
        protected IVirtualGamepad? _virtualController = null;

        public ViGEmTarget(ushort vendorId, ushort productId) : base(vendorId, productId)
        {
        }

        ~ViGEmTarget()
        {
            Dispose(false);
        }

        public override bool Connect()
        {
            if (IsConnected)
                return true;

            try
            {
                _virtualController = CreateVirtualController();
                if (_virtualController == null)
                {
                    LogManager.LogWarning("Failed to create ViGEM device for {0}", ToString());
                    RaiseStatusChanged(VirtualManagerStatus.Failed, 1, 1);
                    return false;
                }

                _virtualController.Connect();

                IsConnected = true;
                RaiseConnected();
                RaiseStatusChanged(VirtualManagerStatus.Connected, 1, 1);
                LogManager.LogInformation("{0} connected via ViGEM", ToString());
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

        public override bool Disconnect()
        {
            if (!IsConnected)
                return false;

            isDisconnecting = true;
            Cleanup();
            IsConnected = false;
            RaiseDisconnected();
            LogManager.LogInformation("{0} disconnected", ToString());
            return true;
        }

        protected virtual void Cleanup()
        {
            try
            {
                _virtualController?.Disconnect();
                _virtualController = null;
            }
            catch { }

            isDisconnecting = false;
        }

        protected abstract IVirtualGamepad CreateVirtualController();

        protected override bool SendInput(byte[] data)
        {
            if (!IsConnected || _virtualController == null)
                return false;

            try
            {
                UpdateVirtualController(_virtualController, data);
                return true;
            }
            catch (Exception ex)
            {
                LogManager.LogError("Failed to send input to ViGEM device: {0}", ex.Message);
                HandleDisconnect();
                return false;
            }
        }

        protected override void SendVibrate(byte LargeMotor, byte SmallMotor)
        {
            base.SendVibrate(LargeMotor, SmallMotor);
        }

        protected abstract void UpdateVirtualController(IVirtualGamepad controller, byte[] reportData);

        private void HandleDisconnect()
        {
            if (isDisconnecting || !IsConnected)
                return;

            IsConnected = false;
            RaiseDisconnected();
            LogManager.LogInformation("{0} disconnected by ViGEM", ToString());
        }

        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected override void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                isDisconnecting = true;
                Disconnect();
                Cleanup();
            }

            _disposed = true;
        }
    }
}
