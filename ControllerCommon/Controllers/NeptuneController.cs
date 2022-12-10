using ControllerCommon.Managers;
using neptune_hidapi.net;
using SharpDX.DirectInput;
using SharpDX.XInput;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ControllerCommon.Controllers
{
    public class NeptuneController : IController
    {
        private neptune_hidapi.net.NeptuneController Controller = new();
        private NeptuneControllerInputEventArgs input;

        private bool isConnected = false;

        public NeptuneController(PnPDetails details)
        {
            Details = details;
            Details.isHooked = true;

            Capacities |= ControllerCapacities.Gyroscope;
            Capacities |= ControllerCapacities.Accelerometer;

            try
            {
                Controller.Open();
                isConnected = true;

                UpdateTimer.Tick += (sender, e) => UpdateReport();
            }
            catch (Exception)
            {
                return;
            }
        }

        public override string ToString()
        {
            // todo: localize me
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

            Inputs.GyroAccelX   = -(float)input.State.AxesState[NeptuneControllerAxis.GyroAccelY] / short.MaxValue * 2.0f;
            Inputs.GyroAccelY   = (float)input.State.AxesState[NeptuneControllerAxis.GyroAccelX] / short.MaxValue * 2.0f;
            Inputs.GyroAccelZ   = -(float)input.State.AxesState[NeptuneControllerAxis.GyroAccelZ] / short.MaxValue * 2.0f;

            Inputs.GyroPitch    = -(float)input.State.AxesState[NeptuneControllerAxis.GyroRoll] / 12.0f;
            Inputs.GyroRoll     = (float)input.State.AxesState[NeptuneControllerAxis.GyroPitch] / 12.0f;
            Inputs.GyroYaw      = -(float)input.State.AxesState[NeptuneControllerAxis.GyroYaw] / 12.0f;

            // todo: map trackpad(s)

            base.UpdateReport();
        }

        private float maxX,maxY, maxZ;
        private float maxpitch, maxroll, maxyaw;

        public override bool IsConnected()
        {
            return isConnected;
        }

        public override async void Rumble()
        {
            for (int i = 0; i < 2; i++)
            {
                SetVibration(ushort.MaxValue, ushort.MaxValue);
                await Task.Delay(100);

                SetVibration(0, 0);
                await Task.Delay(100);
            }
            base.Rumble();
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
            catch (Exception)
            {
                return;
            }

            PipeClient.ServerMessage -= OnServerMessage;
            base.Unplug();
        }

        private bool lastLeftHapticOn = false;
        private bool lastRightHapticOn = false;


        public override void SetVibration(ushort LargeMotor, ushort SmallMotor)
        {
            ushort LeftMotorSpeed = (ushort)((LargeMotor * ushort.MaxValue / byte.MaxValue) * VibrationStrength);
            ushort RightMotorSpeed = (ushort)((SmallMotor * ushort.MaxValue / byte.MaxValue) * VibrationStrength);

            // todo: improve me
            // todo: https://github.com/mKenfenheuer/steam-deck-windows-usermode-driver/blob/69ce8085d3b6afe888cb2e36bd95836cea58084a/SWICD/Services/ControllerService.cs

            byte amplitude = 15;
            byte period = 15;

            bool leftHaptic = LargeMotor > 0;
            bool rightHaptic = SmallMotor > 0;

            if (leftHaptic != lastLeftHapticOn)
                _ = Controller.SetHaptic(1, (ushort)(leftHaptic ? amplitude : 0), (ushort)(leftHaptic ? period : 0), 0);


            if (rightHaptic != lastRightHapticOn)
                _ = Controller.SetHaptic(0, (ushort)(rightHaptic ? amplitude : 0), (ushort)(rightHaptic ? period : 0), 0);

            lastLeftHapticOn = leftHaptic;
            lastRightHapticOn = rightHaptic;
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
