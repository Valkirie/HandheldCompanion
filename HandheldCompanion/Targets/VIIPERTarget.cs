using HandheldCompanion.Controllers;
using HandheldCompanion.Helpers;
using HandheldCompanion.Managers;
using HandheldCompanion.Shared;
using HandheldCompanion.Targets.Viiper;
using HandheldCompanion.Utils;
using System;

namespace HandheldCompanion.Targets
{
    public abstract class VIIPERTarget : VTarget
    {

        protected ViiperService? viiperService;
        protected uint deviceId;
        protected uint? busId;
        private bool _disposed = false;

        public override int? MasterIntervalOverrideHz => null;

        public VIIPERTarget(ushort vendorId, ushort productId) : base(vendorId, productId)
        {
        }

        protected override bool SendInput(byte[] data)
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

        protected override void SendVibrate(byte LargeMotor, byte SmallMotor)
        {
            base.SendVibrate(LargeMotor, SmallMotor);
        }

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

        public override bool Disconnect()
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