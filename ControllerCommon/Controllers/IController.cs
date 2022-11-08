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
    public enum ControllerButtonFlags : uint
    {
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

        RLStickUp = 16777216,
        RStickDown = 33554432,
        RStickLeft = 67108864,
        RStickRight = 134217728
    }

    public struct ControllerInput
    {
        public ControllerButtonFlags Buttons;
        public Vector2 LeftThumb;
        public Vector2 RightThumb;
        public DateTime Timestamp;
    }

    public abstract class IController : IDisposable
    {
        public ControllerInput Inputs;
        public PnPDetails Details;

        public string deviceInstancePath;
        public string baseContainerDeviceInstancePath;

        public string ProductId;
        public string VendorId;
        public string ProductName;
        public string Description;

        public int UserIndex;

        protected PrecisionTimer UpdateTimer;

        protected IController()
        {
            UpdateTimer = new PrecisionTimer();
            UpdateTimer.SetInterval(10);
            UpdateTimer.SetAutoResetMode(true);
        }

        protected virtual void UpdateReport()
        { }

        public bool IsVirtual()
        {
            return Details.isVirtual;
        }

        public virtual bool IsConnected()
        {
            return false;
        }

        public virtual void Identify()
        { }

        public virtual void Dispose()
        { }
    }
}
