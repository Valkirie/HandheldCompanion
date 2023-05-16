using ControllerCommon;
using ControllerCommon.Controllers;
using ControllerCommon.Inputs;
using ControllerCommon.Managers;
using ControllerCommon.Pipes;
using HandheldCompanion.Managers;
using neptune_hidapi.net;
using neptune_hidapi.net.Hid;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HandheldCompanion.Controllers
{
    public class NeptuneController : IController
    {
        private neptune_hidapi.net.NeptuneController Controller;
        private NeptuneControllerInputEventArgs input;

        private bool isConnected = false;
        private bool isVirtualMuted = false;

        private const short TrackPadInner = 21844;

        private NeptuneControllerInputState prevState;

        public byte FeedbackLargeMotor;
        public byte FeedbackSmallMotor;

        public const sbyte MinIntensity = -2;
        public const sbyte MaxIntensity = 10;

        private Thread thread;
        private bool ThreadRunning;

        private Task<byte[]> lastLeftHapticOn;
        private Task<byte[]> lastRightHapticOn;

        public NeptuneController(PnPDetails details)
        {
            if (details is null)
                return;

            Details = details;
            Details.isHooked = true;

            Capacities |= ControllerCapacities.Gyroscope;
            Capacities |= ControllerCapacities.Accelerometer;

            try
            {
                Controller = new();
                Controller.Open();
                isConnected = true;
            }
            catch (Exception ex)
            {
                LogManager.LogError("Couldn't initialize NeptuneController. Exception: {0}", ex.Message);
                return;
            }

            // manage rumble thread
            ThreadRunning = true;
            thread = new Thread(ThreadLoop);
            thread.IsBackground = true;

            // disable lizard state
            SetLizardMouse(false);
            SetLizardButtons(false);

            bool Muted = SettingsManager.GetBoolean("SteamDeckMuteController");
            SetVirtualMuted(Muted);

            // UI
            DrawControls();
            RefreshControls();

            // Additional controller specific source buttons/axes
            SourceButtons.AddRange(new List<ButtonFlags>() { ButtonFlags.L4, ButtonFlags.R4, ButtonFlags.L5, ButtonFlags.R5 });
            SourceButtons.AddRange(new List<ButtonFlags>() { ButtonFlags.LeftThumbTouch, ButtonFlags.RightThumbTouch });
            SourceButtons.AddRange(new List<ButtonFlags>() { ButtonFlags.LeftPadClick, ButtonFlags.LeftPadTouch, ButtonFlags.LeftPadClickUp, ButtonFlags.LeftPadClickDown, ButtonFlags.LeftPadClickLeft, ButtonFlags.LeftPadClickRight });
            SourceButtons.AddRange(new List<ButtonFlags>() { ButtonFlags.RightPadClick, ButtonFlags.RightPadTouch, ButtonFlags.RightPadClickUp, ButtonFlags.RightPadClickDown, ButtonFlags.RightPadClickLeft, ButtonFlags.RightPadClickRight });

            SourceAxis.Add(AxisLayoutFlags.LeftPad);
            SourceAxis.Add(AxisLayoutFlags.RightPad);
        }

        private async void ThreadLoop(object? obj)
        {
            while (ThreadRunning)
            {
                if (GetHapticIntensity(FeedbackLargeMotor, MaxIntensity, out var leftIntensity))
                    lastLeftHapticOn = Controller.SetHaptic2(HapticPad.Left, HapticStyle.Weak, leftIntensity);

                if (GetHapticIntensity(FeedbackSmallMotor, MaxIntensity, out var rightIntensity))
                    lastRightHapticOn = Controller.SetHaptic2(HapticPad.Right, HapticStyle.Weak, rightIntensity);

                Thread.Sleep(TimerManager.GetPeriod() * 2);

                if (lastLeftHapticOn is not null)
                    await lastLeftHapticOn;
                if (lastRightHapticOn is not null)
                    await lastRightHapticOn;
            }
        }

        public override string ToString()
        {
            string baseName = base.ToString();
            if (!string.IsNullOrEmpty(baseName))
                return baseName;
            return "Steam Controller Neptune";
        }

        public override void UpdateInputs(long ticks)
        {
            if (input is null)
                return;

            /*
            if (input.State.ButtonState.Equals(prevState.ButtonState) && input.State.AxesState.Equals(prevState.AxesState) && prevInjectedButtons.Equals(InjectedButtons))
                return;
            */

            Inputs.ButtonState = InjectedButtons.Clone() as ButtonState;

            Inputs.ButtonState[ButtonFlags.B1] = input.State.ButtonState[NeptuneControllerButton.BtnA];
            Inputs.ButtonState[ButtonFlags.B2] = input.State.ButtonState[NeptuneControllerButton.BtnB];
            Inputs.ButtonState[ButtonFlags.B3] = input.State.ButtonState[NeptuneControllerButton.BtnX];
            Inputs.ButtonState[ButtonFlags.B4] = input.State.ButtonState[NeptuneControllerButton.BtnY];

            Inputs.ButtonState[ButtonFlags.Start] = input.State.ButtonState[NeptuneControllerButton.BtnOptions];
            Inputs.ButtonState[ButtonFlags.Back] = input.State.ButtonState[NeptuneControllerButton.BtnMenu];

            Inputs.ButtonState[ButtonFlags.Special] = input.State.ButtonState[NeptuneControllerButton.BtnSteam];
            Inputs.ButtonState[ButtonFlags.OEM1] = input.State.ButtonState[NeptuneControllerButton.BtnQuickAccess];

            var L2 = input.State.AxesState[NeptuneControllerAxis.L2] * byte.MaxValue / short.MaxValue;
            var R2 = input.State.AxesState[NeptuneControllerAxis.R2] * byte.MaxValue / short.MaxValue;

            Inputs.ButtonState[ButtonFlags.L2] = L2 > Gamepad.TriggerThreshold;
            Inputs.ButtonState[ButtonFlags.R2] = R2 > Gamepad.TriggerThreshold;

            Inputs.ButtonState[ButtonFlags.L3] = L2 > Gamepad.TriggerThreshold * 8;
            Inputs.ButtonState[ButtonFlags.R3] = R2 > Gamepad.TriggerThreshold * 8;

            Inputs.ButtonState[ButtonFlags.LeftThumbTouch] = input.State.ButtonState[NeptuneControllerButton.BtnLStickTouch];
            Inputs.ButtonState[ButtonFlags.RightThumbTouch] = input.State.ButtonState[NeptuneControllerButton.BtnRStickTouch];

            Inputs.ButtonState[ButtonFlags.LeftThumb] = input.State.ButtonState[NeptuneControllerButton.BtnLStickPress];
            Inputs.ButtonState[ButtonFlags.RightThumb] = input.State.ButtonState[NeptuneControllerButton.BtnRStickPress];

            Inputs.ButtonState[ButtonFlags.L1] = input.State.ButtonState[NeptuneControllerButton.BtnL1];
            Inputs.ButtonState[ButtonFlags.R1] = input.State.ButtonState[NeptuneControllerButton.BtnR1];
            Inputs.ButtonState[ButtonFlags.L4] = input.State.ButtonState[NeptuneControllerButton.BtnL4];
            Inputs.ButtonState[ButtonFlags.R4] = input.State.ButtonState[NeptuneControllerButton.BtnR4];
            Inputs.ButtonState[ButtonFlags.L5] = input.State.ButtonState[NeptuneControllerButton.BtnL5];
            Inputs.ButtonState[ButtonFlags.R5] = input.State.ButtonState[NeptuneControllerButton.BtnR5];

            Inputs.ButtonState[ButtonFlags.DPadUp] = input.State.ButtonState[NeptuneControllerButton.BtnDpadUp];
            Inputs.ButtonState[ButtonFlags.DPadDown] = input.State.ButtonState[NeptuneControllerButton.BtnDpadDown];
            Inputs.ButtonState[ButtonFlags.DPadLeft] = input.State.ButtonState[NeptuneControllerButton.BtnDpadLeft];
            Inputs.ButtonState[ButtonFlags.DPadRight] = input.State.ButtonState[NeptuneControllerButton.BtnDpadRight];

            // Left Stick
            Inputs.ButtonState[ButtonFlags.LeftThumbLeft] = input.State.AxesState[NeptuneControllerAxis.LeftStickX] < -Gamepad.LeftThumbDeadZone;
            Inputs.ButtonState[ButtonFlags.LeftThumbRight] = input.State.AxesState[NeptuneControllerAxis.LeftStickX] > Gamepad.LeftThumbDeadZone;
            Inputs.ButtonState[ButtonFlags.LeftThumbDown] = input.State.AxesState[NeptuneControllerAxis.LeftStickY] < -Gamepad.LeftThumbDeadZone;
            Inputs.ButtonState[ButtonFlags.LeftThumbUp] = input.State.AxesState[NeptuneControllerAxis.LeftStickY] > Gamepad.LeftThumbDeadZone;

            Inputs.AxisState[AxisFlags.LeftThumbX] = input.State.AxesState[NeptuneControllerAxis.LeftStickX];
            Inputs.AxisState[AxisFlags.LeftThumbY] = input.State.AxesState[NeptuneControllerAxis.LeftStickY];

            // Right Stick
            Inputs.ButtonState[ButtonFlags.RightThumbLeft] = input.State.AxesState[NeptuneControllerAxis.RightStickX] < -Gamepad.RightThumbDeadZone;
            Inputs.ButtonState[ButtonFlags.RightThumbRight] = input.State.AxesState[NeptuneControllerAxis.RightStickX] > Gamepad.RightThumbDeadZone;
            Inputs.ButtonState[ButtonFlags.RightThumbDown] = input.State.AxesState[NeptuneControllerAxis.RightStickY] < -Gamepad.RightThumbDeadZone;
            Inputs.ButtonState[ButtonFlags.RightThumbUp] = input.State.AxesState[NeptuneControllerAxis.RightStickY] > Gamepad.RightThumbDeadZone;

            Inputs.AxisState[AxisFlags.RightThumbX] = input.State.AxesState[NeptuneControllerAxis.RightStickX];
            Inputs.AxisState[AxisFlags.RightThumbY] = input.State.AxesState[NeptuneControllerAxis.RightStickY];

            Inputs.AxisState[AxisFlags.L2] = (short)L2;
            Inputs.AxisState[AxisFlags.R2] = (short)R2;

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
                if (Inputs.AxisState[AxisFlags.LeftPadY] >= TrackPadInner)
                    Inputs.ButtonState[ButtonFlags.LeftPadClickUp] = true;
                else if (Inputs.AxisState[AxisFlags.LeftPadY] <= -TrackPadInner)
                    Inputs.ButtonState[ButtonFlags.LeftPadClickDown] = true;

                if (Inputs.AxisState[AxisFlags.LeftPadX] >= TrackPadInner)
                    Inputs.ButtonState[ButtonFlags.LeftPadClickRight] = true;
                else if (Inputs.AxisState[AxisFlags.LeftPadX] <= -TrackPadInner)
                    Inputs.ButtonState[ButtonFlags.LeftPadClickLeft] = true;
            }

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
                if (Inputs.AxisState[AxisFlags.RightPadY] >= TrackPadInner)
                    Inputs.ButtonState[ButtonFlags.RightPadClickUp] = true;
                else if (Inputs.AxisState[AxisFlags.RightPadY] <= -TrackPadInner)
                    Inputs.ButtonState[ButtonFlags.RightPadClickDown] = true;

                if (Inputs.AxisState[AxisFlags.RightPadX] >= TrackPadInner)
                    Inputs.ButtonState[ButtonFlags.RightPadClickRight] = true;
                else if (Inputs.AxisState[AxisFlags.RightPadX] <= -TrackPadInner)
                    Inputs.ButtonState[ButtonFlags.RightPadClickLeft] = true;
            }

            // update states
            prevState = input.State;

            base.UpdateInputs(ticks);
        }

        public override void UpdateMovements(long ticks)
        {
            if (input is null)
                return;

            Movements.GyroAccelZ = -(float)input.State.AxesState[NeptuneControllerAxis.GyroAccelY] / short.MaxValue * 2.0f;
            Movements.GyroAccelY = -(float)input.State.AxesState[NeptuneControllerAxis.GyroAccelZ] / short.MaxValue * 2.0f;
            Movements.GyroAccelX = -(float)input.State.AxesState[NeptuneControllerAxis.GyroAccelX] / short.MaxValue * 2.0f;

            Movements.GyroPitch = -(float)input.State.AxesState[NeptuneControllerAxis.GyroRoll] / short.MaxValue * 2000.0f;
            Movements.GyroRoll = (float)input.State.AxesState[NeptuneControllerAxis.GyroPitch] / short.MaxValue * 2000.0f;
            Movements.GyroYaw = -(float)input.State.AxesState[NeptuneControllerAxis.GyroYaw] / short.MaxValue * 2000.0f;

            base.UpdateMovements(ticks);
        }

        public override bool IsConnected()
        {
            return isConnected;
        }

        public bool IsLizardMouseEnabled()
        {
            return Controller.LizardMouseEnabled;
        }

        public bool IsLizardButtonsEnabled()
        {
            return Controller.LizardButtonsEnabled;
        }

        public virtual bool IsVirtualMuted()
        {
            return isVirtualMuted;
        }

        public override void Rumble(int loop)
        {
            Task.Factory.StartNew(async () =>
            {
                for (int i = 0; i < loop * 2; i++)
                {
                    if (i % 2 == 0)
                        SetVibration(byte.MaxValue, byte.MaxValue);
                    else
                        SetVibration(0, 0);

                    await Task.Delay(125);
                }
            });

            base.Rumble(loop);
        }

        public override void Plug()
        {
            TimerManager.Tick += UpdateInputs;
            TimerManager.Tick += UpdateMovements;

            thread.Start();

            Controller.OnControllerInputReceived = input => Task.Run(() => OnControllerInputReceived(input));

            PipeClient.ServerMessage += OnServerMessage;
            base.Plug();
        }

        private void OnControllerInputReceived(NeptuneControllerInputEventArgs input)
        {
            this.input = input;
        }

        public override void Unplug()
        {
            try
            {
                // restore lizard state
                SetLizardButtons(true);
                SetLizardMouse(true);

                Controller.Close();
                isConnected = false;
            }
            catch
            {
                return;
            }

            TimerManager.Tick -= UpdateInputs;
            TimerManager.Tick -= UpdateMovements;

            // kill rumble thread
            ThreadRunning = false;

            PipeClient.ServerMessage -= OnServerMessage;
            base.Unplug();
        }

        public bool GetHapticIntensity(byte? input, sbyte maxIntensity, out sbyte output)
        {
            output = default;
            if (input is null || input.Value == 0)
                return false;

            double value = MinIntensity + (maxIntensity - MinIntensity) * input.Value * VibrationStrength / 255;
            output = (sbyte)(value - 5); // convert from dB to values
            return true;
        }

        public override void SetVibrationStrength(double value, bool rumble)
        {
            base.SetVibrationStrength(value, rumble);
            if (rumble)
                this.Rumble(1);
        }

        public override void SetVibration(byte LargeMotor, byte SmallMotor)
        {
            this.FeedbackLargeMotor = LargeMotor;
            this.FeedbackSmallMotor = SmallMotor;
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

        public void SetLizardMouse(bool lizardMode)
        {
            Controller.LizardMouseEnabled = lizardMode;
        }

        public void SetLizardButtons(bool lizardMode)
        {
            Controller.LizardButtonsEnabled = lizardMode;
        }

        public void SetVirtualMuted(bool mute)
        {
            isVirtualMuted = mute;
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
                    return "\u21B0";
                case ButtonFlags.R1:
                    return "\u21B1";
                case ButtonFlags.Back:
                    return "\u21FA";
                case ButtonFlags.Start:
                    return "\u21FB";
                case ButtonFlags.L2:
                case ButtonFlags.L3:
                    return "\u21B2";
                case ButtonFlags.R2:
                case ButtonFlags.R3:
                    return "\u21B3";
                case ButtonFlags.L4:
                    return "\u219c\u24f8";
                case ButtonFlags.L5:
                    return "\u219c\u24f9";
                case ButtonFlags.R4:
                    return "\u219d\u24f8";
                case ButtonFlags.R5:
                    return "\u219d\u24f9";
                case ButtonFlags.Special:
                    return "\u21E4";
                case ButtonFlags.OEM1:
                    return "\u21E5";
                case ButtonFlags.LeftThumbTouch:
                    return "\u21DA";
                case ButtonFlags.RightThumbTouch:
                    return "\u21DB";
                case ButtonFlags.LeftPadTouch:
                    return "\u2268";
                case ButtonFlags.RightPadTouch:
                    return "\u2269";
                case ButtonFlags.LeftPadClick:
                    return "\u2266";
                case ButtonFlags.RightPadClick:
                    return "\u2267";
                case ButtonFlags.LeftPadClickUp:
                    return "\u2270";
                case ButtonFlags.LeftPadClickDown:
                    return "\u2274";
                case ButtonFlags.LeftPadClickLeft:
                    return "\u226E";
                case ButtonFlags.LeftPadClickRight:
                    return "\u2272";
                case ButtonFlags.RightPadClickUp:
                    return "\u2271";
                case ButtonFlags.RightPadClickDown:
                    return "\u2275";
                case ButtonFlags.RightPadClickLeft:
                    return "\u226F";
                case ButtonFlags.RightPadClickRight:
                    return "\u2273";
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
                case AxisLayoutFlags.LeftPad:
                    return "\u2264";
                case AxisLayoutFlags.RightPad:
                    return "\u2265";
            }

            return base.GetGlyph(axis);
        }
    }
}
