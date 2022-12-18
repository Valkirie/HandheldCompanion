using ControllerCommon;
using ControllerCommon.Controllers;
using ControllerCommon.Managers;
using PrecisionTiming;
using System;

namespace HandheldCompanion.Controllers
{
    public abstract class IController
    {
        public ControllerInput Inputs = new();

        public ControllerButtonFlags InjectedButtons;
        public ControllerButtonFlags prevInjectedButtons;

        protected int UserIndex;
        protected double VibrationStrength = 1.0d;
        public ControllerCapacities Capacities = ControllerCapacities.None;

        public const short UPDATE_INTERVAL = 5;

        protected PnPDetails Details;
        protected PrecisionTimer UpdateTimer;

        public event UpdatedEventHandler Updated;
        public delegate void UpdatedEventHandler(ControllerInput Inputs);

        protected IController()
        {
            UpdateTimer = new PrecisionTimer();
            UpdateTimer.SetInterval(UPDATE_INTERVAL);
            UpdateTimer.SetAutoResetMode(true);
        }

        public virtual void UpdateReport()
        {
            // update states
            Inputs.Timestamp = Environment.TickCount;
            prevInjectedButtons = InjectedButtons;

            Updated?.Invoke(Inputs);
        }

        public bool HasGyro()
        {
            return Capacities.HasFlag(ControllerCapacities.Gyroscope);
        }

        public bool HasAccelerometer()
        {
            return Capacities.HasFlag(ControllerCapacities.Accelerometer);
        }

        public bool IsVirtual()
        {
            return Details.isVirtual;
        }

        public bool IsGaming()
        {
            return Details.isGaming;
        }

        public int GetUserIndex()
        {
            return UserIndex;
        }

        public string GetInstancePath()
        {
            return Details.deviceInstanceId;
        }

        public string GetContainerInstancePath()
        {
            return Details.baseContainerDeviceInstanceId;
        }

        public override string ToString()
        {
            return Details.Name;
        }

        public void InjectButton(ControllerButtonFlags button, bool IsKeyDown, bool IsKeyUp)
        {
            if (button == ControllerButtonFlags.None)
                return;

            if (IsKeyDown)
                InjectedButtons |= button;
            else if (IsKeyUp)
                InjectedButtons &= ~button;

            LogManager.LogDebug("Injecting {0} (IsKeyDown:{1}) (IsKeyUp:{2}) to {3}", button, IsKeyDown, IsKeyUp, ToString());
        }

        public virtual void SetVibrationStrength(double value)
        {
            VibrationStrength = value / 100;
        }

        public virtual void SetVibration(byte LargeMotor, byte SmallMotor)
        { }

        public virtual bool IsConnected()
        {
            return false;
        }

        public virtual async void Rumble(int loop)
        { }

        public virtual void Plug()
        {
            InjectedButtons = ControllerButtonFlags.None;
            UpdateTimer.Start();
        }

        public virtual void Unplug()
        {
            UpdateTimer.Stop();
        }

        public virtual void Hide()
        {
            HidHide.HidePath(Details.deviceInstanceId);
            HidHide.HidePath(Details.baseContainerDeviceInstanceId);
        }

        public virtual void Unhide()
        {
            HidHide.UnhidePath(Details.deviceInstanceId);
            HidHide.UnhidePath(Details.baseContainerDeviceInstanceId);
        }
    }
}
