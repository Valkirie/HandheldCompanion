using HandheldCompanion.Controllers;
using HandheldCompanion.Helpers;
using HandheldCompanion.Managers;
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

        protected ViGEmTarget()
        {
        }

        public override string ToString()
        {
            return EnumUtils.GetDescriptionFromEnumValue(HID);
        }

        protected virtual void SendVibrate(byte LargeMotor, byte SmallMotor)
        {
            Vibrated?.Invoke(LargeMotor, SmallMotor);
        }

        public virtual void Connect()
        {
            IsConnected = true;
            Connected?.Invoke(this);
            LogManager.LogInformation("{0} connected", ToString());
        }

        public virtual void Disconnect()
        {
            IsConnected = false;
            Disconnected?.Invoke(this);
            LogManager.LogInformation("{0} disconnected", ToString());
        }

        public virtual void UpdateInputs(ControllerState inputs, GamepadMotion gamepadMotion)
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