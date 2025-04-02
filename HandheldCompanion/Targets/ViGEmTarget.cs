using HandheldCompanion.Controllers;
using HandheldCompanion.Helpers;
using HandheldCompanion.Managers;
using HandheldCompanion.Shared;
using HandheldCompanion.Utils;
using Nefarius.ViGEm.Client;
using System;

namespace HandheldCompanion.Targets
{
    public abstract class ViGEmTarget : IDisposable
    {
        public HIDmode HID = HIDmode.NoController;

        protected IVirtualGamepad virtualController;

        public event ConnectedEventHandler Connected;
        public delegate void ConnectedEventHandler(ViGEmTarget target);

        public event DisconnectedEventHandler Disconnected;
        public delegate void DisconnectedEventHandler(ViGEmTarget target);

        public event VibratedEventHandler Vibrated;
        public delegate void VibratedEventHandler(byte LargeMotor, byte SmallMotor);

        public bool IsConnected = false;

        ~ViGEmTarget()
        {
            Dispose();
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
            if (IsConnected)
                return true;

            try
            {
                virtualController.Connect();
            }
            catch (Exception ex)
            {
                LogManager.LogWarning("Failed to connect {0}. {1}", this.ToString(), ex.Message);
                ManagerFactory.settingsManager.SetProperty("HIDstatus", 0); // Disable controller
                return false;
            }

            IsConnected = true;
            Connected?.Invoke(this);
            LogManager.LogInformation("{0} connected", ToString());

            return true;
        }

        public virtual bool Disconnect()
        {
            if (!IsConnected)
                return false;

            try
            {
                virtualController?.Disconnect();
            }
            catch (Exception ex)
            {
                LogManager.LogWarning("Failed to disconnect {0}. {1}", ToString(), ex.Message);
                ManagerFactory.settingsManager.SetProperty("HIDstatus", 1); // Enable controller
                return false;
            }

            IsConnected = false;
            Disconnected?.Invoke(this);
            LogManager.LogInformation("{0} disconnected", ToString());

            return true;
        }

        public virtual void UpdateInputs(ControllerState inputs, GamepadMotion? gamepadMotion)
        { }

        public virtual unsafe void UpdateReport(long ticks, float delta)
        { }

        public virtual void Dispose()
        {
            Disconnect();
            GC.SuppressFinalize(this);
        }
    }
}