using HandheldCompanion.Actions;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using SharpDX.XInput;
using steam_hidapi.net;
using steam_hidapi.net.Hid;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HandheldCompanion.Controllers;

public class NeptuneController : SteamController
{
    private steam_hidapi.net.NeptuneController Controller;
    private NeptuneControllerInputEventArgs input;

    private const short TrackPadInner = 21844;

    public byte FeedbackLargeMotor;
    public byte FeedbackSmallMotor;

    public const sbyte MinIntensity = -2;
    public const sbyte MaxIntensity = 10;

    // TODO: why not use TimerManager.Tick?
    private Thread rumbleThread;
    private bool rumbleThreadRunning;

    public NeptuneController(PnPDetails details) : base()
    {
<<<<<<< HEAD
        AttachDetails(details);

        // UI
        DrawUI();
        UpdateUI();
=======
        if (details is null)
            return;

        Details = details;
        Details.isHooked = true;

        try
        {
            Controller = new(details.attributes.VendorID, details.attributes.ProductID, details.GetMI());

            // open controller
            Open();
        }
        catch (Exception ex)
        {
            LogManager.LogError("Couldn't initialize NeptuneController. Exception: {0}", ex.Message);
            return;
        }

        // UI
        InitializeComponent();
        DrawControls();
        RefreshControls();
>>>>>>> f8fea3c25fb5fd254f5020d43305b7356ec9770d

        // Additional controller specific source buttons/axes
        SourceButtons.AddRange(new List<ButtonFlags>
            { ButtonFlags.L4, ButtonFlags.R4, ButtonFlags.L5, ButtonFlags.R5 });
        SourceButtons.AddRange(new List<ButtonFlags> { ButtonFlags.LeftStickTouch, ButtonFlags.RightStickTouch });
        SourceButtons.AddRange(new List<ButtonFlags>
        {
            ButtonFlags.LeftPadClick, ButtonFlags.LeftPadTouch, ButtonFlags.LeftPadClickUp,
            ButtonFlags.LeftPadClickDown, ButtonFlags.LeftPadClickLeft, ButtonFlags.LeftPadClickRight
        });
        SourceButtons.AddRange(new List<ButtonFlags>
        {
            ButtonFlags.RightPadClick, ButtonFlags.RightPadTouch, ButtonFlags.RightPadClickUp,
            ButtonFlags.RightPadClickDown, ButtonFlags.RightPadClickLeft, ButtonFlags.RightPadClickRight
        });

        SourceAxis.Add(AxisLayoutFlags.LeftPad);
        SourceAxis.Add(AxisLayoutFlags.RightPad);
        SourceAxis.Add(AxisLayoutFlags.Gyroscope);

        TargetButtons.Add(ButtonFlags.LeftPadClick);
        TargetButtons.Add(ButtonFlags.RightPadClick);
        TargetButtons.Add(ButtonFlags.LeftPadTouch);
        TargetButtons.Add(ButtonFlags.RightPadTouch);
<<<<<<< HEAD

        TargetAxis.Add(AxisLayoutFlags.LeftPad);
        TargetAxis.Add(AxisLayoutFlags.RightPad);
    }

    public override void AttachDetails(PnPDetails details)
    {
        base.AttachDetails(details);

        Controller = new(details.VendorID, details.ProductID, details.GetMI());

        // open controller
        Open();
=======

        TargetAxis.Add(AxisLayoutFlags.LeftPad);
        TargetAxis.Add(AxisLayoutFlags.RightPad);
>>>>>>> f8fea3c25fb5fd254f5020d43305b7356ec9770d
    }

    public override string ToString()
    {
        return "Valve Software Steam Controller";
    }

