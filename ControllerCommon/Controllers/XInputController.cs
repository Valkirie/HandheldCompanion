using ControllerCommon.Managers;
using SharpDX.DirectInput;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCommon.Controllers
{
    public class XInputController : IController
    {
        [StructLayout(LayoutKind.Explicit)]
        protected struct XInputGamepad
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
        protected struct XInputVibration
        {
            [MarshalAs(UnmanagedType.I2)]
            public ushort LeftMotorSpeed;

            [MarshalAs(UnmanagedType.I2)]
            public ushort RightMotorSpeed;
        }

        [StructLayout(LayoutKind.Explicit)]
        protected struct XInputCapabilities
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
        protected struct XInputCapabilitiesEx
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

        [StructLayout(LayoutKind.Sequential)]
        protected struct XInputStateSecret
        {
            public uint eventCount;
            public XInputStateButtons wButtons;
            public byte bLeftTrigger;
            public byte bRightTrigger;
            public short sThumbLX;
            public short sThumbLY;
            public short sThumbRX;
            public short sThumbRY;
        }

        [Flags]
        protected enum XInputStateButtons : ushort
        {
            None = 0,
            Xbox = 1024
        }

        #region imports
        [DllImport("xinput1_4.dll", EntryPoint = "#108")]
        protected static extern int XInputGetCapabilitiesEx
        (
            int a1,            // [in] unknown, should probably be 1
            int dwUserIndex,   // [in] Index of the gamer associated with the device
            int dwFlags,       // [in] Input flags that identify the device type
            ref XInputCapabilitiesEx pCapabilities  // [out] Receives the capabilities
        );

        [DllImport("xinput1_3.dll", EntryPoint = "#100")]
        protected static extern int XInputGetStateSecret13(int playerIndex, out XInputStateSecret struc);
        [DllImport("xinput1_4.dll", EntryPoint = "#100")]
        protected static extern int XInputGetStateSecret14(int playerIndex, out XInputStateSecret struc);
        #endregion

        private Controller Controller;
        private Gamepad Gamepad;
        private Gamepad prevGamepad;
        private XInputStateSecret State;

        public XInputController(int index)
        {
            this.Controller = new Controller((UserIndex)index);
            this.UserIndex = index;

            if (!IsConnected())
                return;

            // pull data from xinput
            var CapabilitiesEx = new XInputCapabilitiesEx();

            if (XInputGetCapabilitiesEx(1, UserIndex, 0, ref CapabilitiesEx) == 0)
            {
                var ProductId = CapabilitiesEx.ProductId.ToString("X4");
                var VendorId = CapabilitiesEx.VendorId.ToString("X4");

                this.Details = SystemManager.GetDetails(CapabilitiesEx.VendorId, CapabilitiesEx.ProductId)[index];
            }

            UpdateTimer.Tick += (sender, e) => UpdateReport();
        }

        protected override void UpdateReport()
        {
            Gamepad = this.Controller.GetState().Gamepad;

            XInputGetStateSecret13(UserIndex, out State);

            if (prevGamepad.GetHashCode() == Gamepad.GetHashCode())
                return;

            Inputs.Buttons = ControllerButtonFlags.None;
            Inputs.Timestamp = DateTime.Now.Ticks;

            if (Gamepad.Buttons.HasFlag(GamepadButtonFlags.A))
                Inputs.Buttons |= ControllerButtonFlags.B1;
            if (Gamepad.Buttons.HasFlag(GamepadButtonFlags.B))
                Inputs.Buttons |= ControllerButtonFlags.B2;
            if (Gamepad.Buttons.HasFlag(GamepadButtonFlags.X))
                Inputs.Buttons |= ControllerButtonFlags.B3;
            if (Gamepad.Buttons.HasFlag(GamepadButtonFlags.Y))
                Inputs.Buttons |= ControllerButtonFlags.B4;

            if (Gamepad.Buttons.HasFlag(GamepadButtonFlags.Start))
                Inputs.Buttons |= ControllerButtonFlags.Start;
            if (Gamepad.Buttons.HasFlag(GamepadButtonFlags.Back))
                Inputs.Buttons |= ControllerButtonFlags.Back;

            if (Gamepad.LeftTrigger > 0)
                Inputs.Buttons |= ControllerButtonFlags.LeftTrigger;
            if (Gamepad.RightTrigger > 0)
                Inputs.Buttons |= ControllerButtonFlags.RightTrigger;

            if (Gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftThumb))
                Inputs.Buttons |= ControllerButtonFlags.LeftThumb;
            if (Gamepad.Buttons.HasFlag(GamepadButtonFlags.RightThumb))
                Inputs.Buttons |= ControllerButtonFlags.RightThumb;

            if (Gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftShoulder))
                Inputs.Buttons |= ControllerButtonFlags.LeftShoulder;
            if (Gamepad.Buttons.HasFlag(GamepadButtonFlags.RightShoulder))
                Inputs.Buttons |= ControllerButtonFlags.RightShoulder;

            if (Gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadUp))
                Inputs.Buttons |= ControllerButtonFlags.DPadUp;
            if (Gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadDown))
                Inputs.Buttons |= ControllerButtonFlags.DPadDown;
            if (Gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadLeft))
                Inputs.Buttons |= ControllerButtonFlags.DPadLeft;
            if (Gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadRight))
                Inputs.Buttons |= ControllerButtonFlags.DPadRight;

            // Left Stick
            if (Gamepad.LeftThumbX < -Gamepad.LeftThumbDeadZone)
                Inputs.Buttons |= ControllerButtonFlags.LStickLeft;
            else if (Gamepad.LeftThumbX > Gamepad.LeftThumbDeadZone)
                Inputs.Buttons |= ControllerButtonFlags.LStickRight;

            if (Gamepad.LeftThumbY < -Gamepad.LeftThumbDeadZone)
                Inputs.Buttons |= ControllerButtonFlags.LStickDown;
            else if (Gamepad.LeftThumbY > Gamepad.LeftThumbDeadZone)
                Inputs.Buttons |= ControllerButtonFlags.LStickUp;

            Inputs.LeftThumb.X = Gamepad.LeftThumbX;
            Inputs.LeftThumb.Y = Gamepad.LeftThumbY;

            // Right Stick
            if (Gamepad.RightThumbX < -Gamepad.RightThumbDeadZone)
                Inputs.Buttons |= ControllerButtonFlags.RStickLeft;
            else if (Gamepad.RightThumbX > Gamepad.RightThumbDeadZone)
                Inputs.Buttons |= ControllerButtonFlags.RStickRight;

            if (Gamepad.RightThumbY < -Gamepad.RightThumbDeadZone)
                Inputs.Buttons |= ControllerButtonFlags.RStickDown;
            else if (Gamepad.RightThumbY > Gamepad.RightThumbDeadZone)
                Inputs.Buttons |= ControllerButtonFlags.RStickUp;

            Inputs.RightThumb.X = Gamepad.RightThumbX;
            Inputs.RightThumb.Y = Gamepad.RightThumbY;

            if (State.wButtons.HasFlag(XInputStateButtons.Xbox))
                Inputs.Buttons |= ControllerButtonFlags.Special;

            Debug.WriteLineIf(Inputs.Buttons != ControllerButtonFlags.None, string.Join(",", Inputs.Buttons));

            prevGamepad = Gamepad;

            base.UpdateReport();
        }

        public override bool IsConnected()
        {
            return (bool)(Controller?.IsConnected);
        }

        public override void Dispose()
        {
            base.Dispose();
        }
    }
}
