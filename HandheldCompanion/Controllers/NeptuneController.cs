using ControllerCommon;
using ControllerCommon.Controllers;
using ControllerCommon.Managers;
using HandheldCompanion.Managers;
using neptune_hidapi.net;
using SharpDX.XInput;
using System;
using System.Threading.Tasks;

namespace HandheldCompanion.Controllers
{
    public class NeptuneController : IController
    {
        private neptune_hidapi.net.NeptuneController Controller;
        private NeptuneControllerInputEventArgs input;

        private bool isConnected = false;

        private bool lastLeftHapticOn = false;
        private bool lastRightHapticOn = false;

        public NeptuneController(PnPDetails details)
        {
            Details = details;
            if (Details is null)
                throw new Exception();

            Details.isHooked = true;

            Capacities |= ControllerCapacities.Gyroscope;
            Capacities |= ControllerCapacities.Accelerometer;

            HideOnHook = false;

            try
            {
                Controller = new();
                Controller.Open();
                isConnected = true;
            }
            catch (Exception ex)
            {
                LogManager.LogError("Couldn't initialize NeptuneController. Exception: {0}", ex.Message);
                throw new Exception();
            }

            UpdateTimer.Tick += (sender, e) => UpdateReport();

            bool LizardMouse = SettingsManager.GetBoolean("SteamDeckLizardMouse");
            SetLizardMouse(LizardMouse);

            bool LizardButtons = SettingsManager.GetBoolean("SteamDeckLizardButtons");
            SetLizardButtons(LizardButtons);

            // ui
            DrawControls();
            RefreshControls();
        }

        public override string ToString()
        {
            if (!string.IsNullOrEmpty(Details.Name))
                return Details.Name;
            return "Steam Controller Neptune";
        }

