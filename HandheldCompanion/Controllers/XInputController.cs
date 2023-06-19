using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Media;
using ControllerCommon.Controllers;
using ControllerCommon.Inputs;
using ControllerCommon.Managers;
using ControllerCommon.Pipes;
using SharpDX.XInput;

namespace HandheldCompanion.Controllers;

public class XInputController : IController
{
    private readonly Controller Controller;
    private Gamepad Gamepad;

    private GamepadButtonFlags prevButtons;
    private XInputStateSecret prevState;

    private XInputStateSecret State;

    public XInputController()
    {
        // UI
        ColoredButtons.Add(ButtonFlags.B1, new SolidColorBrush(Color.FromArgb(255, 81, 191, 61)));
        ColoredButtons.Add(ButtonFlags.B2, new SolidColorBrush(Color.FromArgb(255, 217, 65, 38)));
        ColoredButtons.Add(ButtonFlags.B3, new SolidColorBrush(Color.FromArgb(255, 26, 159, 255)));
        ColoredButtons.Add(ButtonFlags.B4, new SolidColorBrush(Color.FromArgb(255, 255, 200, 44)));
    }

    public XInputController(Controller controller) : this()
    {
        Controller = controller;
        UserIndex = (int)controller.UserIndex;

        if (!IsConnected())
            return;

        // pull data from xinput
        var CapabilitiesEx = new XInputCapabilitiesEx();

        if (XInputGetCapabilitiesEx(1, UserIndex, 0, ref CapabilitiesEx) == 0)
        {
            var ProductId = CapabilitiesEx.ProductId.ToString("X4");
            var VendorId = CapabilitiesEx.VendorId.ToString("X4");

            var devices = DeviceManager.GetDetails(CapabilitiesEx.VendorId, CapabilitiesEx.ProductId);
            Details = devices.FirstOrDefault();
        }

        if (Details is null)
            return;

        Details.isHooked = true;

        DrawControls();
        RefreshControls();
    }

    public override string ToString()
    {
        var baseName = base.ToString();
        if (!string.IsNullOrEmpty(baseName))
            return baseName;
        return $"XInput Controller {UserIndex}";
    }

    public override void UpdateInputs(long ticks)
    {
        // skip if controller isn't connected
        if (!IsConnected())
            return;

        // update gamepad state
        Gamepad = Controller.GetState().Gamepad;

        // update secret state
        XInputGetStateSecret13(UserIndex, out State);

        /*
        if (prevButtons.Equals(Gamepad.Buttons) && State.wButtons.Equals(prevState.wButtons) && prevInjectedButtons.Equals(InjectedButtons))
            return;
        */

        Inputs.ButtonState = InjectedButtons.Clone() as ButtonState;

        Inputs.ButtonState[ButtonFlags.B1] = Gamepad.Buttons.HasFlag(GamepadButtonFlags.A);
        Inputs.ButtonState[ButtonFlags.B2] = Gamepad.Buttons.HasFlag(GamepadButtonFlags.B);
        Inputs.ButtonState[ButtonFlags.B3] = Gamepad.Buttons.HasFlag(GamepadButtonFlags.X);
        Inputs.ButtonState[ButtonFlags.B4] = Gamepad.Buttons.HasFlag(GamepadButtonFlags.Y);

        Inputs.ButtonState[ButtonFlags.Start] = Gamepad.Buttons.HasFlag(GamepadButtonFlags.Start);
        Inputs.ButtonState[ButtonFlags.Back] = Gamepad.Buttons.HasFlag(GamepadButtonFlags.Back);

        Inputs.ButtonState[ButtonFlags.L2] = Gamepad.LeftTrigger > Gamepad.TriggerThreshold;
        Inputs.ButtonState[ButtonFlags.R2] = Gamepad.RightTrigger > Gamepad.TriggerThreshold;

        Inputs.ButtonState[ButtonFlags.L3] = Gamepad.LeftTrigger > Gamepad.TriggerThreshold * 8;
        Inputs.ButtonState[ButtonFlags.R3] = Gamepad.RightTrigger > Gamepad.TriggerThreshold * 8;

        Inputs.ButtonState[ButtonFlags.LeftThumb] = Gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftThumb);
        Inputs.ButtonState[ButtonFlags.RightThumb] = Gamepad.Buttons.HasFlag(GamepadButtonFlags.RightThumb);

