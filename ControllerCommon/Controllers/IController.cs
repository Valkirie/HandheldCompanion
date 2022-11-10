using Nefarius.Utilities.DeviceManagement.PnP;
using PrecisionTiming;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

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
        Special2 = 536870912,
        Special3 = 1073741824,
        Special4 = 2147483648,
        Special5 = 4294967296
    }

    public struct ControllerInput
    {
        public ControllerButtonFlags Buttons;
        public Vector2 LeftThumb;
        public Vector2 RightThumb;
        public float LeftTrigger;
        public float RightTrigger;
        public long Timestamp;
    }

    public abstract class IController : IDisposable
    {
        public ControllerInput Inputs;
        private ControllerButtonFlags InjectedButtons;

        public int UserIndex;

        protected PnPDetails Details;
        protected PrecisionTimer UpdateTimer;

        public event UpdatedEventHandler Updated;
        public delegate void UpdatedEventHandler(ControllerInput Inputs);

        protected IController()
        {
            UpdateTimer = new PrecisionTimer();
            UpdateTimer.SetInterval(5);
            UpdateTimer.SetAutoResetMode(true);
        }

        protected virtual void UpdateReport()
        {
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

        public virtual bool IsConnected()
        {
            return false;
        }

        public void InjectButton(ControllerButtonFlags button, bool IsKeyDown, bool IsKeyUp)
        {
            if (IsKeyDown)
                InjectedButtons |= button;
            else if (IsKeyUp)
                InjectedButtons &= ~button;
        }

        public virtual void Rumble()
        { }

        public virtual void Hook()
        {
            UpdateTimer.Start();
        }

        public virtual void Dispose()
        {
            UpdateTimer.Stop();
            UpdateTimer.Dispose();
        }
    }
}