        public override void UpdateReport()
        {
            if (input is null)
                return;

            Inputs.Buttons = InjectedButtons;

            if (input.State.ButtonState[NeptuneControllerButton.BtnA])
                Inputs.Buttons |= ControllerButtonFlags.B1;
            if (input.State.ButtonState[NeptuneControllerButton.BtnB])
                Inputs.Buttons |= ControllerButtonFlags.B2;
            if (input.State.ButtonState[NeptuneControllerButton.BtnX])
                Inputs.Buttons |= ControllerButtonFlags.B3;
            if (input.State.ButtonState[NeptuneControllerButton.BtnY])
                Inputs.Buttons |= ControllerButtonFlags.B4;

            if (input.State.ButtonState[NeptuneControllerButton.BtnOptions])
                Inputs.Buttons |= ControllerButtonFlags.Start;
            if (input.State.ButtonState[NeptuneControllerButton.BtnMenu])
                Inputs.Buttons |= ControllerButtonFlags.Back;

            if (input.State.ButtonState[NeptuneControllerButton.BtnSteam])
                Inputs.Buttons |= ControllerButtonFlags.Special;
            if (input.State.ButtonState[NeptuneControllerButton.BtnQuickAccess])
                Inputs.Buttons |= ControllerButtonFlags.OEM1;

            var L2 = input.State.AxesState[NeptuneControllerAxis.L2] * byte.MaxValue / short.MaxValue;
            var R2 = input.State.AxesState[NeptuneControllerAxis.R2] * byte.MaxValue / short.MaxValue;

            if (L2 > Gamepad.TriggerThreshold)
                Inputs.Buttons |= ControllerButtonFlags.LeftTrigger;
            if (R2 > Gamepad.TriggerThreshold)
                Inputs.Buttons |= ControllerButtonFlags.RightTrigger;

            if (input.State.ButtonState[NeptuneControllerButton.BtnLStickPress])
                Inputs.Buttons |= ControllerButtonFlags.LeftThumb;
            if (input.State.ButtonState[NeptuneControllerButton.BtnRStickPress])
                Inputs.Buttons |= ControllerButtonFlags.RightThumb;

            if (input.State.ButtonState[NeptuneControllerButton.BtnLStickTouch])
                Inputs.Buttons |= ControllerButtonFlags.OEM2;
            if (input.State.ButtonState[NeptuneControllerButton.BtnRStickTouch])
                Inputs.Buttons |= ControllerButtonFlags.OEM3;

            if (input.State.ButtonState[NeptuneControllerButton.BtnL4])
                Inputs.Buttons |= ControllerButtonFlags.OEM4;
            if (input.State.ButtonState[NeptuneControllerButton.BtnL5])
                Inputs.Buttons |= ControllerButtonFlags.OEM5;

            if (input.State.ButtonState[NeptuneControllerButton.BtnR4])
                Inputs.Buttons |= ControllerButtonFlags.OEM6;
            if (input.State.ButtonState[NeptuneControllerButton.BtnR5])
                Inputs.Buttons |= ControllerButtonFlags.OEM7;

            if (input.State.ButtonState[NeptuneControllerButton.BtnL1])
                Inputs.Buttons |= ControllerButtonFlags.LeftShoulder;
            if (input.State.ButtonState[NeptuneControllerButton.BtnR1])
                Inputs.Buttons |= ControllerButtonFlags.RightShoulder;

            if (input.State.ButtonState[NeptuneControllerButton.BtnDpadUp])
                Inputs.Buttons |= ControllerButtonFlags.DPadUp;
            if (input.State.ButtonState[NeptuneControllerButton.BtnDpadDown])
                Inputs.Buttons |= ControllerButtonFlags.DPadDown;
            if (input.State.ButtonState[NeptuneControllerButton.BtnDpadLeft])
                Inputs.Buttons |= ControllerButtonFlags.DPadLeft;
            if (input.State.ButtonState[NeptuneControllerButton.BtnDpadRight])
                Inputs.Buttons |= ControllerButtonFlags.DPadRight;

            // Left Stick
            if (input.State.AxesState[NeptuneControllerAxis.LeftStickX] < -Gamepad.LeftThumbDeadZone)
                Inputs.Buttons |= ControllerButtonFlags.LStickLeft;
            if (input.State.AxesState[NeptuneControllerAxis.LeftStickX] > Gamepad.LeftThumbDeadZone)
                Inputs.Buttons |= ControllerButtonFlags.LStickRight;

            if (input.State.AxesState[NeptuneControllerAxis.LeftStickY] < -Gamepad.LeftThumbDeadZone)
                Inputs.Buttons |= ControllerButtonFlags.LStickDown;
            if (input.State.AxesState[NeptuneControllerAxis.LeftStickY] > Gamepad.LeftThumbDeadZone)
                Inputs.Buttons |= ControllerButtonFlags.LStickUp;

            Inputs.LeftThumbX = input.State.AxesState[NeptuneControllerAxis.LeftStickX];
            Inputs.LeftThumbY = input.State.AxesState[NeptuneControllerAxis.LeftStickY];

            // Right Stick
            if (input.State.AxesState[NeptuneControllerAxis.RightStickX] < -Gamepad.LeftThumbDeadZone)
                Inputs.Buttons |= ControllerButtonFlags.RStickLeft;
            if (input.State.AxesState[NeptuneControllerAxis.RightStickX] > Gamepad.LeftThumbDeadZone)
                Inputs.Buttons |= ControllerButtonFlags.RStickRight;

            if (input.State.AxesState[NeptuneControllerAxis.RightStickY] < -Gamepad.LeftThumbDeadZone)
                Inputs.Buttons |= ControllerButtonFlags.RStickDown;
            if (input.State.AxesState[NeptuneControllerAxis.RightStickY] > Gamepad.LeftThumbDeadZone)
                Inputs.Buttons |= ControllerButtonFlags.RStickUp;

            Inputs.RightThumbX = input.State.AxesState[NeptuneControllerAxis.RightStickX];
            Inputs.RightThumbY = input.State.AxesState[NeptuneControllerAxis.RightStickY];

            Inputs.LeftTrigger = L2;
            Inputs.RightTrigger = R2;

            if (input.State.AxesState[NeptuneControllerAxis.GyroAccelX] > maxX)
                maxX = input.State.AxesState[NeptuneControllerAxis.GyroAccelX];
            if (input.State.AxesState[NeptuneControllerAxis.GyroAccelY] > maxY)
                maxY = input.State.AxesState[NeptuneControllerAxis.GyroAccelY];
            if (input.State.AxesState[NeptuneControllerAxis.GyroAccelZ] > maxZ)
                maxZ = input.State.AxesState[NeptuneControllerAxis.GyroAccelZ];

            if (input.State.AxesState[NeptuneControllerAxis.GyroPitch] > maxpitch)
                maxpitch = input.State.AxesState[NeptuneControllerAxis.GyroPitch];
            if (input.State.AxesState[NeptuneControllerAxis.GyroRoll] > maxroll)
                maxroll = input.State.AxesState[NeptuneControllerAxis.GyroRoll];
            if (input.State.AxesState[NeptuneControllerAxis.GyroYaw] > maxyaw)
                maxyaw = input.State.AxesState[NeptuneControllerAxis.GyroYaw];

            Inputs.GyroAccelZ = -(float)input.State.AxesState[NeptuneControllerAxis.GyroAccelY] / short.MaxValue * 2.0f;
            Inputs.GyroAccelY = -(float)input.State.AxesState[NeptuneControllerAxis.GyroAccelZ] / short.MaxValue * 2.0f;
            Inputs.GyroAccelX = -(float)input.State.AxesState[NeptuneControllerAxis.GyroAccelX] / short.MaxValue * 2.0f;

            Inputs.GyroPitch = -(float)input.State.AxesState[NeptuneControllerAxis.GyroRoll] / short.MaxValue * 2000.0f;
            Inputs.GyroRoll = (float)input.State.AxesState[NeptuneControllerAxis.GyroPitch] / short.MaxValue * 2000.0f;
            Inputs.GyroYaw = -(float)input.State.AxesState[NeptuneControllerAxis.GyroYaw] / short.MaxValue * 2000.0f;

            Inputs.LeftPadX = short.MaxValue + input.State.AxesState[NeptuneControllerAxis.LeftPadX];
            Inputs.LeftPadY = short.MaxValue - input.State.AxesState[NeptuneControllerAxis.LeftPadY];
            Inputs.LeftPadTouch = input.State.ButtonState[NeptuneControllerButton.BtnLPadTouch];
            Inputs.LeftPadClick = input.State.ButtonState[NeptuneControllerButton.BtnLPadPress];

            Inputs.RightPadX = short.MaxValue + input.State.AxesState[NeptuneControllerAxis.RightPadX];
            Inputs.RightPadY = short.MaxValue - input.State.AxesState[NeptuneControllerAxis.RightPadY];
            Inputs.RightPadTouch = input.State.ButtonState[NeptuneControllerButton.BtnRPadTouch];
            Inputs.RightPadClick = input.State.ButtonState[NeptuneControllerButton.BtnRPadPress];

            if (Inputs.LeftPadTouch)
                Inputs.Buttons |= ControllerButtonFlags.OEM8;
            if (Inputs.LeftPadClick)
                Inputs.Buttons |= ControllerButtonFlags.OEM9;

            if (Inputs.RightPadTouch)
                Inputs.Buttons |= ControllerButtonFlags.OEM10;
            if (Inputs.RightPadClick)
                Inputs.Buttons |= ControllerButtonFlags.OEM11;

            base.UpdateReport();
        }

        private float maxX, maxY, maxZ;
        private float maxpitch, maxroll, maxyaw;

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
    }
}