    public override void UpdateInputs(long ticks)
    {
        if (input is null)
            return;

        Inputs.ButtonState = InjectedButtons.Clone() as ButtonState;

        Inputs.ButtonState[ButtonFlags.B1] = input.State.ButtonState[NeptuneControllerButton.BtnA];
        Inputs.ButtonState[ButtonFlags.B2] = input.State.ButtonState[NeptuneControllerButton.BtnB];
        Inputs.ButtonState[ButtonFlags.B3] = input.State.ButtonState[NeptuneControllerButton.BtnX];
        Inputs.ButtonState[ButtonFlags.B4] = input.State.ButtonState[NeptuneControllerButton.BtnY];

        Inputs.ButtonState[ButtonFlags.DPadUp] = input.State.ButtonState[NeptuneControllerButton.BtnDpadUp];
        Inputs.ButtonState[ButtonFlags.DPadDown] = input.State.ButtonState[NeptuneControllerButton.BtnDpadDown];
        Inputs.ButtonState[ButtonFlags.DPadLeft] = input.State.ButtonState[NeptuneControllerButton.BtnDpadLeft];
        Inputs.ButtonState[ButtonFlags.DPadRight] = input.State.ButtonState[NeptuneControllerButton.BtnDpadRight];

        Inputs.ButtonState[ButtonFlags.Start] = input.State.ButtonState[NeptuneControllerButton.BtnOptions];
        Inputs.ButtonState[ButtonFlags.Back] = input.State.ButtonState[NeptuneControllerButton.BtnMenu];

        Inputs.ButtonState[ButtonFlags.Special] = input.State.ButtonState[NeptuneControllerButton.BtnSteam];
        Inputs.ButtonState[ButtonFlags.OEM1] = input.State.ButtonState[NeptuneControllerButton.BtnQuickAccess];

        var L2 = input.State.AxesState[NeptuneControllerAxis.L2] * byte.MaxValue / short.MaxValue;
        var R2 = input.State.AxesState[NeptuneControllerAxis.R2] * byte.MaxValue / short.MaxValue;

        Inputs.ButtonState[ButtonFlags.L2Soft] = L2 > Gamepad.TriggerThreshold;
        Inputs.ButtonState[ButtonFlags.R2Soft] = R2 > Gamepad.TriggerThreshold;

        Inputs.ButtonState[ButtonFlags.L2Full] = L2 > Gamepad.TriggerThreshold * 8;
        Inputs.ButtonState[ButtonFlags.R2Full] = R2 > Gamepad.TriggerThreshold * 8;

        Inputs.AxisState[AxisFlags.L2] = (short)L2;
        Inputs.AxisState[AxisFlags.R2] = (short)R2;

        Inputs.ButtonState[ButtonFlags.LeftStickTouch] =
            input.State.ButtonState[NeptuneControllerButton.BtnLStickTouch];
        Inputs.ButtonState[ButtonFlags.RightStickTouch] =
            input.State.ButtonState[NeptuneControllerButton.BtnRStickTouch];

        Inputs.ButtonState[ButtonFlags.LeftStickClick] = input.State.ButtonState[NeptuneControllerButton.BtnLStickPress];
        Inputs.ButtonState[ButtonFlags.RightStickClick] = input.State.ButtonState[NeptuneControllerButton.BtnRStickPress];

        Inputs.ButtonState[ButtonFlags.L1] = input.State.ButtonState[NeptuneControllerButton.BtnL1];
        Inputs.ButtonState[ButtonFlags.R1] = input.State.ButtonState[NeptuneControllerButton.BtnR1];
        Inputs.ButtonState[ButtonFlags.L4] = input.State.ButtonState[NeptuneControllerButton.BtnL4];
        Inputs.ButtonState[ButtonFlags.R4] = input.State.ButtonState[NeptuneControllerButton.BtnR4];
        Inputs.ButtonState[ButtonFlags.L5] = input.State.ButtonState[NeptuneControllerButton.BtnL5];
        Inputs.ButtonState[ButtonFlags.R5] = input.State.ButtonState[NeptuneControllerButton.BtnR5];

        // Left Stick
        Inputs.ButtonState[ButtonFlags.LeftStickUp] = input.State.ButtonState[NeptuneControllerButton.BtnLStickTouch];
        Inputs.ButtonState[ButtonFlags.LeftStickClick] = input.State.ButtonState[NeptuneControllerButton.BtnLStickPress];

        Inputs.AxisState[AxisFlags.LeftStickX] = input.State.AxesState[NeptuneControllerAxis.LeftStickX];
        Inputs.AxisState[AxisFlags.LeftStickY] = input.State.AxesState[NeptuneControllerAxis.LeftStickY];

        Inputs.ButtonState[ButtonFlags.LeftStickLeft] =
            input.State.AxesState[NeptuneControllerAxis.LeftStickX] < -Gamepad.LeftThumbDeadZone;
        Inputs.ButtonState[ButtonFlags.LeftStickRight] =
            input.State.AxesState[NeptuneControllerAxis.LeftStickX] > Gamepad.LeftThumbDeadZone;
        Inputs.ButtonState[ButtonFlags.LeftStickDown] =
            input.State.AxesState[NeptuneControllerAxis.LeftStickY] < -Gamepad.LeftThumbDeadZone;
        Inputs.ButtonState[ButtonFlags.LeftStickUp] =
            input.State.AxesState[NeptuneControllerAxis.LeftStickY] > Gamepad.LeftThumbDeadZone;

        // TODO: Implement Inner/Outer Ring button mappings for sticks
        // https://github.com/Havner/HandheldCompanion/commit/e1124ceb6c59051201756d5e95b2eb39a3bb24f6

        /* float leftLength = new Vector2(Inputs.AxisState[AxisFlags.LeftStickX], Inputs.AxisState[AxisFlags.LeftStickY]).Length();
        Inputs.ButtonState[ButtonFlags.LeftStickOuterRing] = leftLength >= (RingThreshold * short.MaxValue);
        Inputs.ButtonState[ButtonFlags.LeftStickInnerRing] = leftLength >= Gamepad.LeftThumbDeadZone && leftLength < (RingThreshold * short.MaxValue); */

        // Right Stick
        Inputs.ButtonState[ButtonFlags.RightStickTouch] = input.State.ButtonState[NeptuneControllerButton.BtnRStickTouch];
        Inputs.ButtonState[ButtonFlags.RightStickClick] = input.State.ButtonState[NeptuneControllerButton.BtnRStickPress];

        Inputs.AxisState[AxisFlags.RightStickX] = input.State.AxesState[NeptuneControllerAxis.RightStickX];
        Inputs.AxisState[AxisFlags.RightStickY] = input.State.AxesState[NeptuneControllerAxis.RightStickY];

        Inputs.ButtonState[ButtonFlags.RightStickLeft] =
            input.State.AxesState[NeptuneControllerAxis.RightStickX] < -Gamepad.RightThumbDeadZone;
        Inputs.ButtonState[ButtonFlags.RightStickRight] =
            input.State.AxesState[NeptuneControllerAxis.RightStickX] > Gamepad.RightThumbDeadZone;
        Inputs.ButtonState[ButtonFlags.RightStickDown] =
            input.State.AxesState[NeptuneControllerAxis.RightStickY] < -Gamepad.RightThumbDeadZone;
        Inputs.ButtonState[ButtonFlags.RightStickUp] =
            input.State.AxesState[NeptuneControllerAxis.RightStickY] > Gamepad.RightThumbDeadZone;

        // TODO: Implement Inner/Outer Ring button mappings for sticks
        // https://github.com/Havner/HandheldCompanion/commit/e1124ceb6c59051201756d5e95b2eb39a3bb24f6

        /* float rightLength = new Vector2(Inputs.AxisState[AxisFlags.RightStickX], Inputs.AxisState[AxisFlags.RightStickY]).Length();
        Inputs.ButtonState[ButtonFlags.RightStickOuterRing] = rightLength >= (RingThreshold * short.MaxValue);
        Inputs.ButtonState[ButtonFlags.RightStickInnerRing] = rightLength >= Gamepad.RightThumbDeadZone && rightLength < (RingThreshold * short.MaxValue); */

        // Left Pad
        Inputs.ButtonState[ButtonFlags.LeftPadTouch] = input.State.ButtonState[NeptuneControllerButton.BtnLPadTouch];
        if (input.State.ButtonState[NeptuneControllerButton.BtnLPadTouch])
        {
            Inputs.AxisState[AxisFlags.LeftPadX] = input.State.AxesState[NeptuneControllerAxis.LeftPadX];
            Inputs.AxisState[AxisFlags.LeftPadY] = input.State.AxesState[NeptuneControllerAxis.LeftPadY];
        }
        else
        {
            Inputs.AxisState[AxisFlags.LeftPadX] = 0;
            Inputs.AxisState[AxisFlags.LeftPadY] = 0;
        }

        Inputs.ButtonState[ButtonFlags.LeftPadClick] = input.State.ButtonState[NeptuneControllerButton.BtnLPadPress];
        if (Inputs.ButtonState[ButtonFlags.LeftPadClick])
        {
            Inputs.ButtonState[ButtonFlags.LeftPadClickUp] = Inputs.AxisState[AxisFlags.LeftPadY] >= TrackPadInner;
            Inputs.ButtonState[ButtonFlags.LeftPadClickDown] = Inputs.AxisState[AxisFlags.LeftPadY] <= -TrackPadInner;
            Inputs.ButtonState[ButtonFlags.LeftPadClickRight] = Inputs.AxisState[AxisFlags.LeftPadX] >= TrackPadInner;
            Inputs.ButtonState[ButtonFlags.LeftPadClickLeft] = Inputs.AxisState[AxisFlags.LeftPadX] <= -TrackPadInner;
        }
        else
        {
            Inputs.ButtonState[ButtonFlags.LeftPadClickUp] = false;
            Inputs.ButtonState[ButtonFlags.LeftPadClickDown] = false;
            Inputs.ButtonState[ButtonFlags.LeftPadClickRight] = false;
            Inputs.ButtonState[ButtonFlags.LeftPadClickLeft] = false;
        }

        // Right Pad
        Inputs.ButtonState[ButtonFlags.RightPadTouch] = input.State.ButtonState[NeptuneControllerButton.BtnRPadTouch];
        if (input.State.ButtonState[NeptuneControllerButton.BtnRPadTouch])
        {
            Inputs.AxisState[AxisFlags.RightPadX] = input.State.AxesState[NeptuneControllerAxis.RightPadX];
            Inputs.AxisState[AxisFlags.RightPadY] = input.State.AxesState[NeptuneControllerAxis.RightPadY];
        }
        else
        {
            Inputs.AxisState[AxisFlags.RightPadX] = 0;
            Inputs.AxisState[AxisFlags.RightPadY] = 0;
        }

        Inputs.ButtonState[ButtonFlags.RightPadClick] = input.State.ButtonState[NeptuneControllerButton.BtnRPadPress];
        if (Inputs.ButtonState[ButtonFlags.RightPadClick])
        {
            Inputs.ButtonState[ButtonFlags.RightPadClickUp] = Inputs.AxisState[AxisFlags.RightPadY] >= TrackPadInner;
            Inputs.ButtonState[ButtonFlags.RightPadClickDown] = Inputs.AxisState[AxisFlags.RightPadY] <= -TrackPadInner;
            Inputs.ButtonState[ButtonFlags.RightPadClickRight] = Inputs.AxisState[AxisFlags.RightPadX] >= TrackPadInner;
            Inputs.ButtonState[ButtonFlags.RightPadClickLeft] = Inputs.AxisState[AxisFlags.RightPadX] <= -TrackPadInner;
        }
        else
        {
            Inputs.ButtonState[ButtonFlags.RightPadClickUp] = false;
            Inputs.ButtonState[ButtonFlags.RightPadClickDown] = false;
            Inputs.ButtonState[ButtonFlags.RightPadClickRight] = false;
            Inputs.ButtonState[ButtonFlags.RightPadClickLeft] = false;
        }

        // TODO: why Z/Y swapped?
        Inputs.GyroState.Accelerometer.X = -(float)input.State.AxesState[NeptuneControllerAxis.GyroAccelX] / short.MaxValue * 2.0f;
        Inputs.GyroState.Accelerometer.Y = -(float)input.State.AxesState[NeptuneControllerAxis.GyroAccelZ] / short.MaxValue * 2.0f;
        Inputs.GyroState.Accelerometer.Z = -(float)input.State.AxesState[NeptuneControllerAxis.GyroAccelY] / short.MaxValue * 2.0f;

        // TODO: why Roll/Pitch swapped?
        Inputs.GyroState.Gyroscope.X = (float)input.State.AxesState[NeptuneControllerAxis.GyroPitch] / short.MaxValue * 2048.0f;  // Roll
        Inputs.GyroState.Gyroscope.Y = -(float)input.State.AxesState[NeptuneControllerAxis.GyroRoll] / short.MaxValue * 2048.0f;   // Pitch
        Inputs.GyroState.Gyroscope.Z = -(float)input.State.AxesState[NeptuneControllerAxis.GyroYaw] / short.MaxValue * 2048.0f;    // Yaw

        base.UpdateInputs(ticks);
    }

