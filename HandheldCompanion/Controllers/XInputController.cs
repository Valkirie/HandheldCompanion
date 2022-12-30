using ControllerCommon;
using ControllerCommon.Controllers;
using ControllerCommon.Managers;
using SharpDX.XInput;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace HandheldCompanion.Controllers
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
        private XInputStateSecret prevState;

        public XInputController(int index)
        {
            Controller = new Controller((UserIndex)index);
            UserIndex = index;

            if (!IsConnected())
                throw new Exception();

            // pull data from xinput
            var CapabilitiesEx = new XInputCapabilitiesEx();

            if (XInputGetCapabilitiesEx(1, UserIndex, 0, ref CapabilitiesEx) == 0)
            {
                var ProductId = CapabilitiesEx.ProductId.ToString("X4");
                var VendorId = CapabilitiesEx.VendorId.ToString("X4");

                Details = SystemManager.GetDetails(CapabilitiesEx.VendorId, CapabilitiesEx.ProductId).FirstOrDefault();
                if (Details is null)
                    throw new Exception();

                Details.isHooked = true;
            }

            UpdateTimer.Tick += (sender, e) => UpdateReport();

            // ui
            DrawControls();
            RefreshControls();
        }

        public override string ToString()
        {
            if (!string.IsNullOrEmpty(Details.Name))
                return Details.Name;
            return $"XInput Controller {UserIndex}";
        }

        public override void UpdateReport()
        {
            // skip if controller isn't connected
            if (!IsConnected())
                return;

            // update gamepad state
            Gamepad = Controller.GetState().Gamepad;

            // update secret state
            XInputGetStateSecret13(UserIndex, out State);

            if (prevGamepad.GetHashCode() == Gamepad.GetHashCode() && State.wButtons == prevState.wButtons && prevInjectedButtons == InjectedButtons)
                return;

            Inputs.Buttons = InjectedButtons;

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

            if (Gamepad.LeftTrigger > Gamepad.TriggerThreshold)
                Inputs.Buttons |= ControllerButtonFlags.LeftTrigger;
            if (Gamepad.RightTrigger > Gamepad.TriggerThreshold)
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

            Inputs.LeftThumbX = Gamepad.LeftThumbX;
            Inputs.LeftThumbY = Gamepad.LeftThumbY;

            // Right Stick
            if (Gamepad.RightThumbX < -Gamepad.RightThumbDeadZone)
                Inputs.Buttons |= ControllerButtonFlags.RStickLeft;
            else if (Gamepad.RightThumbX > Gamepad.RightThumbDeadZone)
                Inputs.Buttons |= ControllerButtonFlags.RStickRight;

            if (Gamepad.RightThumbY < -Gamepad.RightThumbDeadZone)
                Inputs.Buttons |= ControllerButtonFlags.RStickDown;
            else if (Gamepad.RightThumbY > Gamepad.RightThumbDeadZone)
                Inputs.Buttons |= ControllerButtonFlags.RStickUp;

            Inputs.RightThumbX = Gamepad.RightThumbX;
            Inputs.RightThumbY = Gamepad.RightThumbY;

            if (State.wButtons.HasFlag(XInputStateButtons.Xbox))
                Inputs.Buttons |= ControllerButtonFlags.Special;

            Inputs.LeftTrigger = Gamepad.LeftTrigger;
            Inputs.RightTrigger = Gamepad.RightTrigger;

            // update states
            prevGamepad = Gamepad;
            prevState = State;

            base.UpdateReport();
        }

        public override bool IsConnected()
        {
            return (bool)(Controller?.IsConnected);
        }

        public override void SetVibrationStrength(double value)
        {
            base.SetVibrationStrength(value);
            this.Rumble(1);
        }

        public override void SetVibration(byte LargeMotor, byte SmallMotor)
        {
            if (!IsConnected())
                return;

            ushort LeftMotorSpeed = (ushort)((double)LargeMotor / byte.MaxValue * ushort.MaxValue * VibrationStrength);
            ushort RightMotorSpeed = (ushort)((double)SmallMotor / byte.MaxValue * ushort.MaxValue * VibrationStrength);

            Vibration vibration = new Vibration() { LeftMotorSpeed = LeftMotorSpeed, RightMotorSpeed = RightMotorSpeed };
            Controller.SetVibration(vibration);
        }

        public override async void Rumble(int loop)
        {
            for (int i = 0; i < loop; i++)
            {
                SetVibration(byte.MaxValue, byte.MaxValue);
                await Task.Delay(100);

                SetVibration(0, 0);
                await Task.Delay(100);
            }
            base.Rumble(loop);
        }

        public override void Plug()
        {
            PipeClient.ServerMessage += OnServerMessage;
            base.Plug();
        }

        public override void Unplug()
        {
            PipeClient.ServerMessage -= OnServerMessage;
            base.Unplug();
        }

        private void OnServerMessage(PipeMessage message)
        {
            switch (message.code)
            {
                case PipeCode.SERVER_VIBRATION:
                    {
                        PipeClientVibration e = (PipeClientVibration)message;
                        SetVibration(e.LargeMotor, e.SmallMotor);
                    }
                    break;
            }
        }
    }
}
