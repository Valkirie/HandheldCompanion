using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using SharpDX.XInput;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Media;

namespace HandheldCompanion.Controllers;

public class XInputController : IController
{
    private Controller Controller;
    private Gamepad Gamepad;

    private XInputStateSecret State;

    public XInputController()
    { }

    public XInputController(PnPDetails details)
    {
        AttachController(details.XInputUserIndex);
        AttachDetails(details);

        // UI
        ColoredButtons.Add(ButtonFlags.B1, new SolidColorBrush(Color.FromArgb(255, 81, 191, 61)));
        ColoredButtons.Add(ButtonFlags.B2, new SolidColorBrush(Color.FromArgb(255, 217, 65, 38)));
        ColoredButtons.Add(ButtonFlags.B3, new SolidColorBrush(Color.FromArgb(255, 26, 159, 255)));
        ColoredButtons.Add(ButtonFlags.B4, new SolidColorBrush(Color.FromArgb(255, 255, 200, 44)));

        DrawControls();
        RefreshControls();

        string enumerator = Details.GetEnumerator();
        switch (enumerator)
        {
            default:
            case "BTHENUM":
                ProgressBarWarning.Text = Properties.Resources.XInputController_Warning_BTH;
                break;
            case "USB":
                ProgressBarWarning.Text = Properties.Resources.XInputController_Warning_USB;
                break;
        }
    }

    public override string ToString()
    {
        var baseName = base.ToString();
        if (!string.IsNullOrEmpty(baseName))
            return baseName;
        return $"XInput Controller {(UserIndex)UserIndex}";
    }

    public virtual void UpdateInputs(long ticks, bool commit)
    {
        // skip if controller isn't connected
        if (!IsConnected())
            return;

        try
        {
            // update gamepad state
            Gamepad = Controller.GetState().Gamepad;

            // update secret state
            XInputGetStateSecret14(UserIndex, out State);

            Inputs.ButtonState = InjectedButtons.Clone() as ButtonState;

            Inputs.ButtonState[ButtonFlags.B1] = Gamepad.Buttons.HasFlag(GamepadButtonFlags.A);
            Inputs.ButtonState[ButtonFlags.B2] = Gamepad.Buttons.HasFlag(GamepadButtonFlags.B);
            Inputs.ButtonState[ButtonFlags.B3] = Gamepad.Buttons.HasFlag(GamepadButtonFlags.X);
            Inputs.ButtonState[ButtonFlags.B4] = Gamepad.Buttons.HasFlag(GamepadButtonFlags.Y);

            Inputs.ButtonState[ButtonFlags.Start] = Gamepad.Buttons.HasFlag(GamepadButtonFlags.Start);
            Inputs.ButtonState[ButtonFlags.Back] = Gamepad.Buttons.HasFlag(GamepadButtonFlags.Back);

            Inputs.ButtonState[ButtonFlags.L2Soft] = Gamepad.LeftTrigger > Gamepad.TriggerThreshold;
            Inputs.ButtonState[ButtonFlags.R2Soft] = Gamepad.RightTrigger > Gamepad.TriggerThreshold;

            Inputs.ButtonState[ButtonFlags.L2Full] = Gamepad.LeftTrigger > Gamepad.TriggerThreshold * 8;
            Inputs.ButtonState[ButtonFlags.R2Full] = Gamepad.RightTrigger > Gamepad.TriggerThreshold * 8;

            Inputs.ButtonState[ButtonFlags.LeftStickClick] = Gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftThumb);
            Inputs.ButtonState[ButtonFlags.RightStickClick] = Gamepad.Buttons.HasFlag(GamepadButtonFlags.RightThumb);

            Inputs.ButtonState[ButtonFlags.L1] = Gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftShoulder);
            Inputs.ButtonState[ButtonFlags.R1] = Gamepad.Buttons.HasFlag(GamepadButtonFlags.RightShoulder);

