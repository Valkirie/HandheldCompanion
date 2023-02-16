using ControllerCommon;
using ControllerCommon.Controllers;
using ControllerCommon.Inputs;
using ControllerCommon.Managers;
using HandheldCompanion.Managers;
using neptune_hidapi.net;
using PrecisionTiming;
using SharpDX.XInput;
using System;
using System.Runtime.InteropServices;
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

        private bool lastLeftHapticOn = false;
        private bool lastRightHapticOn = false;

        // temporary workaround
        private bool lastLeftPadClick = false;
        private bool lastRightPadClick = false;

        private NeptuneControllerInputState prevState;

        public NeptuneController(PnPDetails details)
        {
            Details = details;
            if (Details is null)
                return;

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

            InputsTimer.Tick += (sender, e) => UpdateInputs();
            MovementsTimer.Tick += (sender, e) => UpdateMovements();

            bool LizardMouse = SettingsManager.GetBoolean("SteamDeckLizardMouse");
            SetLizardMouse(LizardMouse);

            bool LizardButtons = SettingsManager.GetBoolean("SteamDeckLizardButtons");
            SetLizardButtons(LizardButtons);

            bool Muted = SettingsManager.GetBoolean("SteamDeckMuteController");
            SetVirtualMuted(Muted);

            // UI
            DrawControls();
            RefreshControls();

            // Specific buttons
            ButtonSupport.Add(ButtonFlags.L4);
            ButtonSupport.Add(ButtonFlags.R4);
            ButtonSupport.Add(ButtonFlags.L5);
            ButtonSupport.Add(ButtonFlags.R5);

            AxisSupport.Add(AxisFlags.RightPadX);
            AxisSupport.Add(AxisFlags.RightPadY);
        }

        public override string ToString()
        {
            if (!string.IsNullOrEmpty(Details.Name))
                return Details.Name;
            return "Steam Controller Neptune";
        }

        public override void UpdateInputs()
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

            Inputs.ButtonState[ButtonFlags.OEM1] = input.State.ButtonState[NeptuneControllerButton.BtnSteam];
            Inputs.ButtonState[ButtonFlags.OEM2] = input.State.ButtonState[NeptuneControllerButton.BtnQuickAccess];

            var L2 = input.State.AxesState[NeptuneControllerAxis.L2] * byte.MaxValue / short.MaxValue;
            var R2 = input.State.AxesState[NeptuneControllerAxis.R2] * byte.MaxValue / short.MaxValue;

            Inputs.ButtonState[ButtonFlags.L2] = L2 > Gamepad.TriggerThreshold;
            Inputs.ButtonState[ButtonFlags.R2] = R2 > Gamepad.TriggerThreshold;

            Inputs.ButtonState[ButtonFlags.LeftThumbTouch] = input.State.ButtonState[NeptuneControllerButton.BtnLStickTouch];
            Inputs.ButtonState[ButtonFlags.RightThumbTouch] = input.State.ButtonState[NeptuneControllerButton.BtnRStickTouch];

            // you don't want to combine touch + click
            Inputs.ButtonState[ButtonFlags.LeftThumb] = input.State.ButtonState[NeptuneControllerButton.BtnLStickPress];
            if (Inputs.ButtonState[ButtonFlags.LeftThumb])
                Inputs.ButtonState[ButtonFlags.LeftThumbTouch] = false;

            // you don't want to combine touch + click
            Inputs.ButtonState[ButtonFlags.RightThumb] = input.State.ButtonState[NeptuneControllerButton.BtnRStickPress];
            if (Inputs.ButtonState[ButtonFlags.RightThumb])
                Inputs.ButtonState[ButtonFlags.RightThumbTouch] = false;

            Inputs.ButtonState[ButtonFlags.L1] = input.State.ButtonState[NeptuneControllerButton.BtnL1];
            Inputs.ButtonState[ButtonFlags.R2] = input.State.ButtonState[NeptuneControllerButton.BtnR1];
            Inputs.ButtonState[ButtonFlags.L4] = input.State.ButtonState[NeptuneControllerButton.BtnL4];
            Inputs.ButtonState[ButtonFlags.R4] = input.State.ButtonState[NeptuneControllerButton.BtnR4];
            Inputs.ButtonState[ButtonFlags.L5] = input.State.ButtonState[NeptuneControllerButton.BtnL5];
            Inputs.ButtonState[ButtonFlags.R5] = input.State.ButtonState[NeptuneControllerButton.BtnR5];

            Inputs.ButtonState[ButtonFlags.DPadUp] = input.State.ButtonState[NeptuneControllerButton.BtnDpadUp];
            Inputs.ButtonState[ButtonFlags.DPadDown] = input.State.ButtonState[NeptuneControllerButton.BtnDpadDown];
            Inputs.ButtonState[ButtonFlags.DPadLeft] = input.State.ButtonState[NeptuneControllerButton.BtnDpadLeft];
            Inputs.ButtonState[ButtonFlags.DPadRight] = input.State.ButtonState[NeptuneControllerButton.BtnDpadRight];

            // Left Stick
            Inputs.ButtonState[ButtonFlags.LStickLeft] = input.State.AxesState[NeptuneControllerAxis.LeftStickX] < -Gamepad.LeftThumbDeadZone;
            Inputs.ButtonState[ButtonFlags.LStickRight] = input.State.AxesState[NeptuneControllerAxis.LeftStickX] > Gamepad.LeftThumbDeadZone;
            Inputs.ButtonState[ButtonFlags.LStickDown] = input.State.AxesState[NeptuneControllerAxis.LeftStickY] < -Gamepad.LeftThumbDeadZone;
            Inputs.ButtonState[ButtonFlags.LStickUp] = input.State.AxesState[NeptuneControllerAxis.LeftStickY] > Gamepad.LeftThumbDeadZone;

            Inputs.AxisState[AxisFlags.LeftThumbX] = input.State.AxesState[NeptuneControllerAxis.LeftStickX];
            Inputs.AxisState[AxisFlags.LeftThumbY] = input.State.AxesState[NeptuneControllerAxis.LeftStickY];

            // Right Stick
            Inputs.ButtonState[ButtonFlags.RStickLeft] = input.State.AxesState[NeptuneControllerAxis.RightStickX] < -Gamepad.RightThumbDeadZone;
            Inputs.ButtonState[ButtonFlags.RStickRight] = input.State.AxesState[NeptuneControllerAxis.RightStickX] > Gamepad.RightThumbDeadZone;
            Inputs.ButtonState[ButtonFlags.RStickDown] = input.State.AxesState[NeptuneControllerAxis.RightStickY] < -Gamepad.RightThumbDeadZone;
            Inputs.ButtonState[ButtonFlags.RStickUp] = input.State.AxesState[NeptuneControllerAxis.RightStickY] > Gamepad.RightThumbDeadZone;

            Inputs.AxisState[AxisFlags.RightThumbX] = input.State.AxesState[NeptuneControllerAxis.RightStickX];
            Inputs.AxisState[AxisFlags.RightThumbY] = input.State.AxesState[NeptuneControllerAxis.RightStickY];

            Inputs.AxisState[AxisFlags.L2] = (short)L2;
            Inputs.AxisState[AxisFlags.R2] = (short)R2;

            Inputs.ButtonState[ButtonFlags.LPadTouch] = input.State.ButtonState[NeptuneControllerButton.BtnLPadTouch];
            if (Inputs.ButtonState[ButtonFlags.LPadTouch])
            {
                Inputs.AxisState[AxisFlags.LeftPadX] = input.State.AxesState[NeptuneControllerAxis.LeftPadX];
                Inputs.AxisState[AxisFlags.LeftPadY] = input.State.AxesState[NeptuneControllerAxis.LeftPadY];
            }
            else
            {
                Inputs.AxisState[AxisFlags.LeftPadX] = 0;
                Inputs.AxisState[AxisFlags.LeftPadY] = 0;
            }

            Inputs.ButtonState[ButtonFlags.LPadClick] = input.State.ButtonState[NeptuneControllerButton.BtnLPadPress];
            if (Inputs.ButtonState[ButtonFlags.LPadClick])
            {
                // temp
                if (Inputs.AxisState[AxisFlags.LeftPadY] >= 21844)
                    Inputs.ButtonState[ButtonFlags.LPadClickUp] = true;
                else if (Inputs.AxisState[AxisFlags.LeftPadY] <= -21844)
                        Inputs.ButtonState[ButtonFlags.LPadClickDown] = true;

                // you don't want to combine touch + click
                Inputs.ButtonState[ButtonFlags.LPadTouch] = false;

                Console.WriteLine($"X:{Inputs.AxisState[AxisFlags.LeftPadX]},Y:{Inputs.AxisState[AxisFlags.LeftPadY]}");
            }

            Inputs.ButtonState[ButtonFlags.RPadTouch] = input.State.ButtonState[NeptuneControllerButton.BtnRPadTouch];
            if (Inputs.ButtonState[ButtonFlags.RPadTouch])
            {
                Inputs.AxisState[AxisFlags.RightPadX] = input.State.AxesState[NeptuneControllerAxis.RightPadX];
                Inputs.AxisState[AxisFlags.RightPadY] = input.State.AxesState[NeptuneControllerAxis.RightPadY];
            }
            else
            {
                Inputs.AxisState[AxisFlags.RightPadX] = 0;
                Inputs.AxisState[AxisFlags.RightPadY] = 0;
            }

            Inputs.ButtonState[ButtonFlags.RPadClick] = input.State.ButtonState[NeptuneControllerButton.BtnRPadPress];
            if (Inputs.ButtonState[ButtonFlags.RPadClick])
            {
                // you don't want to combine touch + click
                Inputs.ButtonState[ButtonFlags.LPadTouch] = false;
            }

            // temporary workaround
            if (IsLizardMouseEnabled())
            {
                if (Inputs.ButtonState[ButtonFlags.LPadClick] != lastLeftPadClick)
                    lastLeftPadClick = Inputs.ButtonState[ButtonFlags.LPadClick];

                if (Inputs.ButtonState[ButtonFlags.RPadClick] != lastRightPadClick)
                    lastRightPadClick = Inputs.ButtonState[ButtonFlags.RPadClick];
            }

            // update states
            prevState = input.State;

            base.UpdateInputs();
        }

        public override void UpdateMovements()
        {
            if (input is null)
                return;

            Movements.GyroAccelZ = -(float)input.State.AxesState[NeptuneControllerAxis.GyroAccelY] / short.MaxValue * 2.0f;
            Movements.GyroAccelY = -(float)input.State.AxesState[NeptuneControllerAxis.GyroAccelZ] / short.MaxValue * 2.0f;
            Movements.GyroAccelX = -(float)input.State.AxesState[NeptuneControllerAxis.GyroAccelX] / short.MaxValue * 2.0f;

            Movements.GyroPitch = -(float)input.State.AxesState[NeptuneControllerAxis.GyroRoll] / short.MaxValue * 2000.0f;
            Movements.GyroRoll = (float)input.State.AxesState[NeptuneControllerAxis.GyroPitch] / short.MaxValue * 2000.0f;
            Movements.GyroYaw = -(float)input.State.AxesState[NeptuneControllerAxis.GyroYaw] / short.MaxValue * 2000.0f;

            base.UpdateMovements();
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
            new Thread(async () =>
            {
                for (int i = 0; i < loop * 2; i++)
                {
                    if (i % 2 == 0)
                        SetVibration(byte.MaxValue, byte.MaxValue);
                    else
                        SetVibration(0, 0);

                    await Task.Delay(100);
                }
            }).Start();

            base.Rumble(loop);
        }

        public override void Plug()
        {
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
                Controller.Close();
                isConnected = false;
            }
            catch
            {
                return;
            }

            PipeClient.ServerMessage -= OnServerMessage;
            base.Unplug();
        }

        public override void SetVibrationStrength(double value)
        {
            base.SetVibrationStrength(value);
            this.Rumble(1);
        }

        public override void SetVibration(byte LargeMotor, byte SmallMotor)
        {
            // todo: improve me
            // todo: https://github.com/mKenfenheuer/steam-deck-windows-usermode-driver/blob/69ce8085d3b6afe888cb2e36bd95836cea58084a/SWICD/Services/ControllerService.cs

            // Linear motors have a peak bell curve / s curve like responce, use left half, no linearization (yet?)
            // https://www.precisionmicrodrives.com/ab-003
            // Scale motor input request with user vibration strenth 0 to 100% accordingly

            byte AmplitudeLeft = (byte)(LargeMotor * VibrationStrength / byte.MaxValue * 12);

            bool leftHaptic = LargeMotor > 0;
            byte PeriodLeft = (byte)(30 - AmplitudeLeft);

            if (leftHaptic != lastLeftHapticOn)
            {
                _ = Controller.SetHaptic(1, (ushort)(leftHaptic ? AmplitudeLeft : 0), (ushort)(leftHaptic ? PeriodLeft : 0), 0);
                lastLeftHapticOn = leftHaptic;
            }

            byte AmplitudeRight = (byte)(SmallMotor * VibrationStrength / byte.MaxValue * 12);

            bool rightHaptic = SmallMotor > 0;
            byte PeriodRight = (byte)(30 - AmplitudeRight);

            if (rightHaptic != lastRightHapticOn)
            {
                _ = Controller.SetHaptic(0, (ushort)(rightHaptic ? AmplitudeRight : 0), (ushort)(rightHaptic ? PeriodRight : 0), 0);
                lastRightHapticOn = rightHaptic;
            }
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
                    return "\u2198";
                case ButtonFlags.R1:
                    return "\u2199";
                case ButtonFlags.Back:
                    return "\u21FA";
                case ButtonFlags.Start:
                    return "\u21FB";
                case ButtonFlags.L2:
                    return "\u2196";
                case ButtonFlags.R2:
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
    }
}