    private void Open()
    {
        try
        {
            Controller.Open();
            isConnected = true;
        }
        catch { }
    }

    private void Close()
    {
        try
        {
            Controller.Close();
            isConnected = false;
        }
        catch { }
    }

<<<<<<< HEAD
    public override void Hide(bool powerCycle = true)
    {
        Close();
        base.Hide(powerCycle);
        if (!powerCycle)
            Open();
    }

    public override void Unhide(bool powerCycle = true)
    {
        Close();
        base.Unhide(powerCycle);
        if (!powerCycle)
            Open();
    }

=======
>>>>>>> f8fea3c25fb5fd254f5020d43305b7356ec9770d
    private void OnControllerInputReceived(NeptuneControllerInputEventArgs input)
    {
        this.input = input;
    }

    public override void Plug()
    {
        try
        {
            Controller.OnControllerInputReceived = input => Task.Run(() => OnControllerInputReceived(input));

            // open controller
            Open();
        }
        catch (Exception ex)
        {
            LogManager.LogError("Couldn't initialize GordonController. Exception: {0}", ex.Message);
            return;
        }

        // disable lizard state
        Controller.RequestLizardMode(false);

        // manage rumble thread
        rumbleThreadRunning = true;
        rumbleThread = new Thread(RumbleThreadLoop);
        rumbleThread.IsBackground = true;
        rumbleThread.Start();

        SetVirtualMuted(SettingsManager.GetBoolean("SteamControllerMute"));

        TimerManager.Tick += UpdateInputs;

        base.Plug();
    }

