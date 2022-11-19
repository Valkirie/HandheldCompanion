using ControllerCommon.Managers;
using PrecisionTiming;
using System;

namespace ControllerCommon.Controllers
{
    [Flags]
    public enum ControllerButtonFlags : ulong
    {
        None = 0,

        DPadUp = 1,
        DPadDown = 2,
        DPadLeft = 4,
        DPadRight = 8,

        Start = 16,
        Back = 32,

        LeftThumb = 64,
        RightThumb = 128,

        LeftShoulder = 256,
        RightShoulder = 512,

        LeftTrigger = 1024,
        RightTrigger = 2048,

        B1 = 4096,
        B2 = 8192,
        B3 = 16384,
        B4 = 32768,
        B5 = 65536,
        B6 = 131072,
        B7 = 262144,
        B8 = 524288,

        LStickUp = 1048576,
        LStickDown = 2097152,
        LStickLeft = 4194304,
        LStickRight = 8388608,

        RStickUp = 16777216,
        RStickDown = 33554432,
        RStickLeft = 67108864,
        RStickRight = 134217728,

        Special = 268435456,
        OEM1 = 536870912,
        OEM2 = 1073741824,
        OEM3 = 2147483648,
        OEM4 = 4294967296,
        OEM5 = 8589934592
    }

    [Serializable]
    public class ControllerInput
    {
        public ControllerButtonFlags Buttons;
        public float LeftThumbX, LeftThumbY;
        public float RightThumbX, RightThumbY;
        public float LeftTrigger;
        public float RightTrigger;
        public int Timestamp;
    }

    public abstract class IController
    {
        public ControllerInput Inputs = new();

        public ControllerButtonFlags InjectedButtons;
        public ControllerButtonFlags prevInjectedButtons;

        protected int UserIndex;
        protected double VibrationStrength = 100.0d;

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

        public bool IsVirtual()
        {
            return Details.isVirtual;
        }

        public bool IsGaming()
        {
            return Details.isGaming;
        }

        public string GetInstancePath()
        {
            return Details.deviceInstancePath;
        }

        public string GetContainerInstancePath()
        {
            return Details.baseContainerDeviceInstancePath;
        }

        public override string ToString()
        {
            return Details.DeviceDesc;
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

        public void SetVibrationStrength(double value)
        {
            VibrationStrength = value;
        }

        public virtual bool IsConnected()
        {
            return false;
        }

        public virtual async void Rumble()
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
            HidHide.HidePath(Details.deviceInstancePath);
            HidHide.HidePath(Details.baseContainerDeviceInstancePath);
        }

        public virtual void Unhide()
        {
            HidHide.UnhidePath(Details.deviceInstancePath);
            HidHide.UnhidePath(Details.baseContainerDeviceInstancePath);
        }
    }
}