            Inputs.ButtonState[ButtonFlags.DPadUp] = Gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadUp);
            Inputs.ButtonState[ButtonFlags.DPadDown] = Gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadDown);
            Inputs.ButtonState[ButtonFlags.DPadLeft] = Gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadLeft);
            Inputs.ButtonState[ButtonFlags.DPadRight] = Gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadRight);

            // Left Stick
            Inputs.ButtonState[ButtonFlags.LeftStickLeft] = Gamepad.LeftThumbX < -Gamepad.LeftThumbDeadZone;
            Inputs.ButtonState[ButtonFlags.LeftStickRight] = Gamepad.LeftThumbX > Gamepad.LeftThumbDeadZone;
            Inputs.ButtonState[ButtonFlags.LeftStickDown] = Gamepad.LeftThumbY < -Gamepad.LeftThumbDeadZone;
            Inputs.ButtonState[ButtonFlags.LeftStickUp] = Gamepad.LeftThumbY > Gamepad.LeftThumbDeadZone;

            Inputs.AxisState[AxisFlags.LeftStickX] = Gamepad.LeftThumbX;
            Inputs.AxisState[AxisFlags.LeftStickY] = Gamepad.LeftThumbY;

            // Right Stick
            Inputs.ButtonState[ButtonFlags.RightStickLeft] = Gamepad.RightThumbX < -Gamepad.RightThumbDeadZone;
            Inputs.ButtonState[ButtonFlags.RightStickRight] = Gamepad.RightThumbX > Gamepad.RightThumbDeadZone;
            Inputs.ButtonState[ButtonFlags.RightStickDown] = Gamepad.RightThumbY < -Gamepad.RightThumbDeadZone;
            Inputs.ButtonState[ButtonFlags.RightStickUp] = Gamepad.RightThumbY > Gamepad.RightThumbDeadZone;

            Inputs.AxisState[AxisFlags.RightStickX] = Gamepad.RightThumbX;
            Inputs.AxisState[AxisFlags.RightStickY] = Gamepad.RightThumbY;

            Inputs.ButtonState[ButtonFlags.Special] = State.wButtons.HasFlag(XInputStateButtons.Xbox);

            Inputs.AxisState[AxisFlags.L2] = Gamepad.LeftTrigger;
            Inputs.AxisState[AxisFlags.R2] = Gamepad.RightTrigger;
        }
        catch { }

        if (commit)
            base.UpdateInputs(ticks);
    }

    public override bool IsConnected()
    {
        if (Controller is not null)
            return Controller.IsConnected;
        return false;
    }

    public override void SetVibration(byte LargeMotor, byte SmallMotor)
    {
        if (!IsConnected())
            return;

        try
        {
            ushort LeftMotorSpeed = (ushort)((double)LargeMotor / byte.MaxValue * ushort.MaxValue * VibrationStrength);
            ushort RightMotorSpeed = (ushort)((double)SmallMotor / byte.MaxValue * ushort.MaxValue * VibrationStrength);

            Vibration vibration = new Vibration { LeftMotorSpeed = LeftMotorSpeed, RightMotorSpeed = RightMotorSpeed };
            Controller.SetVibration(vibration);
        }
        catch { }
    }

    public override void Plug()
    {
        TimerManager.Tick += (ticks) => UpdateInputs(ticks, true);
        base.Plug();
    }

    public override void Unplug()
    {
        TimerManager.Tick -= (ticks) => UpdateInputs(ticks, true);
        base.Unplug();
    }

    public override void Cleanup()
    {
        TimerManager.Tick -= UpdateInputs;
    }

    public static UserIndex TryGetUserIndex(PnPDetails details)
    {
        XInputCapabilitiesEx capabilitiesEx = new();

        for (int idx = 0; idx < 4; idx++)
        {
            if (XInputGetCapabilitiesEx(1, idx, 0, ref capabilitiesEx) == 0)
            {
                if (capabilitiesEx.ProductId != details.ProductID || capabilitiesEx.VendorId != details.VendorID)
                    continue;

                var devices = DeviceManager.GetDetails(capabilitiesEx.VendorId, capabilitiesEx.ProductId);
                if (devices.FirstOrDefault() is not null)
                    return (UserIndex)idx;
            }
        }

        return SharpDX.XInput.UserIndex.Any;
    }

    public virtual void AttachController(byte userIndex)
    {
        if (UserIndex == userIndex)
            return;

        UserIndex = userIndex;
        Controller = new((UserIndex)userIndex);
    }

    public override void Hide(bool powerCycle = true)
    {
        base.Hide(powerCycle);
    }

    public override void Unhide(bool powerCycle = true)
    {
        base.Unhide(powerCycle);
    }

    public override void CyclePort()
    {
        string enumerator = Details.GetEnumerator();
        switch (enumerator)
        {
            default:
            case "BTHENUM":
                Task.Run(async () =>
                {
                    /*
                    // Bluetooth HID Device
                    Details.InstallNullDrivers(false);
                    Details.InstallCustomDriver("hidbth.inf", false);

                    // Bluetooth XINPUT compatible input device
                    Details.InstallNullDrivers(true);
                    Details.InstallCustomDriver("xinputhid.inf", true);
                    */

                    /*
                    Details.Uninstall(false);   // Bluetooth HID Device
                    Details.Uninstall(true);    // Bluetooth XINPUT compatible input device
                    await Task.Delay(1000);
                    Devcon.Refresh();
                    */
                });
                break;
            case "USB":
                base.CyclePort();
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
            case ButtonFlags.L2Soft:
                return "\u21DC";
            case ButtonFlags.L2Full:
                return "\u2196";
            case ButtonFlags.R2Soft:
                return "\u21DD";
            case ButtonFlags.R2Full:
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
        [MarshalAs(UnmanagedType.I2)] public ushort LeftMotorSpeed;

        [MarshalAs(UnmanagedType.I2)] public ushort RightMotorSpeed;
    }

    [StructLayout(LayoutKind.Explicit)]
    protected struct XInputCapabilities
    {
        [MarshalAs(UnmanagedType.I1)]
        [FieldOffset(0)]
        private readonly byte Type;

        [MarshalAs(UnmanagedType.I1)]
        [FieldOffset(1)]
        public byte SubType;

        [MarshalAs(UnmanagedType.I2)]
        [FieldOffset(2)]
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

    [StructLayout(LayoutKind.Sequential)]
    protected struct XInputBaseBusInformation
    {
        [MarshalAs(UnmanagedType.U2)]
        UInt16 VID;
        [MarshalAs(UnmanagedType.U2)]
        UInt16 PID;
        [MarshalAs(UnmanagedType.U4)]
        UInt32 a3;
        [MarshalAs(UnmanagedType.U4)]
        UInt32 Flags; // probably
        [MarshalAs(UnmanagedType.U1)]
        byte a4;
        [MarshalAs(UnmanagedType.U1)]
        byte a5;
        [MarshalAs(UnmanagedType.U1)]
        byte a6;
        [MarshalAs(UnmanagedType.U1)]
        byte reserved;
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

    [DllImport("xinput1_4.dll", EntryPoint = "#104")]
    protected static extern int XInputGetBaseBusInformation(int dwUserIndex, ref XInputBaseBusInformation pInfo);
    #endregion
}