    public override void Unplug()
    {
        try
        {
            // restore lizard state
            Controller.RequestLizardMode(true);

            // kill rumble thread
            rumbleThreadRunning = false;
            rumbleThread.Join();

            // close controller
            Close();
        }
        catch
        {
            return;
        }

        TimerManager.Tick -= UpdateInputs;

        base.Unplug();
    }

    public override void Cleanup()
    {
        TimerManager.Tick -= UpdateInputs;
    }

    public bool GetHapticIntensity(byte? input, sbyte minIntensity, sbyte maxIntensity, out sbyte output)
    {
        output = default;
        if (input is null || input.Value == 0)
            return false;

        var value = minIntensity + (maxIntensity - minIntensity) * input.Value * VibrationStrength / 255;
        output = (sbyte)(value - 5); // convert from dB to values
        return true;
    }

    public override void SetVibration(byte LargeMotor, byte SmallMotor)
    {
        this.FeedbackLargeMotor = LargeMotor;
        this.FeedbackSmallMotor = SmallMotor;
    }

    private async void RumbleThreadLoop(object? obj)
    {
        while (rumbleThreadRunning)
        {
            if (GetHapticIntensity(FeedbackLargeMotor, MinIntensity, MaxIntensity, out var leftIntensity))
                Controller.SetHaptic2(SCHapticMotor.Left, NCHapticStyle.Weak, leftIntensity);

            if (GetHapticIntensity(FeedbackSmallMotor, MinIntensity, MaxIntensity, out var rightIntensity))
                Controller.SetHaptic2(SCHapticMotor.Right, NCHapticStyle.Weak, rightIntensity);

            await Task.Delay(TimerManager.GetPeriod() * 2);
        }
    }

    public override void SetHaptic(HapticStrength strength, ButtonFlags button)
    {
        ushort value = strength switch
        {
            HapticStrength.Low => 512,
            HapticStrength.Medium => 1024,
            HapticStrength.High => 2048,
            _ => 0,
        };
        Controller.SetHaptic((byte)GetMotorForButton(button), value, 0, 1);
    }
}