        Inputs.ButtonState[ButtonFlags.L1] = Gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftShoulder);
        Inputs.ButtonState[ButtonFlags.R1] = Gamepad.Buttons.HasFlag(GamepadButtonFlags.RightShoulder);

        Inputs.ButtonState[ButtonFlags.DPadUp] = Gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadUp);
        Inputs.ButtonState[ButtonFlags.DPadDown] = Gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadDown);
        Inputs.ButtonState[ButtonFlags.DPadLeft] = Gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadLeft);
        Inputs.ButtonState[ButtonFlags.DPadRight] = Gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadRight);

        // Left Stick
        Inputs.ButtonState[ButtonFlags.LeftThumbLeft] = Gamepad.LeftThumbX < -Gamepad.LeftThumbDeadZone;
        Inputs.ButtonState[ButtonFlags.LeftThumbRight] = Gamepad.LeftThumbX > Gamepad.LeftThumbDeadZone;
        Inputs.ButtonState[ButtonFlags.LeftThumbDown] = Gamepad.LeftThumbY < -Gamepad.LeftThumbDeadZone;
        Inputs.ButtonState[ButtonFlags.LeftThumbUp] = Gamepad.LeftThumbY > Gamepad.LeftThumbDeadZone;

        Inputs.AxisState[AxisFlags.LeftThumbX] = Gamepad.LeftThumbX;
        Inputs.AxisState[AxisFlags.LeftThumbY] = Gamepad.LeftThumbY;

        // Right Stick
        Inputs.ButtonState[ButtonFlags.RightThumbLeft] = Gamepad.RightThumbX < -Gamepad.RightThumbDeadZone;
        Inputs.ButtonState[ButtonFlags.RightThumbRight] = Gamepad.RightThumbX > Gamepad.RightThumbDeadZone;
        Inputs.ButtonState[ButtonFlags.RightThumbDown] = Gamepad.RightThumbY < -Gamepad.RightThumbDeadZone;
        Inputs.ButtonState[ButtonFlags.RightThumbUp] = Gamepad.RightThumbY > Gamepad.RightThumbDeadZone;

        Inputs.AxisState[AxisFlags.RightThumbX] = Gamepad.RightThumbX;
        Inputs.AxisState[AxisFlags.RightThumbY] = Gamepad.RightThumbY;

        Inputs.ButtonState[ButtonFlags.Special] = State.wButtons.HasFlag(XInputStateButtons.Xbox);

        Inputs.AxisState[AxisFlags.L2] = Gamepad.LeftTrigger;
        Inputs.AxisState[AxisFlags.R2] = Gamepad.RightTrigger;

        // update states
        prevButtons = Gamepad.Buttons;
        prevState = State;

        base.UpdateInputs(ticks);
    }

    public override bool IsConnected()
    {
        if (Controller is not null)
            return Controller.IsConnected;
        return false;
    }

    public override void SetVibrationStrength(double value, bool rumble)
    {
        base.SetVibrationStrength(value, rumble);
        if (rumble)
            Rumble();
    }

    public override void SetVibration(byte LargeMotor, byte SmallMotor)
    {
        if (!IsConnected())
            return;

        var LeftMotorSpeed = (ushort)((double)LargeMotor / byte.MaxValue * ushort.MaxValue * VibrationStrength);
        var RightMotorSpeed = (ushort)((double)SmallMotor / byte.MaxValue * ushort.MaxValue * VibrationStrength);

        var vibration = new Vibration { LeftMotorSpeed = LeftMotorSpeed, RightMotorSpeed = RightMotorSpeed };
        Controller.SetVibration(vibration);
    }

    public override void Rumble(int Loop = 1, byte LeftValue = byte.MaxValue, byte RightValue = byte.MaxValue,
        byte Duration = 125)
    {
        Task.Factory.StartNew(async () =>
        {
            for (var i = 0; i < Loop * 2; i++)
            {
                if (i % 2 == 0)
                    SetVibration(LeftValue, RightValue);
                else
                    SetVibration(0, 0);

                await Task.Delay(Duration);
            }
        });
    }

    public override void Plug()
    {
        TimerManager.Tick += UpdateInputs;
        PipeClient.ServerMessage += OnServerMessage;
        base.Plug();
    }

    public override void Unplug()
    {
        TimerManager.Tick -= UpdateInputs;
        PipeClient.ServerMessage -= OnServerMessage;
        base.Unplug();
    }

    private void OnServerMessage(PipeMessage message)
    {
        switch (message.code)
        {
            case PipeCode.SERVER_VIBRATION:
            {
                var e = (PipeClientVibration)message;
                SetVibration(e.LargeMotor, e.SmallMotor);
            }
                break;
        }
    }

    public override string GetGlyph(ButtonFlags button)
    {
        switch (button)
        {
            case ButtonFlags.B1:
                return "\u21D3"; // Button A
            case ButtonFlags.B2:
                return "\u21D2"; // Button B
            case ButtonFlags.B3:
                return "\u21D0"; // Button X
            case ButtonFlags.B4:
                return "\u21D1"; // Button Y
            case ButtonFlags.L1:
                return "\u2198";
            case ButtonFlags.R1:
                return "\u2199";
            case ButtonFlags.Back:
                return "\u21FA";
            case ButtonFlags.Start:
                return "\u21FB";
            case ButtonFlags.L2:
                return "\u21DC";
            case ButtonFlags.L3:
                return "\u2196";
            case ButtonFlags.R2:
                return "\u21DD";
            case ButtonFlags.R3:
                return "\u2197";
            case ButtonFlags.Special:
                return "\uE001";
        }

        return base.GetGlyph(button);
    }

    public override string GetGlyph(AxisFlags axis)
    {
        switch (axis)
        {
            case AxisFlags.L2:
                return "\u2196";
            case AxisFlags.R2:
                return "\u2197";
        }

        return base.GetGlyph(axis);
    }

    public override string GetGlyph(AxisLayoutFlags axis)
    {
        switch (axis)
        {
            case AxisLayoutFlags.L2:
                return "\u2196";
            case AxisLayoutFlags.R2:
                return "\u2197";
        }

        return base.GetGlyph(axis);
    }

    #region struct

    [StructLayout(LayoutKind.Explicit)]
    protected struct XInputGamepad
    {
        [MarshalAs(UnmanagedType.I2)] [FieldOffset(0)]
        public short wButtons;

        [MarshalAs(UnmanagedType.I1)] [FieldOffset(2)]
        public byte bLeftTrigger;

        [MarshalAs(UnmanagedType.I1)] [FieldOffset(3)]
        public byte bRightTrigger;

        [MarshalAs(UnmanagedType.I2)] [FieldOffset(4)]
        public short sThumbLX;

        [MarshalAs(UnmanagedType.I2)] [FieldOffset(6)]
        public short sThumbLY;

        [MarshalAs(UnmanagedType.I2)] [FieldOffset(8)]
        public short sThumbRX;

        [MarshalAs(UnmanagedType.I2)] [FieldOffset(10)]
        public short sThumbRY;
    }

    [StructLayout(LayoutKind.Sequential)]
    protected struct XInputVibration
    {
        [MarshalAs(UnmanagedType.I2)] public ushort LeftMotorSpeed;

        [MarshalAs(UnmanagedType.I2)] public ushort RightMotorSpeed;
    }

    [StructLayout(LayoutKind.Explicit)]
    protected struct XInputCapabilities
    {
        [MarshalAs(UnmanagedType.I1)] [FieldOffset(0)]
        private readonly byte Type;

        [MarshalAs(UnmanagedType.I1)] [FieldOffset(1)]
        public byte SubType;

        [MarshalAs(UnmanagedType.I2)] [FieldOffset(2)]
        public short Flags;

        [FieldOffset(4)] public XInputGamepad Gamepad;

        [FieldOffset(16)] public XInputVibration Vibration;
    }

    [StructLayout(LayoutKind.Sequential)]
    protected struct XInputCapabilitiesEx
    {
        public XInputCapabilities Capabilities;
        [MarshalAs(UnmanagedType.U2)] public ushort VendorId;
        [MarshalAs(UnmanagedType.U2)] public ushort ProductId;
        [MarshalAs(UnmanagedType.U2)] public ushort REV;
        [MarshalAs(UnmanagedType.U4)] public uint XID;
    }

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

    #endregion

    #region imports

    [DllImport("xinput1_4.dll", EntryPoint = "#108")]
    protected static extern int XInputGetCapabilitiesEx
    (
        int a1, // [in] unknown, should probably be 1
        int dwUserIndex, // [in] Index of the gamer associated with the device
        int dwFlags, // [in] Input flags that identify the device type
        ref XInputCapabilitiesEx pCapabilities // [out] Receives the capabilities
    );

    [DllImport("xinput1_3.dll", EntryPoint = "#100")]
    protected static extern int XInputGetStateSecret13(int playerIndex, out XInputStateSecret struc);

    [DllImport("xinput1_4.dll", EntryPoint = "#100")]
    protected static extern int XInputGetStateSecret14(int playerIndex, out XInputStateSecret struc);

    #endregion
}