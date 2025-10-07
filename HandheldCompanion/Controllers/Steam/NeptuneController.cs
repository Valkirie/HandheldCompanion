using HandheldCompanion.Actions;
using HandheldCompanion.Helpers;
using HandheldCompanion.Inputs;
using HandheldCompanion.Managers;
using HandheldCompanion.Shared;
using SharpDX.XInput;
using steam_hidapi.net;
using steam_hidapi.net.Hid;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace HandheldCompanion.Controllers.Steam;

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
    private int rumbleThreadInterval = 10;

    public NeptuneController()
    { }

    public NeptuneController(PnPDetails details) : base()
    {
        AttachDetails(details);
    }

    protected override void InitializeInputOutput()
    {
        // Additional controller specific source buttons/axes
        SourceButtons.AddRange([ButtonFlags.L4, ButtonFlags.R4, ButtonFlags.L5, ButtonFlags.R5]);
        SourceButtons.AddRange([ButtonFlags.LeftStickTouch, ButtonFlags.RightStickTouch]);
        SourceButtons.AddRange([ButtonFlags.LeftPadClick, ButtonFlags.LeftPadTouch, ButtonFlags.LeftPadClickUp, ButtonFlags.LeftPadClickDown, ButtonFlags.LeftPadClickLeft, ButtonFlags.LeftPadClickRight]);
        SourceButtons.AddRange([ButtonFlags.RightPadClick, ButtonFlags.RightPadTouch, ButtonFlags.RightPadClickUp, ButtonFlags.RightPadClickDown, ButtonFlags.RightPadClickLeft, ButtonFlags.RightPadClickRight]);

        SourceAxis.Add(AxisLayoutFlags.LeftPad);
        SourceAxis.Add(AxisLayoutFlags.RightPad);
        SourceAxis.Add(AxisLayoutFlags.Gyroscope);

        TargetButtons.Add(ButtonFlags.LeftPadClick);
        TargetButtons.Add(ButtonFlags.RightPadClick);
        TargetButtons.Add(ButtonFlags.LeftPadTouch);
        TargetButtons.Add(ButtonFlags.RightPadTouch);

        TargetAxis.Add(AxisLayoutFlags.LeftPad);
        TargetAxis.Add(AxisLayoutFlags.RightPad);
    }

    public override void AttachDetails(PnPDetails details)
    {
        base.AttachDetails(details);

        // (un)plug controller if needed
        bool WasPlugged = IsConnected();
        if (WasPlugged) Unplug();

        // create controller
        Controller = new(details.VendorID, details.ProductID, details.GetMI());
        UserIndex = (byte)details.GetMI();

        // (re)plug controller if needed or open it
        if (WasPlugged) Plug(); else Open();
    }

    public override string ToString()
    {
        return "Valve Software Steam Controller";
    }

    public override void Tick(long ticks, float delta, bool commit)
    {
        if (Inputs is null || IsBusy || !IsPlugged || IsDisposing || IsDisposed)
            return;

        ButtonState.Overwrite(InjectedButtons, Inputs.ButtonState);

        Inputs.ButtonState[ButtonFlags.B1] |= input.State.ButtonState[NeptuneControllerButton.BtnA];
        Inputs.ButtonState[ButtonFlags.B2] |= input.State.ButtonState[NeptuneControllerButton.BtnB];
        Inputs.ButtonState[ButtonFlags.B3] |= input.State.ButtonState[NeptuneControllerButton.BtnX];
        Inputs.ButtonState[ButtonFlags.B4] |= input.State.ButtonState[NeptuneControllerButton.BtnY];

        Inputs.ButtonState[ButtonFlags.DPadUp] |= input.State.ButtonState[NeptuneControllerButton.BtnDpadUp];
        Inputs.ButtonState[ButtonFlags.DPadDown] |= input.State.ButtonState[NeptuneControllerButton.BtnDpadDown];
        Inputs.ButtonState[ButtonFlags.DPadLeft] |= input.State.ButtonState[NeptuneControllerButton.BtnDpadLeft];
        Inputs.ButtonState[ButtonFlags.DPadRight] |= input.State.ButtonState[NeptuneControllerButton.BtnDpadRight];

        Inputs.ButtonState[ButtonFlags.Start] |= input.State.ButtonState[NeptuneControllerButton.BtnOptions];
        Inputs.ButtonState[ButtonFlags.Back] |= input.State.ButtonState[NeptuneControllerButton.BtnMenu];

        Inputs.ButtonState[ButtonFlags.Special] |= input.State.ButtonState[NeptuneControllerButton.BtnSteam];
        Inputs.ButtonState[ButtonFlags.OEM1] |= input.State.ButtonState[NeptuneControllerButton.BtnQuickAccess];

        var L2 = input.State.AxesState[NeptuneControllerAxis.L2] * byte.MaxValue / short.MaxValue;
        var R2 = input.State.AxesState[NeptuneControllerAxis.R2] * byte.MaxValue / short.MaxValue;

        Inputs.ButtonState[ButtonFlags.L2Soft] |= L2 > Gamepad.TriggerThreshold;
        Inputs.ButtonState[ButtonFlags.R2Soft] |= R2 > Gamepad.TriggerThreshold;

        Inputs.ButtonState[ButtonFlags.L2Full] |= L2 > Gamepad.TriggerThreshold * 8;
        Inputs.ButtonState[ButtonFlags.R2Full] |= R2 > Gamepad.TriggerThreshold * 8;

        Inputs.AxisState[AxisFlags.L2] = (short)L2;
        Inputs.AxisState[AxisFlags.R2] = (short)R2;

        Inputs.ButtonState[ButtonFlags.L1] = input.State.ButtonState[NeptuneControllerButton.BtnL1];
        Inputs.ButtonState[ButtonFlags.R1] = input.State.ButtonState[NeptuneControllerButton.BtnR1];
        Inputs.ButtonState[ButtonFlags.L4] = input.State.ButtonState[NeptuneControllerButton.BtnL4];
        Inputs.ButtonState[ButtonFlags.R4] = input.State.ButtonState[NeptuneControllerButton.BtnR4];
        Inputs.ButtonState[ButtonFlags.L5] = input.State.ButtonState[NeptuneControllerButton.BtnL5];
        Inputs.ButtonState[ButtonFlags.R5] = input.State.ButtonState[NeptuneControllerButton.BtnR5];

        // Left Stick
        Inputs.ButtonState[ButtonFlags.LeftStickTouch] = input.State.ButtonState[NeptuneControllerButton.BtnLStickTouch];
        Inputs.ButtonState[ButtonFlags.LeftStickClick] |= input.State.ButtonState[NeptuneControllerButton.BtnLStickPress];

        Inputs.AxisState[AxisFlags.LeftStickX] = input.State.AxesState[NeptuneControllerAxis.LeftStickX];
        Inputs.AxisState[AxisFlags.LeftStickY] = input.State.AxesState[NeptuneControllerAxis.LeftStickY];

        // TODO: Implement Inner/Outer Ring button mappings for sticks
        // https://github.com/Havner/HandheldCompanion/commit/e1124ceb6c59051201756d5e95b2eb39a3bb24f6

        /* float leftLength = new Vector2(Inputs.AxisState[AxisFlags.LeftStickX], Inputs.AxisState[AxisFlags.LeftStickY]).Length();
        Inputs.ButtonState[ButtonFlags.LeftStickOuterRing] = leftLength >= (RingThreshold * short.MaxValue);
        Inputs.ButtonState[ButtonFlags.LeftStickInnerRing] = leftLength >= Gamepad.LeftThumbDeadZone && leftLength < (RingThreshold * short.MaxValue); */

        // Right Stick
        Inputs.ButtonState[ButtonFlags.RightStickTouch] = input.State.ButtonState[NeptuneControllerButton.BtnRStickTouch];
        Inputs.ButtonState[ButtonFlags.RightStickClick] |= input.State.ButtonState[NeptuneControllerButton.BtnRStickPress];

        Inputs.AxisState[AxisFlags.RightStickX] = input.State.AxesState[NeptuneControllerAxis.RightStickX];
        Inputs.AxisState[AxisFlags.RightStickY] = input.State.AxesState[NeptuneControllerAxis.RightStickY];

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

        // Accelerometer has 16 bit resolution and a range of +/- 2g
        aX = (float)input.State.AxesState[NeptuneControllerAxis.GyroAccelX] / short.MaxValue * 2.0f;
        aY = (float)input.State.AxesState[NeptuneControllerAxis.GyroAccelZ] / short.MaxValue * 2.0f;
        aZ = -(float)input.State.AxesState[NeptuneControllerAxis.GyroAccelY] / short.MaxValue * 2.0f;

        // Gyroscope has 16 bit resolution and a range of +/- 2000 dps
        gX = (float)input.State.AxesState[NeptuneControllerAxis.GyroPitch] / short.MaxValue * 2000.0f;  // Roll
        gY = (float)input.State.AxesState[NeptuneControllerAxis.GyroRoll] / short.MaxValue * 2000.0f;   // Pitch
        gZ = -(float)input.State.AxesState[NeptuneControllerAxis.GyroYaw] / short.MaxValue * 2000.0f;    // Yaw

        // store motion
        Inputs.GyroState.SetGyroscope(gX, gY, gZ);
        Inputs.GyroState.SetAccelerometer(aX, aY, aZ);

        // process motion
        if (gamepadMotions.TryGetValue(gamepadIndex, out GamepadMotion gamepadMotion))
            gamepadMotion.ProcessMotion(gX, gY, gZ, aX, aY, aZ, delta);

        base.Tick(ticks, delta);
    }

    private Task HandleControllerInput(NeptuneControllerInputEventArgs input)
    {
        this.input = input;
        return Task.CompletedTask;
    }

    private void Open()
    {
        try
        {
            if (Controller is not null)
            {
                Controller.OnControllerInputReceived += HandleControllerInput;

                // open controller
                Controller.Open();

                // disable lizard state
                Controller.RequestLizardMode(false);
            }
        }
        catch { }
    }

    private void Close()
    {
        try
        {
            if (Controller is not null)
            {
                Controller.OnControllerInputReceived -= HandleControllerInput;

                // disable lizard state
                Controller.RequestLizardMode(true);

                // close controller
                Controller.Close();
            }
        }
        catch { }
    }

    public override void Gone()
    {
        lock (hidLock)
        {
            if (Controller is not null)
            {
                Controller.OnControllerInputReceived -= HandleControllerInput;
                Controller.EndRead();
                Controller = null;
            }
        }
    }

    public override void Hide(bool powerCycle = true)
    {
        lock (hidLock)
        {
            Close();
            base.Hide(powerCycle);
            if (!powerCycle)
                Open();
        }
    }

    public override void Unhide(bool powerCycle = true)
    {
        // you shouldn't unhide the controller if steam mode is set to: exclusive
        bool IsExclusiveMode = ManagerFactory.settingsManager.GetBoolean("SteamControllerMode");
        if (IsExclusiveMode)
            return;

        lock (hidLock)
        {
            Close();
            base.Unhide(powerCycle);
            if (!powerCycle)
                Open();
        }
    }

    public override void Plug()
    {
        try
        {
            // open controller
            Open();
        }
        catch (Exception ex)
        {
            LogManager.LogError("Couldn't initialize {0}. Exception: {1}", typeof(NeptuneController), ex.Message);
            return;
        }

        // manage rumble thread
        rumbleThreadRunning = true;
        rumbleThread = new Thread(RumbleThreadLoop)
        {
            IsBackground = true,
            Priority = ThreadPriority.Normal
        };
        rumbleThread.Start();

        // raise events
        switch (ManagerFactory.settingsManager.Status)
        {
            default:
            case ManagerStatus.Initializing:
                ManagerFactory.settingsManager.Initialized += SettingsManager_Initialized;
                break;
            case ManagerStatus.Initialized:
                QuerySettings();
                break;
        }

        base.Plug();
    }

    private void SettingsManager_Initialized()
    {
        QuerySettings();
    }

    private void QuerySettings()
    {
        // manage events
        ManagerFactory.settingsManager.SettingValueChanged += SettingsManager_SettingValueChanged;

        // raise events
        SettingsManager_SettingValueChanged("SteamControllerRumbleInterval", ManagerFactory.settingsManager.GetInt("SteamControllerRumbleInterval"), false);
    }

    private void SettingsManager_SettingValueChanged(string name, object value, bool temporary)
    {
        switch (name)
        {
            case "SteamControllerRumbleInterval":
                rumbleThreadInterval = Convert.ToInt32(value);
                break;
        }
    }

    public override void Unplug()
    {
        try
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

            // close controller
            Close();
        }
        catch
        {
            return;
        }

        ManagerFactory.settingsManager.SettingValueChanged -= SettingsManager_SettingValueChanged;
        ManagerFactory.settingsManager.Initialized -= SettingsManager_Initialized;

        base.Unplug();
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
        FeedbackLargeMotor = LargeMotor;
        FeedbackSmallMotor = SmallMotor;
    }

    private async void RumbleThreadLoop(object? obj)
    {
        while (rumbleThreadRunning)
        {
            if (GetHapticIntensity(FeedbackLargeMotor, MinIntensity, MaxIntensity, out var leftIntensity))
                Controller.SetHaptic2(SCHapticMotor.Left, NCHapticStyle.Weak, leftIntensity);

            if (GetHapticIntensity(FeedbackSmallMotor, MinIntensity, MaxIntensity, out var rightIntensity))
                Controller.SetHaptic2(SCHapticMotor.Right, NCHapticStyle.Weak, rightIntensity);

            Thread.Sleep(rumbleThreadInterval);
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