using HandheldCompanion.Devices;
using HandheldCompanion.Inputs;
using HandheldCompanion.Utils;
using SharpDX.DirectInput;
using System.Threading;

namespace HandheldCompanion.Controllers.MSI;

public class DClawController : DInputController
{
    public override bool IsReady
    {
        get
        {
            if (IDevice.GetCurrent() is ClawA1M clawA1M)
                return clawA1M.IsOpen;

            return false;
        }
    }

    public byte FeedbackLargeMotor;
    public byte FeedbackSmallMotor;

    private Thread rumbleThread;
    private bool rumbleThreadRunning;
    private int rumbleThreadInterval = 100; // (ms)

    public DClawController() : base()
    { }

    public DClawController(PnPDetails details) : base(details)
    {
        // Capabilities
        Capabilities |= ControllerCapabilities.Rumble;
    }

    protected override void InitializeInputOutput()
    {
        // Additional controller specific source buttons
        SourceButtons.Add(ButtonFlags.OEM3);
        SourceButtons.Add(ButtonFlags.OEM4);
    }

    public override void Plug()
    {
        // exclusive to ClawA1M
        if (IDevice.GetCurrent().GetType() == typeof(ClawA1M))
        {
            // manage rumble thread
            rumbleThreadRunning = true;
            rumbleThread = new Thread(RumbleThreadLoop)
            {
                IsBackground = true,
                Priority = ThreadPriority.Normal
            };
            rumbleThread.Start();
        }

        base.Plug();
    }

    public override void Unplug()
    {
        // exclusive to ClawA1M
        if (IDevice.GetCurrent().GetType() == typeof(ClawA1M))
        {
            // kill rumble thread
            if (rumbleThread is not null)
            {
                rumbleThreadRunning = false;
                // Ensure the thread has finished execution
                if (rumbleThread.IsAlive)
                    rumbleThread.Join(3000);
                rumbleThread = null;
            }
        }

        base.Unplug();
    }

    private byte prevLarge = 0;
    private byte prevSmall = 0;
    private async void RumbleThreadLoop(object? obj)
    {
        while (rumbleThreadRunning)
        {
            // values snapshot
            byte LargeMotor = FeedbackLargeMotor;
            byte SmallMotor = FeedbackSmallMotor;

            byte largeVal = (byte)(LargeMotor != 0 ? 193 : 0);
            byte smallVal = (byte)(SmallMotor != 0 ? 193 : 0);

            if (prevLarge != LargeMotor || prevSmall != SmallMotor)
            {
                WriteVibration(largeVal, smallVal);

                // update prev values
                prevLarge = LargeMotor;
                prevSmall = SmallMotor;
            }

            Thread.Sleep(rumbleThreadInterval);
        }
    }

    public override void Tick(long ticks, float delta, bool commit)
    {
        // skip if controller isn't connected
        if (!IsConnected() || IsBusy || !IsPlugged || IsDisposing || IsDisposed)
            return;

        ButtonState.Overwrite(InjectedButtons, Inputs.ButtonState);

        try
        {
            // get state
            JoystickState state = joystick.GetCurrentState();

            // dirty, state is corrupted, first state ?
            if (state.RotationX == 32767 && state.RotationY == 32767 && state.RotationZ == 32767)
                return;

            Inputs.ButtonState[ButtonFlags.B1] |= state.Buttons[1]; // A
            Inputs.ButtonState[ButtonFlags.B2] |= state.Buttons[2]; // B
            Inputs.ButtonState[ButtonFlags.B3] |= state.Buttons[0]; // X
            Inputs.ButtonState[ButtonFlags.B4] |= state.Buttons[3]; // Y

            int pov = state.PointOfViewControllers[0];
            Inputs.ButtonState[ButtonFlags.DPadUp] |= (pov == 0 || pov == 4500 || pov == 31500);
            Inputs.ButtonState[ButtonFlags.DPadRight] |= (pov == 9000 || pov == 4500 || pov == 13500);
            Inputs.ButtonState[ButtonFlags.DPadDown] |= (pov == 18000 || pov == 13500 || pov == 22500);
            Inputs.ButtonState[ButtonFlags.DPadLeft] |= (pov == 27000 || pov == 31500 || pov == 22500);

            Inputs.ButtonState[ButtonFlags.L1] |= state.Buttons[4];
            Inputs.ButtonState[ButtonFlags.R1] |= state.Buttons[5];
            Inputs.ButtonState[ButtonFlags.L2Full] |= state.Buttons[6];
            Inputs.ButtonState[ButtonFlags.R2Full] |= state.Buttons[7];

            Inputs.ButtonState[ButtonFlags.Back] |= state.Buttons[8];
            Inputs.ButtonState[ButtonFlags.Start] |= state.Buttons[9];

            Inputs.ButtonState[ButtonFlags.LeftStickClick] |= state.Buttons[10];
            Inputs.ButtonState[ButtonFlags.RightStickClick] |= state.Buttons[11];

            Inputs.ButtonState[ButtonFlags.OEM3] = state.Buttons[15]; // M1
            Inputs.ButtonState[ButtonFlags.OEM4] = state.Buttons[16]; // M2

            Inputs.AxisState[AxisFlags.LeftStickX] = (short)InputUtils.MapRange(state.X, ushort.MinValue, ushort.MaxValue, short.MinValue, short.MaxValue);
            Inputs.AxisState[AxisFlags.LeftStickY] = (short)InputUtils.MapRange(state.Y, ushort.MaxValue, ushort.MinValue, short.MinValue, short.MaxValue);

            Inputs.AxisState[AxisFlags.RightStickX] = (short)InputUtils.MapRange(state.Z, ushort.MinValue, ushort.MaxValue, short.MinValue, short.MaxValue);
            Inputs.AxisState[AxisFlags.RightStickY] = (short)InputUtils.MapRange(state.RotationZ, ushort.MaxValue, ushort.MinValue, short.MinValue, short.MaxValue);

            Inputs.AxisState[AxisFlags.L2] = (byte)InputUtils.MapRange(state.RotationX, ushort.MinValue, ushort.MaxValue, byte.MinValue, byte.MaxValue);
            Inputs.AxisState[AxisFlags.R2] = (byte)InputUtils.MapRange(state.RotationY, ushort.MinValue, ushort.MaxValue, byte.MinValue, byte.MaxValue);
        }
        catch (SharpDX.SharpDXException ex)
        {
            if (ex.ResultCode == ResultCode.NotAcquired)
            {
                if (IsPlugged)
                    Plug();
            }
            else if (ex.ResultCode == ResultCode.InputLost)
                AttachDetails(Details);
        }

        base.Tick(ticks, delta);
    }

    public override void SetVibration(byte LargeMotor, byte SmallMotor)
    {

        if (IDevice.GetCurrent().GetType() == typeof(ClawA1M))
        {
            FeedbackLargeMotor = LargeMotor;
            FeedbackSmallMotor = SmallMotor;
        }
        else
        {
            WriteVibration(LargeMotor, SmallMotor);
        }
    }

    private void WriteVibration(byte LargeMotor, byte SmallMotor)
    {
        if (!IsConnected())
            return;

        joystickHid?.Write(new byte[]
        {
            05, 01, 00, 00,
            (byte)(SmallMotor * VibrationStrength),
            (byte)(LargeMotor * VibrationStrength),
            00,
            00,
            00,
            00,
            00
        });
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
}