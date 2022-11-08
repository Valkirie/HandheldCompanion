using ControllerCommon.Managers;
using SharpDX.DirectInput;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCommon.Controllers
{
    public class XInputController : IController
    {
        [StructLayout(LayoutKind.Explicit)]
        public struct XInputGamepad
        {
            [MarshalAs(UnmanagedType.I2)]
            [FieldOffset(0)]
            public short wButtons;

            [MarshalAs(UnmanagedType.I1)]
            [FieldOffset(2)]
            public byte bLeftTrigger;

            [MarshalAs(UnmanagedType.I1)]
            [FieldOffset(3)]
            public byte bRightTrigger;

            [MarshalAs(UnmanagedType.I2)]
            [FieldOffset(4)]
            public short sThumbLX;

            [MarshalAs(UnmanagedType.I2)]
            [FieldOffset(6)]
            public short sThumbLY;

            [MarshalAs(UnmanagedType.I2)]
            [FieldOffset(8)]
            public short sThumbRX;

            [MarshalAs(UnmanagedType.I2)]
            [FieldOffset(10)]
            public short sThumbRY;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct XInputVibration
        {
            [MarshalAs(UnmanagedType.I2)]
            public ushort LeftMotorSpeed;

            [MarshalAs(UnmanagedType.I2)]
            public ushort RightMotorSpeed;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct XInputCapabilities
        {
            [MarshalAs(UnmanagedType.I1)]
            [FieldOffset(0)]
            byte Type;

            [MarshalAs(UnmanagedType.I1)]
            [FieldOffset(1)]
            public byte SubType;

            [MarshalAs(UnmanagedType.I2)]
            [FieldOffset(2)]
            public short Flags;

            [FieldOffset(4)]
            public XInputGamepad Gamepad;

            [FieldOffset(16)]
            public XInputVibration Vibration;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct XInputCapabilitiesEx
        {
            public XInputCapabilities Capabilities;
            [MarshalAs(UnmanagedType.U2)]
            public ushort VendorId;
            [MarshalAs(UnmanagedType.U2)]
            public ushort ProductId;
            [MarshalAs(UnmanagedType.U2)]
            public UInt16 REV;
            [MarshalAs(UnmanagedType.U4)]
            public UInt32 XID;
        };

        #region imports
        [DllImport("xinput1_4.dll", EntryPoint = "#108")]
        public static extern int XInputGetCapabilitiesEx
        (
            int a1,            // [in] unknown, should probably be 1
            int dwUserIndex,   // [in] Index of the gamer associated with the device
            int dwFlags,       // [in] Input flags that identify the device type
            ref XInputCapabilitiesEx pCapabilities  // [out] Receives the capabilities
        );
        #endregion

        private Controller Controller;

        public XInputController(int index)
        {
            this.Controller = new Controller((UserIndex)index);
            this.UserIndex = index;

            // pull data from xinput
            var CapabilitiesEx = new XInputCapabilitiesEx();

            if (XInputGetCapabilitiesEx(1, UserIndex, 0, ref CapabilitiesEx) == 0)
            {
                ProductId = CapabilitiesEx.ProductId.ToString("X4");
                VendorId = CapabilitiesEx.VendorId.ToString("X4");
            }

            base.Details = SystemManager.GetDetails(CapabilitiesEx.ProductId, CapabilitiesEx.VendorId)[index];
        }

        public override bool IsConnected()
        {
            return (bool)(Controller?.IsConnected);
        }
    }
}
