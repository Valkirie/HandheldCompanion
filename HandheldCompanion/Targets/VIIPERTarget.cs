using HandheldCompanion.Controllers;
using HandheldCompanion.Managers;
using HandheldCompanion.Shared;
using HandheldCompanion.Utils;
using System;

namespace HandheldCompanion.Targets
{
    public abstract class VIIPERTarget : VTarget
    {
        private HandheldCompanion.Managers.ViiperDeviceHandle? viiperDevice;
        private bool _disposed = false;

        public override int? MasterIntervalOverrideHz => null;

        public VIIPERTarget(ushort vendorId, ushort productId) : base(vendorId, productId)
        {
        }

        protected override bool SendInput(byte[] data)
        {
            if (!IsConnected || isDisconnecting || !viiperDevice.HasValue)
                return false;

            bool ok = ViiperServerManager.SetInput(viiperDevice.Value, data);
            return ok;
        }

        protected bool CanUseViiperDevice => !string.IsNullOrEmpty(DeviceType);

        protected override void SendVibrate(byte LargeMotor, byte SmallMotor)
        {
            base.SendVibrate(LargeMotor, SmallMotor);
        }

        protected virtual void HandleOutput(byte[] buffer)
        {
            if (buffer.Length < 2)
                return;

            SendVibrate(buffer[0], buffer[1]);
        }

        private void HandleOutput(uint callbackBusId, uint callbackDeviceId, byte[] buffer)
        {
            if (!viiperDevice.HasValue || callbackBusId != viiperDevice.Value.BusId || callbackDeviceId != viiperDevice.Value.DeviceId)
                return;

            HandleOutput(buffer);
        }

        private void Close()
        {
            ViiperServerManager.FeedbackReceived -= HandleOutput;
        }

        public override bool Connect()
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

                if (!CanUseViiperDevice)
                    throw new InvalidOperationException("VIIPER device type is not configured.");

                if (!ViiperServerManager.TryCreateDevice(DeviceType, vendorId, productId, out var handle))
                    throw new InvalidOperationException("VIIPER device creation failed.");

                viiperDevice = handle;
                ViiperServerManager.FeedbackReceived += HandleOutput;

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

        public override bool Disconnect()
        {
            if (!IsConnected && !viiperDevice.HasValue)
                return false;

            bool wasConnected = IsConnected;
            isDisconnecting = true;
            Cleanup();
            IsConnected = false;
            if (wasConnected)
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
                if (viiperDevice.HasValue)
                    success = ViiperServerManager.RemoveDevice(viiperDevice.Value);

                viiperDevice = null;
                isDisconnecting = false;
            }
            catch { }

            return success;
        }

        internal bool TrySwitchDeviceType(VIIPERTarget replacement)
        {
            if (!viiperDevice.HasValue || !IsConnected)
                return false;

            if (!ViiperServerManager.TrySwitchDeviceType(viiperDevice.Value, replacement.DeviceType, replacement.vendorId, replacement.productId, out var handle))
                return false;

            Close();
            viiperDevice = null;
            IsConnected = false;
            replacement.viiperDevice = handle;
            replacement.IsConnected = true;
            ViiperServerManager.FeedbackReceived += replacement.HandleOutput;
            return true;
        }

        public override void UpdateInputs(ControllerState inputs, GamepadMotion gamepadMotion)
        {
            base.UpdateInputs(inputs, gamepadMotion);
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
            }

            _disposed = true;
        }
    }
}